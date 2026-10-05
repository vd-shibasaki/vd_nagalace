using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NagaRace.Application.Analytics;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NagaRace.Infrastructure.Analytics
{
    /// <summary>
    /// プレイログを JSON Lines（1行1イベント）で追記する。ファイルが大きくなりすぎたら古いものを1世代だけ残して切り替える。
    /// 記録に失敗してもゲームは止めない。
    /// </summary>
    public sealed class JsonlAnalyticsSink : IAnalyticsSink
    {
        private const long MaxBytes = 2 * 1024 * 1024;
        private readonly string _path;
        private readonly Func<DateTime> _clock;

        public JsonlAnalyticsSink(string path, Func<DateTime> clock = null)
        {
            _path = path;
            _clock = clock ?? (() => DateTime.UtcNow);
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(_path) ?? ".");
        }

        public string Path => _path;

        public void Log(string eventName, IReadOnlyDictionary<string, object> data = null)
        {
            try
            {
                var line = new JObject
                {
                    ["t"] = _clock().ToString("o"),
                    ["event"] = eventName,
                };

                if (data != null)
                {
                    foreach (var pair in data)
                    {
                        line[pair.Key] = pair.Value == null ? JValue.CreateNull() : JToken.FromObject(pair.Value);
                    }
                }

                Rotate();
                File.AppendAllText(_path, line.ToString(Formatting.None) + "\n", new UTF8Encoding(false));
            }
            catch (Exception)
            {
                // ログはゲームの進行より優先しない
            }
        }

        private void Rotate()
        {
            var info = new FileInfo(_path);
            if (!info.Exists || info.Length < MaxBytes) return;
            string old = _path + ".1";
            if (File.Exists(old)) File.Delete(old);
            File.Move(_path, old);
        }
    }
}
