using System;
using System.Collections.Generic;
using NagaRace.Domain.Balance;
using NagaRace.Domain.Common;

namespace NagaRace.Domain.Race
{
    /// <summary>葉脈炉の状態（04 §8.1）。</summary>
    public enum FurnacePhase
    {
        /// <summary>待機中。葉力が足りれば始動できる。</summary>
        Ready,
        /// <summary>始動入力後の立ち上がり（起動ラグ）。</summary>
        Ignition,
        /// <summary>稼働中・安全圏。</summary>
        ActiveSafe,
        /// <summary>稼働中・SafeDuration 超過（UI 表示は「OVER FLOW」）。</summary>
        ActiveRisk,
        /// <summary>解除後の冷却中。</summary>
        Cooldown,
        /// <summary>故障。このレース中は再始動できない。</summary>
        Broken,
    }

    /// <summary>故障の重さ（04 §8.4）。</summary>
    public enum FurnaceFailure
    {
        None,
        MinorDamage,
        FurnaceDamage,
        Blow,
        Critical,
    }

    /// <summary>
    /// 炉能力と状態から、秒数・倍率・故障率を求める（04 §8.2〜8.4）。状態は持たない。
    /// </summary>
    public sealed class FurnaceRules
    {
        private static readonly FurnaceFailure[] Severities =
        {
            FurnaceFailure.MinorDamage, FurnaceFailure.FurnaceDamage, FurnaceFailure.Blow, FurnaceFailure.Critical,
        };

        private readonly FurnaceBalance _f;

        public FurnaceRules(BalanceConfig balance)
        {
            _f = balance.Furnace;
            MinimumLeafPower = balance.LeafPower.FurnaceMinimum;
        }

        public double MinimumLeafPower { get; }
        public double DrainPerSecond => _f.DrainPerSecond;
        public double AccelMultiplier => _f.AccelMultiplier;
        public double ContactSlowdownMultiplier => _f.ContactSlowdownMultiplier;

        public double SafeDuration(RaceEntrant e) => MathUtil.LerpByStat(_f.SafeDurationSecondsByCritical, e.Furnace.CriticalDuration);
        public double IgnitionLag(RaceEntrant e) => MathUtil.LerpByStat(_f.IgnitionLagSecondsByStartup, e.Furnace.Startup);
        public double IgnitionCost(RaceEntrant e) => MathUtil.LerpByStat(_f.IgnitionCostByStartup, e.Furnace.Startup);
        public double Cooldown(RaceEntrant e) => MathUtil.LerpByStat(_f.CooldownSecondsByCooling, e.Furnace.Cooling);
        public double Fragility(RaceEntrant e) => MathUtil.LerpByStat(_f.FragilityByFurnaceDurability, e.Furnace.Durability);
        public double OutputBoost(RaceEntrant e) => MathUtil.LerpByStat(_f.OutputBoostRange, e.Furnace.Output);

        /// <summary>
        /// 稼働時間に応じたブースト倍率（04 §8.3）。始動時 rampStart → SafeDuration で rampAtSafeEnd → RiskZone 中も rampMax へ近づく。
        /// </summary>
        public double Ramp(double activeSeconds, double safeDuration)
        {
            if (activeSeconds <= safeDuration)
            {
                return MathUtil.Lerp(_f.RampStart, _f.RampAtSafeEnd, activeSeconds / safeDuration);
            }

            double overrun = activeSeconds - safeDuration;
            double t = 1.0 - Math.Exp(-overrun / Math.Max(0.01, _f.RiskRampTimeConstantSeconds));
            return _f.RampAtSafeEnd + (_f.RampMax - _f.RampAtSafeEnd) * t;
        }

        /// <summary>速度倍率（1 + ブースト）。</summary>
        public double SpeedMultiplier(RaceEntrant e, double activeSeconds, double safeDuration)
        {
            return 1.0 + OutputBoost(e) * Ramp(activeSeconds, safeDuration);
        }

        /// <summary>RiskZone 中の1秒あたり故障率。耐久が低く疲労が高いほど上がる（10 B-04）。</summary>
        public double HazardPerSecond(RaceEntrant e, double overrunSeconds)
        {
            if (overrunSeconds <= 0) return 0;
            double durabilityRatio = e.MaxDurability > 0 ? (double)e.Durability / e.MaxDurability : 0;
            double damageModifier = 1.0 + (1.0 - MathUtil.Clamp01(durabilityRatio)) * (_f.DamageModifierAtZeroDurability - 1.0);
            double fatigueModifier = 1.0 + Math.Max(0, e.Fatigue - 50) / 50.0 * (_f.FatigueModifierAtMax - 1.0);
            return _f.RiskRampPerSecond * overrunSeconds * Fragility(e) * damageModifier * fatigueModifier;
        }

        /// <summary>故障の重さを抽選する。引っ張った時間が長いほど重い側へ寄る。</summary>
        public FurnaceFailure RollSeverity(IRandomProvider random, double overrunSeconds)
        {
            var w = _f.FailureSeverityWeights;
            double t = MathUtil.Clamp01(overrunSeconds / Math.Max(0.01, w.LongOverrunSeconds));
            var weights = new double[Severities.Length];
            double total = 0;
            for (int i = 0; i < Severities.Length; i++)
            {
                string key = Severities[i].ToString();
                weights[i] = MathUtil.Lerp(Lookup(w.Short, key), Lookup(w.Long, key), t);
                total += weights[i];
            }

            double roll = random.NextDouble() * total;
            for (int i = 0; i < Severities.Length; i++)
            {
                roll -= weights[i];
                if (roll < 0) return Severities[i];
            }

            return Severities[Severities.Length - 1];
        }

        public double MinorSlowdown => _f.MinorSlowdown;
        public double MinorStunSeconds => _f.MinorStunSeconds;
        public double BlowSlowdown => _f.BlowSlowdown;
        public double BlowStunSeconds => _f.BlowStunSeconds;
        public double NpcFinalStretchProgress => _f.NpcFinalStretchProgress;
        public double NpcFullGaugeLeafPower => _f.NpcFullGaugeLeafPower;

        private static double Lookup(Dictionary<string, double> table, string key)
        {
            return table != null && table.TryGetValue(key, out double v) ? v : 0;
        }
    }
}
