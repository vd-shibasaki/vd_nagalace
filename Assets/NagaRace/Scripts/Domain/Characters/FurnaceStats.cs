using NagaRace.Domain.Common;

namespace NagaRace.Domain.Characters
{
    /// <summary>
    /// 葉脈炉の能力（02 §4, 04 §8.2）。
    /// TODO(M3): レースでの使用は M3 で実装する。M1 では出走データの置き場としてのみ持つ。
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

        public static FurnaceStats Uniform(int value)
        {
            return new FurnaceStats(value, value, value, value, value);
        }
    }
}
