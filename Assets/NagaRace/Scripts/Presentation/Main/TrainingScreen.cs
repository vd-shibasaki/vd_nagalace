using System.Collections.Generic;
using System.Linq;
using System.Text;
using NagaRace.Domain.Characters;
using NagaRace.Domain.Race;
using NagaRace.Domain.Training;
using NagaRace.Presentation.App;
using UnityEngine;
using UnityEngine.UI;

namespace NagaRace.Presentation.Main
{
    /// <summary>
    /// SC-06 Training（03 §4）。
    /// 1. カードを選ぶ（おすすめが最初から選ばれている）→ 2. 右のパネルで効果を確認 → 3.「このならしをする」で1日進む。
    /// 結果も同じ画面に出し、続けて育成するかホームへ戻るかを選べる。危険度が高い場合のみ確認ダイアログ。
    /// </summary>
    public sealed class TrainingScreen : UiScreen
    {
        private Text _status;
        private RectTransform _grid;
        private Text _detail;
        private Button _execute;
        private Button _again;
        private Button _home;
        private readonly Dictionary<string, Image> _cardImages = new Dictionary<string, Image>();
        private List<(TrainingDefinition def, TrainingPreview preview)> _previews = new List<(TrainingDefinition, TrainingPreview)>();
        private TrainingDefinition _selected;
        private string _recommendedId;
        private bool _showingResult;

        public TrainingScreen(MainUi ui) : base(ui)
        {
        }

        public override string Title => "育成（ならし）";

        /// <summary>おすすめのならし（テスト用）。</summary>
        public string RecommendedId => _recommendedId;

        protected override void OnBuild(RectTransform root)
        {
            var top = UiKit.CardBox("Status", root, UiKit.Card, 12);
            top.anchorMin = new Vector2(0, 1);
            top.anchorMax = new Vector2(1, 1);
            top.pivot = new Vector2(0.5f, 1);
            top.offsetMin = new Vector2(0, top.offsetMin.y);
            top.offsetMax = Vector2.zero;
            _status = UiKit.Label(top, string.Empty, 28);

            var gridRect = UiKit.Rect("Grid", root);
            gridRect.anchorMin = new Vector2(0, 0);
            gridRect.anchorMax = new Vector2(0.58f, 1);
            gridRect.offsetMin = new Vector2(0, 0);
            gridRect.offsetMax = new Vector2(-10, -90);
            var grid = gridRect.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(205, 190);
            grid.spacing = new Vector2(10, 10);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 5;
            _grid = gridRect;

            var detailPanel = UiKit.Panel("Detail", root, UiKit.Card);
            UiKit.AddShadow(detailPanel.gameObject, 5f, 0.14f);
            var detailRect = detailPanel.rectTransform;
            detailRect.anchorMin = new Vector2(0.58f, 0);
            detailRect.anchorMax = new Vector2(1, 1);
            detailRect.offsetMin = new Vector2(10, 0);
            detailRect.offsetMax = new Vector2(0, -90);
            _detail = UiKit.Text("DetailText", detailRect, string.Empty, 29, TextAnchor.UpperLeft);
            UiKit.Stretch(_detail.rectTransform, 26, 22, 26, 200);

            var buttons = UiKit.VStack("Buttons", detailRect, 10);
            buttons.anchorMin = new Vector2(0, 0);
            buttons.anchorMax = new Vector2(1, 0);
            buttons.pivot = new Vector2(0.5f, 0);
            buttons.offsetMin = new Vector2(26, 20);
            buttons.offsetMax = new Vector2(-26, 190);
            _execute = UiKit.Button(buttons, "このならしをする", ExecuteSelected, UiKit.Accent, 600, 96, 36);
            var row = UiKit.HStack("ResultButtons", buttons, 12, 84);
            row.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = true;
            _again = UiKit.Button(row, "もう1日育成する", () => { _showingResult = false; Rebuild(); }, UiKit.Accent, 300, 84, 30);
            _home = UiKit.Button(row, "ホームへ", () => Ui.ShowRoot<HomeScreen>(), UiKit.Muted, 240, 84, 30);
        }

        public override void OnShow()
        {
            _showingResult = false;
            _selected = null;
            Rebuild();
            Tutorial.ShowOnce(Ui, "training");
        }

        private void Rebuild()
        {
            UiKit.Clear(_grid);
            _cardImages.Clear();
            var c = Ui.SelectedCharacter;
            if (c == null)
            {
                _status.text = "現役の葉っぱがいません";
                _detail.text = string.Empty;
                SetMode(result: true);
                return;
            }

            var session = Services.Session;
            var weather = session.TodayConditions(Services.Balance).Weather;
            int daysToRace = Services.RaceDay.NextRaceDay(session) - session.Day;
            _status.text = $"<b>{c.Name}</b>　{GrowthProfile.Label(c.PhaseOn(session.Day))}期　疲労 <b>{c.Fatigue}</b>　耐久 {c.Durability}/{c.MaxDurability}　" +
                $"今日の天気 <b>{WeatherForecast.WeatherLabel(weather)}</b>　{(daysToRace == 0 ? "<color=#e0761a>今日はレースの日</color>" : $"レースまで {daysToRace}日")}";

            var catalog = Services.Trainings.Load();
            _previews = catalog.Trainings.Select(t => (def: t, preview: Services.Training.Preview(session, c.Id, t.Id))).ToList();
            _recommendedId = _previews.OrderByDescending(p => Score(p.def, p.preview, c)).First().def.Id;
            if (!_showingResult) _selected = catalog.Get(_selected?.Id ?? _recommendedId);

            foreach (var (def, preview) in _previews)
            {
                BuildCard(def, preview);
            }

            Highlight();
            if (!_showingResult) ShowDetail();
            SetMode(_showingResult);
        }

