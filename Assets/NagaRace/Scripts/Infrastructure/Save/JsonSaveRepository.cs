using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using NagaRace.Application.Save;
using NagaRace.Domain.Training;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NagaRace.Infrastructure.Save
{
    /// <summary>
    /// セーブ形式の移行（08 §8）。古いセーブが読めない変更を無処理で入れない。
    /// </summary>
    public interface ISaveMigration
    {
        /// <summary>この移行が受け取る schemaVersion。結果は FromVersion + 1 になる。</summary>
        int FromVersion { get; }

        JObject Migrate(JObject payload);
    }

    /// <summary>
    /// セーブファイルの封筒: { schemaVersion, checksum(SHA-256), payload }。
    /// </summary>
    public sealed class SaveSerializer
    {
        public const int CurrentSchemaVersion = 1;

        private readonly int _targetVersion;
        private readonly Dictionary<int, ISaveMigration> _migrations;

        public SaveSerializer(IEnumerable<ISaveMigration> migrations = null, int targetVersion = CurrentSchemaVersion)
        {
            _targetVersion = targetVersion;
            _migrations = (migrations ?? Enumerable.Empty<ISaveMigration>()).ToDictionary(m => m.FromVersion);
        }

        public string Serialize(SaveDto dto)
        {
            var payload = JObject.FromObject(dto, JsonSerializer.CreateDefault());
            var envelope = new JObject
            {
                ["schemaVersion"] = _targetVersion,
                ["checksum"] = Checksum(payload),
                ["payload"] = payload,
            };
            return envelope.ToString(Formatting.Indented);
        }

        /// <summary>検証・移行してから SaveDto へ戻す。失敗時は例外（呼び出し側でバックアップを試す）。</summary>
        public SaveDto Deserialize(string json)
        {
            var envelope = JObject.Parse(json);
            int version = envelope.Value<int?>("schemaVersion") ?? throw new FormatException("schemaVersion がありません。");
            var payload = envelope["payload"] as JObject ?? throw new FormatException("payload がありません。");
            string checksum = envelope.Value<string>("checksum");
            if (checksum != Checksum(payload)) throw new InvalidDataException("チェックサムが一致しません（破損または改ざん）。");
            if (version > _targetVersion) throw new FormatException($"新しいバージョンのセーブです（{version} > {_targetVersion}）。");

            while (version < _targetVersion)
            {
                if (!_migrations.TryGetValue(version, out var migration))
                {
                    throw new FormatException($"schemaVersion {version} から {version + 1} への移行処理がありません。");
                }

                payload = migration.Migrate(payload);
                version++;
            }

            return payload.ToObject<SaveDto>(JsonSerializer.CreateDefault()) ?? throw new FormatException("payload を読めません。");
        }

        private static string Checksum(JObject payload)
        {
            using (var sha = SHA256.Create())
            {
                var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(payload.ToString(Formatting.None)));
                return BitConverter.ToString(bytes).Replace("-", string.Empty).ToLowerInvariant();
            }
        }
    }

    /// <summary>
    /// ローカル JSON のセーブ（07 §9）。本体 + バックアップの2スロット。
    /// 書き込みは一時ファイル経由で行い、途中で落ちても本体を壊さない。
    /// </summary>
    public sealed class JsonSaveRepository : ISaveRepository
    {
        private readonly string _primaryPath;
        private readonly string _backupPath;
        private readonly string _tempPath;
        private readonly SaveSerializer _serializer;

        public JsonSaveRepository(string directory, string slot = "slot0", SaveSerializer serializer = null)
        {
            Directory.CreateDirectory(directory);
            _primaryPath = Path.Combine(directory, $"{slot}.json");
            _backupPath = Path.Combine(directory, $"{slot}.bak.json");
            _tempPath = Path.Combine(directory, $"{slot}.tmp");
            _serializer = serializer ?? new SaveSerializer();
        }

        public string PrimaryPath => _primaryPath;
        public string BackupPath => _backupPath;

        public bool Exists => File.Exists(_primaryPath) || File.Exists(_backupPath);

        public void Save(GameSession session, GameSettings settings)
        {
            string json = _serializer.Serialize(SaveMapper.ToDto(session, settings));
            File.WriteAllText(_tempPath, json, new UTF8Encoding(false));

            // 直前の正常なセーブをバックアップへ退避してから差し替える
            if (File.Exists(_primaryPath)) File.Copy(_primaryPath, _backupPath, overwrite: true);
            File.Copy(_tempPath, _primaryPath, overwrite: true);
            File.Delete(_tempPath);
        }

        public LoadResult Load()
        {
            string primaryError = null;
            if (File.Exists(_primaryPath))
            {
                var result = TryLoad(_primaryPath, SaveSource.Primary, out primaryError);
                if (result != null) return result;
            }

            if (File.Exists(_backupPath))
            {
                var result = TryLoad(_backupPath, SaveSource.Backup, out string backupError);
                if (result != null)
                {
                    result.Error = $"本体を読めなかったためバックアップから復元しました: {primaryError}";
                    return result;
                }

                return new LoadResult { Source = SaveSource.None, Error = $"本体: {primaryError} / バックアップ: {backupError}" };
            }

            return new LoadResult { Source = SaveSource.None, Error = primaryError ?? "セーブがありません。" };
        }

        public void Delete()
        {
            foreach (var path in new[] { _primaryPath, _backupPath, _tempPath })
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        private LoadResult TryLoad(string path, SaveSource source, out string error)
        {
            try
            {
                var (session, settings) = SaveMapper.FromDto(_serializer.Deserialize(File.ReadAllText(path)));
                error = null;
                return new LoadResult { Session = session, Settings = settings, Source = source };
            }
            catch (Exception e)
            {
                // 壊れた JSON でクラッシュさせない（10 G-03）
                error = e.Message;
                return null;
            }
        }
    }
}
