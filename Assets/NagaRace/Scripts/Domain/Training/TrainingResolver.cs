using System;
using System.Collections.Generic;
using NagaRace.Domain.Balance;
using NagaRace.Domain.Characters;
using NagaRace.Domain.Common;
using NagaRace.Domain.Race;

namespace NagaRace.Domain.Training
{
    /// <summary>ならし1回の結果（08 §4 TrainingResult）。</summary>
    public sealed class TrainingResult
    {
        public string TrainingId { get; set; }
        /// <summary>能力キー → 増減。</summary>
        public Dictionary<string, int> StatDeltas { get; } = new Dictionary<string, int>();
        public int FatigueBefore { get; set; }
        public int FatigueAfter { get; set; }
        public int DurabilityBefore { get; set; }
        public int DurabilityAfter { get; set; }
        public Condition ConditionBefore { get; set; }
        public Condition ConditionAfter { get; set; }
        /// <summary>怪我で受けた耐久ダメージ（0 なら怪我なし）。</summary>
        public int Injury { get; set; }
        public List<string> NewBodyTraits { get; } = new List<string>();
        public bool PersonalityChanged { get; set; }
        public bool Lost { get; set; }
        /// <summary>効率（全倍率の積）。UI の「本日 BONUS」などに使う。</summary>
        public double Efficiency { get; set; }
    }

    /// <summary>実行前の予測（03 §4 Training カード）。</summary>
    public sealed class TrainingPreview
    {
        /// <summary>能力キー → 期待増加量（乱数なし）。</summary>
        public Dictionary<string, double> ExpectedGains { get; } = new Dictionary<string, double>();
        public double Efficiency { get; set; }
        public double WeatherMultiplier { get; set; }
        public int Fatigue { get; set; }
        public double InjuryChance { get; set; }
        public RiskLevel Risk { get; set; }
        /// <summary>実行前に確認ダイアログが必要（危険度が高い場合のみ: 03 §4）。</summary>
        public bool NeedsConfirmation { get; set; }
        /// <summary>このならしで Lost が起こりうる（極端な条件のみ）。</summary>
        public bool LostPossible { get; set; }
    }

    /// <summary>
    /// ならしの解決（05 §3）。
    /// 伸び = 基礎 × 重み × 成長フェーズ × 天候 × 調子 × 疲労 × 連続 × 伸びしろ × 乱数。
    /// 「同じコマンド連打だけが最適」にならないよう、連続実施・疲労・伸びしろで効率が落ちる（09 M5）。
    /// </summary>
    public static class TrainingResolver
    {
        private static readonly Condition[] ConditionOrder = { Condition.Terrible, Condition.Poor, Condition.Normal, Condition.Good, Condition.Excellent };

        public static TrainingPreview Preview(CharacterState c, TrainingDefinition def, Weather weather, GrowthPhase phase, BalanceConfig balance)
        {
            var t = balance.Training;
            int streak = def.Id == c.LastTrainingId ? c.TrainingStreak + 1 : 1;
            double efficiency = Efficiency(c, def, weather, phase, streak, balance);
            var preview = new TrainingPreview
            {
                Efficiency = efficiency,
                WeatherMultiplier = def.WeatherMultiplier(weather),
                Fatigue = def.Fatigue,
                InjuryChance = InjuryChance(c, def, balance),
            };

            foreach (var pair in def.Growth)
            {
                preview.ExpectedGains[pair.Key.Key] = t.BaseGain * pair.Value * efficiency * Headroom(pair.Key.Get(c), balance);
            }

            preview.LostPossible = IsExtremeState(c, def, balance);
            if (preview.LostPossible) preview.Risk = RiskLevel.Extreme;
            else if (def.Risk >= 3 || (def.Risk >= 2 && c.Fatigue >= t.HighFatigueThreshold)) preview.Risk = RiskLevel.Danger;
            else if (def.Risk >= 1 && (c.Fatigue >= t.HighFatigueThreshold || c.Durability < balance.Durability.LightDamageMin)) preview.Risk = RiskLevel.Caution;
            else preview.Risk = RiskLevel.Safe;
            preview.NeedsConfirmation = preview.Risk >= RiskLevel.Danger;
            return preview;
        }

