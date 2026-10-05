using System;
using System.Collections.Generic;
using NagaRace.Application.Lineage;
using NagaRace.Application.Race;
using NagaRace.Domain.Balance;
using NagaRace.Domain.Characters;
using NagaRace.Domain.Common;
using NagaRace.Domain.Lineage;
using NagaRace.Domain.Race;
using NagaRace.Domain.Tournament;
using NagaRace.Domain.Training;

namespace NagaRace.Application.Game
{
    /// <summary>大会定義の取得元。</summary>
    public interface ITournamentCatalogRepository
    {
        TournamentCatalog Load();
    }

    /// <summary>新しくゲームを始める（07 §6 StartNewGame）。最初の葉っぱ3体を迎える。</summary>
    public sealed class NewGameUseCase
    {
        private static readonly (string species, string name)[] Starters =
        {
            ("maple", "アカネ"),
            ("ginkgo", "ギンナン"),
            ("lotus", "ハスマル"),
        };

        private readonly IBalanceRepository _balance;
        private readonly ISpeciesRepository _species;

        public NewGameUseCase(IBalanceRepository balance, ISpeciesRepository species)
        {
            _balance = balance ?? throw new ArgumentNullException(nameof(balance));
            _species = species ?? throw new ArgumentNullException(nameof(species));
        }

        public GameSession Start(ulong worldSeed)
        {
            var balance = _balance.Load();
            var session = new GameSession(worldSeed, 1);
            foreach (var (speciesId, name) in Starters)
            {
                var species = Find(speciesId);
                var random = session.RandomFor("starter", speciesId);
                // 若葉として、系統の能力レンジより少し低めに生まれる
                var generated = NpcFactory.Generate(new NpcSpec(new[] { species }, statOffset: -12), RandomStreams.StableHash(speciesId) ^ worldSeed, 1)[0];
                var c = new CharacterState($"{speciesId}-1", name, speciesId, generated.Stats, FurnaceStats.Uniform(45), generated.Affinities, generated.Personality,
                    100, 1, $"lineage-{speciesId}", 1);
                c.Growth = GrowthProfile.Roll(GrowthType.Normal, balance, random);
                session.Roster.Add(c);
            }

            return session;
        }

        private SpeciesDefinition Find(string id)
        {
            foreach (var s in _species.GetAll())
            {
                if (s.Id == id) return s;
            }

            throw new InvalidOperationException($"系統 {id} がありません。");
        }
    }

    /// <summary>その日の大会と出走資格。</summary>
    public sealed class TournamentOffering
    {
        public TournamentDefinition Tournament { get; set; }
        public RaceCourse Course { get; set; }
        public bool Eligible { get; set; }
        public string Reason { get; set; }
    }

    /// <summary>出走準備の結果。</summary>
    public sealed class PreparedRace
    {
        public TournamentDefinition Tournament { get; set; }
        public RaceRequest Request { get; set; }
        public RiskReport Risk { get; set; }
        public string CharacterId { get; set; }
        public int PlayerIndex => 0;
    }

    /// <summary>レース後の結果（03 §8 Result）。</summary>
    public sealed class RaceDayOutcome
    {
        public TournamentDefinition Tournament { get; set; }
        public RacePlacing Placing { get; set; }
        public AftermathReport Aftermath { get; set; }
        public bool Won { get; set; }
        /// <summary>この勝利で新しいグレードに出られるようになった。</summary>
        public bool Promoted { get; set; }
        public bool Lost { get; set; }
        public ParentTree ParentTree { get; set; }
    }

    /// <summary>
    /// 大会の日（07 §6 EnterRace / StartRace / ResolveRace）。出走すると1日進む（02 §7）。
    /// </summary>
    public sealed class RaceDayUseCase
    {
        private readonly IBalanceRepository _balance;
        private readonly ICourseRepository _courses;
        private readonly ISpeciesRepository _species;
        private readonly ITournamentCatalogRepository _tournaments;
        private readonly ILineageCatalogRepository _lineage;

        public RaceDayUseCase(IBalanceRepository balance, ICourseRepository courses, ISpeciesRepository species, ITournamentCatalogRepository tournaments, ILineageCatalogRepository lineage)
        {
            _balance = balance ?? throw new ArgumentNullException(nameof(balance));
            _courses = courses ?? throw new ArgumentNullException(nameof(courses));
            _species = species ?? throw new ArgumentNullException(nameof(species));
            _tournaments = tournaments ?? throw new ArgumentNullException(nameof(tournaments));
            _lineage = lineage ?? throw new ArgumentNullException(nameof(lineage));
        }

        public TournamentCatalog Catalog => _tournaments.Load();

        public bool IsRaceDay(GameSession session) => TournamentSchedule.IsRaceDay(session.Date(_balance.Load()), Catalog);

