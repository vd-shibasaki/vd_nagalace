using System;
using NagaRace.Application.Race;
using NagaRace.Domain.Characters;
using NagaRace.Domain.Training;

namespace NagaRace.Application.Training
{
    /// <summary>ならし定義の取得元。</summary>
    public interface ITrainingCatalogRepository
    {
        TrainingCatalog Load();
    }

    /// <summary>
    /// ならしの予測と実行（07 §6 ExecuteTraining）。実行すると1日進む（02 §7）。
    /// </summary>
    public sealed class TrainingUseCase
    {
        private readonly IBalanceRepository _balance;
        private readonly ITrainingCatalogRepository _catalog;

        public TrainingUseCase(IBalanceRepository balance, ITrainingCatalogRepository catalog)
        {
            _balance = balance ?? throw new ArgumentNullException(nameof(balance));
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        }

        public TrainingPreview Preview(GameSession session, string characterId, string trainingId)
        {
            var balance = _balance.Load();
            var character = RequireActive(session, characterId);
            var weather = session.TodayConditions(balance).Weather;
            return TrainingResolver.Preview(character, _catalog.Load().Get(trainingId), weather, character.PhaseOn(session.Day), balance);
        }

        /// <summary>
        /// ならしを実行し、1日進める。極端な条件のならしは confirmedExtreme が必要。Lost した場合はアルバムへ移す。
        /// </summary>
        public TrainingResult Execute(GameSession session, string characterId, string trainingId, bool confirmedExtreme = false)
        {
            var balance = _balance.Load();
            var catalog = _catalog.Load();
            var character = RequireActive(session, characterId);
            var weather = session.TodayConditions(balance).Weather;
            var result = TrainingResolver.Apply(
                character,
                catalog.Get(trainingId),
                weather,
                character.PhaseOn(session.Day),
                catalog,
                balance,
                session.RandomFor("training", characterId),
                confirmedExtreme);

            if (result.Lost)
            {
                session.Roster.MarkLost(characterId, session.Day, "LostTraining");
            }

            session.AdvanceDay(balance);
            return result;
        }

        private static CharacterState RequireActive(GameSession session, string characterId)
        {
            var character = session.Roster.Find(characterId);
            if (character == null || !character.IsActive) throw new InvalidOperationException($"現役の個体 {characterId} がいません。");
            return character;
        }
    }
}
