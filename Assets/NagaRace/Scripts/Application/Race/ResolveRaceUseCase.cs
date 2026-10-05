using System;
using System.Collections.Generic;
using NagaRace.Domain.Balance;
using NagaRace.Domain.Characters;
using NagaRace.Domain.Race;

namespace NagaRace.Application.Race
{
    /// <summary>
    /// レース結果を手持ちの個体へ反映する（07 §6 ResolveRace）。Lost した個体は現役から外してアルバムへ移す。
    /// </summary>
    public sealed class ResolveRaceUseCase
    {
        private readonly IBalanceRepository _balanceRepository;

        public ResolveRaceUseCase(IBalanceRepository balanceRepository)
        {
            _balanceRepository = balanceRepository ?? throw new ArgumentNullException(nameof(balanceRepository));
        }

        /// <summary>
        /// entrants と result の出走者 Id で手持ちの個体を探し、見つかったもの（自キャラ）だけ反映する。
        /// </summary>
        public Dictionary<string, AftermathReport> Execute(Roster roster, RaceResult result, string courseId, int day)
        {
            var balance = _balanceRepository.Load();
            var reports = new Dictionary<string, AftermathReport>();
            foreach (var placing in result.Placings)
            {
                var character = roster.Find(placing.EntrantId);
                if (character == null || !character.IsActive) continue;

                reports[character.Id] = RaceAftermath.Apply(character, placing, courseId, day, balance);
                if (placing.Lost)
                {
                    roster.MarkLost(character.Id, day, RaceAftermath.LostReason(placing));
                }
            }

            return reports;
        }
    }
}
