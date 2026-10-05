using System;
using System.Collections;
using System.IO;
using System.Linq;
using NagaRace.Domain.Race;
using NagaRace.Presentation.Race;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace NagaRace.Tests.PlayMode
{
    /// <summary>
    /// 10 H-01「レース中、自キャラが常に識別できる」と M2 完了条件「自分のキャラがどれか・何位かが常に分かる」。
    /// </summary>
    public class RaceViewTests
    {
        private const int MaxFrames = 4000;

        [UnitySetUp]
        public IEnumerator LoadRaceScene()
        {
            // 実時間に依存させず、1フレーム = 1/30 秒として進める
            Time.captureDeltaTime = 1f / 30f;
            SceneManager.LoadScene("Race");
            yield return null;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator ResetTime()
        {
            Time.captureDeltaTime = 0f;
            yield return null;
        }

        [UnityTest]
        public IEnumerator H01_全コースで自キャラが常に画面内にいて順位が表示される()
        {
            var presenter = Object.FindFirstObjectByType<RacePresenter>();
            Assert.IsNotNull(presenter, "Race シーンに RacePresenter がありません");

            for (int course = 0; course < presenter.CourseIds.Count; course++)
            {
                presenter.StartRace(course, 3);
                presenter.ConfirmPanel();
                presenter.PlaybackSpeed = 4f;
                int frames = 0;
                int outOfView = 0;
                while (!presenter.Simulator.IsFinished && frames < MaxFrames)
                {
                    yield return null;
                    frames++;
                    if (!IsInView(presenter.RaceCamera, presenter.PlayerWorldPosition)) outOfView++;

                    int rank = presenter.Simulator.Racers[presenter.PlayerIndex].Rank;
                    StringAssert.Contains($"<b>{rank}</b>", presenter.Hud.RankLabel);
                }

                Assert.IsTrue(presenter.Simulator.IsFinished, $"{presenter.CourseIds[course]}: レースが終わりません");
                Assert.AreEqual(0, outOfView, $"{presenter.CourseIds[course]}: 自キャラが画面外に出たフレームがあります（{outOfView}/{frames}）");

                // ゴール後のカメラでも自キャラが映っている
                for (int i = 0; i < 30; i++) yield return null;
                Assert.IsTrue(IsInView(presenter.RaceCamera, presenter.PlayerWorldPosition), "ゴール後に自キャラが画面外");
            }
        }

        [UnityTest]
        public IEnumerator 全体カメラでも自キャラが画面内にいる()
        {
            var presenter = Object.FindFirstObjectByType<RacePresenter>();
            presenter.StartRace(0, 5);
            presenter.ConfirmPanel();
            presenter.PlaybackSpeed = 4f;
            presenter.CameraController.Mode = RaceCameraMode.WidePack;
            for (int i = 0; i < 30; i++) yield return null;

            int outOfView = 0;
            int frames = 0;
            while (!presenter.Simulator.Racers[presenter.PlayerIndex].IsFinished && frames < MaxFrames)
            {
                yield return null;
                frames++;
                if (!IsInView(presenter.RaceCamera, presenter.PlayerWorldPosition)) outOfView++;
            }

            Assert.AreEqual(0, outOfView, $"全体カメラで自キャラが画面外（{outOfView}/{frames}）");
        }

        [UnityTest]
        public IEnumerator H02_葉脈炉の状態がボタンの文字と色で切り替わる()
        {
            var presenter = Object.FindFirstObjectByType<RacePresenter>();
            presenter.StartRace(0, 7);
            presenter.ConfirmPanel();
            presenter.PlaybackSpeed = 4f;
            var sim = presenter.Simulator;
            var me = sim.Racers[presenter.PlayerIndex];

            // 始動できるまで待つ（葉力チャンスも取る）
            int frames = 0;
            while (!sim.CanActivateFurnace(presenter.PlayerIndex) && frames++ < MaxFrames)
            {
                if (me.IsChanceOpen) presenter.PressCharge();
                yield return null;
            }

            Assert.IsTrue(sim.CanActivateFurnace(presenter.PlayerIndex), "葉力が溜まらない");
            yield return null;
            StringAssert.Contains("始動", presenter.FurnaceHud.ButtonLabel);
            var readyColor = presenter.FurnaceHud.ButtonColor;

            presenter.PlaybackSpeed = 1f;
            presenter.PressFurnace();
            // 0.5 秒（ゲーム内時間）以内に表示が切り替わる
            for (int i = 0; i < 15; i++) yield return null;
            Assert.AreNotEqual(FurnacePhase.Ready, me.FurnacePhase);
            Assert.AreNotEqual(readyColor, presenter.FurnaceHud.ButtonColor);

            // RiskZone では「解除！」表示
            frames = 0;
            while (me.FurnacePhase != FurnacePhase.ActiveRisk && (me.IsFurnaceActive || me.FurnacePhase == FurnacePhase.Ignition) && frames++ < MaxFrames) yield return null;
            if (me.FurnacePhase == FurnacePhase.ActiveRisk)
            {
                yield return null;
                StringAssert.Contains("解除！", presenter.FurnaceHud.ButtonLabel);
            }

            presenter.PressFurnace();
            for (int i = 0; i < 3; i++) yield return null;
            Assert.IsFalse(me.IsFurnaceActive, "解除できない");
        }

        [UnityTest]
        public IEnumerator H03_Lostの可能性があるレースは危険度を確認してから始まる()
        {
            var presenter = Object.FindFirstObjectByType<RacePresenter>();
            int rapid = presenter.CourseIds.ToList().IndexOf("test_rapid");
            presenter.SetPlayerCondition(15, 80);
            presenter.StartRace(rapid, 1);
            Assert.IsTrue(presenter.IsWaitingRiskConfirm);
            Assert.AreEqual(RiskLevel.Extreme, presenter.Risk.Level);
            Assert.IsTrue(presenter.Hud.IsRiskPanelVisible);

            for (int i = 0; i < 30; i++) yield return null;
            Assert.AreEqual(0, presenter.Simulator.Tick, "確認前にレースが始まっている");

            presenter.PressFurnace(); // Space と同じ操作で出走を確定
            for (int i = 0; i < 30; i++) yield return null;
            Assert.Greater(presenter.Simulator.Tick, 0);
            Assert.IsFalse(presenter.Hud.IsRiskPanelVisible);

            // 安全なコースと十分な耐久なら確認なしで始まる（危険度は表示する）
            int straight = presenter.CourseIds.ToList().IndexOf("test_straight");
            presenter.SetPlayerCondition(100, 0);
            presenter.StartRace(straight, 1);
            Assert.IsFalse(presenter.IsWaitingRiskConfirm);
            Assert.IsFalse(presenter.Risk.LostPossible);
            Assert.IsTrue(presenter.Hud.IsRiskPanelVisible);
            for (int i = 0; i < 10; i++) yield return null;
            Assert.Greater(presenter.Simulator.Tick, 0);
        }

        [UnityTest]
        public IEnumerator おまかせ炉ならプレイヤーも炉を使う()
        {
            var presenter = Object.FindFirstObjectByType<RacePresenter>();
            presenter.AutoFurnace = true;
            presenter.StartRace(0, 9);
            presenter.ConfirmPanel();
            presenter.PlaybackSpeed = 8f;
            int frames = 0;
            while (!presenter.Simulator.IsFinished && frames++ < MaxFrames) yield return null;
            presenter.AutoFurnace = false;

            Assert.IsTrue(presenter.Simulator.Events.Any(e => e.EntrantIndex == presenter.PlayerIndex && e.Type == RaceEventType.FurnaceStart));
        }

        /// <summary>
        /// 目視確認用のスクリーンショット。環境変数 NAGARACE_SCREENSHOT_DIR が設定されている場合だけ保存する。
        /// （-nographics では描画されないため、通常のテスト実行ではスキップ）
        /// </summary>
        [UnityTest]
        [Timeout(900000)]
        public IEnumerator スクリーンショットを保存する()
        {
            string dir = Environment.GetEnvironmentVariable("NAGARACE_SCREENSHOT_DIR");
            if (string.IsNullOrEmpty(dir) || SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
            {
                Assert.Ignore("NAGARACE_SCREENSHOT_DIR 未設定、またはグラフィックスなしのためスキップ");
            }

            Directory.CreateDirectory(dir);
            var presenter = Object.FindFirstObjectByType<RacePresenter>();
            for (int course = 0; course < presenter.CourseIds.Count; course++)
            {
                presenter.StartRace(course, 3);
                yield return null;
                Capture(presenter, Path.Combine(dir, $"{presenter.CourseIds[course]}_0_risk.png"));
                presenter.ConfirmPanel();
                presenter.PlaybackSpeed = 1f;
                for (int i = 0; i < 120; i++) yield return null;
                Capture(presenter, Path.Combine(dir, $"{presenter.CourseIds[course]}_1_start.png"));

                presenter.PlaybackSpeed = 4f;
                while (presenter.Simulator.Racers[presenter.PlayerIndex].Progress < 0.45) yield return null;
                presenter.PlaybackSpeed = 1f;
                for (int i = 0; i < 20; i++) yield return null;
                Capture(presenter, Path.Combine(dir, $"{presenter.CourseIds[course]}_2_mid.png"));

                // 葉脈炉（始動できるまで進めて、RiskZone まで引っ張った瞬間を撮る）
                var me = presenter.Simulator.Racers[presenter.PlayerIndex];
                while (!presenter.Simulator.CanActivateFurnace(presenter.PlayerIndex) && !me.IsFinished)
                {
                    if (me.IsChanceOpen) presenter.PressCharge();
                    yield return null;
                }

                if (!me.IsFinished)
                {
                    presenter.PlaybackSpeed = 1f;
                    presenter.PressFurnace();
                    for (int i = 0; i < 40; i++) yield return null;
                    Capture(presenter, Path.Combine(dir, $"{presenter.CourseIds[course]}_2b_furnace.png"));
                    while (!me.IsOut && me.FurnacePhase != FurnacePhase.ActiveRisk && (me.IsFurnaceActive || me.FurnacePhase == FurnacePhase.Ignition)) yield return null;
                    for (int i = 0; i < 10; i++) yield return null;
                    Capture(presenter, Path.Combine(dir, $"{presenter.CourseIds[course]}_2c_overflow.png"));
                    presenter.PressFurnace();
                }

                presenter.CameraController.Mode = RaceCameraMode.WidePack;
                for (int i = 0; i < 60; i++) yield return null;
                Capture(presenter, Path.Combine(dir, $"{presenter.CourseIds[course]}_3_wide.png"));
                presenter.CameraController.Mode = RaceCameraMode.PlayerFollow;

                presenter.PlaybackSpeed = 8f;
                while (!presenter.Simulator.IsFinished) yield return null;
                for (int i = 0; i < 45; i++) yield return null;
                Capture(presenter, Path.Combine(dir, $"{presenter.CourseIds[course]}_4_result.png"));
            }
        }

        private static bool IsInView(Camera camera, Vector3 world)
        {
            var viewport = camera.WorldToViewportPoint(world);
            return viewport.z > 0f && viewport.x >= 0f && viewport.x <= 1f && viewport.y >= 0f && viewport.y <= 1f;
        }

        /// <summary>カメラと HUD を RenderTexture へ描いて PNG 保存する。</summary>
        private static void Capture(RacePresenter presenter, string path)
        {
            const int width = 1600;
            const int height = 900;
            var camera = presenter.RaceCamera;
            var canvas = presenter.Hud.Canvas;
            var rt = new RenderTexture(width, height, 24);
            var previousMode = canvas.renderMode;

            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1f;
            camera.targetTexture = rt;
            Canvas.ForceUpdateCanvases();
            camera.Render();

            var previousActive = RenderTexture.active;
            RenderTexture.active = rt;
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());

            RenderTexture.active = previousActive;
            camera.targetTexture = null;
            canvas.renderMode = previousMode;
            Object.Destroy(texture);
            rt.Release();
            Object.Destroy(rt);
        }
    }
}
