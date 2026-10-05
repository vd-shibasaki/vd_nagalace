using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NagaRace.Infrastructure.Analytics;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace NagaRace.Tests.EditMode
{
    /// <summary>プレイログ（11 §8）の記録。</summary>
    public class AnalyticsTests
    {
        private string _directory;

        [SetUp]
        public void SetUp()
        {
            _directory = Path.Combine(Path.GetTempPath(), "nagarace-analytics-" + Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
        }

        [Test]
        public void イベントは1行1JSONで追記される()
        {
            var sink = new JsonlAnalyticsSink(Path.Combine(_directory, "logs", "events.jsonl"), () => new DateTime(2026, 10, 5, 12, 0, 0, DateTimeKind.Utc));
            sink.Log("race_end", new Dictionary<string, object> { ["rank"] = 2, ["furnaceStartSeconds"] = new List<double> { 31.5, 52.0 } });
            sink.Log("session_end");

            var lines = File.ReadAllLines(sink.Path);
            Assert.AreEqual(2, lines.Length);
            // 日時を自動変換せず文字列のまま読む
            var first = JObject.Load(new Newtonsoft.Json.JsonTextReader(new StringReader(lines[0])) { DateParseHandling = Newtonsoft.Json.DateParseHandling.None });
            Assert.AreEqual("race_end", first.Value<string>("event"));
            Assert.AreEqual(2, first.Value<int>("rank"));
            Assert.AreEqual(2, first["furnaceStartSeconds"].Count());
            StringAssert.StartsWith("2026-10-05T12:00:00", first.Value<string>("t"));
        }

        [Test]
        public void 書き込めなくても例外を出さない()
        {
            var sink = new JsonlAnalyticsSink(Path.Combine(_directory, "events.jsonl"));
            Directory.CreateDirectory(sink.Path); // 同名のフォルダがあって書き込めない状態
            Assert.DoesNotThrow(() => sink.Log("training_choice"));
        }
    }
}
