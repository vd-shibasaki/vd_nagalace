using System.Collections.Generic;

namespace NagaRace.Domain.Balance
{
    /// <summary>
    /// game_balance_defaults.json に対応するバランス設定。
    /// Domain は JSON ライブラリに依存しないため、読込は Infrastructure が行う（07 §10.1）。
    /// レンジ表記 double[2] は [能力0のときの値, 能力100のときの値]。
    /// </summary>
    public sealed class BalanceConfig
    {
        public string Version { get; set; }
        public RaceBalance Race { get; set; } = new RaceBalance();
        public StatsBalance Stats { get; set; } = new StatsBalance();
        public RouteBalance Route { get; set; } = new RouteBalance();
        public AffinityBalance Affinity { get; set; } = new AffinityBalance();
        public ConditionBalance Condition { get; set; } = new ConditionBalance();
        public FatigueBalance Fatigue { get; set; } = new FatigueBalance();
        public LaneBalance Lane { get; set; } = new LaneBalance();
        public RaceConditionsBalance Conditions { get; set; } = new RaceConditionsBalance();
        public Dictionary<string, PersonalityBalance> Personality { get; set; } = new Dictionary<string, PersonalityBalance>();
        public Dictionary<string, AiStyleBalance> AiStyle { get; set; } = new Dictionary<string, AiStyleBalance>();
        public LeafPowerBalance LeafPower { get; set; } = new LeafPowerBalance();
        public FurnaceBalance Furnace { get; set; } = new FurnaceBalance();
        public DurabilityBalance Durability { get; set; } = new DurabilityBalance();
        public LostBalance Lost { get; set; } = new LostBalance();
        public AftermathBalance Aftermath { get; set; } = new AftermathBalance();
        public RiskBalance Risk { get; set; } = new RiskBalance();
        public TrainingBalance Training { get; set; } = new TrainingBalance();
        public CalendarBalance Calendar { get; set; } = new CalendarBalance();
        public WeatherBalance Weather { get; set; } = new WeatherBalance();
        public GrowthBalance Growth { get; set; } = new GrowthBalance();
        public GraftingBalance Grafting { get; set; } = new GraftingBalance();
    }

    public sealed class RaceBalance
    {
        public int EntrantCount { get; set; }
        public double[] TargetDurationSeconds { get; set; }
        public double[] ChampionshipTargetDurationSeconds { get; set; }
        public int TickRate { get; set; }
        public double TimeoutSeconds { get; set; }
        public double SegmentSpeedJitter { get; set; }
        public double StartSpeedRatio { get; set; }
        public double GoodFlowEventThreshold { get; set; }
        public int CollisionBaseDurabilityDamage { get; set; }
    }

    public sealed class StatsBalance
    {
        public int Min { get; set; }
        public int Max { get; set; }
        public Dictionary<string, int> RankThresholds { get; set; } = new Dictionary<string, int>();
        public double[] FlowRateRange { get; set; }
        public double[] AccelRateRange { get; set; }
        public double[] RoughnessPenaltyByBuoyancy { get; set; }
        public double[] ContactSlowdownByStability { get; set; }
        public double[] StunSecondsByStability { get; set; }
        public double StunSpeedFactor { get; set; }
        public double DecelRate { get; set; }
        public double StumbleRatePerSecond { get; set; }
        public double[] StumbleHazardByStability { get; set; }
        public double StumbleSlowdown { get; set; }
        public double[] DamageMultiplierByToughness { get; set; }
        public double[] RouteNoiseByJudgement { get; set; }
        /// <summary>Segmentのゆらぎ抽選の偏り（指数）。1未満なら良い流れを引きやすい。</summary>
        public double[] GoodFlowSkewByJudgement { get; set; }
        public double[] BlockReactionSecondsByJudgement { get; set; }
    }

    public sealed class RouteBalance
    {
        public double DangerPenaltyPerLevel { get; set; }
        public double CrowdPenaltyPerRacer { get; set; }
        public double CrowdLookAheadMeters { get; set; }
        public int TrailingRankFrom { get; set; }
        public double TrailingDangerAversionMul { get; set; }
        public double StumbleTimeLossSeconds { get; set; }
    }

    public sealed class AffinityBalance
    {
        public double[] ModifierRange { get; set; }
        public DistanceBands DistanceBands { get; set; } = new DistanceBands();
        public double DeepWaterDepthMin { get; set; }
    }

    public sealed class DistanceBands
    {
        public double SprintMax { get; set; }
        public double LongMin { get; set; }
    }

    public sealed class ConditionBalance
    {
        public Dictionary<string, double> SpeedModifier { get; set; } = new Dictionary<string, double>();
    }

    public sealed class FatigueBalance
    {
        public double SpeedPenaltyStart { get; set; }
        public double SpeedPenaltyAtMax { get; set; }
    }

