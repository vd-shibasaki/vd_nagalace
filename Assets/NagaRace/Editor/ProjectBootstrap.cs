using System.Collections.Generic;
using System.Linq;
using NagaRace.Presentation.Debug;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NagaRace.EditorTools
{
    /// <summary>
    /// M0/M1 のデバッグシーンを生成する。手作業のシーン編集を減らし、作り直しを可能にするため。
    /// メニュー: NagaRace / Setup / Create Race Debug Scene
    /// バッチ: -executeMethod NagaRace.EditorTools.ProjectBootstrap.BatchSetup
    /// </summary>
    public static class ProjectBootstrap
    {
        private const string ScenePath = "Assets/NagaRace/Scenes/Debug/RaceDebug.unity";
        private const string CourseFolder = "Assets/NagaRace/Data/Courses";

        [MenuItem("NagaRace/Setup/Create Race Debug Scene")]
        public static void CreateRaceDebugScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var lightObject = new GameObject("Directional Light");
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.55f, 0.75f, 0.60f);
            camera.farClipPlane = 2000f;
            cameraObject.transform.position = new Vector3(0f, 18f, -22f);

            var runnerObject = new GameObject("RaceDebug");
            var runner = runnerObject.AddComponent<RaceDebugRunner>();
            var so = new SerializedObject(runner);
            so.FindProperty("_balanceJson").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/NagaRace/Config/game_balance_defaults.json");
            so.FindProperty("_speciesJson").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/NagaRace/Data/Species/species.json");
            so.FindProperty("_camera").objectReferenceValue = camera;

            var courses = AssetDatabase.FindAssets("t:TextAsset", new[] { CourseFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => p.EndsWith(".json"))
                .OrderBy(p => p)
                .Select(AssetDatabase.LoadAssetAtPath<TextAsset>)
                .ToList();
            var courseProperty = so.FindProperty("_courseJsons");
            courseProperty.arraySize = courses.Count;
            for (int i = 0; i < courses.Count; i++)
            {
                courseProperty.GetArrayElementAtIndex(i).objectReferenceValue = courses[i];
            }

            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            UnityEngine.Debug.Log($"[NagaRace] デバッグシーンを作成しました: {ScenePath}（コース {courses.Count} 本）");
        }

        /// <summary>バッチモード用。シーン生成後に保存する。</summary>
        public static void BatchSetup()
        {
            AssetDatabase.Refresh();
            CreateRaceDebugScene();
            AssetDatabase.SaveAssets();
        }
    }
}
