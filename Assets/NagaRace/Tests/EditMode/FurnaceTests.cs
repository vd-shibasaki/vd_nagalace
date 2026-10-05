using System.Collections.Generic;
using System.Linq;
using NagaRace.Domain.Race;
using NUnit.Framework;

namespace NagaRace.Tests.EditMode
{
    /// <summary>10_ACCEPTANCE_TESTS.md の B（葉脈炉）と葉力チャンス。</summary>
    public class FurnaceTests
    {
        private const int Player = 0;

        /// <summary>自キャラの炉の使い方（テスト用の操作方針）。</summary>
        private sealed class Policy
        {
            public bool UseFurnace = true;
            /// <summary>この進捗以降、始動できるときに始動する。</summary>
            public double ActivateAtProgress;
            /// <summary>SafeDuration からの超過秒数で解除する（負なら Safe 内で解除）。</summary>
            public double ReleaseAfterSafe = -0.1;
            public bool TakeChances = true;
        }

        private static RaceSimulator CreateSimulator(ulong seed, System.Func<RaceEntrant, RaceEntrant> modifyPlayer = null)
        {
            var entrants = TestData.UniformEntrants();
            entrants[Player] = (modifyPlayer ?? (e => e))(entrants[Player].AsPlayer());
            return new RaceSimulator(TestData.Course(TestData.StandardCourse), entrants, RaceConditions.Default, TestData.Balance, seed);
        }

        private static RaceResult Run(RaceSimulator sim, Policy policy)
        {
            var me = sim.Racers[Player];
            while (!sim.IsFinished)
            {
                if (!me.IsFinished)
                {
                    if (policy.TakeChances && me.IsChanceOpen && me.ChanceRemainingSeconds <= 0.6)
                    {
                        sim.SubmitInput(Player, RaceInputType.Charge);
                    }

                    if (policy.UseFurnace)
                    {
                        if (me.Progress >= policy.ActivateAtProgress && sim.CanActivateFurnace(Player))
                        {
                            sim.SubmitInput(Player, RaceInputType.FurnaceActivate);
                        }
                        else if (me.IsFurnaceActive && me.FurnaceActiveSeconds >= me.SafeDurationSeconds + policy.ReleaseAfterSafe)
                        {
                            sim.SubmitInput(Player, RaceInputType.FurnaceRelease);
                        }
                    }
                }

                sim.Step();
            }

            return sim.BuildResult();
        }

        private static int PlayerEvents(RaceResult result, RaceEventType type)
        {
            return result.Events.Count(e => e.EntrantIndex == Player && e.Type == type);
        }

        [Test]
        public void B01_葉力不足では始動できない()
        {
            var sim = CreateSimulator(1);
            sim.SubmitInput(Player, RaceInputType.FurnaceActivate);
            sim.Step();

            Assert.AreEqual(FurnacePhase.Ready, sim.Racers[Player].FurnacePhase);
            Assert.IsTrue(sim.Events.Any(e => e.Type == RaceEventType.FurnaceRejected && e.EntrantIndex == Player));
            Assert.IsFalse(sim.CanActivateFurnace(Player));
        }

        [Test]
        public void B02_SafeDuration内に解除すればOverFlowせず故障抽選もしない()
        {
            for (ulong seed = 1; seed <= 100; seed++)
            {
                var result = Run(CreateSimulator(seed), new Policy { ActivateAtProgress = 0.3, ReleaseAfterSafe = -0.2 });
                Assert.Greater(PlayerEvents(result, RaceEventType.FurnaceStart), 0, $"seed {seed}: 炉を一度も使っていない");
                Assert.AreEqual(0, PlayerEvents(result, RaceEventType.FurnaceOverflow), $"seed {seed}");
                Assert.AreEqual(0, PlayerEvents(result, RaceEventType.FurnaceFailureOccurred), $"seed {seed}");
            }
        }

        [Test]
        public void B03_RiskZoneに長くいるほど故障と重大故障が増える()
        {
            var shortRisk = CountFailures(0.5);
            var longRisk = CountFailures(2.5);
            TestContext.WriteLine($"超過0.5秒: 故障 {shortRisk.failures} 重大 {shortRisk.severe} / 超過2.5秒: 故障 {longRisk.failures} 重大 {longRisk.severe}");

            Assert.Greater(longRisk.failures, shortRisk.failures * 2);
            Assert.Greater(longRisk.severe, shortRisk.severe);
        }

        [Test]
        public void B04_同じ炉性能でも低耐久と高疲労の方が故障しやすい()
        {
            var healthy = CountFailures(1.5);
            var worn = CountFailures(1.5, e => e.WithCondition(30, 100, 90));
            TestContext.WriteLine($"健康: {healthy.failures} / 低耐久・高疲労: {worn.failures}");

            Assert.Greater(worn.failures, healthy.failures * 1.5);
        }

