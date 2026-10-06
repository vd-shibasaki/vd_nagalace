using System;
using System.Collections.Generic;
using System.Linq;
using NagaRace.Application.Game;
using NagaRace.Domain.Characters;
using NagaRace.Domain.Training;
using NagaRace.Presentation.App;
using UnityEngine;
using UnityEngine.UI;

namespace NagaRace.Presentation.Main
{
    /// <summary>「今日やること」の提案。</summary>
    public sealed class GuidedAction
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public string ButtonLabel { get; set; }
        public Action Run { get; set; }
        public Color Color { get; set; } = UiKit.Accent;
    }

    /// <summary>
    /// 状況から「今日やること」を1つ決める。迷わず遊べることを優先し、ホームとリザルトで同じ提案を出す。
    /// </summary>
    public static class Guide
    {
        public static GuidedAction Next(MainUi ui)
        {
            var services = GameServices.Instance;
            var session = services.Session;
            var b = services.Balance;
            var c = ui.SelectedCharacter;

            if (c == null)
            {
                return new GuidedAction
                {
                    Title = "現役の葉っぱがいません",
                    Description = "引退した葉っぱは親木になります。親木園で接ぎ木して、新しい若葉を迎えましょう。",
                    ButtonLabel = "親木園へ",
                    Run = () => ui.Show<ParentTreeScreen>(),
                };
            }

            if (services.RaceDay.IsRaceDay(session))
            {
                var recommended = RecommendedTournament(c.Id);
                if (recommended != null)
                {
                    return new GuidedAction
                    {
                        Title = "今日はレースの日！",
                        Description = $"おすすめ: <b>{recommended.Tournament.Name}</b>（{recommended.Course.Name}）\n出走すると1日進みます。",
                        ButtonLabel = "大会に出る",
                        Run = () =>
                        {
                            ui.Get<EntryScreen>().SetTournament(recommended.Tournament.Id);
                            ui.Show<EntryScreen>();
                        },
                        Color = UiKit.Warning,
                    };
                }
            }

            int daysToRace = services.RaceDay.NextRaceDay(session) - session.Day;
            if (c.Fatigue >= 60)
            {
                return new GuidedAction
                {
                    Title = $"{c.Name} は疲れています（疲労 {c.Fatigue}）",
                    Description = "疲れていると育成の伸びが落ち、ケガもしやすくなります。今日は休ませましょう。",
                    ButtonLabel = "休養する（1日進む）",
                    Run = () => ui.Get<HomeScreen>().Rest(),
                };
            }

            if (c.Durability < b.Durability.LightDamageMin)
            {
                return new GuidedAction
                {
                    Title = $"耐久が減っています（{c.Durability}/{c.MaxDurability}）",
                    Description = "耐久が低いままレースに出ると、事故やロストの危険が上がります。休養で回復しましょう。",
                    ButtonLabel = "休養する（1日進む）",
                    Run = () => ui.Get<HomeScreen>().Rest(),
                };
            }

            var phase = c.PhaseOn(session.Day);
            if (phase == GrowthPhase.Autumn || phase == GrowthPhase.Withered)
            {
                return new GuidedAction
                {
                    Title = $"{c.Name} は{GrowthProfile.Label(phase)}期です",
                    Description = "これから少しずつ能力が下がります。引退させて親木にすると、次の世代へつなげられます。",
                    ButtonLabel = "引退について見る",
                    Run = () => ui.Show<CharacterDetailScreen>(),
                    Color = UiKit.Accent,
                };
            }

            return new GuidedAction
            {
                Title = daysToRace == 0 ? "今日は出られる大会がありません" : $"次のレースまであと {daysToRace}日",
                Description = "レースの日までに「ならし」で鍛えましょう。天気に合ったならしが伸びやすい（★の多いもの）。",
                ButtonLabel = "育成する",
                Run = () => ui.Show<TrainingScreen>(),
            };
        }

        /// <summary>出られる大会のうち、ロストの危険がない一番上のグレード（なければ一番上）。</summary>
        public static TournamentOffering RecommendedTournament(string characterId)
        {
            var services = GameServices.Instance;
            var settings = services.Settings;
            var eligible = services.RaceDay.Offerings(services.Session, services.Session.Day).Where(o => o.Eligible).ToList();
            if (eligible.Count == 0) return null;
            var safe = eligible.Where(o => !services.RaceDay.Prepare(services.Session, o.Tournament.Id, characterId, settings.PlayerAiStyle, settings.AutoFurnace).Risk.LostPossible).ToList();
            return (safe.Count > 0 ? safe : eligible).OrderByDescending(o => o.Tournament.Grade).First();
        }
    }

    /// <summary>
    /// SC-03 Home。「今日やること」を中心に置き、迷わず次の行動を選べるようにする。
    /// 3D の背景（HomeStage）は川辺の小さな拠点。
    /// </summary>
    public sealed class HomeScreen : UiScreen
    {
        private RectTransform _leafTabs;
        private Text _character;
        private Text _characterDetail;
        private Text _date;
        private Image _statusCard;
        private Image _statusIcon;
        private RectTransform _week;
        private Text _actionTitle;
        private Text _actionDescription;
        private Button _actionButton;
        private Image _actionCard;
        private Text _summary;
        private GuidedAction _action;

        public HomeScreen(MainUi ui) : base(ui)
        {
        }

        public override string Title => "ホーム";
        public override bool ShowBack => false;
        public override bool ShowHeader => false;
        public override bool ShowBackground => false;

        /// <summary>現在の「今日やること」（テスト用）。</summary>
        public GuidedAction CurrentAction => _action;

        protected override void OnBuild(RectTransform root)
        {
            // 左: 葉っぱの切り替えタブと状態
            var left = UiKit.VStack("Left", root, 14);
            left.anchorMin = new Vector2(0, 0);
            left.anchorMax = new Vector2(0, 1);
            left.pivot = new Vector2(0, 1);
            left.sizeDelta = new Vector2(760, 0);
            var dateCard = UiKit.CardBox("Date", left, UiKit.Card, 14);
            _date = UiKit.Label(dateCard, string.Empty, 28);
            UiKit.Label(left, "<b>育てる葉っぱ</b>（タップで切り替え）", 24, UiKit.TextColor);
            _leafTabs = UiKit.HStack("LeafTabs", left, 10, 76);
            var status = UiKit.CardBox("Status", left);
            _statusCard = status.GetComponent<Image>();
            var statusHead = UiKit.HStack("Head", status, 18, 120);
            _statusIcon = UiKit.LeafIcon(statusHead, null, 118);
            _character = UiKit.Label(statusHead, string.Empty, 29, UiKit.TextColor, TextAnchor.MiddleLeft);
            _characterDetail = UiKit.Label(status, string.Empty, 28);
            var summaryCard = UiKit.CardBox("Summary", left, UiKit.Card, 14);
            _summary = UiKit.Label(summaryCard, string.Empty, 24, UiKit.SubText);

            // 右: 今週の流れ・今日やること・その他
            var right = UiKit.VStack("Right", root, 16);
            right.anchorMin = new Vector2(1, 0);
            right.anchorMax = new Vector2(1, 1);
            right.pivot = new Vector2(1, 1);
            right.sizeDelta = new Vector2(820, 0);

            UiKit.Label(right, "<b>今週の流れ</b>", 24, UiKit.TextColor);
            _week = UiKit.HStack("Week", right, 8, 84);
            _week.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = true;

            _actionCard = UiKit.Panel("TodayAction", right, UiKit.Card);
            UiKit.AddShadow(_actionCard.gameObject, 6f, 0.16f);
            var actionLayout = _actionCard.gameObject.AddComponent<VerticalLayoutGroup>();
            actionLayout.padding = new RectOffset(26, 26, 22, 22);
            actionLayout.spacing = 12;
            actionLayout.childControlWidth = true;
            actionLayout.childControlHeight = true;
            actionLayout.childForceExpandHeight = false;
            _actionCard.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            UiKit.Label(_actionCard.transform, "<b>★ 今日やること</b>", 28, new Color32(0xE0, 0x76, 0x1A, 0xFF));
            _actionTitle = UiKit.Label(_actionCard.transform, string.Empty, 40);
            _actionDescription = UiKit.Label(_actionCard.transform, string.Empty, 26, UiKit.SubText);
            _actionButton = UiKit.Button(_actionCard.transform, "育成する", () => _action?.Run(), UiKit.Accent, 760, 104, 42);

            UiKit.Label(right, "<b>ほかにできること</b>", 24, UiKit.TextColor);
            var row1 = UiKit.HStack("Row1", right, 12, 76);
            row1.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = true;
            UiKit.Button(row1, "休養", Rest, UiKit.Muted, 190, 76, 30);
            UiKit.Button(row1, "詳しく見る", () => Ui.Show<CharacterDetailScreen>(), UiKit.Muted, 190, 76, 28);
            UiKit.Button(row1, "設定", () => Ui.Show<SettingsScreen>(), UiKit.Muted, 160, 76, 28);
        }

        public override void OnShow()
        {
            BuildLeafTabs();
            BuildWeek();
            var c = Ui.SelectedCharacter;
            var b = Services.Balance;
            _date.text = MainUi.DateLine(Services);
            if (c == null)
            {
                _character.text = "<b>現役の葉っぱがいません</b>";
                _characterDetail.text = string.Empty;
                _statusCard.color = UiKit.Card;
                _statusIcon.color = UiKit.Line;
            }
            else
            {
                var phase = c.PhaseOn(Services.Session.Day);
                int left = c.Growth.DaysLeftInPhase(c.AgeDays(Services.Session.Day));
                // キャラごとに UI の色を変える（愛着のため）
                _statusCard.color = UiKit.SpeciesTint(c.SpeciesId, 0.16f);
                _statusIcon.color = UiKit.SpeciesColor(c.SpeciesId);
                _character.text =
                    $"<size=54><b>{c.Name}</b></size>\n{SpeciesName(c.SpeciesId)} / {c.Generation}世代　{GrowthProfile.Label(phase)}期{(left > 0 ? $"（あと{left}日）" : string.Empty)}";
                _characterDetail.text =
                    $"調子 <b>{ConditionLabel(c.Condition)}</b>\n" +
                    $"疲労 {Bar(c.Fatigue, 100, true)} {c.Fatigue}\n" +
                    $"耐久 {Bar(c.Durability, c.MaxDurability, false)} {c.Durability}/{c.MaxDurability}\n" +
                    $"流速 <b>{UiKit.Rank(c.Stats.FlowSpeed, b)}</b>  加速 <b>{UiKit.Rank(c.Stats.Acceleration, b)}</b>  浮力 <b>{UiKit.Rank(c.Stats.Buoyancy, b)}</b>\n" +
                    $"安定 <b>{UiKit.Rank(c.Stats.Stability, b)}</b>  頑丈 <b>{UiKit.Rank(c.Stats.Toughness, b)}</b>  判断 <b>{UiKit.Rank(c.Stats.Judgement, b)}</b>";
            }

            var roster = Services.Session.Roster;
            _summary.text = $"戦績 {(c != null ? $"{c.Record.Starts}戦{c.Record.Wins}勝" : "-")}　最高到達: {GradeLabel(Services.Session.TopGradeWon)}\n" +
                $"現役 {roster.Active.Count}枚 / アルバム {roster.Album.Count}枚 / 親木 {Services.Session.ParentTrees.Count}本";

            _action = Guide.Next(Ui);
            _actionTitle.text = $"<b>{_action.Title}</b>";
            _actionDescription.text = _action.Description;
            UiKit.SetButtonLabel(_actionButton, _action.ButtonLabel);
            UiKit.SetButtonColor(_actionButton, _action.Color);
            Tutorial.ShowOnce(Ui, "home");
        }

        /// <summary>休養（1日進む）。</summary>
        public void Rest()
        {
            var c = Ui.SelectedCharacter;
            if (c == null) return;
            var result = Services.Training.Execute(Services.Session, c.Id, "rest");
            Services.SaveNow();
            Services.Log("training_choice", ("training", "rest"), ("character", c.Id), ("fatigueBefore", result.FatigueBefore));
            Ui.Toast($"{c.Name} はゆっくり休んだ　疲労 {result.FatigueBefore}→{result.FatigueAfter}　耐久 {result.DurabilityBefore}→{result.DurabilityAfter}");
            Ui.RefreshHeader();
            OnShow();
        }

        private void BuildLeafTabs()
        {
            UiKit.Clear(_leafTabs);
            var selected = Ui.SelectedCharacter;
            foreach (var c in Services.Session.Roster.Active)
            {
                var target = c;
                bool isSelected = c == selected;
                var tab = UiKit.Button(_leafTabs, c.Name, () =>
                {
                    Services.Settings.SelectedCharacterId = target.Id;
                    Services.SaveNow();
                    OnShow();
                }, isSelected ? UiKit.SpeciesColor(c.SpeciesId) : UiKit.Muted, 230, 72, 28);
                var icon = UiKit.LeafIcon(tab.transform, c.SpeciesId, 46);
                icon.GetComponent<LayoutElement>().ignoreLayout = true;
                icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = new Vector2(0, 0.5f);
                icon.rectTransform.anchoredPosition = new Vector2(34, 0);
                if (isSelected) icon.color = Color.white;
                tab.GetComponentInChildren<Text>().rectTransform.offsetMin = new Vector2(56, 4);
            }
        }

        private void BuildWeek()
        {
            UiKit.Clear(_week);
            var b = Services.Balance;
            var date = Services.Session.Date(b);
            int raceDay = Services.RaceDay.Catalog.RaceDayOfWeek;
            for (int d = 1; d <= b.Calendar.DaysPerWeek; d++)
            {
                bool today = d == date.DayOfWeek;
                bool race = d == raceDay;
                Color color = today ? UiKit.Accent : (d < date.DayOfWeek ? new Color(0.88f, 0.9f, 0.89f, 0.95f) : UiKit.Card);
                var cell = UiKit.Panel($"Day{d}", _week, color);
                UiKit.AddShadow(cell.gameObject, 3f, 0.1f);
                cell.gameObject.AddComponent<LayoutElement>().preferredWidth = 180;
                string label = $"{d}日目{(race ? "\n<color=#e0761a><b>レース</b></color>" : "\n育成・休養")}";
                if (today) label = $"<b>今日</b>\n{(race ? "<b>レース！</b>" : "育成・休養")}";
                var text = UiKit.Text("Label", cell.transform, label, 24, TextAnchor.MiddleCenter, today ? Color.white : UiKit.TextColor);
                UiKit.Stretch(text.rectTransform, 4, 4, 4, 4);
            }
        }

        private static string Bar(int value, int max, bool higherIsWorse)
        {
            int filled = Mathf.Clamp(Mathf.RoundToInt(10f * value / Mathf.Max(1, max)), 0, 10);
            float ratio = (float)value / Mathf.Max(1, max);
            string color = higherIsWorse
                ? (ratio >= 0.6f ? "#d9442b" : ratio >= 0.4f ? "#e0761a" : "#2f9e4f")
                : (ratio < 0.2f ? "#d9442b" : ratio < 0.4f ? "#e07a1f" : "#2f9e4f");
            return $"<color={color}>{new string('■', filled)}</color><color=#d5ddd9>{new string('■', 10 - filled)}</color>";
        }

        public static string SpeciesName(string id)
        {
            switch (id)
            {
                case "maple": return "モミジ";
                case "ginkgo": return "イチョウ";
                case "lotus": return "ハス";
                case "sakura": return "サクラ";
                case "acorn": return "ドングリ";
                case "bad_leaf": return "悪葉っぱ";
                default: return id;
            }
        }

        public static string ConditionLabel(Condition c)
        {
            switch (c)
            {
                case Condition.Excellent: return "絶好調";
                case Condition.Good: return "好調";
                case Condition.Poor: return "不調";
                case Condition.Terrible: return "絶不調";
                default: return "普通";
            }
        }

        public static string GradeLabel(int grade)
        {
            switch (grade)
            {
                case 0: return "まだ勝利なし";
                case 1: return "地方大会 制覇";
                case 2: return "Village Mountain City Cup 制覇";
                case 3: return "Mountain Shape Championship 制覇";
                case 4: return "Moon Mountain Masters 制覇";
                default: return "MOST HIGHER RIVER 制覇！";
            }
        }
    }
}
