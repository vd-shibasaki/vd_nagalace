using System.Collections.Generic;
using NagaRace.Application.Race;
using NagaRace.Domain.Balance;
using NagaRace.Domain.Characters;
using NagaRace.Domain.Race;
using NagaRace.Infrastructure.Config;
using NagaRace.Infrastructure.Data;
using UnityEngine;

namespace NagaRace.Presentation.Race
{
    /// <summary>
    /// Race シーンの Presenter（07 §7）。
    /// RaceSimulator を固定Tickで進め、Domain の RaceEvent を View（HUD・VFX・カメラ）へ変換する。
    /// ゲームルールは持たない。
    /// </summary>
    public sealed class RacePresenter : MonoBehaviour
    {
        private static readonly Color[] RacerColors =
        {
            new Color(0.92f, 0.28f, 0.20f),
            new Color(0.96f, 0.80f, 0.22f),
            new Color(0.40f, 0.78f, 0.36f),
            new Color(0.96f, 0.62f, 0.78f),
            new Color(0.62f, 0.42f, 0.22f),
            new Color(0.48f, 0.32f, 0.62f),
            new Color(0.30f, 0.66f, 0.88f),
            new Color(0.88f, 0.88f, 0.88f),
        };

        [Header("データ")]
        [SerializeField] private TextAsset _balanceJson;
        [SerializeField] private TextAsset _speciesJson;
        [SerializeField] private TextAsset[] _courseJsons;
        [SerializeField] private int _courseIndex;
        [SerializeField] private int _seed = 1;

        [Header("表示")]
        [SerializeField] private float _playbackSpeed = 1f;
        [SerializeField] private Camera _camera;
        [SerializeField] private Font _font;
        [SerializeField] private Material _waterMaterial;
        [SerializeField] private Material _bankMaterial;
        [SerializeField] private Material _lineMaterial;
        [SerializeField] private Material _racerMaterial;
        [SerializeField] private Material _markerMaterial;
        [SerializeField] private Material _splashMaterial;
        [SerializeField] private RaceCourseGeometry.Settings _geometrySettings = new RaceCourseGeometry.Settings();
        [SerializeField] private bool _showDebugPanel = true;

        private RunRaceUseCase _useCase;
        private BalanceConfig _balance;
        private JsonCourseRepository _courses;
        private List<string> _courseIds;
        private IReadOnlyList<SpeciesDefinition> _species;

        private RaceSimulator _simulator;
        private RaceCourseGeometry _geometry;
        private Transform _riverRoot;
        private readonly List<RacerView> _views = new List<RacerView>();
        private Vector3[] _previousPositions;
        private Vector3[] _currentPositions;
        private Vector3[] _interpolated;
        private Vector3[] _previousForwards;
        private Vector3[] _currentForwards;
        private readonly List<Vector3> _packPositions = new List<Vector3>();
        private readonly List<RankingRow> _rows = new List<RankingRow>();
        private RaceHud _hud;
        private RaceVfxController _vfx;
        private RaceCameraController _cameraController;
        private double _accumulator;
        private int _processedEvents;
        private int _playerIndex;
        private bool _resultShown;
        private double _postFinishSeconds;

        public RaceSimulator Simulator => _simulator;
        public RaceHud Hud => _hud;
        public Camera RaceCamera => _camera;
        public int PlayerIndex => _playerIndex;
        public RaceCameraController CameraController => _cameraController;

        public float PlaybackSpeed
        {
            get => _playbackSpeed;
            set => _playbackSpeed = Mathf.Max(0f, value);
        }

        /// <summary>補間後の自キャラのワールド座標。</summary>
        public Vector3 PlayerWorldPosition => _interpolated != null ? _interpolated[_playerIndex] : Vector3.zero;

        private void Awake()
        {
            _balance = JsonBalanceParser.Parse(_balanceJson.text);
            _useCase = new RunRaceUseCase(new JsonBalanceRepository(_balanceJson.text));
            _species = new JsonSpeciesRepository(_speciesJson.text).GetAll();
            var texts = new List<string>();
            foreach (var asset in _courseJsons)
            {
                texts.Add(asset.text);
            }

            _courses = new JsonCourseRepository(texts);
            _courseIds = new List<string>(_courses.Ids);
            _courseIndex = Mathf.Clamp(_courseIndex, 0, _courseIds.Count - 1);

            _hud = new GameObject("RaceHud").AddComponent<RaceHud>();
            _hud.transform.SetParent(transform, false);
            _hud.Build(_font, "アカネ", _balance.Race.EntrantCount);
            _vfx = new GameObject("RaceVfx").AddComponent<RaceVfxController>();
            _vfx.transform.SetParent(transform, false);
            _vfx.Build(_splashMaterial);
            _cameraController = gameObject.AddComponent<RaceCameraController>();
            _cameraController.SetCamera(_camera);
        }

