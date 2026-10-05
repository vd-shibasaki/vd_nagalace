using System;
using System.Linq;
using NagaRace.Application.Race;
using NagaRace.Application.Training;
using NagaRace.Domain.Balance;
using NagaRace.Domain.Characters;
using NagaRace.Domain.Common;
using NagaRace.Domain.Race;
using NagaRace.Domain.Training;
using NUnit.Framework;

namespace NagaRace.Tests.EditMode
{
    /// <summary>10_ACCEPTANCE_TESTS.md の D（Training）と E（Growth）、09 M5/M6 の完了条件。</summary>
    public class TrainingGrowthTests
    {
        private static BalanceConfig B => TestData.Balance;
        private static TrainingCatalog Catalog => TestData.Trainings;

        private static CharacterState NewCharacter(int stats = 45, int fatigue = 0)
        {
            var c = new CharacterState("c", "c", "maple", CharacterStats.Uniform(stats), FurnaceStats.Uniform(45), null, Personality.Normal);
            c.SetFatigue(fatigue);
            return c;
        }

        private static TrainingResult Train(CharacterState c, string trainingId, Weather weather, ulong seed, GrowthPhase phase = GrowthPhase.Growing)
        {
            return TrainingResolver.Apply(c, Catalog.Get(trainingId), weather, phase, Catalog, B, new Pcg32Random(seed), confirmedExtreme: true);
        }

        private static double AverageGain(string trainingId, string key, Func<CharacterState> create, Weather weather, GrowthPhase phase = GrowthPhase.Growing)
        {
            double sum = 0;
            for (ulong seed = 1; seed <= 300; seed++)
            {
                var result = Train(create(), trainingId, weather, seed, phase);
                result.StatDeltas.TryGetValue(key, out int delta);
                sum += delta;
            }

            return sum / 300;
        }

        [Test]
        public void D01_天候により育成結果が変わる()
        {
            double sunny = AverageGain("sun", "stats.flowSpeed", () => NewCharacter(), Weather.Sunny);
            double rain = AverageGain("sun", "stats.flowSpeed", () => NewCharacter(), Weather.Rain);
            TestContext.WriteLine($"日向ならし 流速: 晴れ {sunny:0.00} / 雨 {rain:0.00}");
            Assert.Greater(sunny, rain * 1.5);
        }

        [Test]
        public void D02_疲労が高いと効率が落ちる()
        {
            double fresh = AverageGain("stream", "stats.judgement", () => NewCharacter(fatigue: 0), Weather.Cloudy);
            double tired = AverageGain("stream", "stats.judgement", () => NewCharacter(fatigue: 90), Weather.Cloudy);
            TestContext.WriteLine($"小川流し 判断: 疲労0 {fresh:0.00} / 疲労90 {tired:0.00}");
            Assert.Greater(fresh, tired * 1.5);
        }

        [Test]
        public void D02_同じならしを続けると効率が落ちる()
        {
            var c = NewCharacter();
            var first = TrainingResolver.Preview(c, Catalog.Get("wind"), Weather.Wind, GrowthPhase.Growing, B).Efficiency;
            Train(c, "wind", Weather.Wind, 1);
            Train(c, "wind", Weather.Wind, 2);
            c.SetFatigue(0);
            var third = TrainingResolver.Preview(c, Catalog.Get("wind"), Weather.Wind, GrowthPhase.Growing, B).Efficiency;
            Assert.Less(third, first);
        }

        [Test]
        public void D03_特定のならしの累積で体質が付き長所と短所がある()
        {
            var c = NewCharacter();
            for (int i = 0; i < 8 && !c.HasBodyTrait("tough_skin"); i++)
            {
                Train(c, "sparring", Weather.Cloudy, (ulong)i + 1);
                c.SetFatigue(0);
            }

            Assert.IsTrue(c.HasBodyTrait("tough_skin"), "ぶつかり稽古を続けても体質「打たれ強い」が付かない");
            var trait = Catalog.FindBodyTrait("tough_skin");
            Assert.IsTrue(trait.Modifiers.Values.Any(v => v > 0) && trait.Modifiers.Values.Any(v => v < 0), "体質が完全な上位互換になっている");
        }

