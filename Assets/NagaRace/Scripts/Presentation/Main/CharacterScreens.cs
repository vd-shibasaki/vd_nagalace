using System.Linq;
using System.Text;
using NagaRace.Domain.Characters;
using NagaRace.Presentation.App;
using UnityEngine;

namespace NagaRace.Presentation.Main
{
    /// <summary>SC-04 Character List（現役とアルバム）。</summary>
    public sealed class CharactersScreen : UiScreen
    {
        private RectTransform _list;

        public CharactersScreen(MainUi ui) : base(ui)
        {
        }

        public override string Title => "葉っぱ一覧";

        protected override void OnBuild(RectTransform root)
        {
            _list = UiKit.ScrollList("List", root, 16);
            UiKit.Stretch((RectTransform)_list.parent);
        }

        public override void OnShow()
        {
            UiKit.Clear(_list);
            var session = Services.Session;
            var b = Services.Balance;
            UiKit.Label(_list, "<b>現役</b>", 34);
            foreach (var c in session.Roster.Active)
            {
                bool selected = Ui.SelectedCharacter == c;
                var card = UiKit.CardBox($"Card {c.Id}", _list, selected ? UiKit.CardHighlight : UiKit.Card);
                UiKit.Label(card,
                    $"<b>{c.Name}</b>{(selected ? "　<color=#ffd84a>（選択中）</color>" : string.Empty)}　{HomeScreen.SpeciesName(c.SpeciesId)} / {c.Generation}世代 / {GrowthProfile.Label(c.PhaseOn(session.Day))}期\n" +
                    $"調子 {HomeScreen.ConditionLabel(c.Condition)}　疲労 {c.Fatigue}　耐久 {c.Durability}/{c.MaxDurability}　戦績 {c.Record.Starts}戦{c.Record.Wins}勝　流速 {UiKit.Rank(c.Stats.FlowSpeed, b)} 加速 {UiKit.Rank(c.Stats.Acceleration, b)}", 26);
                var buttons = UiKit.HStack("Buttons", card, 16, 64);
                var target = c;
                UiKit.Button(buttons, "この子を選ぶ", () => Select(target), UiKit.Accent, 260, 60, 26);
                UiKit.Button(buttons, "詳しく見る", () => ShowDetail(target), UiKit.Muted, 260, 60, 26);
            }

            UiKit.Label(_list, " ", 10);
            UiKit.Label(_list, $"<b>思い出アルバム</b>（{session.Roster.Album.Count}枚）", 34);
            foreach (var entry in session.Roster.Album.Reverse())
            {
                string status = entry.Status == CharacterStatus.Lost ? "<color=#a0c0ff>ロスト</color>" : "引退";
                var card = UiKit.CardBox($"Album {entry.Id}", _list, new Color(0.1f, 0.14f, 0.2f, 0.9f));
                UiKit.Label(card, $"<b>{entry.Name}</b>　{status}　{entry.Record.Starts}戦{entry.Record.Wins}勝（{entry.BornDay}日目〜{entry.EndedDay}日目）\n<color=#bcd>{entry.Comment}</color>", 24);
            }
        }

        private void Select(CharacterState c)
        {
            Services.Settings.SelectedCharacterId = c.Id;
            Services.SaveNow();
            Ui.Toast($"{c.Name} を選びました");
            OnShow();
        }

        private void ShowDetail(CharacterState c)
        {
            Services.Settings.SelectedCharacterId = c.Id;
            Ui.Show<CharacterDetailScreen>();
        }
    }

    /// <summary>SC-05 Character Detail（能力・適性・葉脈炉・特性・戦績・系譜）。</summary>
    public sealed class CharacterDetailScreen : UiScreen
    {
        private UnityEngine.UI.Text _left;
        private UnityEngine.UI.Text _right;

        public CharacterDetailScreen(MainUi ui) : base(ui)
        {
        }

        public override string Title => "葉っぱの詳細";

        protected override void OnBuild(RectTransform root)
        {
            var left = UiKit.CardBox("Left", root);
            left.anchorMin = new Vector2(0, 1);
            left.anchorMax = new Vector2(0.5f, 1);
            left.pivot = new Vector2(0, 1);
            left.offsetMin = new Vector2(0, left.offsetMin.y);
            left.offsetMax = new Vector2(-10, 0);
            _left = UiKit.Label(left, string.Empty, 27);

            var right = UiKit.CardBox("Right", root);
            right.anchorMin = new Vector2(0.5f, 1);
            right.anchorMax = new Vector2(1, 1);
            right.pivot = new Vector2(0, 1);
            right.offsetMin = new Vector2(10, right.offsetMin.y);
            right.offsetMax = Vector2.zero;
            _right = UiKit.Label(right, string.Empty, 27);

            var buttons = UiKit.HStack("Buttons", root, 20, 76);
            buttons.anchorMin = new Vector2(1, 0);
            buttons.anchorMax = new Vector2(1, 0);
            buttons.pivot = new Vector2(1, 0);
            buttons.sizeDelta = new Vector2(600, 76);
            UiKit.Button(buttons, "引退させる", AskRetire, UiKit.Warning, 280, 76, 30);
        }

