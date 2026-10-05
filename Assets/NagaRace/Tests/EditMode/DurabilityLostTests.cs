using System.Linq;
using NagaRace.Application.Race;
using NagaRace.Domain.Characters;
using NagaRace.Domain.Race;
using NUnit.Framework;

namespace NagaRace.Tests.EditMode
{
    /// <summary>10_ACCEPTANCE_TESTS.md の C（Lost）と耐久・危険度表示・レース後の反映。</summary>
    public class DurabilityLostTests
    {
        private const int Player = 0;

        /// <summary>自キャラが炉を「中盤以降に始動し、SafeDuration を overrun 秒超えて引っ張る」レースを走らせる。</summary>
        private static RaceResult RunReckless(string courseId, RaceEntrant player, ulong seed, double overrun = 3.0)
        {
            var entrants = TestData.UniformEntrants();
            entrants[Player] = player;
            var sim = new RaceSimulator(TestData.Course(courseId), entrants, RaceConditions.Default, TestData.Balance, seed);
            var me = sim.Racers[Player];
            while (!sim.IsFinished)
            {
                if (!me.IsOut)
                {
                    if (me.IsChanceOpen && me.ChanceRemainingSeconds <= 0.6) sim.SubmitInput(Player, RaceInputType.Charge);
                    if (me.Progress >= 0.3 && sim.CanActivateFurnace(Player)) sim.SubmitInput(Player, RaceInputType.FurnaceActivate);
                    else if (me.IsFurnaceActive && me.FurnaceActiveSeconds >= me.SafeDurationSeconds + overrun) sim.SubmitInput(Player, RaceInputType.FurnaceRelease);
                }

                sim.Step();
            }

            return sim.BuildResult();
        }

        private static RaceEntrant PlayerWith(int durability, int fatigue = 0, int toughness = 60)
        {
            return new RaceEntrant("player", "player", "test", CharacterStats.Uniform(60).With(StatType.Toughness, toughness), isPlayer: true)
                .WithCondition(durability, 100, fatigue);
        }

        [Test]
        public void C01_安全なコースと十分な耐久では炉を酷使しても突然Lostしない()
        {
            // test_straight は危険度2のコース（ハードガード: 危険度2以下かつ耐久40以上）
            Assert.LessOrEqual(new RaceSimulator(TestData.Course("test_straight"), TestData.UniformEntrants(), RaceConditions.Default, TestData.Balance, 1).CourseDanger, 2);
            int[] durabilities = { 40, 60, 80, 100 };
            for (ulong seed = 1; seed <= 10000; seed++)
            {
                var player = PlayerWith(durabilities[seed % 4], fatigue: 90);
                var result = RunReckless("test_straight", player, seed);
                Assert.IsFalse(result.Placings.Any(p => p.Lost), $"seed {seed} で Lost が発生");
            }
        }

        [Test]
        public void C02_Lostは危険なコースか低耐久のときだけ起きる()
        {
            int lost = 0;
            int[] durabilities = { 10, 15, 30, 45, 70, 100 };
            foreach (var courseId in TestData.CourseIds)
            {
                int danger = new RaceSimulator(TestData.Course(courseId), TestData.UniformEntrants(), RaceConditions.Default, TestData.Balance, 1).CourseDanger;
                for (ulong seed = 1; seed <= 300; seed++)
                {
                    int durability = durabilities[seed % (ulong)durabilities.Length];
                    var result = RunReckless(courseId, PlayerWith(durability, fatigue: 80), seed);
                    var placing = result.PlacingOf(Player);
                    if (!placing.Lost) continue;
                    lost++;
                    Assert.IsTrue(RaceSimulator.IsLostPossible(danger, durability, TestData.Balance),
                        $"{courseId}（危険度{danger}）耐久{durability} で Lost した");
                }
            }

            TestContext.WriteLine($"Lost 件数: {lost}");
            Assert.Greater(lost, 0, "条件が揃っても Lost が起きない（ロスト自体が機能していない）");
        }

        [Test]
        public void 安全と注意の表示ではLostが起こりえない()
        {
            foreach (var courseId in TestData.CourseIds)
            {
                foreach (int durability in new[] { 5, 19, 20, 39, 40, 69, 70, 100 })
                {
                    foreach (int fatigue in new[] { 0, 80 })
                    {
                        var report = RiskAssessment.Evaluate(TestData.Course(courseId), RaceConditions.Default, PlayerWith(durability, fatigue), TestData.Balance);
                        if (report.Level == RiskLevel.Safe || report.Level == RiskLevel.Caution)
                        {
                            Assert.IsFalse(report.LostPossible, $"{courseId} 耐久{durability} 疲労{fatigue}: {report.Label} なのに Lost の可能性あり");
                        }
                        else
                        {
                            Assert.IsTrue(report.LostPossible);
                        }
                    }
                }
            }
        }