        public int NextRaceDay(GameSession session) => TournamentSchedule.NextRaceDay(session.Day, _balance.Load().Calendar.DaysPerWeek, Catalog);

        /// <summary>day の大会（レースの日でなければ空）。カレンダー表示用に未来の日も指定できる。</summary>
        public List<TournamentOffering> Offerings(GameSession session, int day)
        {
            var balance = _balance.Load();
            var list = new List<TournamentOffering>();
            foreach (var t in TournamentSchedule.Offerings(new GameDate(day, balance.Calendar), balance.Calendar.DaysPerWeek, Catalog))
            {
                bool eligible = TournamentSchedule.IsEligible(t, session.TopGradeWon, out string reason);
                list.Add(new TournamentOffering { Tournament = t, Course = _courses.Get(t.CourseId), Eligible = eligible, Reason = reason });
            }

            return list;
        }

        public PreparedRace Prepare(GameSession session, string tournamentId, string characterId, AiStyle aiStyle, bool autoFurnace)
        {
            var balance = _balance.Load();
            if (!IsRaceDay(session)) throw new InvalidOperationException("今日はレースの日ではありません。");
            var tournament = Catalog.Get(tournamentId);
            if (!TournamentSchedule.IsEligible(tournament, session.TopGradeWon, out string reason)) throw new InvalidOperationException(reason);
            var character = session.Roster.Find(characterId);
            if (character == null || !character.IsActive) throw new InvalidOperationException($"現役の個体 {characterId} がいません。");

            var course = _courses.Get(tournament.CourseId);
            var conditions = session.TodayConditions(balance);
            var phase = character.PhaseOn(session.Day);
            var player = RaceEntrant.FromCharacter(character, true, aiStyle, autoFurnace, GrowthRules.RaceSpeedMultiplier(phase, balance));

            // NPC は大会Seed（世界Seed・日・大会）で決まる
            ulong tournamentSeed = session.WorldSeed ^ RandomStreams.SplitMix64((ulong)session.Day) ^ RandomStreams.StableHash(tournament.Id);
            var entrants = new List<RaceEntrant> { player };
            entrants.AddRange(NpcFactory.Generate(new NpcSpec(_species.GetAll(), tournament.NpcStatOffset), tournamentSeed, balance.Race.EntrantCount - 1, tournament.Id));

            ulong raceSeed = RandomStreams.SplitMix64(tournamentSeed ^ RandomStreams.StableHash(characterId));
            return new PreparedRace
            {
                Tournament = tournament,
                Request = new RaceRequest(course, entrants, conditions, raceSeed),
                Risk = RiskAssessment.Evaluate(course, conditions, player, balance),
                CharacterId = characterId,
            };
        }

        /// <summary>レース結果を反映し、1日進める。Lost / 勝利の処理もここで行う。</summary>
        public RaceDayOutcome Complete(GameSession session, PreparedRace race, RaceResult result)
        {
            var balance = _balance.Load();
            var placing = result.PlacingOf(race.PlayerIndex);
            var character = session.Roster.Find(race.CharacterId);
            var outcome = new RaceDayOutcome
            {
                Tournament = race.Tournament,
                Placing = placing,
                Aftermath = RaceAftermath.Apply(character, placing, race.Request.Course.Id, session.Day, balance),
                Won = placing.Rank == 1 && placing.Finished,
                Lost = placing.Lost,
            };

            if (outcome.Won)
            {
                int before = session.TopGradeWon;
                session.RecordWin(race.Tournament.Grade);
                outcome.Promoted = session.TopGradeWon > before;
            }

            if (placing.Lost)
            {
                session.Roster.MarkLost(character.Id, session.Day, RaceAftermath.LostReason(placing));
                // ロストしても親木には登録できる（後天因子は失われる: 05 §10）
                outcome.ParentTree = ParentTree.FromCharacter(character, _lineage.Load(), balance, session.Day);
                session.AddParentTree(outcome.ParentTree);
            }

            session.AdvanceDay(balance);
            return outcome;
        }
    }

    /// <summary>任意引退（02 §10）。引退すると親木に系統登録する。</summary>
    public sealed class RetireUseCase
    {
        private readonly IBalanceRepository _balance;
        private readonly ILineageCatalogRepository _lineage;

        public RetireUseCase(IBalanceRepository balance, ILineageCatalogRepository lineage)
        {
            _balance = balance;
            _lineage = lineage;
        }

        public ParentTree Retire(GameSession session, string characterId)
        {
            var entry = session.Roster.Retire(characterId, session.Day);
            var tree = ParentTree.FromCharacter(entry.Character, _lineage.Load(), _balance.Load(), session.Day);
            session.AddParentTree(tree);
            return tree;
        }
    }
}
