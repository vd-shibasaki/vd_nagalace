using System;
using System.Collections.Generic;
using System.Linq;
using NagaRace.Application.Lineage;
using NagaRace.Application.Race;
using NagaRace.Domain.Balance;
using NagaRace.Domain.Characters;
using NagaRace.Domain.Lineage;
using NagaRace.Domain.Training;
using NUnit.Framework;

namespace NagaRace.Tests.EditMode
{
    /// <summary>10_ACCEPTANCE_TESTS.md の F（Grafting）と 09 M7 の完了条件。</summary>
    public class GraftingTests
    {
        private static BalanceConfig B => TestData.Balance;

        private static CharacterState Retired(string id, string species, CharacterStats stats, FurnaceStats furnace = null, string lineage = null)
        {
            var c = new CharacterState(id, id, species, stats, furnace ?? FurnaceStats.Uniform(55), null, Personality.Normal, lineageId: lineage ?? $"lineage-{id}");
            c.Retire(30);
            return c;
        }

        private static ParentTree Tree(CharacterState c) => ParentTree.FromCharacter(c, TestData.Lineage, B, 30);

        private static List<CharacterState> Children(ParentTree a, ParentTree b, ulong seedFrom, int seeds)
        {
            var list = new List<CharacterState>();
            for (ulong seed = seedFrom; seed < seedFrom + (ulong)seeds; seed++)
            {
                list.AddRange(GraftingResolver.CreateCandidates(a, b, TestData.Species, TestData.Lineage, B, seed, 30, $"c{seed}").Select(c => c.Character));
            }

            return list;
        }

        [Test]
        public void F01_子候補は親の能力を一定程度反映する()
        {
            var fastA = Tree(Retired("fa", "maple", CharacterStats.Uniform(55).With(StatType.FlowSpeed, 90)));
            var fastB = Tree(Retired("fb", "maple", CharacterStats.Uniform(55).With(StatType.FlowSpeed, 85)));
            var slowA = Tree(Retired("sa", "maple", CharacterStats.Uniform(55).With(StatType.FlowSpeed, 40)));
            var slowB = Tree(Retired("sb", "maple", CharacterStats.Uniform(55).With(StatType.FlowSpeed, 45)));

            double fast = Children(fastA, fastB, 1, 100).Average(c => c.Stats.FlowSpeed);
            double slow = Children(slowA, slowB, 1, 100).Average(c => c.Stats.FlowSpeed);
            TestContext.WriteLine($"速い親どうし: 流速 {fast:0.0} / 遅い親どうし: 流速 {slow:0.0}");

            Assert.Greater(fast - slow, 20, "親の流速の差が子に出ない");
            // 完全コピーではない（若葉として生まれ、系統の基準へ寄る）
            Assert.Less(fast, 87.5);
        }

        [Test]
        public void F01_能力の予測幅に実際の子が概ね収まる()
        {
            var a = Tree(Retired("a", "maple", new CharacterStats(80, 70, 50, 45, 50, 60)));
            var b = Tree(Retired("b", "ginkgo", new CharacterStats(60, 50, 55, 75, 75, 60)));
            var preview = GraftingResolver.Preview(a, b, TestData.Species, B);
            var children = Children(a, b, 1, 100);
            int inside = 0, total = 0;
            foreach (var child in children)
            {
                foreach (StatType type in Enum.GetValues(typeof(StatType)))
                {
                    var range = preview.StatRanges[$"stats.{char.ToLowerInvariant(type.ToString()[0])}{type.ToString().Substring(1)}"];
                    // 因子・突然変異の上乗せを除いた範囲なので、少しはみ出すのは許容
                    total++;
                    if (child.Stats[type] >= range.min - 3 && child.Stats[type] <= range.max + 12) inside++;
                }
            }

            Assert.Greater((double)inside / total, 0.9);
        }

