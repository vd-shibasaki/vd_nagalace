using System;
using NagaRace.Domain.Race;
using UnityEngine;
using UnityEngine.UI;

namespace NagaRace.Presentation.Race
{
    /// <summary>
    /// 葉力ゲージ・葉脈炉ボタン・臨界カウント・葉力チャンス（03 §7 下部）。
    /// H-02: 炉の状態は色と短い文字だけで 0.5 秒以内に判別できるようにする。
    /// 演出は画面中央を塞がない（03 §7「葉脈炉起動」）。
    /// </summary>
    public sealed class FurnaceHud : MonoBehaviour
    {
        private static readonly Color GaugeColor = new Color(0.3f, 0.75f, 0.4f);
        private static readonly Color ReadyColor = new Color(0.25f, 0.68f, 0.35f);
        private static readonly Color DisabledColor = new Color(0.62f, 0.67f, 0.65f);
        private static readonly Color Dark = new Color(0.18f, 0.24f, 0.21f);
        private static readonly Color IgnitionColor = new Color(1f, 0.85f, 0.25f);
        private static readonly Color SafeColor = new Color(1f, 0.6f, 0.15f);
        private static readonly Color RiskColor = new Color(0.95f, 0.15f, 0.12f);
        private static readonly Color CooldownColor = new Color(0.45f, 0.6f, 0.55f);
        private static readonly Color BrokenColor = new Color(0.35f, 0.12f, 0.1f);

        private Image _durabilityFill;
        private Text _durabilityText;
        private Image _gaugeFill;
        private Text _gaugeText;
        private Image _buttonImage;
        private Text _buttonText;
        private Text _countdownText;
        private Text _overflowText;
        private Text _autoBadge;
        private GameObject _chanceRoot;
        private Image _chanceFill;
        private Button _chanceButton;
        private float _gaugeWidth;

        public event Action FurnacePressed;
        public event Action ChargePressed;

        /// <summary>現在のボタン表示（テスト用）。</summary>
        public string ButtonLabel => _buttonText != null ? _buttonText.text : string.Empty;
        public Color ButtonColor => _buttonImage != null ? _buttonImage.color : Color.clear;

        public void Build(Transform safeRoot, Font font)
        {
            var bottom = new Vector2(0.5f, 0f);

            // 葉力ゲージ
            _gaugeWidth = 520f;
            var gaugeBack = UiFactory.Panel("LeafGauge", safeRoot, bottom, bottom, new Vector2(-150f, 40f), new Vector2(_gaugeWidth, 30f), new Color(1f, 1f, 1f, 0.9f));
            gaugeBack.gameObject.AddComponent<Shadow>().effectColor = new Color(0f, 0.15f, 0.05f, 0.25f);
            _gaugeFill = UiFactory.Panel("Fill", gaugeBack.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(0f, 30f), GaugeColor);
            // 始動ライン（葉力50）
            UiFactory.Panel("Threshold", gaugeBack.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(4f, 40f), new Color(0.88f, 0.46f, 0.1f));
            _gaugeText = UiFactory.Label("LeafLabel", gaugeBack.transform, font, 26, TextAnchor.MiddleLeft, new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(0f, 4f), new Vector2(300f, 34f), Color.white);

            // 耐久（03 §7 下部）
            var durabilityBack = UiFactory.Panel("Durability", safeRoot, bottom, bottom, new Vector2(-150f, 8f), new Vector2(_gaugeWidth, 14f), new Color(1f, 1f, 1f, 0.9f));
            _durabilityFill = UiFactory.Panel("Fill", durabilityBack.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(_gaugeWidth, 14f), new Color(0.95f, 0.4f, 0.4f));
            _durabilityText = UiFactory.Label("DurabilityLabel", durabilityBack.transform, font, 18, TextAnchor.MiddleLeft, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(6f, 0f), new Vector2(200f, 20f), Dark);

            // 葉脈炉ボタン（タップ / クリック）
            _buttonImage = UiFactory.Panel("FurnaceButton", safeRoot, bottom, bottom, new Vector2(240f, 24f), new Vector2(240f, 110f), DisabledColor);
            _buttonImage.raycastTarget = true;
            var button = _buttonImage.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => FurnacePressed?.Invoke());
            _buttonText = UiFactory.Label("Label", _buttonImage.transform, font, 34, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(230f, 100f), Color.white);
            _autoBadge = UiFactory.Label("Auto", _buttonImage.transform, font, 20, TextAnchor.LowerCenter, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 4f), new Vector2(230f, 26f), new Color(1f, 1f, 1f, 0.85f));

            // 臨界カウント（ボタンの上）
            _countdownText = UiFactory.Label("Countdown", safeRoot, font, 42, TextAnchor.LowerCenter, bottom, bottom, new Vector2(240f, 140f), new Vector2(360f, 60f), Color.white);
            _overflowText = UiFactory.Label("Overflow", safeRoot, font, 48, TextAnchor.LowerCenter, bottom, bottom, new Vector2(240f, 196f), new Vector2(420f, 60f), RiskColor);
            _overflowText.text = "<b>OVER FLOW</b>";

