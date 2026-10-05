using System;
using System.Collections.Generic;
using System.Linq;
using NagaRace.Domain.Characters;
using NagaRace.Domain.Race;
using NUnit.Framework;

namespace NagaRace.Tests.EditMode
{
    /// <summary>
    /// 10_ACCEPTANCE_TESTS.md の A（Race Core）。
    /// 統計テストは Seed 範囲を固定しているため、結果は毎回同じになる（フレーキーにならない）。
    /// </summary>
    public class RaceCoreAcceptanceTests
    {
        private const int StatSeeds = 200;

        [Test]
        public void A01_同じ条件とSeedなら着順とイベントが一致する()
        {
            foreach (var courseId in TestData.CourseIds)
            {
                var entrants = NpcFactory.Generate(new NpcSpec(TestData.Species), 77, 8);
                var first = TestData.Run(courseId, entrants, 123456789UL);
                var second = TestData.Run(courseId, entrants, 123456789UL);

                CollectionAssert.AreEqual(
                    first.Placings.Select(p => (p.EntrantIndex, p.FinishTimeMilliseconds)).ToList(),
                    second.Placings.Select(p => (p.EntrantIndex, p.FinishTimeMilliseconds)).ToList(),
                    courseId);
                CollectionAssert.AreEqual(first.Events, second.Events, courseId);
                Assert.AreEqual(first.TotalTicks, second.TotalTicks, courseId);
            }
        }

        [Test]
        public void A01_Seedが違えば結果が変わる()
        {
            var entrants = TestData.UniformEntrants();
            var winners = new HashSet<int>();
            for (ulong seed = 1; seed <= 20; seed++)
            {
                winners.Add(TestData.Run(TestData.StandardCourse, entrants, seed).Winner.EntrantIndex);
            }

            Assert.Greater(winners.Count, 1, "Seed を変えても毎回同じ個体が勝っている");
        }

        [Test]
        public void A02_流速だけ高い個体は優位だが勝ち切れないこともある()
        {
            var stats = Boosted(TestData.StandardCourse, e => e.WithStats(e.Stats.With(StatType.FlowSpeed, 75)));
            TestContext.WriteLine($"流速75: 1着率 {stats.WinRate:P1} / 平均着順 {stats.AverageRank:0.00}");

            Assert.That(stats.WinRate, Is.InRange(0.25, 0.45), "1着率");
            Assert.LessOrEqual(stats.AverageRank, 3.5, "平均着順");
        }

        [Test]
        public void A02b_しっかり育てた個体は勝ちやすい()
        {
            var stats = Boosted(TestData.StandardCourse, e => e.WithStats(e.Stats.AddAll(15)));
            TestContext.WriteLine($"全能力+15: 1着率 {stats.WinRate:P1} / 3着以内 {stats.Top3Rate:P1}");

            Assert.That(stats.WinRate, Is.InRange(0.50, 0.75), "1着率");
            Assert.GreaterOrEqual(stats.Top3Rate, 0.85, "3着以内率");
        }

        [Test]
        public void A03_枠順による有利不利が出ない()
        {
            const int seeds = 400;
            var wins = new int[8];
            var entrants = TestData.UniformEntrants();
            for (ulong seed = 1; seed <= seeds; seed++)
            {
                wins[TestData.Run(TestData.StandardCourse, entrants, seed).Winner.EntrantIndex]++;
            }

            TestContext.WriteLine("枠別1着率: " + string.Join(" ", wins.Select(w => ((double)w / seeds).ToString("P1"))));
            for (int gate = 0; gate < 8; gate++)
            {
                Assert.That((double)wins[gate] / seeds, Is.InRange(0.125 - 0.06, 0.125 + 0.06), $"枠 {gate}");
            }
        }

