using System.Collections.Generic;
using NagaRace.Application.Game;
using NagaRace.Application.Race;
using NagaRace.Presentation.App;
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
        [SerializeField] private bool _autoFurnace;
        [SerializeField] private AiStyle _playerAiStyle = AiStyle.Standard;
        [SerializeField] private int _playerDurability = 100;
        [SerializeField] private int _playerFatigue;
        [SerializeField] private float _riskBannerSeconds = 2.5f;

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
        private FurnaceHud _furnaceHud;
        private RaceInputController _input;
        private RiskReport _risk;
        private bool _waitingRiskConfirm;
        private float _riskBannerRemaining;
        private bool _lostShown;
        /// <summary>出走登録（Main シーン）から来たレース。null なら開発用のデバッグレース。</summary>
        private PreparedRace _gameRace;
        private bool _gameCompleted;
        private bool _waitingTutorial;
        private string _riskReasons;
        private readonly Dictionary<WhirlZone, GameObject> _whirlViews = new Dictionary<WhirlZone, GameObject>();
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
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
        public FurnaceHud FurnaceHud => _furnaceHud;
        public RiskReport Risk => _risk;
        public bool IsWaitingRiskConfirm => _waitingRiskConfirm;

        /// <summary>出走前の説明・危険度の確認待ち（どちらかが出ている）。</summary>
        public bool IsWaitingPanel => _waitingRiskConfirm || _waitingTutorial;

        /// <summary>デバッグ・テスト用: 次のレースの自キャラの耐久と疲労。</summary>
        public void SetPlayerCondition(int durability, int fatigue)
        {
            _playerDurability = durability;
            _playerFatigue = fatigue;
        }

        public bool AutoFurnace
        {
            get => _autoFurnace;
            set => _autoFurnace = value;
        }

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

            _gameRace = GameServices.Instance?.PendingRace;
            if (_gameRace != null)
            {
                _showDebugPanel = false;
                _playbackSpeed = GameServices.Instance.Settings.PlaybackSpeed;
            }

            _hud = new GameObject("RaceHud").AddComponent<RaceHud>();
            _hud.transform.SetParent(transform, false);
            _hud.Build(_font, _gameRace != null ? _gameRace.Request.Entrants[0].Name : "アカネ", _balance.Race.EntrantCount);
            _vfx = new GameObject("RaceVfx").AddComponent<RaceVfxController>();
            _vfx.transform.SetParent(transform, false);
            _vfx.Build(_splashMaterial);
            _cameraController = gameObject.AddComponent<RaceCameraController>();
            _cameraController.SetCamera(_camera);

            _furnaceHud = _hud.gameObject.AddComponent<FurnaceHud>();
            _furnaceHud.Build(_hud.SafeRoot, _font);
            _furnaceHud.FurnacePressed += PressFurnace;
            _furnaceHud.ChargePressed += PressCharge;
            _input = gameObject.AddComponent<RaceInputController>();
            _input.FurnacePressed += PressFurnace;
            _input.ChargePressed += PressCharge;
            _hud.PanelClicked += ConfirmPanel;
            _hud.SpeedPressed += CycleSpeed;
            if (_gameRace != null) _hud.ShowGameControls(_playbackSpeed);
        }

        private void ShowRiskPanel()
        {
            _waitingRiskConfirm = _risk.LostPossible;
            _riskBannerRemaining = _waitingRiskConfirm ? 0f : _riskBannerSeconds;
            _hud.ShowRisk(_risk.Label, _riskReasons, RiskColor(_risk.Level), _waitingRiskConfirm);
        }

        /// <summary>再生速度 x1 → x2 → x4。本番のレースでも見ているだけの時間を短くできる。</summary>
        public void CycleSpeed()
        {
            _playbackSpeed = _playbackSpeed >= 4f ? 1f : _playbackSpeed * 2f;
            if (_gameRace != null)
            {
                GameServices.Instance.Settings.PlaybackSpeed = _playbackSpeed;
                _hud.ShowGameControls(_playbackSpeed);
            }
        }

        /// <summary>説明・危険度の確認・ロスト画面のスキップ。</summary>
        public void ConfirmPanel()
        {
            if (_waitingTutorial)
            {
                _waitingTutorial = false;
                ShowRiskPanel();
            }
            else if (_waitingRiskConfirm)
            {
                _waitingRiskConfirm = false;
                _hud.HideRisk();
            }
            else if (_hud.IsLostPanelVisible)
            {
                // 「思い出アルバムへ」: MVP ではアルバム画面が Phase2 のため、通常のリザルトを表示する
                _hud.HideLost();
                ShowResult();
            }
        }

        /// <summary>葉脈炉ボタン。待機中なら始動、稼働中なら解除（「いつ始動し、いつ止めるか」だけを操作する）。</summary>
        public void PressFurnace()
        {
            if (_waitingRiskConfirm || _waitingTutorial || _hud.IsLostPanelVisible)
            {
                ConfirmPanel();
                return;
            }

            if (_gameRace != null && _resultShown && !_hud.IsLostPanelVisible)
            {
                CompleteGameRace();
                return;
            }

            if (_simulator == null || _simulator.IsFinished) return;
            var me = _simulator.Racers[_playerIndex];
            if (me.IsFinished) return;
            bool running = me.IsFurnaceActive || me.FurnacePhase == FurnacePhase.Ignition;
            _simulator.SubmitInput(_playerIndex, running ? RaceInputType.FurnaceRelease : RaceInputType.FurnaceActivate);
        }

        /// <summary>葉力チャンスのタップ。</summary>
        public void PressCharge()
        {
            if (_simulator == null || _simulator.IsFinished) return;
            _simulator.SubmitInput(_playerIndex, RaceInputType.Charge);
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

        public bool IsGameRace => _gameRace != null;

        public void StartRace()
        {
            RaceCourse course;
            List<RaceEntrant> entrants;
            RaceConditions conditions;
            ulong seed;
            if (_gameRace != null)
            {
                course = _gameRace.Request.Course;
                entrants = new List<RaceEntrant>(_gameRace.Request.Entrants);
                conditions = _gameRace.Request.Conditions;
                seed = _gameRace.Request.Seed;
            }
            else
            {
                // 開発用のデバッグレース（Race シーンを単独で開いたとき）
                course = _courses.Get(_courseIds[_courseIndex]);
                entrants = new List<RaceEntrant>
                {
                    new RaceEntrant("player", "アカネ", "maple", new CharacterStats(65, 70, 50, 50, 50, 60),
                        personality: Personality.Gambler, aiStyle: _playerAiStyle, furnace: new FurnaceStats(70, 60, 50, 50, 45),
                        isPlayer: true, durability: _playerDurability, autoFurnace: _autoFurnace, fatigue: _playerFatigue),
                };
                entrants.AddRange(NpcFactory.Generate(new NpcSpec(_species), 2026UL, _balance.Race.EntrantCount - 1));
                conditions = RaceConditions.Default;
                seed = (ulong)_seed;
            }

            _simulator = _useCase.Start(new RaceRequest(course, entrants, conditions, seed));
            _playerIndex = 0;
            _accumulator = 0;
            _processedEvents = 0;
            _resultShown = false;
            _lostShown = false;
            _postFinishSeconds = 0;
            _hud.HideLost();
            _hud.ClearNotices();

            // 出走前の危険度（H-03）。Lost の可能性があるレースは確認してから始める
            _risk = RiskAssessment.Evaluate(course, conditions, entrants[0], _balance);
            _riskReasons = _risk.Reasons.Count > 0 ? string.Join(" / ", _risk.Reasons) : "特に問題なし";
            string tutorial = _gameRace != null ? Tutorial.Consume(Tutorial.Race) : null;
            if (tutorial != null)
            {
                // 初めてのレース: 先に操作の説明、閉じたら危険度
                _waitingTutorial = true;
                _waitingRiskConfirm = false;
                _riskBannerRemaining = 0f;
                _hud.ShowInfo(tutorial);
            }
            else
            {
                _waitingTutorial = false;
                ShowRiskPanel();
            }
            _cameraController.Mode = RaceCameraMode.PlayerFollow;
            _hud.HideResult();

            BuildCourse(course);
            BuildRacers();
            UpdateHud();
            _cameraController.Snap(_currentPositions[_playerIndex], _currentForwards[_playerIndex]);
        }

        private void BuildCourse(RaceCourse course)
        {
            foreach (var view in _whirlViews.Values) Destroy(view);
            _whirlViews.Clear();
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

            if (_waitingRiskConfirm || _waitingTutorial)
            {
                UpdateHud();
                UpdateCamera();
                return;
            }

            if (_riskBannerRemaining > 0f)
            {
                _riskBannerRemaining -= Time.deltaTime;
                if (_riskBannerRemaining <= 0f) _hud.HideRisk();
            }

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
            SyncWhirlViews();
            UpdateHud();
            UpdateCamera();

            if (_simulator.IsFinished)
            {
                // 全員ゴール後もゴール後の流れを見せるため、見た目の時間だけ進める
                _postFinishSeconds += Time.deltaTime * _playbackSpeed;
                Capture(_currentPositions, _currentForwards, _postFinishSeconds);
                ApplyPoses(1f);
                var me = _simulator.Racers[_playerIndex];
                if (me.IsLost && !_lostShown)
                {
                    _lostShown = true;
                    _resultShown = true;
                    ShowLost(me);
                }
                else if (!_resultShown)
                {
                    ShowResult();
                }
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
            _furnaceHud.Refresh(player, _simulator.CanActivateFurnace(_playerIndex), _balance.LeafPower.FurnaceMinimum, _balance.LeafPower.ChanceWindowSeconds);
            for (int i = 0; i < _views.Count; i++)
            {
                _views[i].SetFurnacePhase(_simulator.Racers[i].FurnacePhase);
                _views[i].SetOut(_simulator.Racers[i].IsRetired);
            }

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
                if (r.IsLost) status = "LOST";
                else if (r.IsRetired) status = "リタイア";
                else if (r.IsFinished) status = FormatTime(r.FinishTimeSeconds);
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
                    case RaceEventType.FurnaceStart:
                        if (isPlayer)
                        {
                            _hud.ShowNotice("葉脈炉 始動！", new Color(1f, 0.9f, 0.4f));
                            _cameraController.Punch(0.6f);
                        }

                        break;
                    case RaceEventType.FurnaceOverflow:
                        if (isPlayer) _hud.ShowNotice("OVER FLOW!!", new Color(1f, 0.25f, 0.2f));
                        break;
                    case RaceEventType.FurnaceRelease:
                        if (isPlayer) _hud.ShowNotice($"解除（{e.Value:0.0}秒）", new Color(0.9f, 0.95f, 1f));
                        break;
                    case RaceEventType.FurnaceFailureOccurred:
                        _vfx.Splash(position, 30);
                        if (isPlayer) _hud.ShowNotice(FailureLabel((FurnaceFailure)(int)e.Value), new Color(1f, 0.35f, 0.3f));
                        break;
                    case RaceEventType.LeafChanceHit:
                        if (isPlayer) _hud.ShowNotice($"葉力 +{e.Value:0}", new Color(0.6f, 1f, 0.6f));
                        break;
                    case RaceEventType.LeafChanceMiss:
                        if (isPlayer) _hud.ShowNotice("チャンスを逃した", new Color(0.8f, 0.8f, 0.8f));
                        break;
                    case RaceEventType.SkillUsed:
                        var skill = (ObstructionSkill)(int)e.Value;
                        if (e.OtherIndex >= 0) _vfx.Splash(_currentPositions[e.OtherIndex], skill == ObstructionSkill.Ram ? 20 : 10);
                        if (e.OtherIndex == _playerIndex) _hud.ShowNotice($"{_simulator.Racers[e.EntrantIndex].Entrant.Name}の{SkillLabel(skill)}！", new Color(0.85f, 0.6f, 1f));
                        else if (isPlayer) _hud.ShowNotice($"{SkillLabel(skill)}！", new Color(0.85f, 0.6f, 1f));
                        break;
                    case RaceEventType.CourseAccident:
                        _vfx.Splash(position, 40);
                        if (isPlayer) _hud.ShowNotice("事故！", new Color(1f, 0.3f, 0.25f));
                        break;
                    case RaceEventType.Retire:
                        if (isPlayer && !_simulator.Racers[_playerIndex].IsLost) _hud.ShowNotice("リタイア…", new Color(0.8f, 0.8f, 0.85f));
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
            string subtitle = _gameRace != null ? $"{_gameRace.Tournament.Name}　{_simulator.Course.Name}" : $"{_simulator.Course.Name}  Seed {_seed}";
            var lines = new List<string> { "<size=40><b>RESULT</b></size>", subtitle, "" };
            foreach (var placing in result.Placings)
            {
                var entrant = _simulator.Racers[placing.EntrantIndex].Entrant;
                string line = $"{placing.Rank}.  {entrant.Name}  {placing.FormatTime()}";
                lines.Add(entrant.IsPlayer ? $"<color=#e0761a><b>{line}</b></color>" : line);
            }

            if (_gameRace != null)
            {
                lines.Add("");
                lines.Add("<size=26>Space / ボタンでホームへ</size>");
                _hud.ShowResult(lines, "ホームへ", CompleteGameRace);
            }
            else
            {
                _hud.ShowResult(lines);
            }
        }

        /// <summary>レース結果を個体へ反映して保存し、Main シーンへ戻る。</summary>
        public void CompleteGameRace()
        {
            if (_gameRace == null || _gameCompleted || _simulator == null || !_simulator.IsFinished) return;
            _gameCompleted = true;
            var services = GameServices.Instance;
            var result = _simulator.BuildResult();
            services.LastOutcome = services.RaceDay.Complete(services.Session, _gameRace, result);
            LogRaceEnd(services, result);
            services.LastResult = result;
            services.PendingRace = null;
            services.SaveNow();
            SceneFader.LoadScene("Main");
        }

        private void OnGUI()
        {
            if (!_showDebugPanel || _simulator == null) return;

            // 開発用の操作パネル（正式な操作 UI は M8）。左側のランキングの下に縦に並べる
            const float x = 8f;
            const float w = 200f;
            const float h = 30f;
            float y = Screen.height * 0.52f;
            GUI.Label(new Rect(x, y, 600f, 22f), $"Seed {_seed} / {_simulator.Course.Id} / x{_playbackSpeed}  [Space]炉 [F]チャンス");
            y += 24f;
            if (GUI.Button(new Rect(x, y, w / 2 - 2, h), "同Seed再実行")) StartRace();
            if (GUI.Button(new Rect(x + w / 2, y, w / 2, h), "次のSeed")) { _seed++; StartRace(); }
            y += h + 4f;
            if (GUI.Button(new Rect(x, y, w / 2 - 2, h), "コース切替"))
            {
                _courseIndex = (_courseIndex + 1) % _courseIds.Count;
                StartRace();
            }

            if (GUI.Button(new Rect(x + w / 2, y, w / 2, h), $"カメラ:{CameraLabel()}"))
            {
                _cameraController.Mode = _cameraController.Mode == RaceCameraMode.PlayerFollow ? RaceCameraMode.WidePack : RaceCameraMode.PlayerFollow;
            }

            y += h + 4f;
            float[] speeds = { 1f, 2f, 4f, 8f };
            for (int i = 0; i < speeds.Length; i++)
            {
                if (GUI.Button(new Rect(x + i * (w / 4), y, w / 4 - 2, h), $"x{speeds[i]}")) _playbackSpeed = speeds[i];
            }

            y += h + 4f;
            // おまかせ炉・AI作戦は次のレースから反映（レース中の切替はしない）
            if (GUI.Button(new Rect(x, y, w / 2 - 2, h), $"おまかせ:{(_autoFurnace ? "ON" : "OFF")}")) _autoFurnace = !_autoFurnace;
            if (GUI.Button(new Rect(x + w / 2, y, w / 2, h), $"作戦:{AiStyleLabel(_playerAiStyle)}"))
            {
                _playerAiStyle = (AiStyle)(((int)_playerAiStyle + 1) % 3);
            }

            y += h + 4f;
            if (GUI.Button(new Rect(x, y, w / 2 - 2, h), $"耐久:{_playerDurability}"))
            {
                _playerDurability = _playerDurability > 45 ? 45 : (_playerDurability > 15 ? 15 : 100);
            }

            if (GUI.Button(new Rect(x + w / 2, y, w / 2, h), $"疲労:{_playerFatigue}")) _playerFatigue = _playerFatigue > 0 ? 0 : 80;
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

        private void ShowLost(RacerState me)
        {
            var lines = new List<string>
            {
                $"<size=56>{me.Entrant.Name}は、</size>",
                "<size=56>流れの向こうへ旅立った。</size>",
                "",
                $"{_simulator.Course.Name}　{me.Progress:P0} 地点",
                $"最後の葉脈炉　{me.MaxFurnaceSeconds:0.0} 秒",
                "",
                "<size=26>思い出アルバムへ（クリック / Space）</size>",
            };
            _hud.ShowLost(string.Join("\n", lines));
        }

        private static Color RiskColor(RiskLevel level)
        {
            switch (level)
            {
                case RiskLevel.Safe: return new Color(0.45f, 0.95f, 0.55f);
                case RiskLevel.Caution: return new Color(1f, 0.85f, 0.3f);
                case RiskLevel.Danger: return new Color(1f, 0.45f, 0.25f);
                default: return new Color(1f, 0.15f, 0.2f);
            }
        }

        /// <summary>渦（妨害）の区間を水面の紫の円で見せる。</summary>
        private void SyncWhirlViews()
        {
            var zones = _simulator.WhirlZones;
            var expired = new List<WhirlZone>();
            foreach (var pair in _whirlViews)
            {
                bool alive = false;
                foreach (var z in zones)
                {
                    if (z == pair.Key) alive = true;
                }

                if (!alive) expired.Add(pair.Key);
            }

            foreach (var zone in expired)
            {
                Destroy(_whirlViews[zone]);
                _whirlViews.Remove(zone);
            }

            foreach (var zone in zones)
            {
                if (_whirlViews.ContainsKey(zone)) continue;
                var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                Destroy(disc.GetComponent<Collider>());
                disc.name = "Whirl";
                double middle = (zone.From + zone.To) / 2.0;
                disc.transform.position = _geometry.Point(zone.Segment, middle, 0f) + Vector3.up * 0.06f;
                float width = _geometry.WidthAt(zone.Segment, middle);
                disc.transform.localScale = new Vector3(width * 0.9f, 0.02f, (float)(zone.To - zone.From));
                disc.transform.rotation = Quaternion.LookRotation(_geometry.Tangent(zone.Segment.ProgressAt(middle)));
                var renderer = disc.GetComponent<Renderer>();
                renderer.sharedMaterial = _markerMaterial;
                var block = new MaterialPropertyBlock();
                block.SetColor(BaseColorId, new Color(0.55f, 0.35f, 0.85f));
                renderer.SetPropertyBlock(block);
                _whirlViews[zone] = disc;
            }
        }

        /// <summary>11 §8: レース結果・炉の始動タイミング・OVER FLOW・ロスト。</summary>
        private void LogRaceEnd(GameServices services, RaceResult result)
        {
            var placing = result.PlacingOf(_playerIndex);
            var starts = new List<double>();
            int overflows = 0;
            int failures = 0;
            foreach (var e in result.Events)
            {
                if (e.EntrantIndex != _playerIndex) continue;
                if (e.Type == RaceEventType.FurnaceStart) starts.Add(System.Math.Round(e.Tick / (double)_simulator.TickRate, 1));
                else if (e.Type == RaceEventType.FurnaceOverflow) overflows++;
                else if (e.Type == RaceEventType.FurnaceFailureOccurred) failures++;
            }

            services.Log("race_end", ("tournament", _gameRace.Tournament.Id), ("rank", placing.Rank), ("timeMs", placing.FinishTimeMilliseconds),
                ("retired", placing.Retired), ("lost", placing.Lost), ("furnaceStartSeconds", starts), ("overflows", overflows), ("failures", failures),
                ("maxFurnaceSeconds", System.Math.Round(placing.MaxFurnaceSeconds, 2)), ("autoFurnace", _simulator.Racers[_playerIndex].AutoControl));
            if (placing.Lost) services.Log("lost", ("character", _gameRace.CharacterId), ("tournament", _gameRace.Tournament.Id));
        }

        private static string SkillLabel(ObstructionSkill skill)
        {
            switch (skill)
            {
                case ObstructionSkill.Ram: return "体当たり";
                case ObstructionSkill.Mud: return "泥はね";
                case ObstructionSkill.Whirl: return "渦おこし";
                default: return "葉力妨害";
            }
        }

        private static string AiStyleLabel(AiStyle style)
        {
            switch (style)
            {
                case AiStyle.Safe: return "安全";
                case AiStyle.Aggressive: return "攻める";
                default: return "標準";
            }
        }

        private static string FailureLabel(FurnaceFailure failure)
        {
            switch (failure)
            {
                case FurnaceFailure.MinorDamage: return "炉が止まった…";
                case FurnaceFailure.FurnaceDamage: return "炉が壊れた！";
                case FurnaceFailure.Blow: return "ブロー！！";
                default: return "重大故障！！";
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
