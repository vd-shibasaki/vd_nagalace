using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace NagaRace.EditorTools
{
    /// <summary>
    /// ビルド（09 M10「第三者へビルドを渡す」）。
    /// メニュー: NagaRace / Build / ...
    /// バッチ: -executeMethod NagaRace.EditorTools.BuildTools.BuildWindows / BuildAndroid
    /// </summary>
    public static class BuildTools
    {
        private const string OutputRoot = "Builds";

        [MenuItem("NagaRace/Build/Apply Player Settings")]
        public static void ApplyPlayerSettings()
        {
            // 横画面・16:9 基準（01 §4）
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.productName = "NagaRace";
            PlayerSettings.defaultScreenWidth = 1920;
            PlayerSettings.defaultScreenHeight = 1080;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = true;
            AssetDatabase.SaveAssets();
        }

        [MenuItem("NagaRace/Build/Windows (Tester Build)")]
        public static void BuildWindows()
        {
            Build(BuildTarget.StandaloneWindows64, BuildTargetGroup.Standalone, Path.Combine(OutputRoot, "Windows", "NagaRace.exe"));
        }

        [MenuItem("NagaRace/Build/Android (APK)")]
        public static void BuildAndroid()
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
            {
                const string message = "Android Build Support がインストールされていません。Unity Hub の「インストール」→ 6000.3.25f1 の ⚙ →「モジュールを加える」で Android Build Support（OpenJDK / SDK & NDK Tools を含む）を追加してください。";
                Debug.LogError("[NagaRace] " + message);
                if (Application.isBatchMode) EditorApplication.Exit(2);
                return;
            }

            Build(BuildTarget.Android, BuildTargetGroup.Android, Path.Combine(OutputRoot, "Android", "NagaRace.apk"));
        }

        private static void Build(BuildTarget target, BuildTargetGroup group, string path)
        {
            ApplyPlayerSettings();
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0 || !scenes[0].EndsWith("Main.unity", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("ビルドの先頭シーンが Main ではありません。NagaRace > Setup > Create All を実行してください。");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = path,
                target = target,
                targetGroup = group,
                options = BuildOptions.None,
            });

            var summary = report.summary;
            Debug.Log($"[NagaRace] Build {target}: {summary.result} / {summary.totalSize / (1024 * 1024)} MB / {summary.totalTime} / errors {summary.totalErrors} → {path}");
            if (Application.isBatchMode && summary.result != BuildResult.Succeeded) EditorApplication.Exit(1);
        }
    }
}
