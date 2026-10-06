using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace NagaRace.Tests.PlayMode
{
    /// <summary>目視確認用: メインカメラと全 Canvas を RenderTexture に描いて PNG 保存する。</summary>
    internal static class ScreenshotUtil
    {
        public static string Directory => System.Environment.GetEnvironmentVariable("NAGARACE_SCREENSHOT_DIR");

        public static bool Enabled =>
            !string.IsNullOrEmpty(Directory) && SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null;

        /// <summary>16:9 に加えて、狭い画面（4:3）と横長の画面（21:9）でも撮る（重なりの確認用）。</summary>
        public static void Capture(string name)
        {
            if (!Enabled) return;
            Capture(name, 1600, 900);
            Capture(name + "_4x3", 1440, 1080);
            Capture(name + "_21x9", 2100, 900);
            Rescale();
        }

        /// <summary>演出の途中で撮らないように終わらせる。</summary>
        private static void SkipTransitions()
        {
            foreach (var ui in Object.FindObjectsByType<NagaRace.Presentation.Main.MainUi>(FindObjectsSortMode.None)) ui.SkipTransitions();
        }

        /// <summary>CanvasScaler を今の描画先の大きさで計算し直す（OnEnable で再計算される）。</summary>
        public static void Rescale()
        {
            foreach (var adaptive in Object.FindObjectsByType<NagaRace.Presentation.App.AdaptiveCanvasScaler>(FindObjectsSortMode.None)) adaptive.Apply(force: true);
            foreach (var scaler in Object.FindObjectsByType<UnityEngine.UI.CanvasScaler>(FindObjectsSortMode.None))
            {
                scaler.enabled = false;
                scaler.enabled = true;
            }

            Canvas.ForceUpdateCanvases();
        }

        public static void Capture(string name, int width, int height)
        {
            if (!Enabled) return;
            SkipTransitions();
            System.IO.Directory.CreateDirectory(Directory);
            var camera = Camera.main;
            var rt = new RenderTexture(width, height, 24);
            var canvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            var modes = new List<(Canvas canvas, RenderMode mode, int order)>();
            foreach (var canvas in canvases)
            {
                if (!canvas.isRootCanvas) continue;
                modes.Add((canvas, canvas.renderMode, canvas.sortingOrder));
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1f + canvas.sortingOrder * -0.01f + 0.5f;
            }

            camera.targetTexture = rt;
            Rescale();
            camera.Render();
            var previous = RenderTexture.active;
            RenderTexture.active = rt;
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            texture.Apply();
            File.WriteAllBytes(Path.Combine(Directory, name + ".png"), texture.EncodeToPNG());
            RenderTexture.active = previous;
            camera.targetTexture = null;
            foreach (var (canvas, mode, _) in modes) canvas.renderMode = mode;
            Rescale();
            Object.Destroy(texture);
            rt.Release();
            Object.Destroy(rt);
        }
    }
}