        /// <summary>09 M5 完了条件: 10〜20ターン育成するとキャラの方向性に差が出る / 同じコマンド連打だけが最適にならない。</summary>
        [Test]
        public void M5_育成方針で方向性が変わり連打は最適にならない()
        {
            var speed = RunPlan(15, (s, c) => c.Fatigue > 60 ? "rest" : (s.TodayConditions(B).Weather == Weather.Sunny ? "sun" : "stream"));
            var tough = RunPlan(15, (s, c) => c.Fatigue > 60 ? "rest" : (s.Day % 2 == 0 ? "sparring" : "wind"));
            var spam = RunPlan(15, (s, c) => "sun");
            TestContext.WriteLine($"スピード型 流速+{speed.flow:0.0} 頑丈+{speed.tough:0.0} 合計+{speed.total:0.0} / 頑丈型 流速+{tough.flow:0.0} 頑丈+{tough.tough:0.0} / 連打 合計+{spam.total:0.0} 疲労{spam.fatigue:0}");

            Assert.Greater(speed.flow - tough.flow, 10, "スピード型の方が流速が伸びる");
            Assert.Greater(tough.tough - speed.tough, 10, "頑丈型の方が頑丈が伸びる");
            Assert.Greater(speed.total, spam.total, "休養を挟む方が連打より伸びる");
            Assert.GreaterOrEqual(spam.fatigue, B.Training.HighFatigueThreshold, "連打しても疲れない");
        }

        [Test]
        public void 極危険のならしは確認なしでは実行できない()
        {
            var c = NewCharacter(fatigue: 80);
            c.SetDurability(10);
            var preview = TrainingResolver.Preview(c, Catalog.Get("rapid"), Weather.Rain, GrowthPhase.Growing, B);
            Assert.AreEqual(RiskLevel.Extreme, preview.Risk);
            Assert.IsTrue(preview.NeedsConfirmation);
            Assert.IsTrue(preview.LostPossible);
            Assert.Throws<InvalidOperationException>(() =>
                TrainingResolver.Apply(c, Catalog.Get("rapid"), Weather.Rain, GrowthPhase.Growing, Catalog, B, new Pcg32Random(1)));

            // 通常の状態では Lost は起こりえない
            var healthy = TrainingResolver.Preview(NewCharacter(), Catalog.Get("rapid"), Weather.Rain, GrowthPhase.Growing, B);
            Assert.IsFalse(healthy.LostPossible);
        }

        [Test]
        public void E_若葉と成長期で成長係数が異なる()
        {
            double young = AverageGain("stream", "stats.judgement", () => NewCharacter(), Weather.Cloudy, GrowthPhase.Young);
            double growing = AverageGain("stream", "stats.judgement", () => NewCharacter(), Weather.Cloudy, GrowthPhase.Growing);
            TestContext.WriteLine($"若葉 {young:0.00} / 成長 {growing:0.00}");
            Assert.Greater(growing, young * 1.2);
        }

        [Test]
        public void E_紅葉期以降は能力が緩やかに下がり最盛期は下がらない()
        {
            int Decline(GrowthPhase target)
            {
                var session = new GameSession(42);
                var c = NewCharacter(60);
                session.Roster.Add(c);
                // 目標フェーズの初日まで日付を進める
                while (c.PhaseOn(session.Day) != target) session.SetDay(session.Day + 1);
                int before = c.Stats.FlowSpeed + c.Stats.Acceleration + c.Stats.Stability;
                for (int i = 0; i < 12; i++) session.AdvanceDay(B);
                return before - (c.Stats.FlowSpeed + c.Stats.Acceleration + c.Stats.Stability);
            }

            int peak = Decline(GrowthPhase.Peak);
            int autumn = Decline(GrowthPhase.Autumn);
            int withered = Decline(GrowthPhase.Withered);
            TestContext.WriteLine($"12日間の低下: 最盛 {peak} / 紅葉 {autumn} / 枯葉 {withered}");
            Assert.AreEqual(0, peak);
            Assert.Greater(autumn, 0);
            Assert.Greater(withered, autumn);
        }

        [Test]
        public void 成長タイプで最盛期の時期が変わる()
        {
            double PeakStart(GrowthType type)
            {
                double sum = 0;
                for (ulong seed = 1; seed <= 100; seed++)
                {
                    var p = GrowthProfile.Roll(type, B, new Pcg32Random(seed));
                    sum += p.PhaseDays[0] + p.PhaseDays[1];
                }

                return sum / 100;
            }

            Assert.Less(PeakStart(GrowthType.Early), PeakStart(GrowthType.Normal));
            Assert.Less(PeakStart(GrowthType.Normal), PeakStart(GrowthType.Late));
        }

