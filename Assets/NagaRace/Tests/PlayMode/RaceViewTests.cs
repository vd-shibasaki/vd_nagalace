using System;
using System.Collections;
using System.IO;
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

        /// <summary>
        /// 目視確認用のスクリーンショット。環境変数 NAGARACE_SCREENSHOT_DIR が設定されている場合だけ保存する。
        /// （-nographics では描画されないため、通常のテスト実行ではスキップ）
        /// </summary>
        [UnityTest]
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
                presenter.PlaybackSpeed = 1f;
                for (int i = 0; i < 120; i++) yield return null;
                Capture(presenter, Path.Combine(dir, $"{presenter.CourseIds[course]}_1_start.png"));

                presenter.PlaybackSpeed = 4f;
                while (presenter.Simulator.Racers[presenter.PlayerIndex].Progress < 0.45) yield return null;
                presenter.PlaybackSpeed = 1f;
                for (int i = 0; i < 20; i++) yield return null;
                Capture(presenter, Path.Combine(dir, $"{presenter.CourseIds[course]}_2_mid.png"));

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
