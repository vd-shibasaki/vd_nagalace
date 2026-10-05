using System.Linq;
using NagaRace.Domain.Characters;
using NagaRace.Domain.Race;
using NUnit.Framework;

namespace NagaRace.Tests.EditMode
{
    public class RouteSelectionTests
    {
        [Test]
        public void 勝負師は慎重より近道を選びやすい()
        {
            double cautious = ShortcutRate(Personality.Cautious);
            double gambler = ShortcutRate(Personality.Gambler);
            TestContext.WriteLine($"近道選択率 慎重 {cautious:P0} / 勝負師 {gambler:P0}");

            Assert.Greater(gambler - cautious, 0.2);
        }

        [Test]
        public void 分岐では毎回どちらかのルートを選ぶ()
        {
            var result = TestData.Run(TestData.StandardCourse, TestData.UniformEntrants(), 3);
            var choices = result.Events.Where(e => e.Type == RaceEventType.BranchChoice).ToList();
            Assert.AreEqual(8, choices.Count);
            Assert.IsTrue(choices.All(c => c.SegmentId == "main" || c.SegmentId == "shortcut"));
        }

        private static double ShortcutRate(Personality personality)
        {
            int shortcut = 0;
            int total = 0;
            for (ulong seed = 1; seed <= 100; seed++)
            {
                var entrants = TestData.UniformEntrants().Select(e => e.WithPersonality(personality)).ToList();
                foreach (var e in TestData.Run(TestData.StandardCourse, entrants, seed).Events)
                {
                    if (e.Type != RaceEventType.BranchChoice) continue;
                    total++;
                    if (e.SegmentId == "shortcut") shortcut++;
                }
            }

            return (double)shortcut / total;
        }
    }

    public class NpcFactoryTests
    {
        [Test]
        public void 同じ大会Seedなら同じ顔ぶれになる()
        {
            var a = NpcFactory.Generate(new NpcSpec(TestData.Species), 2026, 8);
            var b = NpcFactory.Generate(new NpcSpec(TestData.Species), 2026, 8);
            for (int i = 0; i < 8; i++)
            {
                Assert.AreEqual(a[i].SpeciesId, b[i].SpeciesId);
                Assert.AreEqual(a[i].Stats.ToString(), b[i].Stats.ToString());
                Assert.AreEqual(a[i].Personality, b[i].Personality);
            }
        }

        [Test]
        public void 能力は系統のレンジに収まる()
        {
            var bySpecies = TestData.Species.ToDictionary(s => s.Id);
            foreach (var entrant in NpcFactory.Generate(new NpcSpec(TestData.Species), 1, 200))
            {
                var species = bySpecies[entrant.SpeciesId];
                foreach (var pair in species.BaseRanges)
                {
                    Assert.That(entrant.Stats[pair.Key], Is.InRange(pair.Value.Min, pair.Value.Max), $"{species.Id} {pair.Key}");
                }
            }
        }

        [Test]
        public void 重みが0の性格は出ない()
        {
            var badLeaf = TestData.Species.First(s => s.Id == "bad_leaf");
            var npcs = NpcFactory.Generate(new NpcSpec(new[] { badLeaf }), 9, 200);
            Assert.IsFalse(npcs.Any(n => n.Personality == Personality.Cautious));
        }
    }

    public class BalanceConfigTests
    {
        [Test]
        public void バランス設定を読み込める()
        {
            var balance = TestData.Balance;
            Assert.AreEqual(8, balance.Race.EntrantCount);
            Assert.AreEqual(20, balance.Race.TickRate);
            Assert.AreEqual(2, balance.Stats.FlowRateRange.Length);
        }

        [Test]
        public void レンジ指定が欠けていると読込時にエラーになる()
        {
            Assert.Throws<System.FormatException>(() =>
                NagaRace.Infrastructure.Config.JsonBalanceParser.Parse("{ \"race\": { \"tickRate\": 20, \"timeoutSeconds\": 240 } }"));
        }
    }
}