        private void BuildCard(TrainingDefinition def, TrainingPreview preview)
        {
            var card = UiKit.Panel($"Card {def.Id}", _grid, UiKit.Card);
            card.raycastTarget = true;
            UiKit.AddShadow(card.gameObject, 4f, 0.14f);
            var outline = card.gameObject.AddComponent<Outline>();
            outline.effectColor = UiKit.Accent;
            outline.effectDistance = new Vector2(3f, -3f);
            outline.enabled = false;
            card.gameObject.AddComponent<TapTag>().Kind = TapKind.Icon;
            var button = card.gameObject.AddComponent<Button>();
            button.onClick.AddListener(() => Select(def.Id));
            _cardImages[def.Id] = card;

            var text = new StringBuilder();
            text.AppendLine($"<size=30><b>{def.Name}</b></size>");
            text.AppendLine(MainEffect(def, preview));
            if (def.Id == _recommendedId) text.AppendLine("<color=#e0761a><b>★ おすすめ</b></color>");
            else if (preview.WeatherMultiplier > 1.05) text.AppendLine("<color=#e0761a>天気◎</color>");
            if (preview.Risk >= RiskLevel.Danger) text.AppendLine(RiskLabel(preview.Risk));
            var label = UiKit.Text("Text", card.transform, text.ToString(), 23, TextAnchor.UpperLeft);
            UiKit.Stretch(label.rectTransform, 12, 10, 12, 8);
        }

        /// <summary>カードを選ぶ（まだ実行しない）。</summary>
        public void Select(string trainingId)
        {
            _showingResult = false;
            _selected = Services.Trainings.Load().Get(trainingId);
            Highlight();
            ShowDetail();
            SetMode(result: false);
        }

        private void Highlight()
        {
            foreach (var pair in _cardImages)
            {
                bool selected = _selected != null && pair.Key == _selected.Id;
                pair.Value.color = selected ? UiKit.CardHighlight : UiKit.Card;
                pair.Value.GetComponent<Outline>().enabled = selected;
            }
        }

