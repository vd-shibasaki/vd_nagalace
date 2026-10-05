using System;
using System.Collections.Generic;

namespace NagaRace.Domain.Race
{
    /// <summary>1出走者の着順。</summary>
    public sealed class RacePlacing
    {
        public RacePlacing(int entrantIndex, string entrantId, int rank, bool finished, double finishTimeSeconds)
        {
            EntrantIndex = entrantIndex;
            EntrantId = entrantId;
            Rank = rank;
            Finished = finished;
            FinishTimeSeconds = finishTimeSeconds;
            FinishTimeMilliseconds = finished ? (long)Math.Round(finishTimeSeconds * 1000.0) : -1;
        }

        public int EntrantIndex { get; }
        public string EntrantId { get; }
        /// <summary>1始まり。</summary>
        public int Rank { get; }
        public bool Finished { get; }
        public double FinishTimeSeconds { get; }
        /// <summary>未ゴールは -1。</summary>
        public long FinishTimeMilliseconds { get; }

        /// <summary>"01:42.186" 形式。</summary>
        public string FormatTime()
        {
            if (!Finished) return "--:--.---";
            long ms = FinishTimeMilliseconds;
            return $"{ms / 60000:00}:{ms / 1000 % 60:00}.{ms % 1000:000}";
        }
    }

    /// <summary>レース結果。</summary>
    public sealed class RaceResult
    {
        public RaceResult(ulong seed, int totalTicks, int tickRate, bool timedOut, IReadOnlyList<RacePlacing> placings, IReadOnlyList<RaceEvent> events)
        {
            Seed = seed;
            TotalTicks = totalTicks;
            TickRate = tickRate;
            TimedOut = timedOut;
            Placings = placings;
            Events = events;
        }

        public ulong Seed { get; }
        public int TotalTicks { get; }
        public int TickRate { get; }
        public bool TimedOut { get; }
        /// <summary>着順（1着から）。</summary>
        public IReadOnlyList<RacePlacing> Placings { get; }
        public IReadOnlyList<RaceEvent> Events { get; }

        public RacePlacing Winner => Placings[0];

        public RacePlacing PlacingOf(int entrantIndex)
        {
            foreach (var placing in Placings)
            {
                if (placing.EntrantIndex == entrantIndex) return placing;
            }

            throw new ArgumentOutOfRangeException(nameof(entrantIndex));
        }
    }
}
