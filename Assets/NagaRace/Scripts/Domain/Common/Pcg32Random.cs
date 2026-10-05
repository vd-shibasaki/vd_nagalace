using System;

namespace NagaRace.Domain.Common
{
    /// <summary>
    /// PCG32（XSH RR）による Seed 付き疑似乱数。
    /// 実装がこのファイルで完結しているため、.NET / Unity のバージョン差で系列が変わらない。
    /// </summary>
    public sealed class Pcg32Random : IRandomProvider
    {
        private const ulong Multiplier = 6364136223846793005UL;

        private ulong _state;
        private readonly ulong _increment;

        public Pcg32Random(ulong seed, ulong streamId = 0)
        {
            // increment は奇数である必要がある
            _increment = (streamId << 1) | 1UL;
            _state = 0UL;
            NextUInt();
            _state += seed;
            NextUInt();
        }

        public uint NextUInt()
        {
            ulong old = _state;
            _state = unchecked(old * Multiplier + _increment);
            uint xorShifted = (uint)(((old >> 18) ^ old) >> 27);
            int rot = (int)(old >> 59);
            return (xorShifted >> rot) | (xorShifted << ((-rot) & 31));
        }

        public double NextDouble()
        {
            // 上位53bitを使い [0,1) を作る
            ulong high = NextUInt();
            ulong low = NextUInt();
            ulong bits53 = ((high << 32) | low) >> 11;
            return bits53 * (1.0 / 9007199254740992.0);
        }

        public int Range(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive)
            {
                throw new ArgumentException($"範囲が不正です: [{minInclusive}, {maxExclusive})");
            }

            uint span = (uint)(maxExclusive - minInclusive);
            // 剰余の偏りを避けるため棄却法を使う
            uint threshold = (uint)(-span) % span;
            while (true)
            {
                uint r = NextUInt();
                if (r >= threshold)
                {
                    return minInclusive + (int)(r % span);
                }
            }
        }

        public double Range(double min, double max)
        {
            return min + (max - min) * NextDouble();
        }
    }
}
