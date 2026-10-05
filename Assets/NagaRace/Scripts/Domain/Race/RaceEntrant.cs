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
            bool isPlayer = false,
            int durability = 100,
            int maxDurability = 100,
            bool autoFurnace = false)
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
            MaxDurability = Math.Max(1, maxDurability);
            Durability = MathUtil.Clamp(durability, 0, MaxDurability);
            AutoFurnace = autoFurnace;
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
        /// <summary>出走時の耐久（HP相当）。</summary>
        public int Durability { get; }
        public int MaxDurability { get; }
        /// <summary>プレイヤーの「おまかせ炉」設定（03 §6）。NPC は常に AI が炉を操作する。</summary>
        public bool AutoFurnace { get; }

        public RaceEntrant WithStats(CharacterStats stats) => Copy(stats: stats);
        public RaceEntrant WithPersonality(Personality personality) => Copy(personality: personality);
        public RaceEntrant WithFurnace(FurnaceStats furnace) => Copy(furnace: furnace);
        public RaceEntrant WithCondition(int durability, int maxDurability, int fatigue) => Copy(durability: durability, maxDurability: maxDurability, fatigue: fatigue);
        public RaceEntrant WithAiStyle(AiStyle aiStyle) => Copy(aiStyle: aiStyle);
        public RaceEntrant AsPlayer(bool autoFurnace = false) => Copy(isPlayer: true, autoFurnace: autoFurnace);

        private RaceEntrant Copy(
            CharacterStats stats = null,
            Personality? personality = null,
            AiStyle? aiStyle = null,
            FurnaceStats furnace = null,
            bool? isPlayer = null,
            int? durability = null,
            int? maxDurability = null,
            int? fatigue = null,
            bool? autoFurnace = null)
        {
            return new RaceEntrant(
                Id, Name, SpeciesId,
                stats ?? Stats,
                Affinities,
                personality ?? Personality,
                aiStyle ?? AiStyle,
                Condition,
                fatigue ?? Fatigue,
                furnace ?? Furnace,
                isPlayer ?? IsPlayer,
                durability ?? Durability,
                maxDurability ?? MaxDurability,
                autoFurnace ?? AutoFurnace);
        }
    }
}
