using System;
using NagaRace.Domain.Characters;
using NagaRace.Presentation.App;
using UnityEngine;
using UnityEngine.UI;

namespace NagaRace.Presentation.Main
{
    /// <summary>SC-02 Title。</summary>
    public sealed class TitleScreen : UiScreen
    {
        private Button _continue;

        public TitleScreen(MainUi ui) : base(ui)
        {
        }

        public override string Title => "タイトル";
        public override bool ShowBack => false;

        protected override void OnBuild(RectTransform root)
        {
            var stack = UiKit.VStack("Title", root, 24);
            stack.anchorMin = stack.anchorMax = new Vector2(0.5f, 0.5f);
            stack.sizeDelta = new Vector2(900, 700);
            stack.GetComponent<VerticalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;
            stack.GetComponent<VerticalLayoutGroup>().childForceExpandWidth = false;
            UiKit.Label(stack, "<b>ながレース</b>", 120, UiKit.TextColor, TextAnchor.MiddleCenter);
            UiKit.Label(stack, "NAGARE RACE ― 小さな流れの、大きな物語。", 34, UiKit.SubText, TextAnchor.MiddleCenter);
            UiKit.Label(stack, " ", 30);
            UiKit.Button(stack, "はじめから", NewGame, UiKit.Accent, 520, 90, 38);
            _continue = UiKit.Button(stack, "つづきから", Continue, UiKit.Card, 520, 90, 38);
        }

        public override void OnShow()
        {
            _continue.interactable = Services.Save.HasSave;
        }

        public void NewGame()
        {
            if (Services.Save.HasSave)
            {
                Ui.Confirm("いまのセーブデータを消して、はじめから遊びますか？", StartNew, "はじめから", danger: true);
            }
            else
            {
                StartNew();
            }
        }

        public void Continue()
        {
            var result = Services.Continue();
            if (!result.Success)
            {
                Ui.Notify($"セーブデータを読めませんでした。\n{result.Error}");
                return;
            }

            if (result.Error != null) Ui.Toast("バックアップから復元しました");
            Services.Log("continue", ("characters", Services.Session.Roster.Active.Count));
            Ui.ShowRoot<HomeScreen>();
        }

        private void StartNew()
        {
            Services.StartNewGame((ulong)DateTime.UtcNow.Ticks);
            Services.Log("new_game");
            // 歓迎のあとでホームの説明を出す（ダイアログは1つずつ）
            Ui.Notify("川辺の拠点に、3枚の若葉がやってきた。\n\n育てて、流して、最高峰 MOST HIGHER RIVER を目指そう。", () => Tutorial.ShowOnce(Ui, "home"));
            Ui.ShowRoot<HomeScreen>();
        }
    }

    /// <summary>SC-03 Home。メニュー表ではなく「川辺の小さな拠点」（3D の背景は HomeStage）。</summary>
    public sealed class HomeScreen : UiScreen
    {
        private Text _character;
        private Text _summary;
        private RectTransform _menu;
        private Button _raceButton;

        public HomeScreen(MainUi ui) : base(ui)
        {
        }

        public override string Title => "ホーム";
        public override bool ShowBack => false;

        protected override void OnBuild(RectTransform root)
        {
            var left = UiKit.CardBox("Character", root);
            left.anchorMin = new Vector2(0, 1);
            left.anchorMax = new Vector2(0, 1);
            left.pivot = new Vector2(0, 1);
            left.sizeDelta = new Vector2(820, 0);
            _character = UiKit.Label(left, string.Empty, 29);

            var summary = UiKit.CardBox("Summary", root, new Color(0.08f, 0.15f, 0.18f, 0.85f));
            summary.anchorMin = new Vector2(0, 0);
            summary.anchorMax = new Vector2(0, 0);
            summary.pivot = new Vector2(0, 0);
            summary.sizeDelta = new Vector2(640, 0);
            _summary = UiKit.Label(summary, string.Empty, 26, UiKit.SubText);

            _menu = UiKit.VStack("Menu", root, 18);
            _menu.anchorMin = new Vector2(1, 0.5f);
            _menu.anchorMax = new Vector2(1, 0.5f);
            _menu.pivot = new Vector2(1, 0.5f);
            _menu.sizeDelta = new Vector2(440, 760);
            UiKit.Button(_menu, "育成（ならし）", () => Ui.Show<TrainingScreen>(), UiKit.Accent, 440, 92, 36);
            _raceButton = UiKit.Button(_menu, "大会", () => Ui.Show<CalendarScreen>(), UiKit.Accent, 440, 92, 36);
            UiKit.Button(_menu, "休養", Rest, UiKit.Card, 440, 80, 32);
            UiKit.Button(_menu, "葉っぱ一覧", () => Ui.Show<CharactersScreen>(), UiKit.Card, 440, 80, 32);
            UiKit.Button(_menu, "親木園（接ぎ木）", () => Ui.Show<ParentTreeScreen>(), UiKit.Card, 440, 80, 32);
            UiKit.Button(_menu, "設定", () => Ui.Show<SettingsScreen>(), UiKit.Muted, 440, 70, 30);
        }

