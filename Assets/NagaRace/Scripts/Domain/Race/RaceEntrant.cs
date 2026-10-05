using System;
using NagaRace.Domain.Characters;
using NagaRace.Domain.Common;

namespace NagaRace.Domain.Race
{
    /// <summary>
    /// レースへの出走データ（出走時点のスナップショット）。レース中は変化しない。
    /// 非同期PvPの RaceSnapshot（08 §5）の元になる。
    /// </summary>
    public sealed class RaceEntrant
    {
        public RaceEntrant(
            string id,
            string name,
            string speciesId,
            CharacterStats stats,
            Affinities affinities = null,
            Personality personality = Personality.Normal,
            AiStyle aiStyle = AiStyle.Standard,
            Condition condition = Condition.Normal,
            int fatigue = 0,
            FurnaceStats furnace = null,
            bool isPlayer = false)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Name = name ?? id;
            SpeciesId = speciesId ?? string.Empty;
            Stats = stats ?? throw new ArgumentNullException(nameof(stats));
            Affinities = affinities ?? Affinities.NeutralAll;
            Personality = personality;
            AiStyle = aiStyle;
            Condition = condition;
            Fatigue = MathUtil.Clamp(fatigue, 0, 100);
            Furnace = furnace ?? FurnaceStats.Uniform(50);
            IsPlayer = isPlayer;
        }

        public string Id { get; }
        public string Name { get; }
        public string SpeciesId { get; }
        public CharacterStats Stats { get; }
        public Affinities Affinities { get; }
        public Personality Personality { get; }
        public AiStyle AiStyle { get; }
        public Condition Condition { get; }
        /// <summary>疲労 0〜100。</summary>
        public int Fatigue { get; }
        public FurnaceStats Furnace { get; }
        public bool IsPlayer { get; }

        public RaceEntrant WithStats(CharacterStats stats)
        {
            return new RaceEntrant(Id, Name, SpeciesId, stats, Affinities, Personality, AiStyle, Condition, Fatigue, Furnace, IsPlayer);
        }

        public RaceEntrant WithPersonality(Personality personality)
        {
            return new RaceEntrant(Id, Name, SpeciesId, Stats, Affinities, personality, AiStyle, Condition, Fatigue, Furnace, IsPlayer);
        }

        public RaceEntrant AsPlayer()
        {
            return new RaceEntrant(Id, Name, SpeciesId, Stats, Affinities, Personality, AiStyle, Condition, Fatigue, Furnace, true);
        }
    }
}