        /// <summary>
        /// ならしを実行して個体に反映する。極端な条件で Lost しうるならしは confirmedExtreme が true のときだけ実行できる。
        /// </summary>
        public static TrainingResult Apply(
            CharacterState c,
            TrainingDefinition def,
            Weather weather,
            GrowthPhase phase,
            TrainingCatalog catalog,
            BalanceConfig balance,
            IRandomProvider random,
            bool confirmedExtreme = false)
        {
            if (!c.IsActive) throw new InvalidOperationException($"{c.Name} は現役ではありません。");
            bool extreme = IsExtremeState(c, def, balance);
            if (extreme && !confirmedExtreme)
            {
                throw new InvalidOperationException("極危険のならしは確認が必要です（05 §2）。");
            }

            var t = balance.Training;
            var result = new TrainingResult
            {
                TrainingId = def.Id,
                FatigueBefore = c.Fatigue,
                DurabilityBefore = c.Durability,
                ConditionBefore = c.Condition,
            };

            int streak = c.RecordTraining(def.Id);
            double efficiency = Efficiency(c, def, weather, phase, streak, balance);
            result.Efficiency = efficiency;

            // 能力の伸び（能力ごとに乱数を必ず2回: ゆらぎ・端数の切り上げ）
            foreach (var pair in def.Growth)
            {
                double variance = random.Range(t.RandomRange[0], t.RandomRange[1]);
                double raw = t.BaseGain * pair.Value * efficiency * Headroom(pair.Key.Get(c), balance) * variance;
                double fraction = random.NextDouble();
                int gain = (int)Math.Floor(raw) + (fraction < raw - Math.Floor(raw) ? 1 : 0);
                if (gain <= 0) continue;
                int before = pair.Key.Get(c);
                pair.Key.Add(c, gain);
                int actual = pair.Key.Get(c) - before;
                if (actual != 0) result.StatDeltas[pair.Key.Key] = actual;
            }

            // 疲労・回復
            c.SetFatigue(c.Fatigue + def.Fatigue);
            if (def.DurabilityRecovery > 0) c.SetDurability(c.Durability + def.DurabilityRecovery);

            // 怪我（乱数は必ず2回: 発生・ダメージ量）
            double injuryRoll = random.NextDouble();
            double injuryVariance = random.Range(0.5, 1.5);
            if (def.Risk > 0 && injuryRoll < InjuryChance(c, def, balance))
            {
                double toughness = MathUtil.LerpByStat(balance.Stats.DamageMultiplierByToughness, c.Stats.Toughness);
                int damage = Math.Max(1, (int)Math.Round(def.Risk * t.InjuryDamagePerRisk * injuryVariance * toughness));
                c.SetDurability(c.Durability - damage);
                result.Injury = damage;

                // 極端な条件で耐久が尽きた場合のみ Lost 判定（確認済みのときだけここに来る）
                if (extreme && c.Durability <= 0 && random.NextDouble() < balance.Lost.ChanceOnCandidate)
                {
                    result.Lost = true;
                }
            }

            UpdateCondition(c, def, balance, random);
            AccumulateBody(c, def, catalog, balance, result);

            if (def.PersonalityShiftTo.HasValue && c.Personality != def.PersonalityShiftTo.Value
                && random.NextDouble() < def.PersonalityShiftChance)
            {
                c.Personality = def.PersonalityShiftTo.Value;
                result.PersonalityChanged = true;
            }

            result.FatigueAfter = c.Fatigue;
            result.DurabilityAfter = c.Durability;
            result.ConditionAfter = c.Condition;
            return result;
        }