        private void ShowDetail()
        {
            if (_selected == null) return;
            var (def, preview) = _previews.First(p => p.def.Id == _selected.Id);
            var text = new StringBuilder();
            text.AppendLine($"<size=44><b>{def.Name}</b></size>{(def.Id == _recommendedId ? "　<color=#e0761a>★ おすすめ</color>" : string.Empty)}");
            text.AppendLine();
            if (def.Growth.Count == 0)
            {
                text.AppendLine("疲れと調子を回復します。");
            }
            else
            {
                text.AppendLine("<b>伸びる能力</b>");
                foreach (var pair in def.Growth)
                {
                    preview.ExpectedGains.TryGetValue(pair.Key.Key, out double expected);
                    text.AppendLine($"  {pair.Key.Label}　<b>{UiKit.Plus(expected)}</b>（約 +{expected:0.0}）");
                }
            }

            text.AppendLine();
            text.AppendLine(def.Fatigue >= 0 ? $"疲労　+{def.Fatigue}" : $"疲労　<color=#2f9e4f>{def.Fatigue}</color>");
            if (def.DurabilityRecovery > 0) text.AppendLine($"耐久　<color=#2f9e4f>+{def.DurabilityRecovery}</color>");
            text.AppendLine($"ケガの危険　{(def.Risk <= 0 ? "なし" : new string('▲', def.Risk))}　{RiskLabel(preview.Risk)}");
            if (preview.WeatherMultiplier > 1.05) text.AppendLine("<color=#e0761a>今日の天気に合っている（効果アップ）</color>");
            else if (preview.WeatherMultiplier < 0.95) text.AppendLine("<color=#8a9891>今日の天気には向いていない</color>");
            if (preview.Efficiency < 0.7 && def.Growth.Count > 0) text.AppendLine("<color=#e07a1f>疲れや同じならしの連続で効率が落ちています</color>");
            _detail.text = text.ToString();
        }

        private void ExecuteSelected()
        {
            if (_selected == null) return;
            var (def, preview) = _previews.First(p => p.def.Id == _selected.Id);
            Choose(def, preview);
        }

        public void Choose(TrainingDefinition def, TrainingPreview preview)
        {
            var c = Ui.SelectedCharacter;
            if (c == null) return;
            if (preview.NeedsConfirmation)
            {
                string message = preview.LostPossible
                    ? $"<color=#d9442b><b>極危険</b></color>\n{c.Name} は耐久が低く疲れ切っています。\nこのならしで<b>ロストする可能性</b>があります。本当に行いますか？"
                    : $"<color=#e07a1f><b>危険</b></color>\n「{def.Name}」はケガをしやすいならしです。行いますか？";
                Ui.Confirm(message, () => Execute(def, preview.LostPossible), "行う", danger: true);
                return;
            }

            Execute(def, false);
        }

        private void Execute(TrainingDefinition def, bool confirmedExtreme)
        {
            var c = Ui.SelectedCharacter;
            var weather = Services.Session.TodayConditions(Services.Balance).Weather.ToString();
            var result = Services.Training.Execute(Services.Session, c.Id, def.Id, confirmedExtreme);
            Services.SaveNow();
            Services.Log("training_choice", ("training", def.Id), ("character", c.Id), ("weather", weather), ("efficiency", System.Math.Round(result.Efficiency, 3)),
                ("fatigueBefore", result.FatigueBefore), ("injury", result.Injury), ("lost", result.Lost));
            Ui.RefreshHeader();

            if (result.Lost)
            {
                Ui.Notify($"<b>{c.Name}</b> は激しい流れの中へ消えていった…\n\n思い出はアルバムに残ります。", () => Ui.ShowRoot<HomeScreen>());
                return;
            }

            var text = new StringBuilder();
            text.AppendLine($"<size=40><b>{def.Name}</b> をした！</size>");
            text.AppendLine();
            if (result.StatDeltas.Count > 0)
            {
                foreach (var pair in result.StatDeltas)
                {
                    text.AppendLine($"  {CharacterParameter.Parse(pair.Key).Label}　<color=#2f9e4f><b>+{pair.Value}</b></color>");
                }
            }
            else if (def.Growth.Count > 0)
            {
                text.AppendLine("  今日はあまり伸びなかった…");
            }

            text.AppendLine();
            text.AppendLine($"疲労 {result.FatigueBefore} → <b>{result.FatigueAfter}</b>　耐久 {result.DurabilityBefore} → <b>{result.DurabilityAfter}</b>");
            if (result.Injury > 0) text.AppendLine($"<color=#d9442b>ケガをした（耐久 -{result.Injury}）</color>");
            if (result.ConditionAfter != result.ConditionBefore) text.AppendLine($"調子 {HomeScreen.ConditionLabel(result.ConditionBefore)} → {HomeScreen.ConditionLabel(result.ConditionAfter)}");
            foreach (var id in result.NewBodyTraits)
            {
                text.AppendLine($"<color=#e0761a>体質「{Services.Trainings.Load().FindBodyTrait(id)?.Name}」が身についた！</color>");
            }

            if (result.PersonalityChanged) text.AppendLine($"<color=#e0761a>性格が「{CharacterDetailScreen.PersonalityLabel(c.Personality)}」に変わった</color>");
            text.AppendLine();
            text.AppendLine(Services.RaceDay.IsRaceDay(Services.Session) ? "<color=#e0761a><b>今日はレースの日！</b> ホームから大会に出よう</color>" : "1日が過ぎた。");

            _showingResult = true;
            Rebuild();
            _detail.text = text.ToString();
        }

        private void SetMode(bool result)
        {
            _execute.gameObject.SetActive(!result && Ui.SelectedCharacter != null);
            _again.transform.parent.gameObject.SetActive(result);
            // レースの日になったら「もう1日」より大会を優先させる
            bool raceDay = Services.Session != null && Services.RaceDay.IsRaceDay(Services.Session);
            UiKit.SetButtonLabel(_home, raceDay ? "ホームへ（大会へ）" : "ホームへ");
            _again.gameObject.SetActive(!raceDay);
            if (_selected != null) UiKit.SetButtonLabel(_execute, $"「{_selected.Name}」をする（1日進む）");
        }

        private static string MainEffect(TrainingDefinition def, TrainingPreview preview)
        {
            if (def.Growth.Count == 0) return "疲れを回復";
            var best = def.Growth.OrderByDescending(p => p.Value).Take(2).Select(p =>
            {
                preview.ExpectedGains.TryGetValue(p.Key.Key, out double expected);
                return $"{p.Key.Label}{UiKit.Plus(expected)}";
            });
            return string.Join(" ", best);
        }

        /// <summary>おすすめ度: 期待される伸びの合計。疲れていれば休養、危険なものは下げる。</summary>
        private static double Score(TrainingDefinition def, TrainingPreview p, CharacterState c)
        {
            if (def.Growth.Count == 0) return c.Fatigue >= 60 ? 100 : c.Fatigue / 25.0;
            double gains = p.ExpectedGains.Values.Sum();
            return gains - (p.Risk >= RiskLevel.Danger ? 2.0 : 0);
        }

        public static string RiskLabel(RiskLevel level)
        {
            switch (level)
            {
                case RiskLevel.Caution: return "<color=#e0761a>注意</color>";
                case RiskLevel.Danger: return "<color=#e07a1f>危険</color>";
                case RiskLevel.Extreme: return "<color=#d9442b>極危険</color>";
                default: return "<color=#2f9e4f>安全</color>";
            }
        }
    }
}