            // 葉力チャンス（ゲージの上。受付時間の残りをバーで表示し、中央に目印）
            _chanceRoot = UiFactory.Panel("Chance", safeRoot, bottom, bottom, new Vector2(-150f, 120f), new Vector2(_gaugeWidth, 70f), new Color(1f, 1f, 1f, 0.95f)).gameObject;
            _chanceRoot.AddComponent<Shadow>().effectColor = new Color(0f, 0.15f, 0.05f, 0.3f);
            var chanceOutline = _chanceRoot.AddComponent<Outline>();
            chanceOutline.effectColor = ReadyColor;
            chanceOutline.effectDistance = new Vector2(3f, -3f);
            _chanceRoot.AddComponent<NagaRace.Presentation.App.TapTag>().Kind = NagaRace.Presentation.App.TapKind.Decide;
            var chanceImage = _chanceRoot.GetComponent<Image>();
            chanceImage.raycastTarget = true;
            _chanceButton = _chanceRoot.AddComponent<Button>();
            _chanceButton.transition = Selectable.Transition.None;
            _chanceButton.onClick.AddListener(() => ChargePressed?.Invoke());
            UiFactory.Label("ChanceLabel", _chanceRoot.transform, font, 30, TextAnchor.MiddleCenter, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -4f), new Vector2(_gaugeWidth, 40f), ReadyColor).text = "<b>葉力チャンス！</b> タップ / F";
            var barBack = UiFactory.Panel("Window", _chanceRoot.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 8f), new Vector2(_gaugeWidth - 40f, 12f), new Color(0.88f, 0.91f, 0.89f));
            _chanceFill = UiFactory.Panel("Remaining", barBack.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(_gaugeWidth - 40f, 12f), GaugeColor);
            UiFactory.Panel("Center", barBack.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(6f, 22f), new Color(0.88f, 0.46f, 0.1f));
            _chanceRoot.SetActive(false);
        }

        public void Refresh(RacerState racer, bool canActivate, double minimumLeafPower, double chanceWindowSeconds)
        {
            float ratio = Mathf.Clamp01((float)(racer.LeafPower / 100.0));
            _gaugeFill.rectTransform.sizeDelta = new Vector2(_gaugeWidth * ratio, 30f);
            _gaugeFill.color = racer.LeafPower >= minimumLeafPower ? GaugeColor : GaugeColor * 0.7f;
            _gaugeText.text = $"葉力 <b>{racer.LeafPower:0}</b>";
            float durabilityRatio = Mathf.Clamp01((float)(racer.CurrentDurability / racer.Entrant.MaxDurability));
            _durabilityFill.rectTransform.sizeDelta = new Vector2(_gaugeWidth * durabilityRatio, 14f);
            _durabilityFill.color = durabilityRatio < 0.2f ? new Color(1f, 0.15f, 0.1f) : (durabilityRatio < 0.4f ? new Color(1f, 0.6f, 0.2f) : new Color(0.95f, 0.45f, 0.45f));
            _durabilityText.text = $"耐久 {racer.CurrentDurability:0}/{racer.Entrant.MaxDurability}";
            _autoBadge.text = racer.AutoControl ? "おまかせ中" : string.Empty;

            bool flash = Mathf.Repeat(Time.unscaledTime * 6f, 1f) < 0.5f;
            _overflowText.gameObject.SetActive(racer.FurnacePhase == FurnacePhase.ActiveRisk && flash);
            _countdownText.text = string.Empty;

            switch (racer.FurnacePhase)
            {
                case FurnacePhase.Ready:
                    SetButton(canActivate ? "葉脈炉\n<b>始動</b>" : "葉力不足", canActivate ? ReadyColor : DisabledColor);
                    break;
                case FurnacePhase.Ignition:
                    SetButton("点火…", IgnitionColor);
                    break;
                case FurnacePhase.ActiveSafe:
                    SetButton("<b>解除</b>", SafeColor);
                    _countdownText.text = $"臨界まで <b>{racer.SecondsToCritical:0.0}</b> 秒";
                    _countdownText.color = Color.white;
                    break;
                case FurnacePhase.ActiveRisk:
                    SetButton("<b>解除！</b>", flash ? RiskColor : RiskColor * 0.7f);
                    _countdownText.text = $"超過 <b>{-racer.SecondsToCritical:0.0}</b> 秒";
                    _countdownText.color = RiskColor;
                    break;
                case FurnacePhase.Cooldown:
                    SetButton($"冷却\n{racer.FurnaceCooldownRemaining:0.0}秒", CooldownColor);
                    break;
                case FurnacePhase.Broken:
                    SetButton("故障", BrokenColor);
                    break;
            }

            bool chanceOpen = racer.IsChanceOpen && !racer.IsFinished;
            _chanceRoot.SetActive(chanceOpen);
            if (chanceOpen)
            {
                float remaining = Mathf.Clamp01((float)(racer.ChanceRemainingSeconds / chanceWindowSeconds));
                _chanceFill.rectTransform.sizeDelta = new Vector2((_gaugeWidth - 40f) * remaining, 12f);
            }
        }

        private void SetButton(string label, Color color)
        {
            _buttonText.text = label;
            _buttonImage.color = color;
        }
    }
}
