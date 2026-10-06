using System.Linq;
using System.Text;
using NagaRace.Application.Game;
using NagaRace.Domain.Characters;
using NagaRace.Domain.Race;
using NagaRace.Domain.Training;
using NagaRace.Presentation.App;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace NagaRace.Presentation.Main
{
    /// <summary>SC-07 Race Calendar。今日と次のレースの日の大会（一本道にしない: 03 §5）。</summary>
    public sealed class CalendarScreen : UiScreen
    {
        private RectTransform _list;
        private string _recommendedId;

        public CalendarScreen(MainUi ui) : base(ui)
        {
        }

        public override string Title => "大会";

        protected override void OnBuild(RectTransform root)
        {
            _list = UiKit.ScrollList("List", root, 14);
            UiKit.Stretch((RectTransform)_list.parent);
        }

        public override void OnShow()
        {
            UiKit.Clear(_list);
            var session = Services.Session;
            var b = Services.Balance;
            int day = Services.RaceDay.NextRaceDay(session);
            var c = Ui.SelectedCharacter;
            _recommendedId = c != null && day == session.Day ? Guide.RecommendedTournament(c.Id)?.Tournament.Id : null;

            // いま何をすればいいか
            var banner = UiKit.CardBox("Banner", _list, new Color(0.12f, 0.3f, 0.2f, 0.96f), 18);
            if (day == session.Day)
            {
                UiKit.Label(banner, "<size=36><b>今日はレースの日！</b></size>\n出たい大会の「出走登録へ」を押しましょう。<color=#ffd84a>★おすすめ</color> は今の実力で挑みやすい大会です。", 28);
            }
            else
            {
                UiKit.Label(banner, $"<size=36><b>次のレースの日まで あと{day - session.Day}日</b></size>\n今日は大会に出られません。それまでは育成で鍛えましょう。", 28);
                UiKit.Button(banner, "育成へ", () => Ui.Show<TrainingScreen>(), UiKit.Accent, 320, 76, 32);
            }
            for (int week = 0; week < 3; week++, day += b.Calendar.DaysPerWeek)
            {
                bool today = day == session.Day;
                var date = new GameDate(day, b.Calendar);
                UiKit.Label(_list, today ? $"<b>今日（{date.Label}）</b>" : $"<b>{date.Label}</b>（{day - session.Day}日後）", 32, today ? new Color(1f, 0.85f, 0.3f) : UiKit.TextColor);
                foreach (var offering in Services.RaceDay.Offerings(session, day))
                {
                    BuildCard(offering, today);
                }
            }

            Tutorial.ShowOnce(Ui, "calendar");
        }

        private void BuildCard(TournamentOffering o, bool today)
        {
            var t = o.Tournament;
            var course = o.Course;
            var card = UiKit.CardBox($"Tournament {t.Id}", _list, o.Eligible ? UiKit.Card : new Color(0.12f, 0.14f, 0.16f, 0.9f));
            int danger = course.Segments.Max(s => s.Danger);
            bool recommended = today && t.Id == _recommendedId;
            UiKit.Label(card,
                $"<size=32><b>{t.Name}</b></size>　[{t.GradeLabel}]{(recommended ? "　<color=#ffd84a><b>★おすすめ</b></color>" : string.Empty)}\n" +
                $"{course.Name}　{course.Distance:0}m　危険度 {new string('▲', danger)}" +
                (o.Eligible ? string.Empty : $"\n<color=#a0a0a0>出走できません: {o.Reason}</color>"), 26);
            if (today && o.Eligible)
            {
                var buttons = UiKit.HStack("Buttons", card, 16, 64);
                var target = o;
                UiKit.Button(buttons, "出走登録へ", () =>
                {
                    Ui.Get<EntryScreen>().SetTournament(target.Tournament.Id);
                    Ui.Show<EntryScreen>();
                }, UiKit.Accent, 280, 60, 28);
            }
        }
    }

    /// <summary>SC-08 Race Entry。出走8体・自キャラの調子・適性・耐久・危険度・AI作戦（03 §6）。</summary>
    public sealed class EntryScreen : UiScreen
    {
        private string _tournamentId;
        private Text _info;
        private Text _entrants;
        private Button _styleButton;
        private Button _autoButton;
        private PreparedRace _prepared;

        public EntryScreen(MainUi ui) : base(ui)
        {
        }

        public override string Title => "出走登録";

        public void SetTournament(string tournamentId)
        {
            _tournamentId = tournamentId;
        }

        protected override void OnBuild(RectTransform root)
        {
            var left = UiKit.CardBox("Info", root);
            left.anchorMin = new Vector2(0, 1);
            left.anchorMax = new Vector2(0.55f, 1);
            left.pivot = new Vector2(0, 1);
            left.offsetMin = new Vector2(0, left.offsetMin.y);
            left.offsetMax = new Vector2(-10, 0);
            _info = UiKit.Label(left, string.Empty, 28);

            var right = UiKit.CardBox("Entrants", root);
            right.anchorMin = new Vector2(0.55f, 1);
            right.anchorMax = new Vector2(1, 1);
            right.pivot = new Vector2(0, 1);
            right.offsetMin = new Vector2(10, right.offsetMin.y);
            right.offsetMax = Vector2.zero;
            _entrants = UiKit.Label(right, string.Empty, 26);

            var buttons = UiKit.HStack("Buttons", root, 20, 84);
            buttons.anchorMin = new Vector2(0, 0);
            buttons.anchorMax = new Vector2(1, 0);
            buttons.pivot = new Vector2(0.5f, 0);
            buttons.sizeDelta = new Vector2(0, 84);
            buttons.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleRight;
            UiKit.Button(buttons, "出走する子を変える", NextCharacter, UiKit.Muted, 320, 80, 28);
            _styleButton = UiKit.Button(buttons, "AI作戦", NextStyle, UiKit.Card, 300, 80, 28);
            _autoButton = UiKit.Button(buttons, "おまかせ炉", ToggleAuto, UiKit.Card, 300, 80, 28);
            UiKit.Button(buttons, "出走！", Enter, UiKit.Accent, 320, 84, 38);
        }

        public override void OnShow()
        {
            var c = Ui.SelectedCharacter;
            var settings = Services.Settings;
            UiKit.SetButtonLabel(_styleButton, $"AI作戦: {StyleLabel(settings.PlayerAiStyle)}");
            UiKit.SetButtonLabel(_autoButton, $"おまかせ炉: {(settings.AutoFurnace ? "ON" : "OFF")}");
            if (c == null || _tournamentId == null)
            {
                _info.text = "出走できる葉っぱがいません";
                _entrants.text = string.Empty;
                _prepared = null;
                return;
            }

            _prepared = Services.RaceDay.Prepare(Services.Session, _tournamentId, c.Id, settings.PlayerAiStyle, settings.AutoFurnace);
            var request = _prepared.Request;
            var b = Services.Balance;
            var risk = _prepared.Risk;
            var info = new StringBuilder();
            info.AppendLine($"<size=40><b>{_prepared.Tournament.Name}</b></size>");
            info.AppendLine($"{request.Course.Name}　{request.Course.Distance:0}m　天気 {WeatherForecast.WeatherLabel(request.Conditions.Weather)}　水量 {WeatherForecast.WaterLabel(request.Conditions.WaterLevel)}");
            info.AppendLine();
            info.AppendLine($"<b>{c.Name}</b>　{GrowthProfile.Label(c.PhaseOn(Services.Session.Day))}期　調子 {HomeScreen.ConditionLabel(c.Condition)}");
            info.AppendLine($"耐久 {c.Durability}/{c.MaxDurability}　疲労 {c.Fatigue}");
            info.AppendLine($"適性: 激流 {UiKit.Rank(c.Affinities[AffinityType.Rapid], b)} 渦 {UiKit.Rank(c.Affinities[AffinityType.Whirlpool], b)} 狭所 {UiKit.Rank(c.Affinities[AffinityType.Narrow], b)}");
            info.AppendLine();
            info.AppendLine($"<size=36>危険度 <b>{TrainingScreen.RiskLabel(risk.Level)}</b></size>");
            info.AppendLine(risk.Reasons.Count > 0 ? string.Join(" / ", risk.Reasons) : "特に問題なし");
            if (risk.LostPossible) info.AppendLine("<color=#ff7050>このレースではロストの可能性があります</color>");
            _info.text = info.ToString();

            var list = new StringBuilder("<b>出走表</b>\n");
            for (int i = 0; i < request.Entrants.Count; i++)
            {
                var e = request.Entrants[i];
                string name = e.IsPlayer ? $"<color=#ffd84a><b>{e.Name}</b></color>" : e.Name;
                list.AppendLine($"{i + 1}枠  {name}（{HomeScreen.SpeciesName(e.SpeciesId)}）流速 {UiKit.Rank(e.Stats.FlowSpeed, b)}");
            }

            _entrants.text = list.ToString();
            Tutorial.ShowOnce(Ui, "entry");
        }

        private void NextCharacter()
        {
            var active = Services.Session.Roster.Active;
            if (active.Count == 0) return;
            int index = active.ToList().IndexOf(Ui.SelectedCharacter);
            Services.Settings.SelectedCharacterId = active[(index + 1) % active.Count].Id;
            OnShow();
        }

        private void NextStyle()
        {
            Services.Settings.PlayerAiStyle = (AiStyle)(((int)Services.Settings.PlayerAiStyle + 1) % 3);
            OnShow();
        }

        private void ToggleAuto()
        {
            Services.Settings.AutoFurnace = !Services.Settings.AutoFurnace;
            OnShow();
        }

        public void Enter()
        {
            if (_prepared == null) return;
            if (_prepared.Risk.LostPossible)
            {
                Ui.Confirm($"危険度「{_prepared.Risk.Label}」\nこのレースではロストの可能性があります。出走しますか？", Go, "出走する", danger: true);
                return;
            }

            Go();
        }

        private void Go()
        {
            Services.PendingRace = _prepared;
            Services.SaveNow();
            Services.Log("race_start", ("tournament", _prepared.Tournament.Id), ("grade", _prepared.Tournament.Grade), ("character", _prepared.CharacterId),
                ("risk", _prepared.Risk.Level.ToString()), ("autoFurnace", Services.Settings.AutoFurnace), ("aiStyle", Services.Settings.PlayerAiStyle.ToString()));
            SceneManager.LoadScene("Race");
        }

        public static string StyleLabel(AiStyle style)
        {
            switch (style)
            {
                case AiStyle.Safe: return "安全";
                case AiStyle.Aggressive: return "攻める";
                default: return "標準";
            }
        }
    }

    /// <summary>SC-10 Race Result（03 §8）。ロスト時の演出は Race シーン側で済ませてから来る。</summary>
    public sealed class ResultScreen : UiScreen
    {
        private Text _text;
        private Text _next;

        public ResultScreen(MainUi ui) : base(ui)
        {
        }

        public override string Title => "リザルト";
        public override bool ShowBack => false;

        protected override void OnBuild(RectTransform root)
        {
            var card = UiKit.CardBox("Result", root);
            card.anchorMin = card.anchorMax = new Vector2(0.5f, 0.55f);
            card.sizeDelta = new Vector2(1100, 0);
            _text = UiKit.Label(card, string.Empty, 32, UiKit.TextColor, TextAnchor.UpperCenter);
            _next = UiKit.Label(card, string.Empty, 28, new Color(1f, 0.85f, 0.3f), TextAnchor.UpperCenter);

            var button = UiKit.Button(root, "ホームへ", () => Ui.ShowRoot<HomeScreen>(), UiKit.Accent, 360, 90, 36);
            var rect = (RectTransform)button.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0);
            rect.pivot = new Vector2(0.5f, 0);
            rect.anchoredPosition = new Vector2(0, 20);
            rect.sizeDelta = new Vector2(360, 90);
        }

        public override void OnShow()
        {
            var o = Services.LastOutcome;
            if (o == null)
            {
                _text.text = "結果がありません";
                return;
            }

            var a = o.Aftermath;
            var text = new StringBuilder();
            text.AppendLine($"<size=40><b>{o.Tournament.Name}</b></size>");
            if (o.Lost) text.AppendLine("<size=60><color=#a0c0ff><b>LOST</b></color></size>");
            else if (o.Placing.Retired) text.AppendLine("<size=60><b>リタイア</b></size>");
            else text.AppendLine($"<size=80><b>{o.Placing.Rank}</b></size><size=40>着</size>　{o.Placing.FormatTime()}");
            if (o.Won) text.AppendLine("<color=#ffd84a><b>優勝！</b></color>");
            if (o.Promoted) text.AppendLine("<color=#ffd84a>上の大会に出られるようになった！</color>");
            text.AppendLine();
            text.AppendLine($"耐久 {a.DurabilityBefore} → {a.DurabilityAfter}" + (a.Injured ? $"　<color=#ff8060>大怪我（最大耐久 {a.MaxDurabilityBefore} → {a.MaxDurabilityAfter}）</color>" : string.Empty));
            text.AppendLine($"疲労 {a.FatigueBefore} → {a.FatigueAfter}");
            if (a.FurnaceDurabilityLoss > 0) text.AppendLine($"<color=#ffb060>炉耐久 -{a.FurnaceDurabilityLoss}</color>");
            if (o.Placing.MaxFurnaceSeconds > 0) text.AppendLine($"葉脈炉 最長 {o.Placing.MaxFurnaceSeconds:0.0}秒");
            if (o.ParentTree != null) text.AppendLine($"\n「{o.ParentTree.Name}」が親木園に加わった");
            _text.text = text.ToString();
            var next = Guide.Next(Ui);
            _next.text = $"次にやること: <b>{next.Title}</b>";
            Tutorial.ShowOnce(Ui, "result");
        }
    }
}