        [Test]
        public void A04_勝者タイムが目標時間に収まる()
        {
            var range = TestData.Balance.Race.TargetDurationSeconds;
            foreach (var courseId in TestData.CourseIds)
            {
                int inRange = 0;
                for (ulong seed = 1; seed <= StatSeeds; seed++)
                {
                    var entrants = NpcFactory.Generate(new NpcSpec(TestData.Species), seed * 7919UL, 8);
                    double time = TestData.Run(courseId, entrants, seed).Winner.FinishTimeSeconds;
                    if (time >= range[0] && time <= range[1]) inRange++;
                }

                TestContext.WriteLine($"{courseId}: 目標時間内 {(double)inRange / StatSeeds:P0}");
                Assert.GreaterOrEqual((double)inRange / StatSeeds, 0.95, courseId);
            }
        }

        // 頑丈（Toughness）はダメージ軽減の能力で、ダメージは M4 で実装するため、M1 では着順に影響しない。
        // TODO(M4): 頑丈は「受けるダメージ量が減る」ことをテストする（10 A-05 の注記）。
        [TestCase(StatType.FlowSpeed, "test_straight")]
        [TestCase(StatType.Acceleration, "test_rapid")]
        [TestCase(StatType.Buoyancy, "test_rapid")]
        [TestCase(StatType.Stability, "test_rapid")]
        [TestCase(StatType.Judgement, "test_branch")]
        public void A05_能力を上げると平均着順が良くなる(StatType stat, string courseId)
        {
            var stats = Boosted(courseId, e => e.WithStats(e.Stats.With(stat, e.Stats[stat] + 15)));
            TestContext.WriteLine($"{stat}+15 @ {courseId}: 平均着順 {stats.AverageRank:0.00}（基準 {stats.BaselineAverageRank:0.00}）");

            Assert.Less(stats.AverageRank, stats.BaselineAverageRank - 0.15);
        }

        [Test]
        public void A06_レースは必ず制限時間内に終わる()
        {
            foreach (var courseId in TestData.CourseIds)
            {
                for (ulong seed = 1; seed <= 50; seed++)
                {
                    var entrants = NpcFactory.Generate(new NpcSpec(TestData.Species, statOffset: -30), seed, 8);
                    var result = TestData.Run(courseId, entrants, seed);
                    Assert.IsFalse(result.TimedOut, $"{courseId} seed {seed}");
                    Assert.IsTrue(result.Placings.All(p => p.Finished), $"{courseId} seed {seed}");
                }
            }
        }

        [Test]
        public void 着順はゴールタイム順で同着は枠番順()
        {
            var result = TestData.Run(TestData.StandardCourse, TestData.UniformEntrants(), 5);
            for (int i = 1; i < result.Placings.Count; i++)
            {
                var prev = result.Placings[i - 1];
                var current = result.Placings[i];
                Assert.LessOrEqual(prev.FinishTimeMilliseconds, current.FinishTimeMilliseconds);
                if (prev.FinishTimeMilliseconds == current.FinishTimeMilliseconds)
                {
                    Assert.Less(prev.EntrantIndex, current.EntrantIndex);
                }
            }
        }

        private sealed class BoostStats
        {
            public double WinRate;
            public double Top3Rate;
            public double AverageRank;
            public double BaselineAverageRank;
        }

        /// <summary>
        /// 全能力60の8体のうち1体だけ変更して Seed 1〜200 を走らせる。
        /// 変更する個体の枠は Seed ごとにずらし、枠の影響を打ち消す。
        /// </summary>
        private static BoostStats Boosted(string courseId, Func<RaceEntrant, RaceEntrant> modify)
        {
            int wins = 0;
            int top3 = 0;
            double rankSum = 0;
            double baselineRankSum = 0;
            for (ulong seed = 1; seed <= StatSeeds; seed++)
            {
                int index = (int)(seed % 8);
                var entrants = TestData.UniformEntrants();
                baselineRankSum += TestData.Run(courseId, entrants, seed).PlacingOf(index).Rank;

                entrants[index] = modify(entrants[index]);
                int rank = TestData.Run(courseId, entrants, seed).PlacingOf(index).Rank;
                if (rank == 1) wins++;
                if (rank <= 3) top3++;
                rankSum += rank;
            }

            return new BoostStats
            {
                WinRate = (double)wins / StatSeeds,
                Top3Rate = (double)top3 / StatSeeds,
                AverageRank = rankSum / StatSeeds,
                BaselineAverageRank = baselineRankSum / StatSeeds,
            };
        }
    }
}
