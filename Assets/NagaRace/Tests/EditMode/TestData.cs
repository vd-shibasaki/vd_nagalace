using System.Collections.Generic;
using System.IO;
using NagaRace.Domain.Balance;
using NagaRace.Domain.Characters;
using NagaRace.Domain.Race;
using NagaRace.Infrastructure.Config;
using NagaRace.Infrastructure.Data;

namespace NagaRace.Tests.EditMode
{
    /// <summary>
    /// テスト共通のデータ。バランス値は本番と同じ JSON を読む（09 M1・14 §5.4）。
    /// </summary>
    internal static class TestData
    {
        /// <summary>A-02 等の統計テストで使う標準コース。</summary>
        public const string StandardCourse = "test_branch";

        private const string Root = "Assets/NagaRace";
        private static BalanceConfig _balance;
        private static Dictionary<string, RaceCourse> _courses;
        private static List<SpeciesDefinition> _species;

        public static BalanceConfig Balance =>
            _balance ?? (_balance = JsonBalanceParser.Parse(File.ReadAllText(Path.Combine(Root, "Config/game_balance_defaults.json"))));

        public static IReadOnlyList<SpeciesDefinition> Species =>
            _species ?? (_species = JsonSpeciesParser.Parse(File.ReadAllText(Path.Combine(Root, "Data/Species/species.json"))));

        public static IEnumerable<string> CourseIds
        {
            get
            {
                LoadCourses();
                return _courses.Keys;
            }
        }

        public static RaceCourse Course(string id)
        {
            LoadCourses();
            return _courses[id];
        }

        /// <summary>全能力 value の8体。</summary>
        public static List<RaceEntrant> UniformEntrants(int value = 60, int count = 8)
        {
            var list = new List<RaceEntrant>(count);
            for (int i = 0; i < count; i++)
            {
                list.Add(new RaceEntrant($"e{i}", $"e{i}", "test", CharacterStats.Uniform(value)));
            }

            return list;
        }

        public static RaceResult Run(string courseId, IReadOnlyList<RaceEntrant> entrants, ulong seed, RaceConditions conditions = null)
        {
            return new RaceSimulator(Course(courseId), entrants, conditions ?? RaceConditions.Default, Balance, seed).RunToEnd();
        }

        private static void LoadCourses()
        {
            if (_courses != null) return;
            _courses = new Dictionary<string, RaceCourse>();
            foreach (var file in Directory.GetFiles(Path.Combine(Root, "Data/Courses"), "*.json"))
            {
                var course = JsonCourseParser.Parse(File.ReadAllText(file));
                _courses[course.Id] = course;
            }
        }
    }
}