        public override void OnShow()
        {
            var c = Ui.SelectedCharacter;
            var b = Services.Balance;
            if (c == null)
            {
                _character.text = "<b>現役の葉っぱがいません</b>\n\n親木園で接ぎ木して、新しい若葉を迎えましょう。";
            }
            else
            {
                var phase = c.PhaseOn(Services.Session.Day);
                int left = c.Growth.DaysLeftInPhase(c.AgeDays(Services.Session.Day));
                _character.text =
                    $"<size=46><b>{c.Name}</b></size>　{SpeciesName(c.SpeciesId)} / {c.Generation}世代\n" +
                    $"{GrowthProfile.Label(phase)}期（{GrowthProfile.Label(c.Growth.Type)}）{(left > 0 ? $" あと{left}日" : string.Empty)}\n" +
                    $"調子 {ConditionLabel(c.Condition)}　疲労 {c.Fatigue}　耐久 {c.Durability}/{c.MaxDurability}\n" +
                    $"流速 {UiKit.Rank(c.Stats.FlowSpeed, b)}  加速 {UiKit.Rank(c.Stats.Acceleration, b)}  浮力 {UiKit.Rank(c.Stats.Buoyancy, b)}  安定 {UiKit.Rank(c.Stats.Stability, b)}  頑丈 {UiKit.Rank(c.Stats.Toughness, b)}  判断 {UiKit.Rank(c.Stats.Judgement, b)}\n" +
                    Warnings(c);
            }

            var roster = Services.Session.Roster;
            _summary.text = $"現役 {roster.Active.Count}枚 / アルバム {roster.Album.Count}枚 / 親木 {Services.Session.ParentTrees.Count}本\n" +
                $"最高到達: {GradeLabel(Services.Session.TopGradeWon)}\n" +
                "遠くに MOST HIGHER RIVER が見える…";
            _raceButton.interactable = c != null;
            Tutorial.ShowOnce(Ui, "home");
        }

        private void Rest()
        {
            var c = Ui.SelectedCharacter;
            if (c == null) return;
            var result = Services.Training.Execute(Services.Session, c.Id, "rest");
            Services.SaveNow();
            Ui.Toast($"{c.Name} はゆっくり休んだ（疲労 {result.FatigueBefore}→{result.FatigueAfter}）");
            OnShow();
            Ui.RefreshHeader();
        }

        private string Warnings(CharacterState c)
        {
            var b = Services.Balance;
            string warnings = string.Empty;
            if (c.Fatigue >= b.Training.HighFatigueThreshold) warnings += "<color=#ff9a40>疲労が溜まっています。休養をおすすめします</color>\n";
            if (c.Durability < b.Durability.DamagedMin) warnings += "<color=#ff5040>耐久がかなり低い。危険なレースではロストの可能性</color>\n";
            else if (c.Durability < b.Durability.LightDamageMin) warnings += "<color=#ffb040>耐久が低めです</color>\n";
            var phase = c.PhaseOn(Services.Session.Day);
            if (phase == GrowthPhase.Autumn) warnings += "<color=#ffd84a>紅葉期。引退して親木に登録する時期を考えましょう</color>\n";
            if (phase == GrowthPhase.Withered) warnings += "<color=#ffd84a>枯葉期。能力が下がっています</color>\n";
            return warnings;
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
