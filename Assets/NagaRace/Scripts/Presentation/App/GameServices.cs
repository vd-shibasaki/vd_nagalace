using System;
using System.Collections.Generic;
using NagaRace.Application.Analytics;
using NagaRace.Application.Game;
using NagaRace.Application.Lineage;
using NagaRace.Application.Race;
using NagaRace.Application.Save;
using NagaRace.Application.Training;
using NagaRace.Domain.Balance;
using NagaRace.Domain.Race;
using NagaRace.Domain.Training;
using NagaRace.Infrastructure.Analytics;
using NagaRace.Infrastructure.Config;
using NagaRace.Infrastructure.Data;
using NagaRace.Infrastructure.Save;
using UnityEngine;

namespace NagaRace.Presentation.App
{
    /// <summary>定義データ（JSON の TextAsset）一式。シーンの Bootstrap から渡す。</summary>
    [Serializable]
    public sealed class GameDataAssets
    {
        [SerializeField] private TextAsset _balance;
        [SerializeField] private TextAsset _species;
        [SerializeField] private TextAsset _trainings;
        [SerializeField] private TextAsset _factors;
        [SerializeField] private TextAsset _tournaments;
        [SerializeField] private TextAsset[] _courses;

        public TextAsset Balance => _balance;
        public TextAsset Species => _species;
        public TextAsset Trainings => _trainings;
        public TextAsset Factors => _factors;
        public TextAsset Tournaments => _tournaments;
        public IReadOnlyList<TextAsset> Courses => _courses;

        public bool IsComplete => _balance != null && _species != null && _trainings != null && _factors != null && _tournaments != null && _courses != null && _courses.Length > 0;
    }

    /// <summary>
    /// シーンをまたいで共有するサービスと進行中のゲーム（Main ⇔ Race）。
    /// Singleton は最小限にする方針（CLAUDE.md §4）のため、共有はこのクラスだけに集約する。
    /// </summary>
    public sealed class GameServices
    {
        private GameServices(GameDataAssets assets, string saveDirectory)
        {
            BalanceRepository = new JsonBalanceRepository(assets.Balance.text);
            Balance = BalanceRepository.Load();
            var courseTexts = new List<string>();
            foreach (var c in assets.Courses) courseTexts.Add(c.text);
            Courses = new JsonCourseRepository(courseTexts);
            Species = new JsonSpeciesRepository(assets.Species.text);
            Trainings = new JsonTrainingCatalogRepository(assets.Trainings.text);
            Lineage = new JsonLineageCatalogRepository(assets.Factors.text);
            Tournaments = new JsonTournamentCatalogRepository(assets.Tournaments.text);

            NewGame = new NewGameUseCase(BalanceRepository, Species);
            Training = new TrainingUseCase(BalanceRepository, Trainings);
            RaceDay = new RaceDayUseCase(BalanceRepository, Courses, Species, Tournaments, Lineage);
            RunRace = new RunRaceUseCase(BalanceRepository);
            Grafting = new GraftingUseCase(BalanceRepository, Species, Lineage);
            Retire = new RetireUseCase(BalanceRepository, Lineage);
            Save = new SaveUseCase(new JsonSaveRepository(saveDirectory));
            Analytics = new JsonlAnalyticsSink(System.IO.Path.Combine(saveDirectory, "logs", "events.jsonl"));
            AnalyticsPath = System.IO.Path.Combine(saveDirectory, "logs", "events.jsonl");
        }

        /// <summary>プレイログ（11 §8）。端末内のファイルにだけ記録する。</summary>
        public IAnalyticsSink Analytics { get; }
        public string AnalyticsPath { get; }

        /// <summary>プレイログを1件記録する。日付は自動で付ける。</summary>
        public void Log(string eventName, params (string key, object value)[] data)
        {
            var dictionary = new Dictionary<string, object>();
            if (Session != null) dictionary["day"] = Session.Day;
            foreach (var (key, value) in data) dictionary[key] = value;
            Analytics.Log(eventName, dictionary);
        }

        public static GameServices Instance { get; private set; }

        /// <summary>テスト用: セーブの保存先を差し替える（本物のセーブを上書きしない）。</summary>
        public static string SaveDirectoryOverride { get; set; }

        /// <summary>まだなければ作る。テストでは saveDirectory を一時フォルダにする。</summary>
        public static GameServices Ensure(GameDataAssets assets, string saveDirectory = null)
        {
            if (Instance != null) return Instance;
            if (assets == null || !assets.IsComplete) throw new InvalidOperationException("GameDataAssets が設定されていません。");
            Instance = new GameServices(assets, saveDirectory ?? SaveDirectoryOverride ?? System.IO.Path.Combine(UnityEngine.Application.persistentDataPath, "save"));
            return Instance;
        }

        /// <summary>テスト用。共有状態を破棄する。</summary>
        public static void ResetForTests()
        {
            Instance = null;
        }

        public IBalanceRepository BalanceRepository { get; }
        public BalanceConfig Balance { get; }
        public ICourseRepository Courses { get; }
        public ISpeciesRepository Species { get; }
        public ITrainingCatalogRepository Trainings { get; }
        public ILineageCatalogRepository Lineage { get; }
        public ITournamentCatalogRepository Tournaments { get; }

        public NewGameUseCase NewGame { get; }
        public TrainingUseCase Training { get; }
        public RaceDayUseCase RaceDay { get; }
        public RunRaceUseCase RunRace { get; }
        public GraftingUseCase Grafting { get; }
        public RetireUseCase Retire { get; }
        public SaveUseCase Save { get; }

        public GameSession Session { get; private set; }
        public GameSettings Settings { get; private set; } = new GameSettings();

        /// <summary>Race シーンへ渡す出走準備。</summary>
        public PreparedRace PendingRace { get; set; }
        /// <summary>Race シーンから戻ったときに Main で表示する結果。</summary>
        public RaceDayOutcome LastOutcome { get; set; }
        public RaceResult LastResult { get; set; }

        public void StartNewGame(ulong seed)
        {
            Session = NewGame.Start(seed);
            Settings = new GameSettings { SelectedCharacterId = Session.Roster.Active.Count > 0 ? Session.Roster.Active[0].Id : null };
            SaveNow();
        }

        public LoadResult Continue()
        {
            var result = Save.Load();
            if (result.Success)
            {
                Session = result.Session;
                Settings = result.Settings ?? new GameSettings();
            }

            return result;
        }

        /// <summary>保存契機: 育成後・レース後・接ぎ木後・引退後・設定変更後（07 §9）。</summary>
        public void SaveNow()
        {
            if (Session != null) Save.Save(Session, Settings);
        }
    }
}