    public sealed class LaneBalance
    {
        public double CenterFlowBonus { get; set; }
        public double EdgeFlowPenalty { get; set; }
        public double RacerWidthMeters { get; set; }
        public double BlockDistanceMeters { get; set; }
        public double LateralSpeedPerSecond { get; set; }
        public double[] LateralSpeedMulByAgility { get; set; }
        public double ContactSpeedDiffThreshold { get; set; }
        public double ReturnToHomeLaneDelaySeconds { get; set; }
        public double ClearLookAheadMeters { get; set; }
    }

    public sealed class RaceConditionsBalance
    {
        public Dictionary<string, double> WaterLevelFlowModifier { get; set; } = new Dictionary<string, double>();
        public double HighWaterRoughnessAdd { get; set; }
        public int HeavyRainDangerAdd { get; set; }
    }

    public sealed class PersonalityBalance
    {
        public double DangerAversion { get; set; }
        public double ShortcutBonus { get; set; }
    }

    public sealed class AiStyleBalance
    {
        public double DangerAversionMul { get; set; }
        public double FurnaceReleaseRatio { get; set; }
    }

    public sealed class LeafPowerBalance
    {
        public double Max { get; set; }
        public double FurnaceMinimum { get; set; }
        public double PassiveGainPerSecond { get; set; }
        public Dictionary<string, double> BehindBonusPerSecond { get; set; } = new Dictionary<string, double>();
        public int[] ChanceCountRange { get; set; }
        public double[] ChanceProgressRange { get; set; }
        public double ChanceWindowSeconds { get; set; }
        public double ChanceGain { get; set; }
        /// <summary>受付時間の端で入力したときの獲得倍率（中央で 1.0）。</summary>
        public double ChanceMinQuality { get; set; }
        public double[] NpcChanceSuccessByJudgement { get; set; }
    }

    public sealed class FurnaceBalance
    {
        public double[] OutputBoostRange { get; set; }
        public double[] IgnitionLagSecondsByStartup { get; set; }
        public double[] IgnitionCostByStartup { get; set; }
        public double DrainPerSecond { get; set; }
        public double[] SafeDurationSecondsByCritical { get; set; }
        public double[] CooldownSecondsByCooling { get; set; }
        public double[] FragilityByFurnaceDurability { get; set; }
        public double RampStart { get; set; }
        public double RampAtSafeEnd { get; set; }
        public double RampMax { get; set; }
        public double RiskRampPerSecond { get; set; }
        public double DamageModifierAtZeroDurability { get; set; }
        public double FatigueModifierAtMax { get; set; }
        public FailureSeverityWeights FailureSeverityWeights { get; set; } = new FailureSeverityWeights();
        public double AccelMultiplier { get; set; }
        public double ContactSlowdownMultiplier { get; set; }
        public double MinorSlowdown { get; set; }
        public double MinorStunSeconds { get; set; }
        public double BlowSlowdown { get; set; }
        public double BlowStunSeconds { get; set; }
        /// <summary>RiskZone 中にブーストが上限へ近づく速さ（秒）。</summary>
        public double RiskRampTimeConstantSeconds { get; set; }
        /// <summary>NPC はこの進捗以降を勝負所として炉を使う。</summary>
        public double NpcFinalStretchProgress { get; set; }
        /// <summary>NPC は葉力がここまで溜まったら勝負所の前でも使う（溢れさせないため）。</summary>
        public double NpcFullGaugeLeafPower { get; set; }
    }

    public sealed class FailureSeverityWeights
    {
        public Dictionary<string, double> Short { get; set; } = new Dictionary<string, double>();
        public Dictionary<string, double> Long { get; set; } = new Dictionary<string, double>();
        public double LongOverrunSeconds { get; set; }
    }

    public sealed class DurabilityBalance
    {
        public int GoodMin { get; set; }
        public int LightDamageMin { get; set; }
        public int DamagedMin { get; set; }
        public int DangerMin { get; set; }
        public double StumbleDamage { get; set; }
        public Dictionary<string, double> FailureDamage { get; set; } = new Dictionary<string, double>();
        /// <summary>危険度3以上の Segment で姿勢を崩したときに重大事故になる確率（危険度ごと）。</summary>
        public Dictionary<string, double> AccidentChanceOnStumbleByDanger { get; set; } = new Dictionary<string, double>();
        public double AccidentDamage { get; set; }
        /// <summary>耐久が lightDamageMin 未満のときの事故率倍率。</summary>
        public double LowDurabilityAccidentMultiplier { get; set; }
        /// <summary>耐久が十分なとき（lightDamageMin 以上）の事故率倍率。</summary>
        public double HealthyAccidentMultiplier { get; set; }
        /// <summary>1レースでこれ以上の耐久ダメージを受けると大怪我（最大耐久が下がる）。</summary>
        public double InjuryDamageThreshold { get; set; }
        public double InjuryMaxDurabilityLossRatio { get; set; }
        public int FurnaceDurabilityLossOnDamage { get; set; }
    }

