using NagaRace.Domain.Common;

namespace NagaRace.Domain.Characters
{
    public enum FurnaceStat
    {
        Output,
        Startup,
        CriticalDuration,
        Cooling,
        Durability,
    }

    /// <summary>
    /// 葉脈炉の能力（02 §4, 04 §8.2）。
    /// </summary>
    public sealed class FurnaceStats
    {
        public FurnaceStats(int output, int startup, int criticalDuration, int cooling, int durability)
        {
            Output = MathUtil.Clamp(output, 0, 100);
            Startup = MathUtil.Clamp(startup, 0, 100);
            CriticalDuration = MathUtil.Clamp(criticalDuration, 0, 100);
            Cooling = MathUtil.Clamp(cooling, 0, 100);
            Durability = MathUtil.Clamp(durability, 0, 100);
        }

        public int Output { get; }
        public int Startup { get; }
        public int CriticalDuration { get; }
        public int Cooling { get; }
        /// <summary>炉耐久。</summary>
        public int Durability { get; }

        public int this[FurnaceStat stat]
        {
            get
            {
                switch (stat)
                {
                    case FurnaceStat.Output: return Output;
                    case FurnaceStat.Startup: return Startup;
                    case FurnaceStat.CriticalDuration: return CriticalDuration;
                    case FurnaceStat.Cooling: return Cooling;
                    default: return Durability;
                }
            }
        }

        public FurnaceStats With(FurnaceStat stat, int value)
        {
            return new FurnaceStats(
                stat == FurnaceStat.Output ? value : Output,
                stat == FurnaceStat.Startup ? value : Startup,
                stat == FurnaceStat.CriticalDuration ? value : CriticalDuration,
                stat == FurnaceStat.Cooling ? value : Cooling,
                stat == FurnaceStat.Durability ? value : Durability);
        }

        public static FurnaceStats Uniform(int value)
        {
            return new FurnaceStats(value, value, value, value, value);
        }
    }
}
