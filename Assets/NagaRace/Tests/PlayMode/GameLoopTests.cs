using System;
using System.Collections;
using System.IO;
using System.Linq;
using NagaRace.Domain.Characters;
using NagaRace.Domain.Race;
using NagaRace.Presentation.App;
using NagaRace.Presentation.Main;
using NagaRace.Presentation.Race;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace NagaRace.Tests.PlayMode
{
    /// <summary>
    /// 09 M8 完了条件「新規ゲームから20ターン以上遊べる」。画面の操作 API を通して通しプレイする。
    /// </summary>
    public class GameLoopTests
    {
        private string _saveDirectory;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _saveDirectory = Path.Combine(Path.GetTempPath(), "nagarace-loop-" + Guid.NewGuid().ToString("N"));
            GameServices.ResetForTests();
            GameServices.SaveDirectoryOverride = _saveDirectory;
            Time.captureDeltaTime = 1f / 30f;
            SceneManager.LoadScene("Main");
            yield return null;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.captureDeltaTime = 0f;
            GameServices.ResetForTests();
            GameServices.SaveDirectoryOverride = null;
            if (Directory.Exists(_saveDirectory)) Directory.Delete(_saveDirectory, true);
            yield return null;
        }

        private static MainUi Ui => Object.FindFirstObjectByType<AppBootstrap>().Ui;

        private static void CloseDialogs()
        {
            for (int i = 0; i < 5 && Ui.IsDialogOpen; i++) Ui.PressDialogOk();
        }

        [UnityTest]
        [Timeout(900000)]
        public IEnumerator M8_新規ゲームから20ターン以上遊べる()
        {
            Assert.IsInstanceOf<TitleScreen>(Ui.Current, "最初はタイトル画面");
            Ui.Get<TitleScreen>().NewGame();
            CloseDialogs();
            Assert.IsInstanceOf<HomeScreen>(Ui.Current);

            var services = GameServices.Instance;
            services.Settings.AutoFurnace = true; // テストでは炉を AI に任せる
            services.Settings.PlaybackSpeed = 4f;
            int startDay = services.Session.Day;
            int races = 0;
            int trainings = 0;

            for (int turn = 0; turn < 24; turn++)
            {
                var session = GameServices.Instance.Session;
                var character = Ui.SelectedCharacter;
                Assert.IsNotNull(character, $"{turn}ターン目: 現役の葉っぱがいない");

                var offering = GameServices.Instance.RaceDay.IsRaceDay(session)
                    ? GameServices.Instance.RaceDay.Offerings(session, session.Day).FirstOrDefault(o => o.Eligible)
                    : null;
                if (offering != null)
                {
                    yield return RunRace(offering.Tournament.Id);
                    races++;
                }
                else
                {
                    Train(character);
                    trainings++;
                }

                yield return null;
            }

            services = GameServices.Instance;
            TestContext.WriteLine($"{services.Session.Day - startDay}日経過 / レース {races}回 / 育成 {trainings}回 / 最高到達 {services.Session.TopGradeWon}");
            Assert.GreaterOrEqual(services.Session.Day - startDay, 20);
            Assert.GreaterOrEqual(races, 3, "レースの日にレースできていない");
            Assert.Greater(services.Session.Roster.Active.Concat(services.Session.Roster.Album.Select(a => a.Character)).Sum(c => c.Record.Starts), 0);

            // 保存 → 読込で状態が戻る
            int day = services.Session.Day;
            GameServices.ResetForTests();
            SceneManager.LoadScene("Main");
            yield return null;
            yield return null;
            Ui.Get<TitleScreen>().Continue();
            Assert.IsInstanceOf<HomeScreen>(Ui.Current);
            Assert.AreEqual(day, GameServices.Instance.Session.Day);

            // プレイログ（11 §8）が記録されている
            var log = File.ReadAllText(GameServices.Instance.AnalyticsPath);
            foreach (var name in new[] { "new_game", "tutorial_step", "training_choice", "race_start", "race_end", "session_start" })
            {
                StringAssert.Contains($"\"event\":\"{name}\"", log, $"プレイログに {name} がない");
            }
        }

        /// <summary>
        /// 迷わず遊べるか: ホームの「今日やること」と、各画面の一番目立つボタンだけで20日以上進められる。
        /// </summary>
        [UnityTest]
        [Timeout(900000)]
        public IEnumerator 今日やることボタンだけで20日以上遊べる()
        {
            Ui.Get<TitleScreen>().NewGame();
            CloseDialogs();
            var services = GameServices.Instance;
            services.Settings.AutoFurnace = true;
            int startDay = services.Session.Day;
            int races = 0;

            for (int step = 0; step < 40 && GameServices.Instance.Session.Day - startDay < 21; step++)
            {
                CloseDialogs();
                Assert.IsInstanceOf<HomeScreen>(Ui.Current, $"{step}手目: ホームにいない");
                var action = Ui.Get<HomeScreen>().CurrentAction;
                Assert.IsNotNull(action, "今日やることが出ていない");
                int day = GameServices.Instance.Session.Day;
                action.Run();
                CloseDialogs();

                if (Ui.Current is TrainingScreen training)
                {
                    // 最初からおすすめが選ばれているので、そのまま実行ボタン
                    Assert.IsNotNull(training.RecommendedId);
                    var t = GameServices.Instance.Trainings.Load().Get(training.RecommendedId);
                    var c = Ui.SelectedCharacter;
                    training.Choose(t, GameServices.Instance.Training.Preview(GameServices.Instance.Session, c.Id, t.Id));
                    CloseDialogs();
                    Ui.ShowRoot<HomeScreen>();
                }
                else if (Ui.Current is EntryScreen entry)
                {
                    entry.Enter();
                    CloseDialogs();
                    yield return FinishRaceAndReturn();
                    races++;
                }
                else if (!(Ui.Current is HomeScreen))
                {
                    Assert.Fail($"今日やることの行き先が想定外: {Ui.Current.GetType().Name}");
                }

                yield return null;
                Assert.Greater(GameServices.Instance.Session.Day, day, $"{step}手目: 今日やることを実行しても日が進まない（{action.Title}）");
            }

            Assert.GreaterOrEqual(GameServices.Instance.Session.Day - startDay, 20);
            Assert.GreaterOrEqual(races, 3, "今日やることからレースに出られていない");
        }

        /// <summary>目視確認用。NAGARACE_SCREENSHOT_DIR を設定し、描画ありで実行したときだけ撮る。</summary>
        [UnityTest]
        [Timeout(900000)]
        public IEnumerator 画面のスクリーンショットを保存する()
        {
            if (!ScreenshotUtil.Enabled) Assert.Ignore("NAGARACE_SCREENSHOT_DIR 未設定、またはグラフィックスなしのためスキップ");

            yield return null;
            ScreenshotUtil.Capture("ui_01_title");
            Ui.Get<TitleScreen>().NewGame();
            yield return null;
            ScreenshotUtil.Capture("ui_02_welcome");
            CloseDialogs();
            var services = GameServices.Instance;
            services.Settings.AutoFurnace = true;
            yield return null;
            ScreenshotUtil.Capture("ui_03_home");

            Ui.Show<TrainingScreen>();
            yield return null;
            ScreenshotUtil.Capture("ui_04_training");
            Ui.ShowRoot<HomeScreen>();

            // レースの日まで育成して、大会と出走登録を撮る
            while (!services.RaceDay.IsRaceDay(services.Session)) Train(Ui.SelectedCharacter);
            Ui.Show<CalendarScreen>();
            yield return null;
            ScreenshotUtil.Capture("ui_05_calendar");
            var offering = services.RaceDay.Offerings(services.Session, services.Session.Day).First(o => o.Eligible);
            Ui.Get<EntryScreen>().SetTournament(offering.Tournament.Id);
            Ui.Show<EntryScreen>();
            yield return null;
            ScreenshotUtil.Capture("ui_06_entry");
            Ui.ShowRoot<HomeScreen>();
            yield return RunRace(offering.Tournament.Id);
            yield return null;
            Ui.Get<TitleScreen>(); // 型参照のため
            Ui.Show<ResultScreen>(remember: false);
            yield return null;
            ScreenshotUtil.Capture("ui_07_result");

            Ui.ShowRoot<HomeScreen>();
            Ui.Show<CharacterDetailScreen>();
            yield return null;
            ScreenshotUtil.Capture("ui_08_detail");

            // 2体を引退させて親木園で接ぎ木
            var ids = services.Session.Roster.Active.Take(2).Select(c => c.Id).ToList();
            foreach (var id in ids) services.Retire.Retire(services.Session, id);
            Ui.ShowRoot<HomeScreen>();
            Ui.Show<ParentTreeScreen>();
            yield return null;
            var trees = services.Session.ParentTrees;
            var screen = Ui.Get<ParentTreeScreen>();
            screen.SelectForTests(trees[0].Id, trees[1].Id);
            yield return null;
            screen.GraftForTests();
            yield return null;
            ScreenshotUtil.Capture("ui_09_parent_tree");
        }

        private static void Train(CharacterState character)
        {
            var services = GameServices.Instance;
            Ui.Show<TrainingScreen>();
            string id = character.Fatigue > 60 ? "rest" : services.Trainings.Load().Trainings
                .Where(t => t.Id != "rest")
                .OrderByDescending(t => services.Training.Preview(services.Session, character.Id, t.Id).ExpectedGains.Values.Sum())
                .First().Id;
            var def = services.Trainings.Load().Get(id);
            Ui.Get<TrainingScreen>().Choose(def, services.Training.Preview(services.Session, character.Id, id));
            CloseDialogs();
            Ui.ShowRoot<HomeScreen>();
        }

        private static IEnumerator RunRace(string tournamentId)
        {
            Ui.Show<CalendarScreen>();
            var entry = Ui.Get<EntryScreen>();
            entry.SetTournament(tournamentId);
            Ui.Show<EntryScreen>();
            entry.Enter();
            CloseDialogs();

            yield return FinishRaceAndReturn();
        }

        /// <summary>シーン切り替えのワイプ演出が終わるまで待つ。</summary>
        private static IEnumerator WaitSceneTransition()
        {
            yield return null;
            for (int i = 0; i < 600 && SceneFader.IsBusy; i++) yield return null;
            Assert.IsFalse(SceneFader.IsBusy, "シーン切り替えの演出が終わらない");
            yield return null;
        }

        /// <summary>Race シーンでレースを最後まで進め、Main のリザルトからホームへ戻る。</summary>
        private static IEnumerator FinishRaceAndReturn()
        {
            yield return WaitSceneTransition();
            var presenter = Object.FindFirstObjectByType<RacePresenter>();
            Assert.IsNotNull(presenter, "Race シーンに移らない");
            Assert.IsTrue(presenter.IsGameRace);
            for (int i = 0; i < 3 && presenter.IsWaitingPanel; i++) presenter.ConfirmPanel();
            presenter.PlaybackSpeed = 20f;
            int frames = 0;
            while (!presenter.Simulator.IsFinished && frames++ < 6000) yield return null;
            Assert.IsTrue(presenter.Simulator.IsFinished, "レースが終わらない");
            for (int i = 0; i < 5; i++) yield return null;
            if (presenter.Hud.IsLostPanelVisible) presenter.ConfirmPanel();
            presenter.CompleteGameRace();

            // Main シーンへ戻ってリザルト
            yield return WaitSceneTransition();
            Assert.IsInstanceOf<ResultScreen>(Ui.Current, "レース後にリザルトが出ない");
            Ui.ShowRoot<HomeScreen>();
        }
    }
}