        [Test]
        public void B05_炉の判断で着順が動く()
        {
            double none = AverageRank(new Policy { UseFurnace = false });
            double good = AverageRank(new Policy { ActivateAtProgress = 0.4 });
            double tooLate = AverageRank(new Policy { ActivateAtProgress = 0.93 });
            TestContext.WriteLine($"使わない {none:0.00} / 中盤以降に使う {good:0.00} / 遅すぎ {tooLate:0.00}");

            Assert.GreaterOrEqual(none - good, 1.0, "炉を使っても着順が変わらない");
            Assert.GreaterOrEqual(tooLate - good, 0.5, "使い残しの損が小さすぎる");
        }

        [Test]
        public void 葉力チャンスは1回の受付で1度しか獲得できない()
        {
            var sim = CreateSimulator(4);
            var me = sim.Racers[Player];
            while (!me.IsChanceOpen && !sim.IsFinished) sim.Step();
            Assert.IsTrue(me.IsChanceOpen, "葉力チャンスが開かない");

            for (int i = 0; i < 10; i++)
            {
                sim.SubmitInput(Player, RaceInputType.Charge);
                sim.Step();
            }

            Assert.AreEqual(1, sim.Events.Count(e => e.EntrantIndex == Player && e.Type == RaceEventType.LeafChanceHit));
        }

        [Test]
        public void 葉力チャンスは中央で入力するほど多く獲得できる()
        {
            double center = ChanceGain(waitTicks: 12);
            double edge = ChanceGain(waitTicks: 0);
            TestContext.WriteLine($"中央 {center:0.0} / 開始直後 {edge:0.0}");
            Assert.Greater(center, edge);
        }

        [Test]
        public void 入力ログを再生すると同じ結果になる()
        {
            var original = Run(CreateSimulator(21), new Policy { ActivateAtProgress = 0.4, ReleaseAfterSafe = 1.0 });
            Assert.Greater(original.Inputs.Count, 0);

            var replay = CreateSimulator(21);
            var inputs = original.Inputs.ToList();
            int next = 0;
            while (!replay.IsFinished)
            {
                // 入力は「反映された Tick」で記録されているため、その直前の Step 前に投入する
                while (next < inputs.Count && inputs[next].Tick == replay.Tick + 1)
                {
                    replay.SubmitInput(inputs[next].EntrantIndex, inputs[next].Type);
                    next++;
                }

                replay.Step();
            }

            var result = replay.BuildResult();
            CollectionAssert.AreEqual(original.Events, result.Events);
            CollectionAssert.AreEqual(original.Inputs, result.Inputs);
        }

        [Test]
        public void おまかせ炉はプレイヤーが炉を操作すると手動に切り替わる()
        {
            var sim = CreateSimulator(8, e => e.AsPlayer(autoFurnace: true));
            Assert.IsTrue(sim.Racers[Player].AutoControl);
            sim.SubmitInput(Player, RaceInputType.FurnaceRelease);
            sim.Step();
            Assert.IsFalse(sim.Racers[Player].AutoControl);
        }

        [Test]
        public void NPCは炉を使う()
        {
            var result = TestData.Run(TestData.StandardCourse, TestData.UniformEntrants(), 2);
            Assert.Greater(result.Events.Count(e => e.Type == RaceEventType.FurnaceStart), 4);
        }

        private static (int failures, int severe) CountFailures(double releaseAfterSafe, System.Func<RaceEntrant, RaceEntrant> modify = null)
        {
            int failures = 0;
            int severe = 0;
            for (ulong seed = 1; seed <= 300; seed++)
            {
                var result = Run(CreateSimulator(seed, modify), new Policy { ActivateAtProgress = 0.3, ReleaseAfterSafe = releaseAfterSafe });
                foreach (var e in result.Events.Where(e => e.EntrantIndex == Player && e.Type == RaceEventType.FurnaceFailureOccurred))
                {
                    failures++;
                    if (e.Value >= (int)FurnaceFailure.Blow) severe++;
                }
            }

            return (failures, severe);
        }

        private static double AverageRank(Policy policy)
        {
            double sum = 0;
            for (ulong seed = 1; seed <= 200; seed++)
            {
                sum += Run(CreateSimulator(seed), policy).PlacingOf(Player).Rank;
            }

            return sum / 200;
        }

        private static double ChanceGain(int waitTicks)
        {
            var sim = CreateSimulator(4);
            var me = sim.Racers[Player];
            while (!me.IsChanceOpen) sim.Step();
            for (int i = 0; i < waitTicks; i++) sim.Step();
            sim.SubmitInput(Player, RaceInputType.Charge);
            sim.Step();
            return sim.Events.First(e => e.EntrantIndex == Player && e.Type == RaceEventType.LeafChanceHit).Value;
        }
    }
}
