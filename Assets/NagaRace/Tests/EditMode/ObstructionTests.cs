using System.Collections.Generic;
using System.Linq;
using NagaRace.Domain.Characters;
using NagaRace.Domain.Race;
using NUnit.Framework;

namespace NagaRace.Tests.EditMode
{
    /// <summary>悪葉っぱの妨害スキル（04 §10）と 10 C-03。</summary>
    public class ObstructionTests
    {
        private static List<RaceEntrant> Field(string npcSpecies, RaceEntrant player)
        {
            var list = Enumerable.Range(0, 8).Select(i => new RaceEntrant($"e{i}", $"e{i}", npcSpecies, CharacterStats.Uniform(60))).ToList();
            list[0] = player;
            return list;
        }

        [Test]
        public void C03_妨害スキル単独ではLostせず妨害で耐久が尽きることもない()
        {
            int skillHitsOnPlayer = 0;
            for (ulong seed = 1; seed <= 1000; seed++)
            {
                var player = new RaceEntrant("p", "p", "test", CharacterStats.Uniform(60), isPlayer: true, durability: 3);
                var result = TestData.Run("test_straight", Field("bad_leaf", player), seed);
                Assert.IsFalse(result.PlacingOf(0).Lost, $"seed {seed}: 妨害だけで Lost した");

                // 妨害によるダメージで耐久が0になっていないこと
                double durability = 3;
                foreach (var e in result.Events.Where(e => e.EntrantIndex == 0))
                {
                    if (e.Type == RaceEventType.SkillUsed) continue;
                    if (e.Type != RaceEventType.DurabilityDamaged) continue;
                    durability -= e.Value;
                    // 妨害で実際に耐久が減った場合、残りは必ず1以上（通常の接触で既に1未満なら妨害ダメージは0になる）
                    if (e.OtherIndex == (int)DamageCause.Obstruction && e.Value > 0)
                    {
                        Assert.GreaterOrEqual(durability, 1 - 1e-9, $"seed {seed}: 妨害で耐久が1未満になった");
                    }
                }

                skillHitsOnPlayer += result.Events.Count(e => e.Type == RaceEventType.SkillUsed && e.OtherIndex == 0);
            }

            Assert.Greater(skillHitsOnPlayer, 100, "妨害スキルがほとんど使われていない");
        }

        [Test]
        public void 妨害スキルは悪葉っぱだけが代償を払って使う()
        {
            var result = TestData.Run(TestData.StandardCourse, Field("bad_leaf", new RaceEntrant("p", "p", "test", CharacterStats.Uniform(60), isPlayer: true)), 3);
            var used = result.Events.Where(e => e.Type == RaceEventType.SkillUsed).ToList();
            Assert.Greater(used.Count, 0);
            Assert.IsFalse(used.Any(e => e.EntrantIndex == 0), "悪葉っぱでない自キャラがスキルを使った");

            var none = TestData.Run(TestData.StandardCourse, TestData.UniformEntrants(), 3);
            Assert.IsFalse(none.Events.Any(e => e.Type == RaceEventType.SkillUsed), "悪葉っぱがいないのにスキルが使われた");

            // 同じ悪葉っぱの連続使用はクールダウンで間が空く
            foreach (var group in used.GroupBy(e => e.EntrantIndex))
            {
                var ticks = group.Select(e => e.Tick).OrderBy(t => t).ToList();
                for (int i = 1; i < ticks.Count; i++)
                {
                    Assert.GreaterOrEqual(ticks[i] - ticks[i - 1], (int)(TestData.Balance.Obstruction.GlobalCooldownSeconds * 20) - 1);
                }
            }
        }

        [Test]
        public void 妨害を受けると遅くなる()
        {
            double withObstruction = 0, without = 0;
            for (ulong seed = 1; seed <= 200; seed++)
            {
                var player = new RaceEntrant("p", "p", "test", CharacterStats.Uniform(60), isPlayer: true, autoFurnace: true);
                withObstruction += TestData.Run(TestData.StandardCourse, Field("bad_leaf", player), seed).PlacingOf(0).FinishTimeSeconds;
                without += TestData.Run(TestData.StandardCourse, Field("test", player), seed).PlacingOf(0).FinishTimeSeconds;
            }

            TestContext.WriteLine($"平均タイム 妨害あり {withObstruction / 200:0.00} / なし {without / 200:0.00}");
            Assert.Greater(withObstruction, without);
        }
    }
}