    /// <summary>レース後の反映（疲労など）。</summary>
    public sealed class AftermathBalance
    {
        public double RaceFatigue { get; set; }
        public double FatiguePerFurnaceSecond { get; set; }
        public double FatiguePerOverflowSecond { get; set; }
    }

    /// <summary>出走前の危険度表示（04 §9.2）。</summary>
    public sealed class RiskBalance
    {
        public int CautionFatigueMin { get; set; }
        public int ExtremeFactorCount { get; set; }
    }

    public sealed class LostBalance
    {
        public int CandidateCourseDangerMin { get; set; }
        public int HardGuardCourseDangerMax { get; set; }
        public int HardGuardDurabilityMin { get; set; }
        public double ChanceOnCandidate { get; set; }
    }

    /// <summary>育成「ならし」（05 §3）。</summary>
    public sealed class TrainingBalance
    {
        public int FatigueMax { get; set; }
        public int HighFatigueThreshold { get; set; }
        /// <summary>重み1.0のならし1回で上がる能力の目安。</summary>
        public double BaseGain { get; set; }
        public double[] RandomRange { get; set; }
        public Dictionary<string, double> ConditionMultiplier { get; set; } = new Dictionary<string, double>();
        public double FatiguePenaltyStart { get; set; }
        public double FatigueMultiplierAtMax { get; set; }
        /// <summary>同じならしを続けたときの倍率（1回目, 2回目, ...）。最後の値がそれ以降に使われる。</summary>
        public double[] MonotonyMultipliers { get; set; }
        /// <summary>高い能力ほど伸びにくくする指数（1 - (値/上限)^指数）。</summary>
        public double DiminishingExponent { get; set; }
        public double StatSoftCap { get; set; }
        public double InjuryChancePerRisk { get; set; }
        public double InjuryFatigueMultiplierAtMax { get; set; }
        public double InjuryDamagePerRisk { get; set; }
        public int DailyDurabilityRecovery { get; set; }
        public int DailyFatigueRecovery { get; set; }
        public double ConditionDriftChance { get; set; }
        public double HighFatigueConditionDropChance { get; set; }
        public double BodyTraitThreshold { get; set; }
    }

    /// <summary>天気（季節ごとの出現率）と水量。</summary>
    public sealed class WeatherBalance
    {
        public Dictionary<string, Dictionary<string, double>> SeasonWeights { get; set; } = new Dictionary<string, Dictionary<string, double>>();
        public Dictionary<string, double> HighWaterChance { get; set; } = new Dictionary<string, double>();
        public Dictionary<string, double> LowWaterChance { get; set; } = new Dictionary<string, double>();
    }

    /// <summary>成長フェーズ（02 §6, 09 M6）。</summary>
    public sealed class GrowthBalance
    {
        public Dictionary<string, GrowthTypeBalance> Types { get; set; } = new Dictionary<string, GrowthTypeBalance>();
        public Dictionary<string, double> TrainingMultiplier { get; set; } = new Dictionary<string, double>();
        public Dictionary<string, double> RaceSpeedMultiplier { get; set; } = new Dictionary<string, double>();
        public Dictionary<string, double> DailyDeclineChance { get; set; } = new Dictionary<string, double>();
        public string[] DeclineStats { get; set; }
    }

    /// <summary>接ぎ木（05 §7〜9）。</summary>
    public sealed class GraftingBalance
    {
        public int CandidateCount { get; set; }
        public double[] ParentWeightRange { get; set; }
        /// <summary>系統の基準値へ寄せる割合。</summary>
        public double SpeciesPull { get; set; }
        public double VarianceSd { get; set; }
        /// <summary>若葉として生まれるときの能力の割合（育成で伸ばす前提）。</summary>
        public double ChildStartRatio { get; set; }
        public double FactorInheritChancePerLevel { get; set; }
        public int MaxFactors { get; set; }
        public int FactorBonusPerLevel { get; set; }
        public double MutationChance { get; set; }
        public double GrowthTypeRandomChance { get; set; }
        public double PersonalityRandomChance { get; set; }
        public CompatibilityBalance Compatibility { get; set; } = new CompatibilityBalance();
        /// <summary>後天因子のレベル1〜3の閾値。</summary>
        public int[] AcquiredFactorThresholds { get; set; }
    }

    public sealed class CompatibilityBalance
    {
        public double Base { get; set; }
        public double DifferentLineage { get; set; }
        public double SameSpecies { get; set; }
        public double FactorOverlapPenalty { get; set; }
        /// <summary>相性100のときのばらつきの縮小率。</summary>
        public double VarianceReductionAtMax { get; set; }
    }

    public sealed class GrowthTypeBalance
    {
        /// <summary>フェーズ名 → [最小日数, 最大日数]。</summary>
        public Dictionary<string, int[]> PhaseDays { get; set; } = new Dictionary<string, int[]>();
    }

    public sealed class CalendarBalance
    {
        public int DaysPerWeek { get; set; }
        public int WeeksPerSeason { get; set; }
        public int SeasonsPerYear { get; set; }
        public int TournamentIntervalDays { get; set; }
    }
}
