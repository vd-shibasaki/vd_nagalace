using System.Collections.Generic;

namespace NagaRace.Domain.Characters
{
    /// <summary>[最小, 最大] の整数レンジ。</summary>
    public readonly struct IntRange
    {
        public IntRange(int min, int max)
        {
            Min = min;
            Max = max;
        }

        public int Min { get; }
        public int Max { get; }
    }

    /// <summary>系統定義（08 §2, 12 Character Species）。定義データであり可変状態は持たない。</summary>
    public sealed class SpeciesDefinition
    {
        public SpeciesDefinition(
            string id,
            string displayName,
            IReadOnlyDictionary<StatType, IntRange> baseRanges,
            IReadOnlyDictionary<AffinityType, IntRange> affinityRanges,
            IReadOnlyDictionary<Personality, int> personalityWeights)
        {
            Id = id;
            DisplayName = displayName;
            BaseRanges = baseRanges;
            AffinityRanges = affinityRanges;
            PersonalityWeights = personalityWeights;
        }

        public string Id { get; }
        public string DisplayName { get; }
        public IReadOnlyDictionary<StatType, IntRange> BaseRanges { get; }
        /// <summary>未指定の適性は 50（補正なし）。</summary>
        public IReadOnlyDictionary<AffinityType, IntRange> AffinityRanges { get; }
        /// <summary>未指定なら性格は均等。</summary>
        public IReadOnlyDictionary<Personality, int> PersonalityWeights { get; }
    }
}
