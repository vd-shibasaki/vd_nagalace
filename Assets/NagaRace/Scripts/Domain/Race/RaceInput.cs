using System;

namespace NagaRace.Domain.Race
{
    public enum RaceInputType
    {
        /// <summary>葉脈炉を始動する。</summary>
        FurnaceActivate,
        /// <summary>葉脈炉を解除する。</summary>
        FurnaceRelease,
        /// <summary>葉力チャンスの受付中にタップ / スワイプした。</summary>
        Charge,
    }

    /// <summary>
    /// プレイヤー入力。Tick 番号で記録し、リプレイ・非同期PvP の検証に使う（08 §6）。
    /// </summary>
    public readonly struct RaceInput : IEquatable<RaceInput>
    {
        public RaceInput(int tick, int entrantIndex, RaceInputType type)
        {
            Tick = tick;
            EntrantIndex = entrantIndex;
            Type = type;
        }

        /// <summary>この入力が反映された Tick。</summary>
        public int Tick { get; }
        public int EntrantIndex { get; }
        public RaceInputType Type { get; }

        public bool Equals(RaceInput other)
        {
            return Tick == other.Tick && EntrantIndex == other.EntrantIndex && Type == other.Type;
        }

        public override bool Equals(object obj)
        {
            return obj is RaceInput other && Equals(other);
        }

        public override int GetHashCode()
        {
            return (Tick * 397) ^ (EntrantIndex * 31) ^ (int)Type;
        }

        public override string ToString()
        {
            return $"[{Tick}] #{EntrantIndex} {Type}";
        }
    }
}
