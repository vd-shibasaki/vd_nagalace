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
        public TrainingBalance Training { get; set; } = new TrainingBalance();
        public CalendarBalance Calendar { get; set; } = new CalendarBalance();
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
    }

    // TODO(M3): 葉脈炉の実装時に参照する。M1 では読込のみ。
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
    }

    public sealed class FailureSeverityWeights
    {
        public Dictionary<string, double> Short { get; set; } = new Dictionary<string, double>();
        public Dictionary<string, double> Long { get; set; } = new Dictionary<string, double>();
        public double LongOverrunSeconds { get; set; }
    }

    // TODO(M4): 耐久・Lost 実装時に参照する。M1 では読込のみ。
    public sealed class DurabilityBalance
    {
        public int GoodMin { get; set; }
        public int LightDamageMin { get; set; }
        public int DamagedMin { get; set; }
        public int DangerMin { get; set; }
    }

    public sealed class LostBalance
    {
        public int CandidateCourseDangerMin { get; set; }
        public int HardGuardCourseDangerMax { get; set; }
        public int HardGuardDurabilityMin { get; set; }
        public double ChanceOnCandidate { get; set; }
    }

    public sealed class TrainingBalance
    {
        public int FatigueMax { get; set; }
        public int HighFatigueThreshold { get; set; }
    }

    public sealed class CalendarBalance
    {
        public int DaysPerWeek { get; set; }
        public int WeeksPerSeason { get; set; }
        public int SeasonsPerYear { get; set; }
        public int TournamentIntervalDays { get; set; }
    }
}