        private void Start()
        {
            StartRace();
        }

        public IReadOnlyList<string> CourseIds => _courseIds;

        /// <summary>コースと Seed を指定してレースを始め直す（デバッグ・テスト用）。</summary>
        public void StartRace(int courseIndex, int seed)
        {
            _courseIndex = Mathf.Clamp(courseIndex, 0, _courseIds.Count - 1);
            _seed = seed;
            StartRace();
        }

        public void StartRace()
        {
            var course = _courses.Get(_courseIds[_courseIndex]);
            var entrants = new List<RaceEntrant>
            {
                // TODO(M8): 出走登録画面（SC-08）から渡す。現在は固定の自キャラ
                new RaceEntrant("player", "アカネ", "maple", new CharacterStats(65, 70, 50, 50, 50, 60),
                    personality: Personality.Gambler, isPlayer: true),
            };
            entrants.AddRange(NpcFactory.Generate(new NpcSpec(_species), 2026UL, _balance.Race.EntrantCount - 1));

            _simulator = _useCase.Start(new RaceRequest(course, entrants, RaceConditions.Default, (ulong)_seed));
            _playerIndex = 0;
            _accumulator = 0;
            _processedEvents = 0;
            _resultShown = false;
            _postFinishSeconds = 0;
            _cameraController.Mode = RaceCameraMode.PlayerFollow;
            _hud.HideResult();

            BuildCourse(course);
            BuildRacers();
            UpdateHud();
            _cameraController.Snap(_currentPositions[_playerIndex], _currentForwards[_playerIndex]);
        }

        private void BuildCourse(RaceCourse course)
        {
            if (_riverRoot != null) Destroy(_riverRoot.gameObject);
            _geometry = new RaceCourseGeometry(course, _geometrySettings);
            _riverRoot = RiverBuilder.Build(_geometry, _waterMaterial, _bankMaterial, _lineMaterial);

            // ミニマップはコースごとに描き直す
            foreach (Transform child in _hud.MinimapRoot)
            {
                Destroy(child.gameObject);
            }

            var colors = new List<Color>();
            for (int i = 0; i < _balance.Race.EntrantCount; i++)
            {
                colors.Add(RacerColors[i % RacerColors.Length]);
            }

            _hud.SetMinimap(new MinimapView(_hud.MinimapRoot, _geometry, colors, _playerIndex));
        }

        private void BuildRacers()
        {
            foreach (var view in _views)
            {
                Destroy(view.gameObject);
            }

            _views.Clear();
            int count = _simulator.Racers.Count;
            _previousPositions = new Vector3[count];
            _currentPositions = new Vector3[count];
            _interpolated = new Vector3[count];
            _previousForwards = new Vector3[count];
            _currentForwards = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                var racer = _simulator.Racers[i];
                _views.Add(RacerView.Create($"Racer {i} {racer.Entrant.Name}", RacerColors[i % RacerColors.Length],
                    racer.Entrant.IsPlayer, _racerMaterial, _markerMaterial, i));
            }

            Capture(_previousPositions, _previousForwards);
            Capture(_currentPositions, _currentForwards);
            ApplyPoses(1f);
        }

        private void Update()
        {
            if (_simulator == null) return;

            _accumulator += Time.deltaTime * _playbackSpeed;
            while (_accumulator >= _simulator.DeltaTime && !_simulator.IsFinished)
            {
                _accumulator -= _simulator.DeltaTime;
                System.Array.Copy(_currentPositions, _previousPositions, _currentPositions.Length);
                System.Array.Copy(_currentForwards, _previousForwards, _currentForwards.Length);
                _simulator.Step();
                Capture(_currentPositions, _currentForwards);
                ProcessEvents();
            }

            float alpha = _simulator.IsFinished ? 1f : (float)(_accumulator / _simulator.DeltaTime);
            ApplyPoses(alpha);
            UpdateHud();
            UpdateCamera();

            if (_simulator.IsFinished)
            {
                // 全員ゴール後もゴール後の流れを見せるため、見た目の時間だけ進める
                _postFinishSeconds += Time.deltaTime * _playbackSpeed;
                Capture(_currentPositions, _currentForwards, _postFinishSeconds);
                ApplyPoses(1f);
                if (!_resultShown) ShowResult();
            }
        }