        [Test]
        public void F02_候補3体は完全同一にならない()
        {
            var a = Tree(Retired("a", "maple", CharacterStats.Uniform(70)));
            var b = Tree(Retired("b", "lotus", CharacterStats.Uniform(60)));
            for (ulong seed = 1; seed <= 50; seed++)
            {
                var candidates = GraftingResolver.CreateCandidates(a, b, TestData.Species, TestData.Lineage, B, seed, 30, "c");
                Assert.AreEqual(B.Grafting.CandidateCount, candidates.Count);
                var signatures = candidates.Select(c => c.Character.Stats + "|" + c.Character.SpeciesId).Distinct().Count();
                Assert.AreEqual(candidates.Count, signatures, $"seed {seed}: 同じ候補がある");
            }
        }

        [Test]
        public void F03_突然変異はSeedで再現でき長所と短所がある()
        {
            var a = Tree(Retired("a", "maple", CharacterStats.Uniform(70)));
            var b = Tree(Retired("b", "maple", CharacterStats.Uniform(70)));
            ulong found = 0;
            string mutation = null;
            for (ulong seed = 1; seed <= 2000 && found == 0; seed++)
            {
                var child = GraftingResolver.CreateCandidates(a, b, TestData.Species, TestData.Lineage, B, seed, 30, "c").First().Character;
                if (child.MutationId == null) continue;
                found = seed;
                mutation = child.MutationId;
            }

            Assert.AreNotEqual(0UL, found, "突然変異が起きない");
            var again = GraftingResolver.CreateCandidates(a, b, TestData.Species, TestData.Lineage, B, found, 30, "c").First().Character;
            Assert.AreEqual(mutation, again.MutationId, "同じ Seed で同じ突然変異にならない");

            foreach (var m in TestData.Lineage.Mutations)
            {
                Assert.IsTrue(m.Modifiers.Values.Any(v => v > 0) && m.Modifiers.Values.Any(v => v < 0), $"{m.Name} が上位互換になっている");
            }
        }

        [Test]
        public void 引退すると後天因子が登録されロストでは失われる()
        {
            var stats = CharacterStats.Uniform(60).With(StatType.FlowSpeed, 88);
            var retired = Retired("r", "maple", stats, new FurnaceStats(90, 50, 50, 50, 50));
            var lost = new CharacterState("l", "l", "maple", stats, new FurnaceStats(90, 50, 50, 50, 50), null, Personality.Normal);
            lost.MarkLost(30, "LostFurnace");

            var retiredTree = Tree(retired);
            var lostTree = Tree(lost);
            Assert.IsTrue(retiredTree.Factors.Any(f => f.FactorId == "swift" && f.Level == 2), "流速88で「俊流」Lv2 が付かない");
            Assert.IsTrue(retiredTree.Factors.Any(f => f.FactorId == "high_output_vein" && f.Level == 2));
            Assert.AreEqual(0, lostTree.Factors.Count, "ロストでも後天因子が登録されている");
            Assert.IsTrue(lostTree.FromLost);
            Assert.Throws<InvalidOperationException>(() => Tree(new CharacterState("x", "x", "maple", null, null, null, Personality.Normal)));
        }

        [Test]
        public void 因子は全部は継承されず継承すると能力が上がる()
        {
            var a = Tree(Retired("a", "maple", CharacterStats.Uniform(60).With(StatType.FlowSpeed, 96).With(StatType.Judgement, 90), new FurnaceStats(95, 50, 50, 50, 50)));
            var b = Tree(Retired("b", "maple", CharacterStats.Uniform(60)));
            int parentFactors = a.Factors.Count;
            Assert.GreaterOrEqual(parentFactors, 2);

            var children = Children(a, b, 1, 100);
            double average = children.Average(c => c.Factors.Count);
            TestContext.WriteLine($"親の因子 {parentFactors} 個 / 子の平均 {average:0.00} 個");
            Assert.Greater(average, 0.3);
            Assert.Less(average, parentFactors);

            double with = children.Where(c => c.Factors.Any(f => f.FactorId == "swift")).Average(c => c.Stats.FlowSpeed);
            double without = children.Where(c => c.Factors.All(f => f.FactorId != "swift")).Average(c => c.Stats.FlowSpeed);
            Assert.Greater(with, without);
        }