        [Test]
        public void 成長フェーズはレースの速度に反映される()
        {
            Assert.Less(GrowthRules.RaceSpeedMultiplier(GrowthPhase.Young, B), GrowthRules.RaceSpeedMultiplier(GrowthPhase.Peak, B));
            Assert.Less(GrowthRules.RaceSpeedMultiplier(GrowthPhase.Withered, B), GrowthRules.RaceSpeedMultiplier(GrowthPhase.Peak, B));

            var c = NewCharacter(60);
            var peak = RaceEntrant.FromCharacter(c, true, phaseSpeedMultiplier: GrowthRules.RaceSpeedMultiplier(GrowthPhase.Peak, B));
            var withered = RaceEntrant.FromCharacter(c, true, phaseSpeedMultiplier: GrowthRules.RaceSpeedMultiplier(GrowthPhase.Withered, B));
            double sumPeak = 0, sumWithered = 0;
            for (ulong seed = 1; seed <= 100; seed++)
            {
                var a = TestData.UniformEntrants();
                a[0] = peak;
                sumPeak += TestData.Run(TestData.StandardCourse, a, seed).PlacingOf(0).Rank;
                a[0] = withered;
                sumWithered += TestData.Run(TestData.StandardCourse, a, seed).PlacingOf(0).Rank;
            }

            Assert.Less(sumPeak, sumWithered, "最盛期の方が着順が良い");
        }

        [Test]
        public void 暦は1週4日1季12日1年48日()
        {
            Assert.AreEqual("1年目 春 1週1日", new GameDate(1, B.Calendar).Label);
            Assert.AreEqual("1年目 春 3週4日", new GameDate(12, B.Calendar).Label);
            Assert.AreEqual("1年目 夏 1週1日", new GameDate(13, B.Calendar).Label);
            Assert.AreEqual("2年目 春 1週1日", new GameDate(49, B.Calendar).Label);
        }

        [Test]
        public void 天気は同じSeedと日付なら同じで季節で出方が変わる()
        {
            var date = new GameDate(5, B.Calendar);
            Assert.AreEqual(WeatherForecast.For(7, date, B).Weather, WeatherForecast.For(7, date, B).Weather);

            int summerHeavy = 0, winterHeavy = 0;
            for (ulong seed = 1; seed <= 2000; seed++)
            {
                if (WeatherForecast.For(seed, new GameDate(13, B.Calendar), B).Weather == Weather.HeavyRain) summerHeavy++;
                if (WeatherForecast.For(seed, new GameDate(37, B.Calendar), B).Weather == Weather.HeavyRain) winterHeavy++;
            }

            Assert.Greater(summerHeavy, winterHeavy * 2);
        }

        [Test]
        public void ならしを実行すると1日進む()
        {
            var session = new GameSession(3);
            session.Roster.Add(NewCharacter());
            var useCase = new TrainingUseCase(new FixedBalance(B), new FixedCatalog(Catalog));
            var preview = useCase.Preview(session, "c", "stream");
            Assert.Greater(preview.ExpectedGains.Count, 0);
            useCase.Execute(session, "c", "stream");
            Assert.AreEqual(2, session.Day);
        }

        private static (double flow, double tough, double total, double fatigue) RunPlan(int turns, Func<GameSession, CharacterState, string> plan)
        {
            double flow = 0, tough = 0, total = 0, fatigue = 0;
            const int runs = 200;
            for (ulong seed = 1; seed <= runs; seed++)
            {
                var session = new GameSession(seed * 1000003UL);
                var c = NewCharacter();
                c.Growth = GrowthProfile.Roll(GrowthType.Normal, B, session.RandomFor("growth", c.Id));
                session.Roster.Add(c);
                int before = Total(c);
                for (int t = 0; t < turns && c.IsActive; t++)
                {
                    TrainingResolver.Apply(c, Catalog.Get(plan(session, c)), session.TodayConditions(B).Weather, c.PhaseOn(session.Day), Catalog, B, session.RandomFor("training", c.Id), true);
                    session.AdvanceDay(B);
                }

                flow += c.Stats.FlowSpeed - 45;
                tough += c.Stats.Toughness - 45;
                total += Total(c) - before;
                fatigue += c.Fatigue;
            }

            return (flow / runs, tough / runs, total / runs, fatigue / runs);
        }

        private static int Total(CharacterState c)
        {
            int sum = 0;
            foreach (StatType t in Enum.GetValues(typeof(StatType))) sum += c.Stats[t];
            return sum + c.Furnace.Output + c.Furnace.Startup + c.Furnace.CriticalDuration + c.Furnace.Cooling + c.Furnace.Durability
                + c.Affinities[AffinityType.Rapid] + c.Affinities[AffinityType.Whirlpool] + c.Affinities[AffinityType.Narrow];
        }

        private sealed class FixedBalance : IBalanceRepository
        {
            private readonly BalanceConfig _b;
            public FixedBalance(BalanceConfig b) { _b = b; }
            public BalanceConfig Load() => _b;
        }

        private sealed class FixedCatalog : ITrainingCatalogRepository
        {
            private readonly TrainingCatalog _c;
            public FixedCatalog(TrainingCatalog c) { _c = c; }
            public TrainingCatalog Load() => _c;
        }
    }
}