        [Test]
        public void 危険度は条件が重なるほど上がる()
        {
            var course = TestData.Course("test_rapid");
            Assert.AreEqual(RiskLevel.Caution, RiskAssessment.Evaluate(TestData.Course("test_straight"), RaceConditions.Default, PlayerWith(100), TestData.Balance).Level);
            Assert.AreEqual(RiskLevel.Danger, RiskAssessment.Evaluate(course, RaceConditions.Default, PlayerWith(100), TestData.Balance).Level);
            Assert.AreEqual(RiskLevel.Extreme, RiskAssessment.Evaluate(course, RaceConditions.Default, PlayerWith(15, 80), TestData.Balance).Level);

            // 大雨で危険度が上がる（危険度2のコースが3になる）
            var rain = new RaceConditions(Weather.HeavyRain, WaterLevel.High);
            Assert.AreEqual(RiskLevel.Danger, RiskAssessment.Evaluate(TestData.Course("test_straight"), rain, PlayerWith(100), TestData.Balance).Level);
        }

        [Test]
        public void 耐久が0になるとリタイアし最下位になる()
        {
            for (ulong seed = 1; seed <= 300; seed++)
            {
                var result = RunReckless("test_rapid", PlayerWith(5, 80), seed);
                var placing = result.PlacingOf(Player);
                if (!placing.Retired || placing.Lost) continue;

                Assert.IsFalse(placing.Finished);
                Assert.AreEqual("リタイア", placing.FormatTime());
                Assert.IsTrue(result.Placings.Skip(placing.Rank).All(p => p.Retired), "リタイアより下位に完走者がいる");
                return;
            }

            Assert.Fail("リタイアが起きない");
        }

        [Test]
        public void 頑丈が高いほど受けるダメージが減る()
        {
            double Average(int toughness)
            {
                double sum = 0;
                for (ulong seed = 1; seed <= 200; seed++)
                {
                    sum += RunReckless("test_rapid", PlayerWith(100, 0, toughness), seed, overrun: -0.1).PlacingOf(Player).DurabilityDamage;
                }

                return sum / 200;
            }

            double normal = Average(60);
            double tough = Average(75);
            TestContext.WriteLine($"頑丈60: {normal:0.0} / 頑丈75: {tough:0.0}");
            Assert.Less(tough, normal);
        }

        [Test]
        public void C04_Lostした個体は現役から外れアルバムに残る()
        {
            var balance = TestData.Balance;
            var resolve = new ResolveRaceUseCase(new FixedBalanceRepository(balance));
            for (ulong seed = 1; seed <= 500; seed++)
            {
                var character = new CharacterState("akane", "アカネ", "maple", CharacterStats.Uniform(60), FurnaceStats.Uniform(50), null, Personality.Normal);
                character.SetDurability(15);
                character.SetFatigue(80);
                var roster = new Roster();
                roster.Add(character);

                var result = RunReckless("test_rapid", RaceEntrant.FromCharacter(character, isPlayer: true), seed);
                if (!result.PlacingOf(Player).Lost) continue;

                resolve.Execute(roster, result, "test_rapid", 12);
                Assert.IsFalse(roster.Active.Any(c => c.Id == "akane"), "Lost 後も現役に残っている");
                var entry = roster.Album.Single(a => a.Id == "akane");
                Assert.AreEqual(CharacterStatus.Lost, entry.Status);
                Assert.AreEqual(12, entry.EndedDay);
                StringAssert.StartsWith("Lost", entry.EndReason);
                Assert.AreEqual(1, entry.Record.Starts);
                Assert.IsNotEmpty(entry.Comment);
                return;
            }

            Assert.Fail("Lost が起きる Seed が見つからない");
        }

        [Test]
        public void レース後に耐久と疲労と戦績が反映され大怪我は最大耐久を下げる()
        {
            var resolve = new ResolveRaceUseCase(new FixedBalanceRepository(TestData.Balance));
            bool sawInjury = false;
            for (ulong seed = 1; seed <= 300 && !sawInjury; seed++)
            {
                var character = new CharacterState("akane", "アカネ", "maple", CharacterStats.Uniform(60), FurnaceStats.Uniform(50), null, Personality.Normal);
                var roster = new Roster();
                roster.Add(character);
                var result = RunReckless("test_rapid", RaceEntrant.FromCharacter(character, isPlayer: true), seed);
                var placing = result.PlacingOf(Player);
                if (placing.Lost) continue;

                var report = resolve.Execute(roster, result, "test_rapid", 3)["akane"];
                Assert.AreEqual(1, character.Record.Starts);
                Assert.Greater(report.FatigueAfter, report.FatigueBefore, "疲労が増えていない");
                Assert.LessOrEqual(report.DurabilityAfter, report.DurabilityBefore);
                if (report.Injured)
                {
                    sawInjury = true;
                    Assert.Less(report.MaxDurabilityAfter, report.MaxDurabilityBefore);
                }
            }

            Assert.IsTrue(sawInjury, "大怪我が一度も起きない");
        }

        [Test]
        public void 引退した個体もアルバムに残る()
        {
            var roster = new Roster();
            roster.Add(new CharacterState("a", "a", "maple", null, null, null, Personality.Normal));
            roster.Retire("a", 30);
            Assert.AreEqual(0, roster.Active.Count);
            Assert.AreEqual(CharacterStatus.Retired, roster.Album[0].Status);
        }

        private sealed class FixedBalanceRepository : IBalanceRepository
        {
            private readonly NagaRace.Domain.Balance.BalanceConfig _balance;

            public FixedBalanceRepository(NagaRace.Domain.Balance.BalanceConfig balance)
            {
                _balance = balance;
            }

            public NagaRace.Domain.Balance.BalanceConfig Load() => _balance;
        }
    }
}
