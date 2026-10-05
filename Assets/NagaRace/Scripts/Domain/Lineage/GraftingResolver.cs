using System;
using System.Collections.Generic;
using System.Linq;
using NagaRace.Domain.Balance;
using NagaRace.Domain.Characters;
using NagaRace.Domain.Common;

namespace NagaRace.Domain.Lineage
{
    /// <summary>接ぎ木の予測（03 §10 中央「継承予測」）。</summary>
    public sealed class GraftingPreview
    {
        /// <summary>相性 0〜100。高いほど子のばらつきが小さい。</summary>
        public int Compatibility { get; set; }
        /// <summary>能力キー → 生まれたときの予測幅 [最小, 最大]。</summary>
        public Dictionary<string, (int min, int max)> StatRanges { get; } = new Dictionary<string, (int, int)>();
        /// <summary>継承しうる因子と、そのおおよその継承率。</summary>
        public Dictionary<string, double> FactorChances { get; } = new Dictionary<string, double>();
        /// <summary>特異葉（突然変異）の可能性。正確な確率は出さない（03 §10）。</summary>
        public string MutationHint { get; set; }
    }

    /// <summary>接ぎ木で生まれる若葉の候補。</summary>
    public sealed class GraftCandidate
    {
        public GraftCandidate(CharacterState character)
        {
            Character = character;
        }

        public CharacterState Character { get; }
    }

    /// <summary>
    /// 接ぎ木（05 §7）。親木A + 親木B から若葉の候補を作る。
    /// 各能力 = 親の寄与（比率はランダム）を系統の基準へ少し寄せ + ばらつき + 因子 + 突然変異。
    /// 親の特徴は感じられるが、完全コピーにはならない（09 M7）。
    /// </summary>
    public static class GraftingResolver
    {
        private static readonly StatType[] StatTypes = (StatType[])Enum.GetValues(typeof(StatType));
        private static readonly AffinityType[] AffinityTypes = (AffinityType[])Enum.GetValues(typeof(AffinityType));
        private static readonly FurnaceStat[] FurnaceStatsAll = (FurnaceStat[])Enum.GetValues(typeof(FurnaceStat));
        private static readonly Personality[] Personalities = (Personality[])Enum.GetValues(typeof(Personality));
        private static readonly GrowthType[] GrowthTypes = (GrowthType[])Enum.GetValues(typeof(GrowthType));

        public static int Compatibility(ParentTree a, ParentTree b, BalanceConfig balance)
        {
            var c = balance.Grafting.Compatibility;
            double score = c.Base;
            if (a.LineageId != b.LineageId) score += c.DifferentLineage;
            if (a.SpeciesId == b.SpeciesId) score += c.SameSpecies;
            int overlap = a.Factors.Count(f => b.Factors.Any(g => g.FactorId == f.FactorId));
            score -= overlap * c.FactorOverlapPenalty;
            return MathUtil.Clamp((int)Math.Round(score), 0, 100);
        }

        public static GraftingPreview Preview(ParentTree a, ParentTree b, IReadOnlyList<SpeciesDefinition> species, BalanceConfig balance)
        {
            var g = balance.Grafting;
            var preview = new GraftingPreview { Compatibility = Compatibility(a, b, balance) };
            double sd = VarianceSd(preview.Compatibility, balance);
            foreach (var type in StatTypes)
            {
                double low = Math.Min(a.Stats[type], b.Stats[type]);
                double high = Math.Max(a.Stats[type], b.Stats[type]);
                double centerA = SpeciesCenter(Find(species, a.SpeciesId), type);
                double centerB = SpeciesCenter(Find(species, b.SpeciesId), type);
                double min = MathUtil.Lerp(low, Math.Min(centerA, centerB), g.SpeciesPull) - sd * 2;
                double max = MathUtil.Lerp(high, Math.Max(centerA, centerB), g.SpeciesPull) + sd * 2;
                preview.StatRanges[$"stats.{Camel(type.ToString())}"] = (Clamp(min * g.ChildStartRatio), Clamp(max * g.ChildStartRatio));
            }

            foreach (var factor in MergeFactors(a, b))
            {
                preview.FactorChances[factor.FactorId] = Math.Min(0.9, factor.Level * g.FactorInheritChancePerLevel);
            }

            preview.MutationHint = preview.Compatibility >= 70 ? "まれに特異葉が生まれる" : "ごくまれに特異葉が生まれる";
            return preview;
        }

        /// <summary>候補を作る。乱数は候補ごとに独立して消費する（候補数が変わっても既存候補が変わらない）。</summary>
        public static List<GraftCandidate> CreateCandidates(
            ParentTree a,
            ParentTree b,
            IReadOnlyList<SpeciesDefinition> species,
            LineageCatalog catalog,
            BalanceConfig balance,
            ulong seed,
            int day,
            string idPrefix)
        {
            var list = new List<GraftCandidate>();
            for (int i = 0; i < balance.Grafting.CandidateCount; i++)
            {
                var random = RandomStreams.Create(seed, i, RandomStreamId.Generation);
                list.Add(new GraftCandidate(CreateOne(a, b, species, catalog, balance, random, day, $"{idPrefix}-{i}")));
            }

            return list;
        }

