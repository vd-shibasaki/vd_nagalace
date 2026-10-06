using System.Collections.Generic;
using NagaRace.Presentation.App;
using NagaRace.Presentation.Race;
using UnityEngine;

namespace NagaRace.Presentation.Main
{
    /// <summary>
    /// Main シーンの起動（SC-01 Boot）。サービスを用意し、画面を組み立てて最初の画面を出す。
    /// </summary>
    public sealed class AppBootstrap : MonoBehaviour
    {
        [SerializeField] private GameDataAssets _assets = new GameDataAssets();
        [SerializeField] private Font _font;
        [SerializeField] private Material _groundMaterial;
        [SerializeField] private Material _waterMaterial;
        [SerializeField] private Material _racerMaterial;
        [SerializeField] private Material _markerMaterial;

        public MainUi Ui { get; private set; }
        public HomeStage Stage { get; private set; }

        private void Awake()
        {
            UiKit.Font = _font;
            var services = GameServices.Ensure(_assets);
            SessionTracker.Ensure();

            Ui = gameObject.AddComponent<MainUi>();
            Ui.Initialize(
                new TitleScreen(Ui),
                new HomeScreen(Ui),
                new CharactersScreen(Ui),
                new CharacterDetailScreen(Ui),
                new TrainingScreen(Ui),
                new CalendarScreen(Ui),
                new EntryScreen(Ui),
                new ResultScreen(Ui),
                new SettingsScreen(Ui),
                new ParentTreeScreen(Ui));

            Stage = new GameObject("HomeStage").AddComponent<HomeStage>();
            Stage.Build(_groundMaterial, _waterMaterial, _racerMaterial, _markerMaterial);

            if (services.LastOutcome != null && services.Session != null)
            {
                // Race シーンから戻ってきた
                Ui.ShowRoot<ResultScreen>();
                services.LastOutcome = null;
            }
            else if (services.Session != null)
            {
                Ui.ShowRoot<HomeScreen>();
            }
            else
            {
                Ui.ShowRoot<TitleScreen>();
            }
        }
    }

    /// <summary>
    /// ホームの3D背景（03 §2: メニュー表ではなく「川辺の小さな拠点」）。手持ちの葉っぱが池に浮かんでのんびりしている。
    /// </summary>
    public sealed class HomeStage : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private readonly List<RacerView> _leaves = new List<RacerView>();
        private readonly List<(float angle, float speed, float radius)> _paths = new List<(float, float, float)>();
        private Material _racer;
        private Material _marker;
        private string _signature;

        public void Build(Material ground, Material water, Material racer, Material marker)
        {
            _racer = racer;
            _marker = marker;
            var plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
            Destroy(plane.GetComponent<Collider>());
            plane.transform.SetParent(transform, false);
            plane.transform.localScale = new Vector3(8, 1, 8);
            plane.GetComponent<Renderer>().sharedMaterial = ground;
            SetColor(plane, new Color(0.42f, 0.66f, 0.36f));

            var pond = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(pond.GetComponent<Collider>());
            pond.transform.SetParent(transform, false);
            pond.transform.localPosition = new Vector3(4, 0.02f, 4);
            pond.transform.localScale = new Vector3(18, 0.02f, 12);
            pond.GetComponent<Renderer>().sharedMaterial = water;
            SetColor(pond, new Color(0.3f, 0.6f, 0.88f));

            var camera = Camera.main;
            if (camera != null)
            {
                camera.transform.position = new Vector3(4, 9, -9);
                camera.transform.LookAt(new Vector3(4, 0, 4));
            }
        }

        private void Update()
        {
            var services = GameServices.Instance;
            if (services?.Session == null) return;

            // 手持ちが変わったときだけ作り直す
            string signature = string.Join(",", System.Linq.Enumerable.Select(services.Session.Roster.Active, c => c.Id));
            if (signature == _signature)
            {
                Wander();
                return;
            }
            _signature = signature;
            foreach (var leaf in _leaves) Destroy(leaf.gameObject);
            _leaves.Clear();
            _paths.Clear();

            var active = services.Session.Roster.Active;
            for (int i = 0; i < active.Count; i++)
            {
                float angle = i * Mathf.PI * 2f / Mathf.Max(1, active.Count);
                var view = RacerView.Create(active[i].Name, NagaRace.Presentation.App.UiKit.SpeciesColor(active[i].SpeciesId), false, _racer, _marker, i);
                view.transform.SetParent(transform, false);
                view.SetPose(new Vector3(4 + Mathf.Cos(angle) * 4f, 0.1f, 4 + Mathf.Sin(angle) * 2.5f), new Vector3(-Mathf.Sin(angle), 0, Mathf.Cos(angle)));
                _leaves.Add(view);
                // 一枚ずつ違う速さ・半径で池をのんびり回る
                _paths.Add((angle, 0.12f + 0.05f * (i % 3), 0.7f + 0.15f * (i % 2)));
            }
        }

        /// <summary>葉っぱたちが池でのんびり過ごす（03 §2: 眠る・水に浮く・他キャラと話す などの代わり）。</summary>
        private void Wander()
        {
            for (int i = 0; i < _leaves.Count; i++)
            {
                var (angle, speed, radius) = _paths[i];
                angle += speed * Time.deltaTime * (i % 2 == 0 ? 1f : -1f);
                _paths[i] = (angle, speed, radius);
                var position = new Vector3(4 + Mathf.Cos(angle) * 4.5f * radius, 0.1f, 4 + Mathf.Sin(angle) * 2.8f * radius);
                var forward = new Vector3(-Mathf.Sin(angle), 0, Mathf.Cos(angle)) * (i % 2 == 0 ? 1f : -1f);
                _leaves[i].SetPose(position, forward);
            }
        }

        private static void SetColor(GameObject go, Color color)
        {
            var block = new MaterialPropertyBlock();
            block.SetColor(BaseColorId, color);
            go.GetComponent<Renderer>().SetPropertyBlock(block);
        }
    }
}