        private void Capture(Vector3[] positions, Vector3[] forwards, double extraSeconds = 0)
        {
            float racerWidth = (float)_balance.Lane.RacerWidthMeters;
            for (int i = 0; i < _simulator.Racers.Count; i++)
            {
                var racer = _simulator.Racers[i];
                double sinceFinish = racer.IsFinished ? _simulator.ElapsedSeconds + extraSeconds - racer.FinishTimeSeconds : 0;
                positions[i] = _geometry.RacerPosition(racer, racerWidth, sinceFinish);
                forwards[i] = _geometry.Tangent(racer.Progress);
            }
        }

        private void ApplyPoses(float alpha)
        {
            for (int i = 0; i < _views.Count; i++)
            {
                _interpolated[i] = Vector3.Lerp(_previousPositions[i], _currentPositions[i], alpha);
                _views[i].SetPose(_interpolated[i], Vector3.Slerp(_previousForwards[i], _currentForwards[i], alpha));
            }
        }

        private void UpdateCamera()
        {
            var player = _simulator.Racers[_playerIndex];
            if (player.IsFinished && _cameraController.Mode == RaceCameraMode.PlayerFollow)
            {
                _cameraController.Mode = RaceCameraMode.Finish;
            }

            _packPositions.Clear();
            for (int i = 0; i < _simulator.Racers.Count; i++)
            {
                if (!_simulator.Racers[i].IsFinished) _packPositions.Add(_interpolated[i]);
            }

            var goal = _simulator.Course.GoalSegment;
            var goalPosition = _geometry.Point(goal, goal.Length, 0f);
            // カメラはレース時間で追従させる（倍速再生でも自キャラを見失わないように）
            float cameraDelta = Time.deltaTime * Mathf.Max(1f, _playbackSpeed);
            _cameraController.UpdateCamera(_interpolated[_playerIndex], _currentForwards[_playerIndex], _packPositions, goalPosition, cameraDelta);
            if (_camera != null)
            {
                _hud.UpdatePlayerLabel(_camera.WorldToScreenPoint(_interpolated[_playerIndex] + Vector3.up * 2.5f));
            }
        }

        private void UpdateHud()
        {
            var player = _simulator.Racers[_playerIndex];
            double distance = _simulator.Course.Distance;
            float remaining = (float)((1.0 - player.Progress) * distance);
            _hud.UpdateStatus(player.Rank, _simulator.Racers.Count, SectionLabel(player.Segment), remaining,
                (float)player.CurrentSpeed, (float)(player.IsFinished ? player.FinishTimeSeconds : _simulator.ElapsedSeconds));

            _rows.Clear();
            var leader = _simulator.RacerAtRank(1);
            for (int rank = 1; rank <= _simulator.Racers.Count; rank++)
            {
                var r = _simulator.RacerAtRank(rank);
                string status;
                if (r.IsFinished) status = FormatTime(r.FinishTimeSeconds);
                else if (rank == 1) status = SectionLabel(r.Segment);
                else status = $"+{(leader.Progress - r.Progress) * distance:0}m";
                _rows.Add(new RankingRow { Name = r.Entrant.Name, Status = status, Color = RacerColors[r.Index % RacerColors.Length], IsPlayer = r.Entrant.IsPlayer });
            }

            _hud.UpdateRanking(_rows);
            _hud.Minimap?.UpdateDots(_interpolated);
        }

        private void ProcessEvents()
        {
            var events = _simulator.Events;
            for (; _processedEvents < events.Count; _processedEvents++)
            {
                var e = events[_processedEvents];
                bool isPlayer = e.EntrantIndex == _playerIndex;
                Vector3 position = e.EntrantIndex >= 0 ? _currentPositions[e.EntrantIndex] : Vector3.zero;
                switch (e.Type)
                {
                    case RaceEventType.GoodFlow:
                        if (isPlayer) _hud.ShowNotice("流れに乗った！", new Color(0.6f, 1f, 0.7f));
                        break;
                    case RaceEventType.Stumble:
                        _vfx.Splash(position, 18);
                        if (isPlayer) _hud.ShowNotice("姿勢を崩した…", new Color(1f, 0.75f, 0.5f));
                        break;
                    case RaceEventType.Contact:
                        _vfx.Splash(position, 14);
                        if (isPlayer || e.OtherIndex == _playerIndex) _hud.ShowNotice("接触！", new Color(1f, 0.55f, 0.45f));
                        break;
                    case RaceEventType.EnterSegment:
                        var segment = _simulator.Course.GetSegment(e.SegmentId);
                        if (segment.HasTag(SegmentTags.Waterfall) || segment.HasTag(SegmentTags.Rapid)) _vfx.Splash(position, 8);
                        if (isPlayer && e.Tick > 0 && IsShowPiece(segment)) _hud.ShowNotice($"{SectionLabel(segment)}！", Color.white);
                        break;
                    case RaceEventType.BranchChoice:
                        if (isPlayer) _hud.ShowNotice($"→ {SectionLabel(_simulator.Course.GetSegment(e.SegmentId))}へ", new Color(0.8f, 0.9f, 1f));
                        break;
                    case RaceEventType.Finish:
                        if (isPlayer) _hud.ShowNotice($"ゴール！ {_simulator.Racers[_playerIndex].Rank}着", new Color(1f, 0.9f, 0.3f));
                        break;
                }
            }
        }

