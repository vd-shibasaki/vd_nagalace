using System.Collections.Generic;
using System.Text;
using NagaRace.Application.Race;
using NagaRace.Domain.Characters;
using NagaRace.Domain.Race;
using NagaRace.Infrastructure.Config;
using NagaRace.Infrastructure.Data;
using UnityEngine;

namespace NagaRace.Presentation.Debug
{
    /// <summary>
    /// M1 のデバッグ観戦。カプセルと板ポリで8体レースを表示する。
    /// ゲームルールは持たず、RaceSimulator を固定Tickで進めて描画するだけ（CLAUDE.md §4）。
    /// </summary>
    public sealed class RaceDebugRunner : MonoBehaviour
    {
        private static readonly Color[] RacerColors =
        {
            new Color(0.90f, 0.25f, 0.20f), // 自キャラ（モミジ色）
            new Color(0.95f, 0.80f, 0.20f),
            new Color(0.35f, 0.75f, 0.35f),
            new Color(0.95f, 0.60f, 0.75f),
            new Color(0.60f, 0.40f, 0.20f),
            new Color(0.45f, 0.30f, 0.60f),
            new Color(0.30f, 0.65f, 0.85f),
            new Color(0.85f, 0.85f, 0.85f),
        };

        [SerializeField] private TextAsset _balanceJson;
        [SerializeField] private TextAsset _speciesJson;
        [SerializeField] private TextAsset[] _courseJsons;
        [SerializeField] private int _courseIndex;
        [SerializeField] private int _seed = 1;
        [SerializeField] private float _playbackSpeed = 1f;
        [SerializeField] private float _lengthScale = 0.25f;
        [SerializeField] private Camera _camera;

        private RunRaceUseCase _useCase;
        private JsonCourseRepository _courses;
        private List<string> _courseIds;
        private IReadOnlyList<SpeciesDefinition> _species;
        private RaceSimulator _simulator;
        private CourseLayout _layout;
        private readonly List<Transform> _racerViews = new List<Transform>();
        private Vector3[] _previous;
        private Vector3[] _current;
        private Transform _courseRoot;
        private double _accumulator;
        private int _shownEventCount;
        private readonly List<string> _log = new List<string>();
        private GUIStyle _style;

        public int Seed => _seed;
        public float PlaybackSpeed => _playbackSpeed;

        private void Start()
        {
            var balanceRepository = new JsonBalanceRepository(_balanceJson.text);
            _useCase = new RunRaceUseCase(balanceRepository);
            _species = new JsonSpeciesRepository(_speciesJson.text).GetAll();

            var texts = new List<string>();
            foreach (var asset in _courseJsons)
            {
                texts.Add(asset.text);
            }

            _courses = new JsonCourseRepository(texts);
            _courseIds = new List<string>(_courses.Ids);
            _courseIndex = Mathf.Clamp(_courseIndex, 0, _courseIds.Count - 1);
            StartRace();
        }

        private void Update()
        {
            if (_simulator == null) return;

            _accumulator += Time.deltaTime * _playbackSpeed;
            while (_accumulator >= _simulator.DeltaTime && !_simulator.IsFinished)
            {
                _accumulator -= _simulator.DeltaTime;
                CapturePositions(_previous);
                _simulator.Step();
                CapturePositions(_current);
                CollectEvents();
            }

            float alpha = _simulator.IsFinished ? 1f : (float)(_accumulator / _simulator.DeltaTime);
            for (int i = 0; i < _racerViews.Count; i++)
            {
                _racerViews[i].position = Vector3.Lerp(_previous[i], _current[i], alpha);
            }

            FollowPlayer();
        }

        private void StartRace()
        {
            var course = _courses.Get(_courseIds[_courseIndex]);
            var entrants = new List<RaceEntrant>
            {
                new RaceEntrant("player", "アカネ", "maple", new CharacterStats(65, 70, 50, 50, 50, 60),
                    personality: Personality.Gambler, isPlayer: true),
            };
            // 大会Seedを固定し、レースSeedだけを変えて同じ顔ぶれで何度も走らせる
            entrants.AddRange(NpcFactory.Generate(new NpcSpec(_species), 2026UL, 7));

            _simulator = _useCase.Start(new RaceRequest(course, entrants, RaceConditions.Default, (ulong)_seed));
            _accumulator = 0;
            _shownEventCount = 0;
            _log.Clear();
            BuildCourseView(course);
            BuildRacerViews();
        }

        private void BuildCourseView(RaceCourse course)
        {
            if (_courseRoot != null) Destroy(_courseRoot.gameObject);
            _courseRoot = new GameObject("Course").transform;
            _layout = new CourseLayout(course, _lengthScale, 16f);

            foreach (var segment in course.Segments)
            {
                var strip = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Destroy(strip.GetComponent<Collider>());
                strip.name = $"Segment {segment.Id}";
                strip.transform.SetParent(_courseRoot);
                float zStart = (float)segment.ProgressStart * _layout.TotalLength;
                float zEnd = (float)segment.ProgressEnd * _layout.TotalLength;
                strip.transform.position = new Vector3(_layout.BranchOffset(segment), -0.1f, (zStart + zEnd) / 2f);
                strip.transform.localScale = new Vector3((float)segment.Width, 0.2f, zEnd - zStart);
                strip.GetComponent<Renderer>().material.color = SegmentColor(segment);
            }
        }

