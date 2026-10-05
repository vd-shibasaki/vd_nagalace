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
        private static readonly Color PlayerHighlight = new Color(1f, 0.85f, 0.1f);
        private static readonly Color PanelColor = new Color(0.05f, 0.12f, 0.18f, 0.62f);

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
        private Text _resultText;
        private RectTransform _minimapRoot;
        private Canvas _canvas;

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
            BuildPlayerLabel(playerName);
            BuildResultPanel();
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
                string statusColor = row.IsPlayer ? "#5a3a00" : "#cceeff";
                _rowTexts[i].text = $"{i + 1}  {row.Name}  <color={statusColor}>{row.Status}</color>";
                _rowSwatches[i].color = row.Color;
                _rowBackgrounds[i].color = row.IsPlayer ? new Color(1f, 0.78f, 0.1f, 0.85f) : new Color(0f, 0f, 0f, 0.35f);
                _rowTexts[i].color = row.IsPlayer ? new Color(0.15f, 0.1f, 0f) : Color.white;
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

        public void ShowResult(IReadOnlyList<string> lines)
        {
            _resultPanel.SetActive(true);
            _resultText.text = string.Join("\n", lines);
        }

        public void HideResult()
        {
            _resultPanel.SetActive(false);
        }

        private void Update()
        {
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
            _rankText = UiFactory.Label("Rank", panel.transform, _font, 44, TextAnchor.MiddleLeft, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, -10f), new Vector2(200f, 100f), Color.white);
            _nameText = UiFactory.Label("PlayerName", panel.transform, _font, 30, TextAnchor.MiddleLeft, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(170f, -24f), new Vector2(180f, 40f), PlayerHighlight);
            _nameText.text = $"<b>{playerName}</b>";
            _timeText = UiFactory.Label("Time", panel.transform, _font, 26, TextAnchor.MiddleLeft, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(170f, -70f), new Vector2(180f, 36f), Color.white);

            for (int i = 0; i < racerCount; i++)
            {
                float y = -186f - i * 40f;
                var background = UiFactory.Panel($"Row{i}", _safeRoot, anchor, anchor, new Vector2(24f, y), new Vector2(360f, 36f), new Color(0f, 0f, 0f, 0.35f));
                var swatch = UiFactory.Panel("Swatch", background.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(6f, 0f), new Vector2(10f, 26f), Color.white);
                var text = UiFactory.Label("Text", background.transform, _font, 22, TextAnchor.MiddleLeft, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(26f, 0f), new Vector2(330f, 36f), Color.white);
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

        private void BuildPlayerLabel(string playerName)
        {
            _playerLabel = UiFactory.Label("PlayerLabel", _canvas.transform, _font, 28, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(300f, 40f), PlayerHighlight);
            _playerLabel.text = $"▼ <b>{playerName}</b>";
        }

        private void BuildResultPanel()
        {
            var center = new Vector2(0.5f, 0.5f);
            var panel = UiFactory.Panel("ResultPanel", _safeRoot, center, center, Vector2.zero, new Vector2(760f, 560f), new Color(0.03f, 0.08f, 0.12f, 0.88f));
            _resultText = UiFactory.Label("ResultText", panel.transform, _font, 30, TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(40f, -30f), new Vector2(680f, 500f), Color.white);
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
