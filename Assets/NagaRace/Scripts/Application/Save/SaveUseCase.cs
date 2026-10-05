using System;
using NagaRace.Domain.Characters;
using NagaRace.Domain.Training;

namespace NagaRace.Application.Save
{
    /// <summary>プレイヤー設定（保存対象）。</summary>
    public sealed class GameSettings
    {
        public bool AutoFurnace { get; set; }
        public AiStyle PlayerAiStyle { get; set; } = AiStyle.Standard;
        public float PlaybackSpeed { get; set; } = 1f;
        /// <summary>ホームで選んでいる個体。</summary>
        public string SelectedCharacterId { get; set; }

        /// <summary>表示済みのチュートリアル（キー）。</summary>
        public System.Collections.Generic.HashSet<string> TutorialSeen { get; set; } = new System.Collections.Generic.HashSet<string>();
    }

    public enum SaveSource
    {
        None,
        Primary,
        Backup,
    }

    public sealed class LoadResult
    {
        public GameSession Session { get; set; }
        public GameSettings Settings { get; set; }
        public SaveSource Source { get; set; }
        /// <summary>失敗・バックアップ使用時の理由。</summary>
        public string Error { get; set; }
        public bool Success => Session != null;
    }

    /// <summary>セーブの保存先（07 §9）。実装は Infrastructure の JsonSaveRepository。</summary>
    public interface ISaveRepository
    {
        bool Exists { get; }
        void Save(GameSession session, GameSettings settings);
        LoadResult Load();
        void Delete();
    }

    /// <summary>
    /// 保存と読込（07 §6 SaveGame / LoadGame）。保存契機は 育成後・レース後・接ぎ木後・引退後・設定変更後（07 §9）。
    /// </summary>
    public sealed class SaveUseCase
    {
        private readonly ISaveRepository _repository;

        public SaveUseCase(ISaveRepository repository)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        public bool HasSave => _repository.Exists;

        public void Save(GameSession session, GameSettings settings)
        {
            _repository.Save(session, settings ?? new GameSettings());
        }

        public LoadResult Load()
        {
            return _repository.Load();
        }

        public void Delete()
        {
            _repository.Delete();
        }
    }
}
