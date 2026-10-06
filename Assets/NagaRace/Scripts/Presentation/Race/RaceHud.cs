using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace NagaRace.Presentation.Race
{
    /// <summary>ランキング1行分の表示データ。</summary>
    public struct RankingRow
    {
        public string Name;
        public string Status;
        public Color Color;
        public bool IsPlayer;
    }

    /// <summary>
    /// レース HUD（03 §7）。M2 の範囲は「自分のキャラがどれか・何位か」が常に分かること（H-01）。
    /// 葉力ゲージ・葉脈炉ボタン・臨界カウント・耐久は M3 / M4 で下部に追加する。
    /// </summary>
    public sealed class RaceHud : MonoBehaviour
    {
        private static readonly Color PlayerHighlight = new Color(0.88f, 0.46f, 0.1f);
        private static readonly Color PanelColor = new Color(1f, 1f, 1f, 0.9f);
        private static readonly Color Dark = new Color(0.18f, 0.24f, 0.21f);
        private static readonly Color Green = new Color(0.25f, 0.68f, 0.35f);

        private Font _font;
        private RectTransform _safeRoot;
        private Text _rankText;
        private Text _nameText;
        private Text _timeText;
        private Text _sectionText;
        private Text _speedText;
        private Text _playerLabel;
        private readonly List<Image> _rowBackgrounds = new List<Image>();
        private readonly List<Image> _rowSwatches = new List<Image>();
        private readonly List<Text> _rowTexts = new List<Text>();
        private readonly List<Text> _noticeTexts = new List<Text>();
        private readonly List<float> _noticeTimers = new List<float>();
        private GameObject _resultPanel;
        private Button _resultButton;
        private Text _resultButtonText;
        private GameObject _riskPanel;
        private Text _riskText;
        private Image _riskImage;
        private GameObject _lostPanel;
        private Text _lostText;
        private Text _resultText;
        private RectTransform _minimapRoot;
        private Canvas _canvas;
        private GameObject _commentaryPanel;
        private Image _commentaryBar;
        private Text _commentaryText;
        private Text _commentaryPrevious;
        private float _commentaryAge = -1f;

        public MinimapView Minimap { get; private set; }

        public Canvas Canvas => _canvas;

        /// <summary>セーフエリア内のルート（他の HUD 部品の親）。</summary>
        public RectTransform SafeRoot => _safeRoot;

        /// <summary>現在の順位表示（テスト・デバッグ用）。</summary>
        public string RankLabel => _rankText != null ? _rankText.text : string.Empty;

        public void Build(Font font, string playerName, int racerCount)
        {
            _font = font;

            var canvasObject = new GameObject("RaceHudCanvas", typeof(Canvas), typeof(CanvasScaler));
            canvasObject.transform.SetParent(transform, false);
            _canvas = canvasObject.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 10;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<App.AdaptiveCanvasScaler>();

            _safeRoot = UiFactory.Rect("SafeArea", canvasObject.transform, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            _safeRoot.anchorMin = Vector2.zero;
            _safeRoot.anchorMax = Vector2.one;
            _safeRoot.offsetMin = Vector2.zero;
            _safeRoot.offsetMax = Vector2.zero;
            _safeRoot.gameObject.AddComponent<SafeAreaFitter>();

            BuildTopLeft(playerName, racerCount);
            BuildTopRight();
            BuildNotices();
            BuildBottom();
            BuildCommentary();
            BuildPlayerLabel(playerName);
            BuildResultPanel();
            BuildRiskPanel();
            BuildLostPanel();
        }

        private Button _speedButton;
        private Text _speedButtonText;
        private Text _controls;

        /// <summary>再生速度ボタンが押された。</summary>
        public event System.Action SpeedPressed;

        /// <summary>本番のレースで使う、速度ボタンと操作の案内。</summary>
        public void ShowGameControls(float speed)
        {
            if (_speedButton == null)
            {
                var anchor = new Vector2(1f, 1f);
                var image = UiFactory.Panel("SpeedButton", _safeRoot, anchor, anchor, new Vector2(-24f, -350f), new Vector2(200f, 64f), Color.white);
                image.gameObject.AddComponent<Shadow>().effectColor = new Color(0f, 0.15f, 0.05f, 0.25f);
                image.gameObject.AddComponent<NagaRace.Presentation.App.TapTag>().Kind = NagaRace.Presentation.App.TapKind.Back;
                image.raycastTarget = true;
                _speedButton = image.gameObject.AddComponent<Button>();
                _speedButton.onClick.AddListener(() => SpeedPressed?.Invoke());
                _speedButtonText = UiFactory.Label("Label", image.transform, _font, 28, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(190f, 60f), Green);

                var bottomLeft = new Vector2(0f, 0f);
                _controls = UiFactory.Label("Controls", _safeRoot, _font, 24, TextAnchor.LowerLeft, bottomLeft, bottomLeft, new Vector2(24f, 20f), new Vector2(560f, 110f), new Color(1f, 1f, 1f, 0.9f));
                _controls.text = "<b>操作</b>\n葉脈炉の始動・解除: <b>Space</b> / 炉のボタン\n葉力チャンス: <b>F</b> / 緑の枠をタップ";
            }

            _speedButtonText.text = $"速度 x{speed:0}";
        }

        /// <summary>危険度パネル・ロスト画面のクリック。</summary>
        public event System.Action PanelClicked;

        public bool IsRiskPanelVisible => _riskPanel != null && _riskPanel.activeSelf;
        public bool IsLostPanelVisible => _lostPanel != null && _lostPanel.activeSelf;

        /// <summary>出走前の危険度（H-03）。needsConfirm なら「Space / クリックで出走」を表示。</summary>
        public void ShowRisk(string label, string reasons, Color color, bool needsConfirm)
        {
            _riskPanel.SetActive(true);
            _riskImage.color = new Color(1f, 1f, 1f, 0.97f);
            _riskText.text = $"<size=30>出走前の危険度</size>\n<size=64><b><color=#{ColorUtility.ToHtmlStringRGB(color)}>{label}</color></b></size>\n{reasons}"
                + (needsConfirm ? "\n\n<size=26>ロストの可能性があります。Space / クリックで出走</size>" : string.Empty);
        }

        /// <summary>説明パネル（初めてのレースのチュートリアル）。クリック / Space で閉じる。</summary>
        public void ShowInfo(string text)
        {
            _riskPanel.SetActive(true);
            _riskImage.color = new Color(0.94f, 0.98f, 0.94f, 0.97f);
            _riskText.text = text + "\n\n<size=24>Space / クリックで次へ</size>";
        }

        public void HideRisk()
        {
            _riskPanel.SetActive(false);
        }

        /// <summary>ロスト専用の画面（03 §9: 過度に悲惨にしない・静かな水面・戦績・スキップ可）。</summary>
        public void ShowLost(string text)
        {
            _resultPanel.SetActive(false);
            _lostPanel.SetActive(true);
            _lostText.text = text;
        }

        public void HideLost()
        {
            _lostPanel.SetActive(false);
        }

        private void BuildRiskPanel()
        {
            var center = new Vector2(0.5f, 0.5f);
            _riskImage = UiFactory.Panel("RiskPanel", _safeRoot, center, center, new Vector2(0f, 60f), new Vector2(760f, 330f), new Color(0f, 0f, 0f, 0.9f));
            _riskText = UiFactory.Label("RiskText", _riskImage.transform, _font, 32, TextAnchor.MiddleCenter, center, center, Vector2.zero, new Vector2(720f, 310f), Dark);
            _riskImage.gameObject.AddComponent<Shadow>().effectColor = new Color(0f, 0.15f, 0.05f, 0.3f);
            _riskPanel = _riskImage.gameObject;
            _riskImage.raycastTarget = true;
            _riskPanel.AddComponent<Button>().onClick.AddListener(() => PanelClicked?.Invoke());
            _riskPanel.SetActive(false);
        }

        private void BuildLostPanel()
        {
            // 画面全体を静かな水面色で覆う
            var panel = UiFactory.Panel("LostPanel", _canvas.transform, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, new Color(0.08f, 0.16f, 0.24f, 0.94f));
            panel.rectTransform.anchorMin = Vector2.zero;
            panel.rectTransform.anchorMax = Vector2.one;
            panel.rectTransform.offsetMin = Vector2.zero;
            panel.rectTransform.offsetMax = Vector2.zero;
            var center = new Vector2(0.5f, 0.5f);
            _lostText = UiFactory.Label("LostText", panel.transform, _font, 34, TextAnchor.MiddleCenter, center, center, Vector2.zero, new Vector2(1100f, 700f), new Color(0.9f, 0.95f, 1f));
            _lostPanel = panel.gameObject;
            panel.raycastTarget = true;
            _lostPanel.AddComponent<Button>().onClick.AddListener(() => PanelClicked?.Invoke());
            _lostPanel.SetActive(false);
        }

        public RectTransform MinimapRoot => _minimapRoot;

        public void SetMinimap(MinimapView minimap)
        {
            Minimap = minimap;
        }

        public void UpdateStatus(int playerRank, int racerCount, string sectionLabel, float remainingMeters, float speed, float elapsedSeconds)
        {
            _rankText.text = $"<size=96><b>{playerRank}</b></size><size=44>/{racerCount}</size>";
            _timeText.text = FormatTime(elapsedSeconds);
            _sectionText.text = $"{sectionLabel}\n残り <b>{Mathf.Max(0f, remainingMeters):0}</b> m";
            _speedText.text = $"{speed * 3.6f:0} km/h";
        }

        public void UpdateRanking(IReadOnlyList<RankingRow> rows)
        {
            for (int i = 0; i < _rowTexts.Count; i++)
            {
                bool visible = i < rows.Count;
                _rowBackgrounds[i].gameObject.SetActive(visible);
                if (!visible) continue;

                var row = rows[i];
                string statusColor = row.IsPlayer ? "#fff3e0" : "#6d7d76";
                _rowTexts[i].text = $"{i + 1}  {row.Name}  <color={statusColor}>{row.Status}</color>";
                _rowSwatches[i].color = row.Color;
                _rowBackgrounds[i].color = row.IsPlayer ? new Color(0.94f, 0.54f, 0.14f, 0.95f) : new Color(1f, 1f, 1f, 0.82f);
                _rowTexts[i].color = row.IsPlayer ? Color.white : Dark;
            }
        }

        /// <summary>現在の実況（テスト・デバッグ用）。</summary>
        public string CommentaryLabel => _commentaryText != null ? _commentaryText.text : string.Empty;

        /// <summary>左下の実況欄に1行出す。前の行は上に小さく残す。aboutPlayer なら左の帯をオレンジにする。</summary>
        public void ShowCommentary(string text, bool aboutPlayer)
        {
            _commentaryPrevious.text = _commentaryText.text;
            _commentaryText.text = text;
            _commentaryBar.color = aboutPlayer ? PlayerHighlight : Green;
            _commentaryPanel.SetActive(true);
            _commentaryAge = 0f;
        }

        public void ClearCommentary()
        {
            _commentaryText.text = string.Empty;
            _commentaryPrevious.text = string.Empty;
            _commentaryPanel.SetActive(false);
            _commentaryAge = -1f;
        }

        public void ClearNotices()
        {
            for (int i = 0; i < _noticeTexts.Count; i++)
            {
                _noticeTexts[i].text = string.Empty;
                _noticeTimers[i] = 0f;
            }
        }

        /// <summary>画面上部中央に短い通知を出す（流れに乗った・接触 など）。</summary>
        public void ShowNotice(string text, Color color)
        {
            // 古いものから押し出す
            for (int i = _noticeTexts.Count - 1; i > 0; i--)
            {
                _noticeTexts[i].text = _noticeTexts[i - 1].text;
                _noticeTexts[i].color = _noticeTexts[i - 1].color;
                _noticeTimers[i] = _noticeTimers[i - 1];
            }

            _noticeTexts[0].text = text;
            _noticeTexts[0].color = color;
            _noticeTimers[0] = 2.2f;
        }

        /// <summary>自キャラの頭上ラベル。screenPosition はスクリーン座標（z &lt; 0 なら非表示）。</summary>
        public void UpdatePlayerLabel(Vector3 screenPosition)
        {
            bool visible = screenPosition.z > 0f;
            _playerLabel.gameObject.SetActive(visible);
            if (!visible) return;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                (RectTransform)_canvas.transform, screenPosition, null, out var local);
            var rect = (RectTransform)_playerLabel.transform;
            rect.anchoredPosition = local + new Vector2(0f, 70f);
        }

        public void ShowResult(IReadOnlyList<string> lines, string buttonLabel = null, System.Action onButton = null)
        {
            _resultPanel.SetActive(true);
            _resultText.text = string.Join("\n", lines);
            if (buttonLabel == null) return;

            if (_resultButton == null)
            {
                var image = UiFactory.Panel("ResultButton", _resultPanel.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 24f), new Vector2(320f, 76f), Green);
                image.gameObject.AddComponent<Shadow>().effectColor = new Color(0f, 0.15f, 0.05f, 0.3f);
                image.gameObject.AddComponent<NagaRace.Presentation.App.TapTag>().Kind = NagaRace.Presentation.App.TapKind.Decide;
                image.raycastTarget = true;
                _resultButton = image.gameObject.AddComponent<Button>();
                _resultButtonText = UiFactory.Label("Label", image.transform, _font, 32, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(300f, 70f), Color.white);
            }

            _resultButtonText.text = buttonLabel;
            _resultButton.onClick.RemoveAllListeners();
            _resultButton.onClick.AddListener(() => onButton?.Invoke());
        }

        public void HideResult()
        {
            _resultPanel.SetActive(false);
        }

        private void Update()
        {
            // 新しい実況は少し下からふわっと出す
            if (_commentaryAge >= 0f)
            {
                _commentaryAge += Time.deltaTime;
                float t = Mathf.Clamp01(_commentaryAge / 0.25f);
                float e = 1f - (1f - t) * (1f - t);
                var c = _commentaryText.color;
                c.a = e;
                _commentaryText.color = c;
                _commentaryText.rectTransform.anchoredPosition = new Vector2(24f, -46f - 14f * (1f - e));
                if (t >= 1f) _commentaryAge = -1f;
            }

            for (int i = 0; i < _noticeTexts.Count; i++)
            {
                if (_noticeTimers[i] <= 0f) continue;
                _noticeTimers[i] -= Time.deltaTime;
                var c = _noticeTexts[i].color;
                c.a = Mathf.Clamp01(_noticeTimers[i] / 0.5f);
                _noticeTexts[i].color = c;
            }
        }

        private void BuildTopLeft(string playerName, int racerCount)
        {
            var anchor = new Vector2(0f, 1f);
            var panel = UiFactory.Panel("RankPanel", _safeRoot, anchor, anchor, new Vector2(24f, -24f), new Vector2(360f, 150f), PanelColor);
            _rankText = UiFactory.Label("Rank", panel.transform, _font, 44, TextAnchor.MiddleLeft, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, -10f), new Vector2(200f, 100f), Green);
            panel.gameObject.AddComponent<Shadow>().effectColor = new Color(0f, 0.15f, 0.05f, 0.2f);
            _nameText = UiFactory.Label("PlayerName", panel.transform, _font, 30, TextAnchor.MiddleLeft, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(170f, -24f), new Vector2(180f, 40f), PlayerHighlight);
            _nameText.text = $"<b>{playerName}</b>";
            _timeText = UiFactory.Label("Time", panel.transform, _font, 26, TextAnchor.MiddleLeft, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(170f, -70f), new Vector2(180f, 36f), Dark);

            for (int i = 0; i < racerCount; i++)
            {
                float y = -186f - i * 40f;
                var background = UiFactory.Panel($"Row{i}", _safeRoot, anchor, anchor, new Vector2(24f, y), new Vector2(360f, 36f), new Color(1f, 1f, 1f, 0.82f));
                var swatch = UiFactory.Panel("Swatch", background.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(6f, 0f), new Vector2(10f, 26f), Color.white);
                var text = UiFactory.Label("Text", background.transform, _font, 22, TextAnchor.MiddleLeft, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(26f, 0f), new Vector2(330f, 36f), Dark);
                _rowBackgrounds.Add(background);
                _rowSwatches.Add(swatch);
                _rowTexts.Add(text);
            }
        }

        private void BuildTopRight()
        {
            var anchor = new Vector2(1f, 1f);
            var panel = UiFactory.Panel("MinimapPanel", _safeRoot, anchor, anchor, new Vector2(-24f, -24f), new Vector2(420f, 230f), PanelColor);
            _minimapRoot = panel.rectTransform;
            _sectionText = UiFactory.Label("Section", _safeRoot, _font, 26, TextAnchor.UpperRight, anchor, anchor, new Vector2(-28f, -264f), new Vector2(420f, 80f), Color.white);
        }

        private void BuildNotices()
        {
            var anchor = new Vector2(0.5f, 1f);
            for (int i = 0; i < 3; i++)
            {
                var notice = UiFactory.Label($"Notice{i}", _safeRoot, _font, i == 0 ? 40 : 30, TextAnchor.MiddleCenter, anchor, anchor, new Vector2(0f, -60f - i * 48f), new Vector2(800f, 48f), Color.white);
                notice.text = string.Empty;
                _noticeTexts.Add(notice);
                _noticeTimers.Add(0f);
            }
        }

        private void BuildBottom()
        {
            var anchor = new Vector2(1f, 0f);
            _speedText = UiFactory.Label("Speed", _safeRoot, _font, 34, TextAnchor.LowerRight, anchor, anchor, new Vector2(-32f, 28f), new Vector2(300f, 50f), Color.white);
            // 葉力ゲージ・葉脈炉ボタンは FurnaceHud が下部中央に置く。TODO(M4): 耐久表示を追加
        }

        /// <summary>実況欄（左下。操作の案内の上、順位表の下の空いている所）。</summary>
        private void BuildCommentary()
        {
            var anchor = new Vector2(0f, 0f);
            var panel = UiFactory.Panel("Commentary", _safeRoot, anchor, anchor, new Vector2(24f, 140f), new Vector2(520f, 132f), PanelColor);
            panel.gameObject.AddComponent<Shadow>().effectColor = new Color(0f, 0.15f, 0.05f, 0.2f);
            _commentaryPanel = panel.gameObject;

            var top = new Vector2(0f, 1f);
            _commentaryBar = UiFactory.Panel("Bar", panel.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(6f, 0f), new Vector2(8f, 108f), Green);
            var tag = UiFactory.Panel("Tag", panel.transform, top, top, new Vector2(24f, -12f), new Vector2(84f, 30f), Green);
            UiFactory.Label("Label", tag.transform, _font, 19, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(84f, 30f), Color.white).text = "<b>実況</b>";
            _commentaryPrevious = UiFactory.Label("Previous", panel.transform, _font, 19, TextAnchor.MiddleLeft, top, top, new Vector2(120f, -12f), new Vector2(384f, 30f), new Color(0.43f, 0.49f, 0.46f));
            _commentaryPrevious.horizontalOverflow = HorizontalWrapMode.Wrap;
            _commentaryPrevious.verticalOverflow = VerticalWrapMode.Truncate;
            _commentaryText = UiFactory.Label("Text", panel.transform, _font, 27, TextAnchor.UpperLeft, top, top, new Vector2(24f, -46f), new Vector2(480f, 78f), Dark);
            _commentaryText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _commentaryText.verticalOverflow = VerticalWrapMode.Truncate;
            ClearCommentary();
        }

        private void BuildPlayerLabel(string playerName)
        {
            _playerLabel = UiFactory.Label("PlayerLabel", _canvas.transform, _font, 28, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(300f, 40f), PlayerHighlight);
            _playerLabel.text = $"▼ <b>{playerName}</b>";
        }

        private void BuildResultPanel()
        {
            var center = new Vector2(0.5f, 0.5f);
            var panel = UiFactory.Panel("ResultPanel", _safeRoot, center, center, Vector2.zero, new Vector2(760f, 600f), new Color(1f, 1f, 1f, 0.96f));
            panel.gameObject.AddComponent<Shadow>().effectColor = new Color(0f, 0.15f, 0.05f, 0.3f);
            _resultText = UiFactory.Label("ResultText", panel.transform, _font, 30, TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(40f, -30f), new Vector2(680f, 500f), Dark);
            _resultPanel = panel.gameObject;
            _resultPanel.SetActive(false);
        }

        private static string FormatTime(float seconds)
        {
            int ms = Mathf.FloorToInt(seconds * 1000f);
            return $"{ms / 60000:00}:{ms / 1000 % 60:00}.{ms % 1000:000}";
        }
    }
}