        private static CharacterState CreateOne(ParentTree a, ParentTree b, IReadOnlyList<SpeciesDefinition> species, LineageCatalog catalog, BalanceConfig balance, IRandomProvider random, int day, string id)
        {
            var g = balance.Grafting;
            double sd = VarianceSd(Compatibility(a, b, balance), balance);
            string speciesId = random.NextDouble() < 0.5 ? a.SpeciesId : b.SpeciesId;
            var speciesDef = Find(species, speciesId);

            // 主能力: 親の寄与 → 系統の基準へ寄せる → ばらつき → 若葉としての割合
            var statValues = new int[StatTypes.Length];
            for (int i = 0; i < StatTypes.Length; i++)
            {
                var type = StatTypes[i];
                double w = random.Range(g.ParentWeightRange[0], g.ParentWeightRange[1]);
                double mix = a.Stats[type] * w + b.Stats[type] * (1 - w);
                double value = MathUtil.Lerp(mix, SpeciesCenter(speciesDef, type), g.SpeciesPull) + MathUtil.ApproxGaussian(random) * sd;
                statValues[i] = Clamp(value * g.ChildStartRatio);
            }

            var furnaceValues = new int[FurnaceStatsAll.Length];
            for (int i = 0; i < FurnaceStatsAll.Length; i++)
            {
                double w = random.Range(g.ParentWeightRange[0], g.ParentWeightRange[1]);
                double mix = a.Furnace[FurnaceStatsAll[i]] * w + b.Furnace[FurnaceStatsAll[i]] * (1 - w);
                furnaceValues[i] = Clamp((mix + MathUtil.ApproxGaussian(random) * sd) * g.ChildStartRatio);
            }

            var affinityValues = new int[AffinityTypes.Length];
            for (int i = 0; i < AffinityTypes.Length; i++)
            {
                double w = random.Range(g.ParentWeightRange[0], g.ParentWeightRange[1]);
                double mix = a.Affinities[AffinityTypes[i]] * w + b.Affinities[AffinityTypes[i]] * (1 - w);
                affinityValues[i] = Clamp(mix + MathUtil.ApproxGaussian(random) * sd);
            }

            var growthType = random.NextDouble() < g.GrowthTypeRandomChance
                ? GrowthTypes[random.Range(0, GrowthTypes.Length)]
                : (random.NextDouble() < 0.5 ? a.GrowthType : b.GrowthType);
            var personality = random.NextDouble() < g.PersonalityRandomChance
                ? Personalities[random.Range(0, Personalities.Length)]
                : (random.NextDouble() < 0.5 ? a.Personality : b.Personality);

            var child = new CharacterState(
                id,
                id,
                speciesId,
                new CharacterStats(statValues[0], statValues[1], statValues[2], statValues[3], statValues[4], statValues[5]),
                new FurnaceStats(furnaceValues[0], furnaceValues[1], furnaceValues[2], furnaceValues[3], furnaceValues[4]),
                new Affinities(affinityValues[0], affinityValues[1], affinityValues[2], affinityValues[3], affinityValues[4], affinityValues[5], affinityValues[6]),
                personality,
                100,
                day,
                a.LineageId,
                Math.Max(a.Generation, b.Generation) + 1);
            child.Growth = GrowthProfile.Roll(growthType, balance, random);

            // 因子: 全部は継承しない。継承した因子はその能力を底上げする
            var inherited = new List<GeneFactor>();
            foreach (var factor in MergeFactors(a, b))
            {
                double chance = Math.Min(0.9, factor.Level * g.FactorInheritChancePerLevel);
                if (random.NextDouble() >= chance) continue;
                inherited.Add(new GeneFactor(factor.FactorId, factor.Level, acquired: false));
            }

            inherited = inherited.OrderByDescending(f => f.Level).Take(g.MaxFactors).ToList();
            foreach (var factor in inherited)
            {
                catalog.Factor(factor.FactorId)?.Parameter.Add(child, factor.Level * g.FactorBonusPerLevel);
            }

            child.SetFactors(inherited);

            // 突然変異（低確率）。乱数は必ず2回引く
            double mutationRoll = random.NextDouble();
            int mutationPick = catalog.Mutations.Count > 0 ? random.Range(0, catalog.Mutations.Count) : 0;
            if (catalog.Mutations.Count > 0 && mutationRoll < g.MutationChance)
            {
                var mutation = catalog.Mutations[mutationPick];
                child.MutationId = mutation.Id;
                foreach (var modifier in mutation.Modifiers)
                {
                    modifier.Key.Add(child, modifier.Value);
                }
            }

            return child;
        }

        /// <summary>両親の因子をまとめる（同じ因子は高い方のレベル）。</summary>
        private static List<GeneFactor> MergeFactors(ParentTree a, ParentTree b)
        {
            var merged = new Dictionary<string, GeneFactor>();
            foreach (var f in a.Factors.Concat(b.Factors))
            {
                if (!merged.TryGetValue(f.FactorId, out var current) || current.Level < f.Level) merged[f.FactorId] = f;
            }

            return merged.Values.OrderBy(f => f.FactorId, StringComparer.Ordinal).ToList();
        }

        private static double VarianceSd(int compatibility, BalanceConfig balance)
        {
            var g = balance.Grafting;
            return g.VarianceSd * (1.0 - g.Compatibility.VarianceReductionAtMax * compatibility / 100.0);
        }

        private static double SpeciesCenter(SpeciesDefinition species, StatType type)
        {
            if (species != null && species.BaseRanges.TryGetValue(type, out var range)) return (range.Min + range.Max) / 2.0;
            return 55;
        }

        private static SpeciesDefinition Find(IReadOnlyList<SpeciesDefinition> species, string id)
        {
            if (species == null) return null;
            foreach (var s in species)
            {
                if (s.Id == id) return s;
            }

            return null;
        }

        private static int Clamp(double value) => MathUtil.Clamp((int)Math.Round(value), 0, 100);

        private static string Camel(string name) => char.ToLowerInvariant(name[0]) + name.Substring(1);
    }
}
