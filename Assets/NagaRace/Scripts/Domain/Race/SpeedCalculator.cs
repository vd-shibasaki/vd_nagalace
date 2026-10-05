using System;
using NagaRace.Domain.Balance;
using NagaRace.Domain.Characters;
using NagaRace.Domain.Common;

namespace NagaRace.Domain.Race
{
    /// <summary>
    /// 04 §4.1 の targetSpeed と加速追従を計算する。
    /// </summary>
    public sealed class SpeedCalculator
    {
        private readonly BalanceConfig _balance;
        private readonly RaceConditions _conditions;
        private readonly double _waterLevelModifier;
        private readonly double _roughnessAdd;
        private readonly AffinityType _distanceAffinity;

        public SpeedCalculator(BalanceConfig balance, RaceConditions conditions, RaceCourse course)
        {
            _balance = balance;
            _conditions = conditions;
            _waterLevelModifier = Lookup(balance.Conditions.WaterLevelFlowModifier, conditions.WaterLevel.ToString(), 1.0);
            _roughnessAdd = conditions.WaterLevel == WaterLevel.High ? balance.Conditions.HighWaterRoughnessAdd : 0.0;
            _distanceAffinity = DistanceAffinityOf(course.Distance, balance.Affinity.DistanceBands);
        }

        public AffinityType DistanceAffinity => _distanceAffinity;

        /// <summary>天候補正込みの Segment 危険度（1〜4）。</summary>
        public int EffectiveDanger(FlowSegment segment)
        {
            int add = _conditions.Weather == Weather.HeavyRain ? _balance.Conditions.HeavyRainDangerAdd : 0;
            return MathUtil.Clamp(segment.Danger + add, 1, 4);
        }

        /// <summary>レース中に変化しない倍率（流速・調子・疲労・距離適性）。</summary>
        public double StaticFactor(RaceEntrant entrant)
        {
            double flowRate = MathUtil.LerpByStat(_balance.Stats.FlowRateRange, entrant.Stats.FlowSpeed);
            double condition = Lookup(_balance.Condition.SpeedModifier, entrant.Condition.ToString(), 1.0);
            return flowRate * condition * FatigueModifier(entrant.Fatigue) * AffinityModifier(entrant.Affinities[_distanceAffinity]) * entrant.SpeedMultiplier;
        }

        /// <summary>
        /// 横位置・ゆらぎ・スタンを除いた「その Segment での期待速度」。進路選択の見積もりに使う。
        /// </summary>
        public double ExpectedSpeed(RaceEntrant entrant, double staticFactor, FlowSegment segment)
        {
            return segment.BaseFlowSpeed
                * _waterLevelModifier
                * staticFactor
                * TerrainAffinityModifier(entrant.Affinities, segment)
                * RoughnessModifier(entrant.Stats.Buoyancy, segment);
        }

        public double TargetSpeed(RacerState racer)
        {
            var segment = racer.Segment;
            double speed = ExpectedSpeed(racer.Entrant, racer.StaticSpeedFactor, segment)
                * LaneModifier(racer.Lateral)
                * racer.SegmentJitter;
            if (racer.StunRemaining > 0)
            {
                speed *= _balance.Stats.StunSpeedFactor;
            }

            return speed;
        }

        /// <summary>荒れた水面で姿勢を崩す確率（1秒あたり）。安定が高いほど低い。</summary>
        public double StumbleHazardPerSecond(RaceEntrant entrant, FlowSegment segment, double extraRoughness = 0)
        {
            double roughness = MathUtil.Clamp01(segment.Roughness + _roughnessAdd + extraRoughness);
            return roughness * _balance.Stats.StumbleRatePerSecond
                * MathUtil.LerpByStat(_balance.Stats.StumbleHazardByStability, entrant.Stats.Stability);
        }

        /// <summary>加速度（m/s²）。</summary>
        public double AccelRate(RaceEntrant entrant)
        {
            return MathUtil.LerpByStat(_balance.Stats.AccelRateRange, entrant.Stats.Acceleration);
        }

        public double LaneModifier(double lateral)
        {
            var lane = _balance.Lane;
            return 1.0 + lane.CenterFlowBonus - (lane.CenterFlowBonus + lane.EdgeFlowPenalty) * Math.Abs(lateral);
        }

        private double FatigueModifier(int fatigue)
        {
            var f = _balance.Fatigue;
            if (fatigue <= f.SpeedPenaltyStart) return 1.0;
            double t = (fatigue - f.SpeedPenaltyStart) / (100.0 - f.SpeedPenaltyStart);
            return 1.0 - f.SpeedPenaltyAtMax * MathUtil.Clamp01(t);
        }

        private double RoughnessModifier(int buoyancy, FlowSegment segment)
        {
            double roughness = MathUtil.Clamp01(segment.Roughness + _roughnessAdd);
            return 1.0 - roughness * MathUtil.LerpByStat(_balance.Stats.RoughnessPenaltyByBuoyancy, buoyancy);
        }

        private double TerrainAffinityModifier(Affinities affinities, FlowSegment segment)
        {
            double modifier = 1.0;
            if (segment.HasTag(SegmentTags.Rapid)) modifier *= AffinityModifier(affinities[AffinityType.Rapid]);
            if (segment.HasTag(SegmentTags.Whirlpool)) modifier *= AffinityModifier(affinities[AffinityType.Whirlpool]);
            if (segment.HasTag(SegmentTags.Narrow)) modifier *= AffinityModifier(affinities[AffinityType.Narrow]);
            if (segment.WaterDepth >= _balance.Affinity.DeepWaterDepthMin) modifier *= AffinityModifier(affinities[AffinityType.Deep]);
            return modifier;
        }

        private double AffinityModifier(int value)
        {
            return MathUtil.LerpByStat(_balance.Affinity.ModifierRange, value);
        }

        private static AffinityType DistanceAffinityOf(double distance, DistanceBands bands)
        {
            if (distance <= bands.SprintMax) return AffinityType.Sprint;
            if (distance >= bands.LongMin) return AffinityType.Long;
            return AffinityType.Middle;
        }

        private static double Lookup(System.Collections.Generic.Dictionary<string, double> table, string key, double fallback)
        {
            return table != null && table.TryGetValue(key, out double value) ? value : fallback;
        }
    }
}
