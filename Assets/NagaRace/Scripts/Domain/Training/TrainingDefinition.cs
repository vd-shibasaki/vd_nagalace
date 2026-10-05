using System.Collections.Generic;
using NagaRace.Domain.Characters;
using NagaRace.Domain.Race;

namespace NagaRace.Domain.Training
{
    /// <summary>ならしの定義（05 §2, 12 Training）。定義データであり可変状態は持たない。</summary>
    public sealed class TrainingDefinition
    {
        public TrainingDefinition(
            string id,
            string name,
            int fatigue,
            int risk,
            IReadOnlyDictionary<CharacterParameter, double> growth,
            IReadOnlyDictionary<Weather, double> weatherMultipliers,
            IReadOnlyDictionary<string, double> bodyAccumulation,
            int durabilityRecovery = 0,
            double conditionUp = 0,
            bool extreme = false,
            Personality? personalityShiftTo = null,
            double personalityShiftChance = 0)
        {
            Id = id;
            Name = name;
            Fatigue = fatigue;
            Risk = risk;
            Growth = growth;
            WeatherMultipliers = weatherMultipliers;
            BodyAccumulation = bodyAccumulation;
            DurabilityRecovery = durabilityRecovery;
            ConditionUp = conditionUp;
            Extreme = extreme;
            PersonalityShiftTo = personalityShiftTo;
            PersonalityShiftChance = personalityShiftChance;
        }

        public string Id { get; }
        public string Name { get; }
        /// <summary>疲労の増減（負なら回復）。</summary>
        public int Fatigue { get; }
        /// <summary>耐久リスク 0〜3。</summary>
        public int Risk { get; }
        /// <summary>能力ごとの伸びやすさ（重み）。</summary>
        public IReadOnlyDictionary<CharacterParameter, double> Growth { get; }
        public IReadOnlyDictionary<Weather, double> WeatherMultipliers { get; }
        public IReadOnlyDictionary<string, double> BodyAccumulation { get; }
        public int DurabilityRecovery { get; }
        /// <summary>調子が1段階上がる確率。</summary>
        public double ConditionUp { get; }
        /// <summary>極端な条件（耐久20未満かつ高疲労）でのみ Lost 判定がありうる（05 §2 v1.1）。</summary>
        public bool Extreme { get; }
        public Personality? PersonalityShiftTo { get; }
        public double PersonalityShiftChance { get; }

        public double WeatherMultiplier(Weather weather)
        {
            return WeatherMultipliers != null && WeatherMultipliers.TryGetValue(weather, out double v) ? v : 1.0;
        }
    }

    /// <summary>体質の定義（05 §4）。累積値が閾値を超えると獲得し、能力が上下する（完全な上位互換にしない）。</summary>
    public sealed class BodyTraitDefinition
    {
        public BodyTraitDefinition(string id, string name, string accumulation, IReadOnlyDictionary<CharacterParameter, int> modifiers)
        {
            Id = id;
            Name = name;
            Accumulation = accumulation;
            Modifiers = modifiers;
        }

        public string Id { get; }
        public string Name { get; }
        public string Accumulation { get; }
        public IReadOnlyDictionary<CharacterParameter, int> Modifiers { get; }
    }

    /// <summary>ならし・体質の定義一式。</summary>
    public sealed class TrainingCatalog
    {
        private readonly Dictionary<string, TrainingDefinition> _byId = new Dictionary<string, TrainingDefinition>();

        public TrainingCatalog(IReadOnlyList<TrainingDefinition> trainings, IReadOnlyList<BodyTraitDefinition> bodyTraits)
        {
            Trainings = trainings;
            BodyTraits = bodyTraits;
            foreach (var t in trainings)
            {
                _byId[t.Id] = t;
            }
        }

        public IReadOnlyList<TrainingDefinition> Trainings { get; }
        public IReadOnlyList<BodyTraitDefinition> BodyTraits { get; }

        public TrainingDefinition Get(string id)
        {
            if (!_byId.TryGetValue(id, out var t)) throw new KeyNotFoundException($"ならし '{id}' がありません。");
            return t;
        }

        public BodyTraitDefinition FindBodyTrait(string id)
        {
            foreach (var b in BodyTraits)
            {
                if (b.Id == id) return b;
            }

            return null;
        }
    }
}