        public override void OnShow()
        {
            var c = Ui.SelectedCharacter;
            if (c == null)
            {
                _left.text = "現役の葉っぱがいません";
                _right.text = string.Empty;
                return;
            }

            var b = Services.Balance;
            var lineage = Services.Lineage.Load();
            var training = Services.Trainings.Load();
            int day = Services.Session.Day;
            var phase = c.PhaseOn(day);

            var left = new StringBuilder();
            left.AppendLine($"<size=48><b>{c.Name}</b></size>");
            left.AppendLine($"{HomeScreen.SpeciesName(c.SpeciesId)}　{c.Generation}世代　葉統 {c.LineageId}");
            left.AppendLine($"{GrowthProfile.Label(phase)}期（成長型: {GrowthProfile.Label(c.Growth.Type)}）　生まれて{c.AgeDays(day)}日");
            left.AppendLine($"調子 {HomeScreen.ConditionLabel(c.Condition)}　疲労 {c.Fatigue}　耐久 {c.Durability}/{c.MaxDurability}");
            left.AppendLine();
            left.AppendLine("<b>能力</b>");
            foreach (StatType t in System.Enum.GetValues(typeof(StatType)))
            {
                left.AppendLine($"  {StatLabel(t),-4} <b>{UiKit.Rank(c.Stats[t], b)}</b>  ({c.Stats[t]})");
            }

            left.AppendLine("<b>葉脈炉</b>");
            left.AppendLine($"  出力 {UiKit.Rank(c.Furnace.Output, b)}  起動 {UiKit.Rank(c.Furnace.Startup, b)}  臨界 {UiKit.Rank(c.Furnace.CriticalDuration, b)}  冷却 {UiKit.Rank(c.Furnace.Cooling, b)}  炉耐久 {UiKit.Rank(c.Furnace.Durability, b)}");

            var right = new StringBuilder();
            right.AppendLine("<b>適性</b>");
            right.AppendLine($"  激流 {UiKit.Rank(c.Affinities[AffinityType.Rapid], b)}  渦 {UiKit.Rank(c.Affinities[AffinityType.Whirlpool], b)}  狭所 {UiKit.Rank(c.Affinities[AffinityType.Narrow], b)}  深水 {UiKit.Rank(c.Affinities[AffinityType.Deep], b)}");
            right.AppendLine($"  短距離 {UiKit.Rank(c.Affinities[AffinityType.Sprint], b)}  中距離 {UiKit.Rank(c.Affinities[AffinityType.Middle], b)}  長距離 {UiKit.Rank(c.Affinities[AffinityType.Long], b)}");
            right.AppendLine($"<b>性格</b>  {PersonalityLabel(c.Personality)}");
            right.AppendLine("<b>体質</b>  " + (c.BodyTraits.Count > 0 ? string.Join("・", c.BodyTraits.Select(id => training.FindBodyTrait(id)?.Name ?? id)) : "まだわからない"));
            right.AppendLine("<b>因子</b>  " + (c.Factors.Count > 0 ? string.Join("・", c.Factors.Select(f => $"{lineage.Factor(f.FactorId)?.Name ?? f.FactorId} Lv{f.Level}")) : "なし"));
            if (c.MutationId != null) right.AppendLine($"<b>特異葉</b>  <color=#ffd84a>{lineage.Mutation(c.MutationId)?.Name ?? c.MutationId}</color>");
            right.AppendLine();
            right.AppendLine("<b>戦績</b>");
            right.AppendLine($"  {c.Record.Starts}戦 {c.Record.Wins}勝 3着内{c.Record.Top3}回 リタイア{c.Record.Retirements}回");
            right.AppendLine($"  葉脈炉 最長 {c.Record.MaxFurnaceSeconds:0.0}秒");
            foreach (var h in c.Record.History.Reverse().Take(5))
            {
                right.AppendLine($"  {h.Day}日目 {h.CourseId} {(h.Retired ? "リタイア" : $"{h.Rank}着")}");
            }

            _left.text = left.ToString();
            _right.text = right.ToString();
        }

        private void AskRetire()
        {
            var c = Ui.SelectedCharacter;
            if (c == null) return;
            Ui.Confirm($"{c.Name} を引退させますか？\n\n引退すると親木に登録され、因子を安全に次の世代へ残せます。\n現役には戻れません。", () =>
            {
                var tree = Services.Retire.Retire(Services.Session, c.Id);
                Services.Settings.SelectedCharacterId = null;
                Services.SaveNow();
                Ui.Notify($"{c.Name} は引退しました。\n「{tree.Name}」が親木園に加わりました（因子 {tree.Factors.Count}個）。", () => Ui.ShowRoot<HomeScreen>());
            }, "引退させる");
        }

        public static string StatLabel(StatType t)
        {
            switch (t)
            {
                case StatType.FlowSpeed: return "流速";
                case StatType.Acceleration: return "加速";
                case StatType.Buoyancy: return "浮力";
                case StatType.Stability: return "安定";
                case StatType.Toughness: return "頑丈";
                default: return "判断";
            }
        }

        public static string PersonalityLabel(Personality p)
        {
            switch (p)
            {
                case Personality.Cautious: return "慎重";
                case Personality.Gambler: return "勝負師";
                default: return "標準";
            }
        }
    }
}
