using System;
using System.Collections.Generic;
using NagaRace.Domain.Training;

namespace NagaRace.Domain.Tournament
{
    /// <summary>大会の定義（12 Tournament）。</summary>
    public sealed class TournamentDefinition
    {
        public TournamentDefinition(string id, string name, int grade, string courseId, int npcStatOffset)
        {
            Id = id;
            Name = name;
            Grade = grade;
            CourseId = courseId;
            NpcStatOffset = npcStatOffset;
        }

        public string Id { get; }
        public string Name { get; }
        /// <summary>1〜5（5 = MOST HIGHER RIVER）。</summary>
        public int Grade { get; }
        public string CourseId { get; }
        /// <summary>NPC の強さ帯（系統の能力レンジに加算）。</summary>
        public int NpcStatOffset { get; }

        public string GradeLabel
        {
            get
            {
                switch (Grade)
                {
                    case 1: return "地方";
                    case 2: return "G2";
                    case 3: return "G3";
                    case 4: return "G4";
                    default: return "最高峰";
                }
            }
        }
    }

    /// <summary>大会一覧と開催スケジュール。</summary>
    public sealed class TournamentCatalog
    {
        private readonly Dictionary<string, TournamentDefinition> _byId = new Dictionary<string, TournamentDefinition>();

        public TournamentCatalog(IReadOnlyList<TournamentDefinition> tournaments, int raceDayOfWeek, IReadOnlyList<IReadOnlyList<string>> weekly)
        {
            Tournaments = tournaments;
            RaceDayOfWeek = raceDayOfWeek;
            Weekly = weekly;
            foreach (var t in tournaments) _byId[t.Id] = t;
        }

        public IReadOnlyList<TournamentDefinition> Tournaments { get; }
        public int RaceDayOfWeek { get; }
        public IReadOnlyList<IReadOnlyList<string>> Weekly { get; }

        public TournamentDefinition Get(string id)
        {
            if (!_byId.TryGetValue(id, out var t)) throw new KeyNotFoundException($"大会 '{id}' がありません。");
            return t;
        }
    }

    /// <summary>開催日・出走資格（03 §5 Race Calendar）。</summary>
    public static class TournamentSchedule
    {
        public static bool IsRaceDay(GameDate date, TournamentCatalog catalog)
        {
            return date.DayOfWeek == catalog.RaceDayOfWeek;
        }

        /// <summary>その日に開かれる大会（レースの日でなければ空）。</summary>
        public static IReadOnlyList<TournamentDefinition> Offerings(GameDate date, int daysPerWeek, TournamentCatalog catalog)
        {
            var list = new List<TournamentDefinition>();
            if (!IsRaceDay(date, catalog) || catalog.Weekly.Count == 0) return list;
            int weekIndex = (date.Day - 1) / Math.Max(1, daysPerWeek);
            foreach (var id in catalog.Weekly[weekIndex % catalog.Weekly.Count])
            {
                list.Add(catalog.Get(id));
            }

            return list;
        }

        /// <summary>today 以降で最初のレースの日。</summary>
        public static int NextRaceDay(int today, int daysPerWeek, TournamentCatalog catalog)
        {
            int dayOfWeek = (today - 1) % daysPerWeek + 1;
            int offset = (catalog.RaceDayOfWeek - dayOfWeek + daysPerWeek) % daysPerWeek;
            return today + offset;
        }

        /// <summary>出走資格。前のグレードで1勝していること。最高峰は Moon Mountain Masters 制覇まで閉じる。</summary>
        public static bool IsEligible(TournamentDefinition t, int topGradeWon, out string reason)
        {
            if (t.Grade <= 1 || topGradeWon >= t.Grade - 1)
            {
                reason = null;
                return true;
            }

            reason = t.Grade >= 5 ? "Moon Mountain Masters を制すると挑戦できる" : $"{GradeName(t.Grade - 1)}で1勝が必要";
            return false;
        }

        private static string GradeName(int grade)
        {
            switch (grade)
            {
                case 1: return "地方大会";
                case 2: return "G2";
                case 3: return "G3";
                default: return "G4";
            }
        }
    }
}
