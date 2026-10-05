using System;
using System.Collections.Generic;
using System.Linq;
using NagaRace.Domain.Balance;
using NagaRace.Domain.Characters;

namespace NagaRace.Domain.Lineage
{
    /// <summary>
    /// 親木（05 §6）。引退・ロストした個体の「流れ・葉脈の記憶」を受け取り、次の若葉へ伝える。
    /// 「葉っぱそのものが生殖する」表現にはしない。
    /// </summary>
    public sealed class ParentTree
    {
        public ParentTree(
            string id,
            string name,
            string sourceCharacterId,
            string speciesId,
            string lineageId,
            int generation,
            CharacterStats stats,
            FurnaceStats furnace,
            Affinities affinities,
            GrowthType growthType,
            Personality personality,
            IReadOnlyList<GeneFactor> factors,
            int registeredDay,
            bool fromLost)
        {
            Id = id;
            Name = name;
            SourceCharacterId = sourceCharacterId;
            SpeciesId = speciesId;
            LineageId = lineageId;
            Generation = generation;
            Stats = stats;
            Furnace = furnace;
            Affinities = affinities;
            GrowthType = growthType;
            Personality = personality;
            Factors = factors;
            RegisteredDay = registeredDay;
            FromLost = fromLost;
        }

        public string Id { get; }
        public string Name { get; }
        public string SourceCharacterId { get; }
        public string SpeciesId { get; }
        public string LineageId { get; }
        public int Generation { get; }
        public CharacterStats Stats { get; }
        public FurnaceStats Furnace { get; }
        public Affinities Affinities { get; }
        public GrowthType GrowthType { get; }
        public Personality Personality { get; }
        public IReadOnlyList<GeneFactor> Factors { get; }
        public int RegisteredDay { get; }
        /// <summary>ロストした個体から登録された（後天因子なし）。</summary>
        public bool FromLost { get; }

        /// <summary>
        /// 引退・ロストした個体から親木を作る。
        /// 引退なら現役中の能力から後天因子を得る。ロストでは後天因子の登録チャンスを失う（05 §10）。
        /// </summary>
        public static ParentTree FromCharacter(CharacterState c, LineageCatalog catalog, BalanceConfig balance, int day)
        {
            if (c.IsActive) throw new InvalidOperationException("現役の個体は親木に登録できません（引退またはロスト後）。");

            bool lost = c.Status == CharacterStatus.Lost;
            var factors = new Dictionary<string, GeneFactor>();
            foreach (var inherited in c.Factors)
            {
                factors[inherited.FactorId] = new GeneFactor(inherited.FactorId, inherited.Level, acquired: false);
            }

            if (!lost)
            {
                var thresholds = balance.Grafting.AcquiredFactorThresholds;
                foreach (var def in catalog.Factors)
                {
                    if (!def.AcquiredFrom.HasValue) continue;
                    int value = def.AcquiredFrom.Value.Get(c);
                    int level = thresholds.Count(t => value >= t);
                    if (level == 0) continue;
                    if (!factors.TryGetValue(def.Id, out var current) || current.Level < level)
                    {
                        factors[def.Id] = new GeneFactor(def.Id, level, acquired: true);
                    }
                }
            }

            // 主要因子は2〜4程度（05 §8）。レベルの高い順に残す
            var kept = factors.Values
                .OrderByDescending(f => f.Level)
                .ThenBy(f => f.FactorId, StringComparer.Ordinal)
                .Take(balance.Grafting.MaxFactors)
                .ToList();

            return new ParentTree(
                $"tree-{c.Id}",
                $"{c.Name}の親木",
                c.Id,
                c.SpeciesId,
                c.LineageId ?? $"lineage-{c.Id}",
                c.Generation,
                c.Stats,
                c.Furnace,
                c.Affinities,
                c.Growth.Type,
                c.Personality,
                kept,
                day,
                lost);
        }
    }
}
