using System;
using System.Collections.Generic;
using NagaRace.Domain.Balance;
using NagaRace.Domain.Characters;

namespace NagaRace.Domain.Race
{
    /// <summary>出走前に表示する危険度（02 §11, 04 §9.2）。</summary>
    public enum RiskLevel
    {
        /// <summary>Lost は発生しない。</summary>
        Safe,
        /// <summary>Lost は発生しない。故障・ダメージに注意。</summary>
        Caution,
        /// <summary>条件次第で Lost の候補になる。</summary>
        Danger,
        /// <summary>Lost の候補になりやすい（危険要素が重なっている）。</summary>
        Extreme,
    }

    public sealed class RiskReport
    {
        public RiskReport(RiskLevel level, int courseDanger, bool lostPossible, IReadOnlyList<string> reasons)
        {
            Level = level;
            CourseDanger = courseDanger;
            LostPossible = lostPossible;
            Reasons = reasons;
        }

        public RiskLevel Level { get; }
        public int CourseDanger { get; }
        /// <summary>このレースで Lost が起きうるか。Safe / Caution では必ず false。</summary>
        public bool LostPossible { get; }
        /// <summary>表示用の理由（「危険なコース」「耐久が低い」など）。</summary>
        public IReadOnlyList<string> Reasons { get; }

        public string Label
        {
            get
            {
                switch (Level)
                {
                    case RiskLevel.Caution: return "注意";
                    case RiskLevel.Danger: return "危険";
                    case RiskLevel.Extreme: return "極危険";
                    default: return "安全";
                }
            }
        }
    }

    /// <summary>
    /// 出走前の危険度判定。Lost の可否は RaceSimulator と同じ判定（IsLostPossible）を使うため、
    /// 「安全」「注意」と表示したレースで Lost が起きることはない。
    /// </summary>
    public static class RiskAssessment
    {
        public static RiskReport Evaluate(RaceCourse course, RaceConditions conditions, RaceEntrant entrant, BalanceConfig balance)
        {
            conditions = conditions ?? RaceConditions.Default;
            var speed = new SpeedCalculator(balance, conditions, course);
            int courseDanger = 1;
            foreach (var segment in course.Segments)
            {
                courseDanger = Math.Max(courseDanger, speed.EffectiveDanger(segment));
            }

            var d = balance.Durability;
            var reasons = new List<string>();
            bool lostPossible = RaceSimulator.IsLostPossible(courseDanger, entrant.Durability, balance);

            if (courseDanger >= 4) reasons.Add("極めて危険なコース");
            else if (courseDanger >= 3) reasons.Add("危険なコース");
            if (conditions.Weather == Weather.HeavyRain) reasons.Add("大雨で増水");
            if (entrant.Durability < d.DamagedMin) reasons.Add("耐久がかなり低い");
            else if (entrant.Durability < d.LightDamageMin) reasons.Add("耐久が低い");
            if (entrant.Fatigue >= balance.Risk.CautionFatigueMin) reasons.Add("疲労が高い");
            if (entrant.Condition == Condition.Poor || entrant.Condition == Condition.Terrible) reasons.Add("調子が悪い");

            RiskLevel level;
            if (!lostPossible)
            {
                bool caution = courseDanger >= 2
                    || entrant.Durability < d.GoodMin
                    || entrant.Fatigue >= balance.Risk.CautionFatigueMin
                    || entrant.Condition == Condition.Poor
                    || entrant.Condition == Condition.Terrible;
                level = caution ? RiskLevel.Caution : RiskLevel.Safe;
            }
            else
            {
                int factors = 0;
                if (courseDanger >= 4) factors++;
                if (entrant.Durability < d.DamagedMin) factors++;
                else if (courseDanger >= 3 && entrant.Durability < d.LightDamageMin) factors++;
                if (entrant.Fatigue >= balance.Risk.CautionFatigueMin) factors++;
                level = factors >= balance.Risk.ExtremeFactorCount ? RiskLevel.Extreme : RiskLevel.Danger;
            }

            return new RiskReport(level, courseDanger, lostPossible, reasons);
        }
    }
}