        private void BuildRacerViews()
        {
            foreach (var view in _racerViews)
            {
                Destroy(view.gameObject);
            }

            _racerViews.Clear();
            int count = _simulator.Racers.Count;
            _previous = new Vector3[count];
            _current = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                var racer = _simulator.Racers[i];
                var view = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                Destroy(view.GetComponent<Collider>());
                view.name = $"Racer {i} {racer.Entrant.Name}";
                view.transform.localScale = racer.Entrant.IsPlayer ? new Vector3(1.4f, 0.7f, 1.4f) : new Vector3(1f, 0.5f, 1f);
                view.GetComponent<Renderer>().material.color = RacerColors[i % RacerColors.Length];
                _racerViews.Add(view.transform);
            }

            CapturePositions(_previous);
            CapturePositions(_current);
        }

        private void CapturePositions(Vector3[] buffer)
        {
            for (int i = 0; i < _simulator.Racers.Count; i++)
            {
                var racer = _simulator.Racers[i];
                float halfWidth = Mathf.Max(0f, ((float)racer.Segment.Width - 1.2f) / 2f);
                buffer[i] = _layout.PositionOf(racer, halfWidth);
            }
        }

        private void FollowPlayer()
        {
            if (_camera == null) return;
            var target = _racerViews[0].position;
            var desired = target + new Vector3(0f, 18f, -22f);
            _camera.transform.position = Vector3.Lerp(_camera.transform.position, desired, 1f - Mathf.Exp(-4f * Time.deltaTime));
            _camera.transform.LookAt(target + new Vector3(0f, 0f, 10f));
        }

        private void CollectEvents()
        {
            var events = _simulator.Events;
            for (; _shownEventCount < events.Count; _shownEventCount++)
            {
                var e = events[_shownEventCount];
                string text = Describe(e);
                if (text == null) continue;
                _log.Add($"{e.Tick / (float)_simulator.TickRate:0.0}s {text}");
                if (_log.Count > 8) _log.RemoveAt(0);
            }
        }

        private string Describe(RaceEvent e)
        {
            string name = e.EntrantIndex >= 0 ? _simulator.Racers[e.EntrantIndex].Entrant.Name : "";
            switch (e.Type)
            {
                case RaceEventType.BranchChoice: return $"{name} → {e.SegmentId} を選択";
                case RaceEventType.Contact: return $"{name} が {_simulator.Racers[e.OtherIndex].Entrant.Name} に接触";
                case RaceEventType.Stumble: return $"{name} が姿勢を崩した";
                case RaceEventType.GoodFlow: return e.EntrantIndex == 0 ? $"{name} 流れに乗った！" : null;
                case RaceEventType.Finish: return $"{name} ゴール {e.Value:0.000}s";
                case RaceEventType.Timeout: return "タイムアウト";
                default: return null;
            }
        }

        private void OnGUI()
        {
            if (_simulator == null) return;
            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.label) { fontSize = 16 };
                _style.normal.textColor = Color.white;
            }

            var player = _simulator.Racers[0];
            var sb = new StringBuilder();
            sb.AppendLine($"<size=28><b>{player.Rank}/{_simulator.Racers.Count}</b></size>  {player.Entrant.Name}");
            sb.AppendLine($"コース: {_simulator.Course.Name}  Seed: {_seed}  時間: {_simulator.ElapsedSeconds:0.0}s  x{_playbackSpeed}");
            sb.AppendLine($"Segment: {player.Segment.Id}  速度: {player.CurrentSpeed:0.0} m/s  進捗: {player.Progress:P0}");
            sb.AppendLine();
            for (int rank = 1; rank <= _simulator.Racers.Count; rank++)
            {
                var r = _simulator.RacerAtRank(rank);
                string mark = r.Entrant.IsPlayer ? "▶" : "  ";
                string status = r.IsFinished ? $"{r.FinishTimeSeconds:0.000}s" : $"{r.Progress:P0} {r.Segment.Id}";
                sb.AppendLine($"{mark}{rank}. {r.Entrant.Name} ({r.Entrant.SpeciesId}/{r.Entrant.Personality}) {status}");
            }

            sb.AppendLine();
            foreach (var line in _log)
            {
                sb.AppendLine(line);
            }

            _style.richText = true;
            GUI.Box(new Rect(8, 8, 470, 440), GUIContent.none);
            GUI.Label(new Rect(16, 12, 460, 440), sb.ToString(), _style);

            float y = 456;
            if (GUI.Button(new Rect(8, y, 150, 32), "同Seedで再実行")) StartRace();
            if (GUI.Button(new Rect(164, y, 150, 32), "次のSeed")) { _seed++; StartRace(); }
            if (GUI.Button(new Rect(320, y, 158, 32), "コース切替"))
            {
                _courseIndex = (_courseIndex + 1) % _courseIds.Count;
                StartRace();
            }

            y += 38;
            float[] speeds = { 1f, 2f, 4f, 8f };
            for (int i = 0; i < speeds.Length; i++)
            {
                if (GUI.Button(new Rect(8 + i * 80, y, 74, 28), $"x{speeds[i]}")) _playbackSpeed = speeds[i];
            }
        }

        private static Color SegmentColor(FlowSegment segment)
        {
            if (segment.HasTag(SegmentTags.Waterfall)) return new Color(0.85f, 0.92f, 1f);
            if (segment.HasTag(SegmentTags.Whirlpool)) return new Color(0.35f, 0.30f, 0.65f);
            if (segment.HasTag(SegmentTags.Rapid)) return new Color(0.45f, 0.75f, 0.95f);
            if (segment.HasTag(SegmentTags.Narrow) || segment.HasTag(SegmentTags.Shortcut)) return new Color(0.40f, 0.55f, 0.50f);
            return new Color(0.25f, 0.50f, 0.80f);
        }
    }
}
