using System;
using System.Collections.Generic;
using NagaRace.Domain.Characters;
using NagaRace.Domain.Common;

namespace NagaRace.Domain.Race
{
    /// <summary>大会ごとの NPC 出走者の条件（04 §13）。</summary>
    public sealed class NpcSpec
    {
        public NpcSpec(IReadOnlyList<SpeciesDefinition> speciesPool, int statOffset = 0, AiStyle aiStyle = AiStyle.Standard)
        {
            if (speciesPool == null || speciesPool.Count == 0) throw new ArgumentException("系統プールが空です。", nameof(speciesPool));
            SpeciesPool = speciesPool;
            StatOffset = statOffset;
            AiStyle = aiStyle;
        }

        public IReadOnlyList<SpeciesDefinition> SpeciesPool { get; }
        /// <summary>大会の強さ帯。系統の能力レンジに加算する。</summary>
        public int StatOffset { get; }
        public AiStyle AiStyle { get; }
    }

    /// <summary>
    /// 系統定義のレンジから NPC を生成する。レースSeedとは別の大会Seedを使うので、
    /// 同じ大会には同じ顔ぶれを出せる。
    /// </summary>
    public static class NpcFactory
    {
        private static readonly string[] Names =
        {
            "かぜのは", "たきのは", "ぎんなん", "はすすけ", "くろは", "せせらぎ", "いしのは", "あかぎり",
            "ひなた", "あさつゆ", "こもれび", "みずは", "そよぎ", "はなび", "つきのは", "どんぐり丸",
        };

        public static List<RaceEntrant> Generate(NpcSpec spec, ulong tournamentSeed, int count, string idPrefix = "npc")
        {
            var random = RandomStreams.Create(tournamentSeed, RandomStreams.SharedIndex, RandomStreamId.Generation);
            var result = new List<RaceEntrant>(count);
            for (int i = 0; i < count; i++)
            {
                var species = spec.SpeciesPool[random.Range(0, spec.SpeciesPool.Count)];
                var stats = new CharacterStats(
                    Roll(random, species, StatType.FlowSpeed, spec.StatOffset),
                    Roll(random, species, StatType.Acceleration, spec.StatOffset),
                    Roll(random, species, StatType.Buoyancy, spec.StatOffset),
                    Roll(random, species, StatType.Stability, spec.StatOffset),
                    Roll(random, species, StatType.Toughness, spec.StatOffset),
                    Roll(random, species, StatType.Judgement, spec.StatOffset));
                var affinities = new Affinities(
                    RollAffinity(random, species, AffinityType.Rapid),
                    RollAffinity(random, species, AffinityType.Whirlpool),
                    RollAffinity(random, species, AffinityType.Narrow),
                    RollAffinity(random, species, AffinityType.Deep),
                    RollAffinity(random, species, AffinityType.Sprint),
                    RollAffinity(random, species, AffinityType.Middle),
                    RollAffinity(random, species, AffinityType.Long));
                var personality = RollPersonality(random, species);
                string name = Names[random.Range(0, Names.Length)];

                result.Add(new RaceEntrant(
                    $"{idPrefix}-{i}",
                    name,
                    species.Id,
                    stats,
                    affinities,
                    personality,
                    spec.AiStyle));
            }

            return result;
        }

        private static int Roll(IRandomProvider random, SpeciesDefinition species, StatType type, int offset)
        {
            var range = species.BaseRanges.TryGetValue(type, out var r) ? r : new IntRange(50, 50);
            return random.Range(range.Min, range.Max + 1) + offset;
        }

        private static int RollAffinity(IRandomProvider random, SpeciesDefinition species, AffinityType type)
        {
            if (species.AffinityRanges == null || !species.AffinityRanges.TryGetValue(type, out var range))
            {
                return Affinities.Neutral;
            }

            return random.Range(range.Min, range.Max + 1);
        }

        private static Personality RollPersonality(IRandomProvider random, SpeciesDefinition species)
        {
            var values = (Personality[])Enum.GetValues(typeof(Personality));
            int total = 0;
            foreach (var p in values)
            {
                total += Weight(species, p);
            }

            int roll = random.Range(0, total);
            foreach (var p in values)
            {
                roll -= Weight(species, p);
                if (roll < 0) return p;
            }

            return Personality.Normal;
        }

        private static int Weight(SpeciesDefinition species, Personality personality)
        {
            if (species.PersonalityWeights == null || species.PersonalityWeights.Count == 0) return 1;
            return species.PersonalityWeights.TryGetValue(personality, out int w) ? Math.Max(0, w) : 0;
        }
    }
}
