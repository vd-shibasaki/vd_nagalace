using System.Collections.Generic;
using System.IO;
using System.Linq;
using NagaRace.Presentation.Debug;
using NagaRace.Presentation.Race;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NagaRace.EditorTools
{
    /// <summary>
    /// 仮アートのマテリアルと、Race / RaceDebug シーンを生成する。手作業のシーン編集を減らし、作り直しを可能にするため。
    /// メニュー: NagaRace / Setup / ...
    /// バッチ: -executeMethod NagaRace.EditorTools.ProjectBootstrap.BatchSetup
    /// </summary>
    public static class ProjectBootstrap
    {
        private const string DebugScenePath = "Assets/NagaRace/Scenes/Debug/RaceDebug.unity";
        private const string RaceScenePath = "Assets/NagaRace/Scenes/Race.unity";
        private const string CourseFolder = "Assets/NagaRace/Data/Courses";
        private const string MaterialFolder = "Assets/NagaRace/Art/Materials";
        private const string BalancePath = "Assets/NagaRace/Config/game_balance_defaults.json";
        private const string SpeciesPath = "Assets/NagaRace/Data/Species/species.json";

        [MenuItem("NagaRace/Setup/Create All (Materials + Scenes)")]
        public static void CreateAll()
        {
            CreateMaterials();
            CreateRaceScene();
            CreateRaceDebugScene();
            UpdateBuildScenes();
        }

        [MenuItem("NagaRace/Setup/Create Race Debug Scene")]
        public static void CreateRaceDebugScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateLight();
            var camera = CreateCamera(new Color(0.55f, 0.75f, 0.60f));
            camera.transform.position = new Vector3(0f, 18f, -22f);

            var runner = new GameObject("RaceDebug").AddComponent<RaceDebugRunner>();
            var so = new SerializedObject(runner);
            so.FindProperty("_balanceJson").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TextAsset>(BalancePath);
            so.FindProperty("_speciesJson").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TextAsset>(SpeciesPath);
            so.FindProperty("_camera").objectReferenceValue = camera;
            AssignCourses(so.FindProperty("_courseJsons"));
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, DebugScenePath);
            UpdateBuildScenes();
            Debug.Log($"[NagaRace] デバッグシーンを作成しました: {DebugScenePath}");
        }

        [MenuItem("NagaRace/Setup/Create Race Scene")]
        public static void CreateRaceScene()
        {
            CreateMaterials();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateLight();
            var camera = CreateCamera(new Color(0.62f, 0.82f, 0.95f));
            camera.fieldOfView = 55f;

            var presenter = new GameObject("RacePresenter").AddComponent<RacePresenter>();
            var so = new SerializedObject(presenter);
            so.FindProperty("_balanceJson").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TextAsset>(BalancePath);
            so.FindProperty("_speciesJson").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TextAsset>(SpeciesPath);
            so.FindProperty("_camera").objectReferenceValue = camera;
            so.FindProperty("_font").objectReferenceValue = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            so.FindProperty("_waterMaterial").objectReferenceValue = LoadMaterial("Water");
            so.FindProperty("_bankMaterial").objectReferenceValue = LoadMaterial("Bank");
            so.FindProperty("_lineMaterial").objectReferenceValue = LoadMaterial("Line");
            so.FindProperty("_racerMaterial").objectReferenceValue = LoadMaterial("Racer");
            so.FindProperty("_markerMaterial").objectReferenceValue = LoadMaterial("Marker");
            so.FindProperty("_splashMaterial").objectReferenceValue = LoadMaterial("Splash");
            AssignCourses(so.FindProperty("_courseJsons"));
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, RaceScenePath);
            UpdateBuildScenes();
            Debug.Log($"[NagaRace] Race シーンを作成しました: {RaceScenePath}");
        }

        [MenuItem("NagaRace/Setup/Create Materials")]
        public static void CreateMaterials()
        {
            Directory.CreateDirectory(MaterialFolder);
            CreateMaterial("Water", "Universal Render Pipeline/Lit", Color.white, 0.85f);
            CreateMaterial("Bank", "Universal Render Pipeline/Lit", Color.white, 0.1f);
            CreateMaterial("Racer", "Universal Render Pipeline/Lit", Color.white, 0.35f);
            CreateMaterial("Line", "Universal Render Pipeline/Unlit", Color.white, 0f);
            CreateMaterial("Marker", "Universal Render Pipeline/Unlit", Color.white, 0f);
            CreateMaterial("Splash", "Universal Render Pipeline/Particles/Unlit", new Color(0.95f, 0.98f, 1f), 0f);

            // 水しぶきは丸い粒に見えるよう、組み込みの粒子テクスチャ + アルファクリップ
            var splash = LoadMaterial("Splash");
            if (splash != null && splash.GetTexture("_BaseMap") == null)
            {
                splash.SetTexture("_BaseMap", AssetDatabase.GetBuiltinExtraResource<Texture2D>("Default-Particle.psd"));
                splash.SetFloat("_AlphaClip", 1f);
                splash.SetFloat("_Cutoff", 0.35f);
                splash.EnableKeyword("_ALPHATEST_ON");
                EditorUtility.SetDirty(splash);
            }

            AssetDatabase.SaveAssets();
        }

        /// <summary>バッチモード用。マテリアルとシーンをすべて作り直す。</summary>
        public static void BatchSetup()
        {
            AssetDatabase.Refresh();
            CreateAll();
            AssetDatabase.SaveAssets();
        }

        private static void CreateMaterial(string name, string shaderName, Color color, float smoothness)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(path) != null) return;

            var shader = Shader.Find(shaderName);
            if (shader == null)
            {
                Debug.LogError($"[NagaRace] シェーダーが見つかりません: {shaderName}");
                return;
            }

            var material = new Material(shader) { name = name };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
            AssetDatabase.CreateAsset(material, path);
        }

        private static Material LoadMaterial(string name)
        {
            return AssetDatabase.LoadAssetAtPath<Material>($"{MaterialFolder}/{name}.mat");
        }

        private static void CreateLight()
        {
            var lightObject = new GameObject("Directional Light");
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            light.shadows = LightShadows.Soft;
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        private static Camera CreateCamera(Color background)
        {
            var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = background;
            camera.farClipPlane = 2000f;
            cameraObject.AddComponent<AudioListener>();
            return camera;
        }

        private static void AssignCourses(SerializedProperty property)
        {
            var courses = AssetDatabase.FindAssets("t:TextAsset", new[] { CourseFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => p.EndsWith(".json"))
                .OrderBy(p => p)
                .Select(AssetDatabase.LoadAssetAtPath<TextAsset>)
                .ToList();
            property.arraySize = courses.Count;
            for (int i = 0; i < courses.Count; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = courses[i];
            }
        }

        private static void UpdateBuildScenes()
        {
            var scenes = new List<EditorBuildSettingsScene>();
            foreach (var path in new[] { RaceScenePath, DebugScenePath })
            {
                if (File.Exists(path)) scenes.Add(new EditorBuildSettingsScene(path, true));
            }

            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
