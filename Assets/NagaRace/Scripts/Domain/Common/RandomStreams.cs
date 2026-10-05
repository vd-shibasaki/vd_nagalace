namespace NagaRace.Domain.Common
{
    /// <summary>乱数サブストリームの用途（04 §1.2）。</summary>
    public enum RandomStreamId
    {
        Route = 1,
        Movement = 2,
        Contact = 3,
        Furnace = 4,
        Event = 5,
        Generation = 6,
    }

    /// <summary>
    /// レースSeedから用途別・出走者別のサブストリームを派生させる。
    /// プレイヤーの炉操作の有無などで、他の出走者の抽選結果が連鎖的に変わることを防ぐ。
    /// </summary>
    public static class RandomStreams
    {
        /// <summary>出走者に属さない共通ストリームに使う index。</summary>
        public const int SharedIndex = -1;

        public static IRandomProvider Create(ulong seed, int entrantIndex, RandomStreamId streamId)
        {
            ulong key = seed;
            key ^= SplitMix64(unchecked((ulong)(entrantIndex + 1) * 0x9E3779B97F4A7C15UL));
            key ^= SplitMix64(unchecked((ulong)streamId * 0xBF58476D1CE4E5B9UL));
            ulong derivedSeed = SplitMix64(key);
            ulong derivedStream = SplitMix64(derivedSeed ^ 0x94D049BB133111EBUL);
            return new Pcg32Random(derivedSeed, derivedStream);
        }

        public static ulong SplitMix64(ulong x)
        {
            unchecked
            {
                x += 0x9E3779B97F4A7C15UL;
                x = (x ^ (x >> 30)) * 0xBF58476D1CE4E5B9UL;
                x = (x ^ (x >> 27)) * 0x94D049BB133111EBUL;
                return x ^ (x >> 31);
            }
        }
    }
}
