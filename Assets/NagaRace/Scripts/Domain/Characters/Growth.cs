using System;
using System.Collections.Generic;
using NagaRace.Domain.Balance;
using NagaRace.Domain.Common;

namespace NagaRace.Domain.Characters
{
    /// <summary>成長タイプ（02 §6 v1.1）。接ぎ木で継承される。</summary>
    public enum GrowthType
    {
        Early,
        Normal,
        Late,
        Lasting,
    }

    /// <summary>成長フェーズ（02 §6）。</summary>
    public enum GrowthPhase
    {
        Young,
        Growing,
        Peak,
        Autumn,
        Withered,
    }

    /// <summary>
    /// 個体ごとの成長曲線。各フェーズの日数は誕生時に成長タイプのレンジから決まる（固定日数にしない）。
    /// </summary>
    public sealed class GrowthProfile
    {
        private static readonly GrowthPhase[] Timed = { GrowthPhase.Young, GrowthPhase.Growing, GrowthPhase.Peak, GrowthPhase.Autumn };

        public GrowthProfile(GrowthType type, int youngDays, int growingDays, int peakDays, int autumnDays)
        {
            Type = type;
            PhaseDays = new[] { youngDays, growingDays, peakDays, autumnDays };
        }

        public GrowthType Type { get; }

        /// <summary>若葉・成長・最盛・紅葉の日数。以降は枯葉。</summary>
        public IReadOnlyList<int> PhaseDays { get; }

        public static GrowthProfile Roll(GrowthType type, BalanceConfig balance, IRandomProvider random)
        {
            var days = new int[4];
            if (balance.Growth.Types != null && balance.Growth.Types.TryGetValue(type.ToString(), out var t))
            {
                for (int i = 0; i < Timed.Length; i++)
                {
                    var range = t.PhaseDays[Timed[i].ToString()];
                    days[i] = random.Range(range[0], range[1] + 1);
                }
            }
            else
            {
                days = new[] { 10, 24, 28, 16 };
            }

            return new GrowthProfile(type, days[0], days[1], days[2], days[3]);
        }

        /// <summary>生まれてからの日数（0始まり）でのフェーズ。</summary>
        public GrowthPhase PhaseAt(int ageDays)
        {
            int elapsed = 0;
            for (int i = 0; i < Timed.Length; i++)
            {
                elapsed += PhaseDays[i];
                if (ageDays < elapsed) return Timed[i];
            }

            return GrowthPhase.Withered;
        }

        /// <summary>現在のフェーズが終わるまでの残り日数（枯葉は -1）。</summary>
        public int DaysLeftInPhase(int ageDays)
        {
            int elapsed = 0;
            for (int i = 0; i < Timed.Length; i++)
            {
                elapsed += PhaseDays[i];
                if (ageDays < elapsed) return elapsed - ageDays;
            }

            return -1;
        }

        public static string Label(GrowthPhase phase)
        {
            switch (phase)
            {
                case GrowthPhase.Young: return "若葉";
                case GrowthPhase.Growing: return "成長";
                case GrowthPhase.Peak: return "最盛";
                case GrowthPhase.Autumn: return "紅葉";
                default: return "枯葉";
            }
        }

        public static string Label(GrowthType type)
        {
            switch (type)
            {
                case GrowthType.Early: return "早熟";
                case GrowthType.Late: return "晩成";
                case GrowthType.Lasting: return "持続";
                default: return "普通";
            }
        }
    }

    /// <summary>成長フェーズがレース・育成に与える係数と、紅葉以降の衰え（09 M6）。</summary>
    public static class GrowthRules
    {
        public static double TrainingMultiplier(GrowthPhase phase, BalanceConfig balance)
        {
            return Lookup(balance.Growth.TrainingMultiplier, phase.ToString(), 1.0);
        }

        public static double RaceSpeedMultiplier(GrowthPhase phase, BalanceConfig balance)
        {
            return Lookup(balance.Growth.RaceSpeedMultiplier, phase.ToString(), 1.0);
        }

        /// <summary>1日経過したときの衰え。紅葉期は緩やかに、枯葉期ははっきりと能力が下がる（10 E）。</summary>
        public static StatType? DailyDecline(CharacterState character, GrowthPhase phase, BalanceConfig balance, IRandomProvider random)
        {
            double chance = Lookup(balance.Growth.DailyDeclineChance, phase.ToString(), 0);
            // 乱数は毎日2回引く（衰えるか・どの能力か）
            double roll = random.NextDouble();
            var stats = balance.Growth.DeclineStats;
            int pick = stats != null && stats.Length > 0 ? random.Range(0, stats.Length) : 0;
            if (roll >= chance || stats == null || stats.Length == 0) return null;
            if (!Enum.TryParse(stats[pick], out StatType type)) return null;

            character.Stats = character.Stats.With(type, character.Stats[type] - 1);
            return type;
        }

        private static double Lookup(Dictionary<string, double> table, string key, double fallback)
        {
            return table != null && table.TryGetValue(key, out double v) ? v : fallback;
        }
    }
}
