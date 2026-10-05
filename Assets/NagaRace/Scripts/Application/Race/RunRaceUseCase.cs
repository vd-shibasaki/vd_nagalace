using System;
using System.Collections.Generic;
using NagaRace.Domain.Balance;
using NagaRace.Domain.Race;

namespace NagaRace.Application.Race
{
    /// <summary>レース実行の入力。</summary>
    public sealed class RaceRequest
    {
        public RaceRequest(RaceCourse course, IReadOnlyList<RaceEntrant> entrants, RaceConditions conditions, ulong seed)
        {
            Course = course ?? throw new ArgumentNullException(nameof(course));
            Entrants = entrants ?? throw new ArgumentNullException(nameof(entrants));
            Conditions = conditions ?? RaceConditions.Default;
            Seed = seed;
        }

        public RaceCourse Course { get; }
        public IReadOnlyList<RaceEntrant> Entrants { get; }
        public RaceConditions Conditions { get; }
        public ulong Seed { get; }
    }

    /// <summary>
    /// レースを開始・実行するユースケース（07 §6 StartRace / ResolveRace）。
    /// 観戦する場合は <see cref="Start"/> で得たシミュレーターを View 側が Tick ごとに進める。
    /// </summary>
    public sealed class RunRaceUseCase
    {
        private readonly IBalanceRepository _balanceRepository;

        public RunRaceUseCase(IBalanceRepository balanceRepository)
        {
            _balanceRepository = balanceRepository ?? throw new ArgumentNullException(nameof(balanceRepository));
        }

        public RaceSimulator Start(RaceRequest request)
        {
            BalanceConfig balance = _balanceRepository.Load();
            int expected = balance.Race.EntrantCount;
            if (expected > 0 && request.Entrants.Count != expected)
            {
                throw new ArgumentException($"出走数は {expected} 体です（指定 {request.Entrants.Count} 体）。");
            }

            return new RaceSimulator(request.Course, request.Entrants, request.Conditions, balance, request.Seed);
        }

        /// <summary>観戦せずに最後まで計算する（スキップ・テスト・NPC同士のレース用）。</summary>
        public RaceResult Execute(RaceRequest request)
        {
            return Start(request).RunToEnd();
        }
    }
}