        /// <summary>極端な条件（05 §2 v1.1）: 耐久20未満かつ高疲労で、極端なならし。</summary>
        public static bool IsExtremeState(CharacterState c, TrainingDefinition def, BalanceConfig balance)
        {
            return def.Extreme && c.Durability < balance.Durability.DamagedMin && c.Fatigue >= balance.Training.HighFatigueThreshold;
        }

        private static double Efficiency(CharacterState c, TrainingDefinition def, Weather weather, GrowthPhase phase, int streak, BalanceConfig balance)
        {
            var t = balance.Training;
            double condition = t.ConditionMultiplier != null && t.ConditionMultiplier.TryGetValue(c.Condition.ToString(), out double cm) ? cm : 1.0;
            double fatigue = 1.0;
            if (c.Fatigue > t.FatiguePenaltyStart)
            {
                double ratio = (c.Fatigue - t.FatiguePenaltyStart) / (100.0 - t.FatiguePenaltyStart);
                fatigue = MathUtil.Lerp(1.0, t.FatigueMultiplierAtMax, ratio);
            }

            var monotony = t.MonotonyMultipliers;
            double repeat = monotony == null || monotony.Length == 0 ? 1.0 : monotony[Math.Min(streak, monotony.Length) - 1];
            return GrowthRules.TrainingMultiplier(phase, balance) * def.WeatherMultiplier(weather) * condition * fatigue * repeat;
        }

        /// <summary>伸びしろ。高い能力ほど伸びにくい。</summary>
        private static double Headroom(int value, BalanceConfig balance)
        {
            var t = balance.Training;
            double ratio = MathUtil.Clamp01(value / t.StatSoftCap);
            return Math.Max(0.05, 1.0 - Math.Pow(ratio, t.DiminishingExponent));
        }

        private static double InjuryChance(CharacterState c, TrainingDefinition def, BalanceConfig balance)
        {
            if (def.Risk <= 0) return 0;
            var t = balance.Training;
            double fatigue = 1.0 + c.Fatigue / 100.0 * (t.InjuryFatigueMultiplierAtMax - 1.0);
            return MathUtil.Clamp01(def.Risk * t.InjuryChancePerRisk * fatigue);
        }

        private static void UpdateCondition(CharacterState c, TrainingDefinition def, BalanceConfig balance, IRandomProvider random)
        {
            var t = balance.Training;
            int level = Array.IndexOf(ConditionOrder, c.Condition);
            // 乱数は必ず3回（回復・疲労による悪化・ゆらぎ）
            double up = random.NextDouble();
            double drop = random.NextDouble();
            double drift = random.NextDouble();
            if (up < def.ConditionUp) level++;
            if (c.Fatigue >= t.HighFatigueThreshold && drop < t.HighFatigueConditionDropChance) level--;
            if (drift < t.ConditionDriftChance) level += drift < t.ConditionDriftChance / 2 ? 1 : -1;
            c.Condition = ConditionOrder[MathUtil.Clamp(level, 0, ConditionOrder.Length - 1)];
        }

        private static void AccumulateBody(CharacterState c, TrainingDefinition def, TrainingCatalog catalog, BalanceConfig balance, TrainingResult result)
        {
            if (def.BodyAccumulation == null) return;
            foreach (var pair in def.BodyAccumulation)
            {
                c.AddBodyAccumulation(pair.Key, pair.Value);
            }

            foreach (var trait in catalog.BodyTraits)
            {
                if (c.HasBodyTrait(trait.Id)) continue;
                if (!c.BodyAccumulation.TryGetValue(trait.Accumulation, out double value) || value < balance.Training.BodyTraitThreshold) continue;

                c.AddBodyTrait(trait.Id);
                foreach (var modifier in trait.Modifiers)
                {
                    modifier.Key.Add(c, modifier.Value);
                }

                result.NewBodyTraits.Add(trait.Id);
            }
        }
    }
}
