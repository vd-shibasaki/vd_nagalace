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

        public static void Capture(string name, int width = 1600, int height = 900)
        {
            if (!Enabled) return;
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
            Canvas.ForceUpdateCanvases();
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
            Object.Destroy(texture);
            rt.Release();
            Object.Destroy(rt);
        }
    }
}