        private void ShowResult()
        {
            _resultShown = true;
            var result = _simulator.BuildResult();
            var lines = new List<string> { "<size=40><b>RESULT</b></size>", $"{_simulator.Course.Name}  Seed {_seed}", "" };
            foreach (var placing in result.Placings)
            {
                var entrant = _simulator.Racers[placing.EntrantIndex].Entrant;
                string line = $"{placing.Rank}.  {entrant.Name}  {placing.FormatTime()}";
                lines.Add(entrant.IsPlayer ? $"<color=#ffd84a><b>{line}</b></color>" : line);
            }

            _hud.ShowResult(lines);
        }

        private void OnGUI()
        {
            if (!_showDebugPanel || _simulator == null) return;

            // 開発用の操作パネル（正式な操作 UI は M8）
            float x = 8f;
            float y = Screen.height - 46f;
            if (GUI.Button(new Rect(x, y, 120, 36), "同Seed再実行")) StartRace();
            if (GUI.Button(new Rect(x += 126, y, 100, 36), "次のSeed")) { _seed++; StartRace(); }
            if (GUI.Button(new Rect(x += 106, y, 110, 36), "コース切替"))
            {
                _courseIndex = (_courseIndex + 1) % _courseIds.Count;
                StartRace();
            }

            if (GUI.Button(new Rect(x += 116, y, 110, 36), $"カメラ:{CameraLabel()}"))
            {
                _cameraController.Mode = _cameraController.Mode == RaceCameraMode.PlayerFollow ? RaceCameraMode.WidePack : RaceCameraMode.PlayerFollow;
            }

            float[] speeds = { 1f, 2f, 4f, 8f };
            foreach (float speed in speeds)
            {
                if (GUI.Button(new Rect(x += (speed == 1f ? 116 : 56), y, 50, 36), $"x{speed}")) _playbackSpeed = speed;
            }

            GUI.Label(new Rect(8f, y - 24f, 600f, 22f), $"Seed {_seed} / {_simulator.Course.Id} / x{_playbackSpeed}");
        }

        private string CameraLabel()
        {
            switch (_cameraController.Mode)
            {
                case RaceCameraMode.WidePack: return "全体";
                case RaceCameraMode.Finish: return "ゴール";
                default: return "追従";
            }
        }

        private static bool IsShowPiece(FlowSegment segment)
        {
            return segment.HasTag(SegmentTags.Rapid) || segment.HasTag(SegmentTags.Waterfall)
                || segment.HasTag(SegmentTags.Whirlpool) || segment.HasTag(SegmentTags.Narrow);
        }

        /// <summary>Segment の表示名（タグから決める）。</summary>
        public static string SectionLabel(FlowSegment segment)
        {
            if (segment.HasTag(SegmentTags.Waterfall)) return "滝";
            if (segment.HasTag(SegmentTags.Whirlpool)) return "渦";
            if (segment.HasTag(SegmentTags.Shortcut)) return "近道";
            if (segment.HasTag(SegmentTags.Narrow)) return "狭い岩間";
            if (segment.HasTag(SegmentTags.Rapid)) return "激流";
            if (segment.HasTag(SegmentTags.Mud)) return "泥の流れ";
            if (segment.IsGoal) return "最終直線";
            return "穏やかな流れ";
        }

        private static string FormatTime(double seconds)
        {
            long ms = (long)System.Math.Round(seconds * 1000.0);
            return $"{ms / 60000:00}:{ms / 1000 % 60:00}.{ms % 1000:000}";
        }
    }
}
