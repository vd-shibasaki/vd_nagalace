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
    /// SC-06 Training。カード UI（03 §4）: メニュー名・予想効果・疲労・耐久リスク・天候補正・おすすめ度。
    /// 危険度が高い場合のみ確認ダイアログを出す。
    /// </summary>
    public sealed class TrainingScreen : UiScreen
    {
        private Text _status;
        private RectTransform _grid;

        public TrainingScreen(MainUi ui) : base(ui)
        {
        }

        public override string Title => "育成（ならし）";

        protected override void OnBuild(RectTransform root)
        {
            var top = UiKit.CardBox("Status", root, new Color(0.08f, 0.15f, 0.18f, 0.9f), 14);
            top.anchorMin = new Vector2(0, 1);
            top.anchorMax = new Vector2(1, 1);
            top.pivot = new Vector2(0.5f, 1);
            top.offsetMin = new Vector2(0, top.offsetMin.y);
            top.offsetMax = Vector2.zero;
            _status = UiKit.Label(top, string.Empty, 28);

            var gridRect = UiKit.Rect("Grid", root);
            UiKit.Stretch(gridRect, 0, 120, 0, 0);
            var grid = gridRect.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(352, 400);
            grid.spacing = new Vector2(14, 14);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 5;
            _grid = gridRect;
        }

        public override void OnShow()
        {
            UiKit.Clear(_grid);
            var c = Ui.SelectedCharacter;
            if (c == null)
            {
                _status.text = "現役の葉っぱがいません";
                return;
            }

            var session = Services.Session;
            var weather = session.TodayConditions(Services.Balance).Weather;
            _status.text = $"<b>{c.Name}</b>　{GrowthProfile.Label(c.PhaseOn(session.Day))}期　調子 {HomeScreen.ConditionLabel(c.Condition)}　疲労 {c.Fatigue}　耐久 {c.Durability}/{c.MaxDurability}　今日の天気: <b>{WeatherForecast.WeatherLabel(weather)}</b>";

            var catalog = Services.Trainings.Load();
            var previews = catalog.Trainings.Select(t => (def: t, preview: Services.Training.Preview(session, c.Id, t.Id))).ToList();
            double best = previews.Max(p => Score(p.preview));
            foreach (var (def, preview) in previews)
            {
                BuildCard(def, preview, best);
            }

            Tutorial.ShowOnce(Ui, "training");
        }

        private void BuildCard(TrainingDefinition def, TrainingPreview preview, double best)
        {
            Color color = preview.Risk >= RiskLevel.Danger ? new Color(0.3f, 0.14f, 0.12f, 0.95f) : UiKit.Card;
            var card = UiKit.Panel($"Card {def.Id}", _grid, color);
            var button = card.gameObject.AddComponent<Button>();
            button.onClick.AddListener(() => Choose(def, preview));

            var text = new StringBuilder();
            text.AppendLine($"<size=34><b>{def.Name}</b></size>");
            if (def.Growth.Count == 0)
            {
                text.AppendLine("疲労・調子の回復");
            }

            foreach (var pair in def.Growth)
            {
                preview.ExpectedGains.TryGetValue(pair.Key.Key, out double expected);
                text.AppendLine($"{pair.Key.Label} <b>{UiKit.Plus(expected)}</b>");
            }

            text.AppendLine(def.Fatigue >= 0 ? $"疲労 +{def.Fatigue}" : $"<color=#9fe39f>疲労 {def.Fatigue}</color>");
            text.AppendLine($"耐久リスク {RiskMarks(def.Risk)}　{RiskLabel(preview.Risk)}");
            if (preview.WeatherMultiplier > 1.05) text.AppendLine("<color=#ffd84a>本日 BONUS</color>");
            else if (preview.WeatherMultiplier < 0.95) text.AppendLine("<color=#9ab>本日は不向き</color>");
            text.AppendLine($"おすすめ {Stars(Score(preview), best)}");

            var label = UiKit.Text("Text", card.transform, text.ToString(), 25, TextAnchor.UpperLeft);
            UiKit.Stretch(label.rectTransform, 16, 14, 16, 10);
        }

        public void Choose(TrainingDefinition def, TrainingPreview preview)
        {
            var c = Ui.SelectedCharacter;
            if (c == null) return;
            if (preview.NeedsConfirmation)
            {
                string message = preview.LostPossible
                    ? $"<color=#ff6050><b>極危険</b></color>\n{c.Name} は耐久が低く疲れ切っています。\nこのならしで<b>ロストする可能性</b>があります。本当に行いますか？"
                    : $"<color=#ffa040><b>危険</b></color>\n「{def.Name}」はケガをしやすいならしです。行いますか？";
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

            var text = new StringBuilder();
            text.AppendLine($"<b>{c.Name}</b> は「{def.Name}」をした。");
            if (result.StatDeltas.Count > 0)
            {
                text.AppendLine(string.Join("　", result.StatDeltas.Select(p => $"{CharacterParameter.Parse(p.Key).Label} +{p.Value}")));
            }
            else if (def.Growth.Count > 0)
            {
                text.AppendLine("今日はあまり伸びなかった…");
            }

            text.AppendLine($"疲労 {result.FatigueBefore}→{result.FatigueAfter}　耐久 {result.DurabilityBefore}→{result.DurabilityAfter}");
            if (result.Injury > 0) text.AppendLine($"<color=#ff8060>ケガをした（耐久 -{result.Injury}）</color>");
            if (result.ConditionAfter != result.ConditionBefore) text.AppendLine($"調子: {HomeScreen.ConditionLabel(result.ConditionBefore)} → {HomeScreen.ConditionLabel(result.ConditionAfter)}");
            foreach (var id in result.NewBodyTraits)
            {
                text.AppendLine($"<color=#ffd84a>体質「{Services.Trainings.Load().FindBodyTrait(id)?.Name}」が身についた！</color>");
            }

            if (result.PersonalityChanged) text.AppendLine($"<color=#ffd84a>性格が「{CharacterDetailScreen.PersonalityLabel(c.Personality)}」に変わった</color>");

            if (result.Lost)
            {
                Ui.Notify($"<b>{c.Name}</b> は激しい流れの中へ消えていった…\n\n思い出はアルバムに残ります。", () => Ui.ShowRoot<HomeScreen>());
                return;
            }

            Ui.Notify(text.ToString(), () =>
            {
                Ui.RefreshHeader();
                OnShow();
            });
        }

        private static double Score(TrainingPreview p)
        {
            double gains = p.ExpectedGains.Values.Sum();
            if (p.ExpectedGains.Count == 0) gains = -p.Fatigue / 20.0; // 休養系は疲れているほどおすすめ
            return gains - (p.Risk >= RiskLevel.Danger ? 1.5 : 0);
        }

        private static string Stars(double score, double best)
        {
            if (best <= 0) return "★";
            double ratio = score / best;
            if (ratio >= 0.85) return "<color=#ffd84a>★★★</color>";
            if (ratio >= 0.55) return "★★";
            return "★";
        }

        private static string RiskMarks(int risk)
        {
            return risk <= 0 ? "なし" : new string('▲', risk);
        }

        public static string RiskLabel(RiskLevel level)
        {
            switch (level)
            {
                case RiskLevel.Caution: return "<color=#ffd84a>注意</color>";
                case RiskLevel.Danger: return "<color=#ff9040>危険</color>";
                case RiskLevel.Extreme: return "<color=#ff4030>極危険</color>";
                default: return "<color=#9fe39f>安全</color>";
            }
        }
    }
}