        [Test]
        public void 系統の違う親木どうしは相性が良くばらつきが小さい()
        {
            var a = Tree(Retired("a", "maple", CharacterStats.Uniform(60), lineage: "L1"));
            var sameLineage = Tree(Retired("b", "ginkgo", CharacterStats.Uniform(60), lineage: "L1"));
            var otherLineage = Tree(Retired("c", "ginkgo", CharacterStats.Uniform(60), lineage: "L2"));
            Assert.Greater(GraftingResolver.Compatibility(a, otherLineage, B), GraftingResolver.Compatibility(a, sameLineage, B));
        }

        [Test]
        public void 子は若葉として世代が進み同じ葉統に属する()
        {
            var a = Tree(Retired("a", "maple", CharacterStats.Uniform(60), lineage: "akane-line"));
            var b = Tree(Retired("b", "ginkgo", CharacterStats.Uniform(60)));
            var child = GraftingResolver.CreateCandidates(a, b, TestData.Species, TestData.Lineage, B, 5, 40, "c").First().Character;
            Assert.AreEqual(2, child.Generation);
            Assert.AreEqual("akane-line", child.LineageId);
            Assert.AreEqual(GrowthPhase.Young, child.PhaseOn(40));
            Assert.IsTrue(child.IsActive);
        }

        [Test]
        public void ユースケースで登録から迎え入れまでできる()
        {
            var session = new GameSession(9, 20);
            var a = new CharacterState("a", "アカネ", "maple", CharacterStats.Uniform(70), null, null, Personality.Gambler);
            var b = new CharacterState("b", "ギンナン", "ginkgo", CharacterStats.Uniform(65), null, null, Personality.Cautious);
            session.Roster.Add(a);
            session.Roster.Add(b);
            session.Roster.Retire("a", 20);
            session.Roster.Retire("b", 20);

            var useCase = new GraftingUseCase(new FixedBalance(B), new FixedSpecies(TestData.Species), new FixedLineage(TestData.Lineage));
            var treeA = useCase.RegisterParentTree(session, "a");
            var treeB = useCase.RegisterParentTree(session, "b");
            Assert.Throws<InvalidOperationException>(() => useCase.RegisterParentTree(session, "a"), "二重登録できてしまう");

            var preview = useCase.Preview(session, treeA.Id, treeB.Id);
            Assert.AreEqual(6, preview.StatRanges.Count);
            var candidates = useCase.CreateCandidates(session, treeA.Id, treeB.Id);
            var again = useCase.CreateCandidates(session, treeA.Id, treeB.Id);
            Assert.AreEqual(candidates[0].Character.Stats.ToString(), again[0].Character.Stats.ToString(), "同じ日・同じ組み合わせで候補が変わる");

            useCase.Adopt(session, candidates[1], "モミジ丸");
            Assert.AreEqual(1, session.Roster.Active.Count);
            Assert.AreEqual("モミジ丸", session.Roster.Active[0].Name);
            Assert.AreEqual(20, session.Day, "接ぎ木で日が進んでいる");
        }

        private sealed class FixedBalance : IBalanceRepository
        {
            private readonly BalanceConfig _b;
            public FixedBalance(BalanceConfig b) { _b = b; }
            public BalanceConfig Load() => _b;
        }

        private sealed class FixedSpecies : ISpeciesRepository
        {
            private readonly IReadOnlyList<SpeciesDefinition> _s;
            public FixedSpecies(IReadOnlyList<SpeciesDefinition> s) { _s = s; }
            public IReadOnlyList<SpeciesDefinition> GetAll() => _s;
        }

        private sealed class FixedLineage : ILineageCatalogRepository
        {
            private readonly LineageCatalog _c;
            public FixedLineage(LineageCatalog c) { _c = c; }
            public LineageCatalog Load() => _c;
        }
    }
}
