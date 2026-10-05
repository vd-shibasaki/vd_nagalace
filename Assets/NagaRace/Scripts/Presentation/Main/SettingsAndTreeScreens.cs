using System.Collections.Generic;
using System.Linq;
using System.Text;
using NagaRace.Domain.Lineage;
using NagaRace.Presentation.App;
using UnityEngine;
using UnityEngine.UI;

namespace NagaRace.Presentation.Main
{
    /// <summary>SC-14 Settings。</summary>
    public sealed class SettingsScreen : UiScreen
    {
        private Button _speed;
        private Button _auto;

        public SettingsScreen(MainUi ui) : base(ui)
        {
        }

        public override string Title => "設定";

        protected override void OnBuild(RectTransform root)
        {
            var stack = UiKit.VStack("Settings", root, 20, 0, expandWidth: false);
            stack.anchorMin = stack.anchorMax = new Vector2(0, 1);
            stack.pivot = new Vector2(0, 1);
            stack.sizeDelta = new Vector2(700, 600);
            _speed = UiKit.Button(stack, "レースの再生速度", NextSpeed, UiKit.Card, 600, 80);
            _auto = UiKit.Button(stack, "おまかせ炉", ToggleAuto, UiKit.Card, 600, 80);
            UiKit.Button(stack, "タイトルへ戻る", () => Ui.ShowRoot<TitleScreen>(), UiKit.Muted, 600, 80);
            UiKit.Button(stack, "セーブデータを消す", AskDelete, UiKit.Danger, 600, 80);
        }

        public override void OnShow()
        {
            UiKit.SetButtonLabel(_speed, $"レースの再生速度: x{Services.Settings.PlaybackSpeed:0}");
            UiKit.SetButtonLabel(_auto, $"おまかせ炉（既定）: {(Services.Settings.AutoFurnace ? "ON" : "OFF")}");
        }

        private void NextSpeed()
        {
            float[] speeds = { 1f, 2f, 4f };
            int index = System.Array.IndexOf(speeds, Services.Settings.PlaybackSpeed);
            Services.Settings.PlaybackSpeed = speeds[(index + 1) % speeds.Length];
            Services.SaveNow();
            OnShow();
        }

        private void ToggleAuto()
        {
            Services.Settings.AutoFurnace = !Services.Settings.AutoFurnace;
            Services.SaveNow();
            OnShow();
        }

        private void AskDelete()
        {
            Ui.Confirm("セーブデータを消しますか？元に戻せません。", () =>
            {
                Services.Save.Delete();
                Ui.ShowRoot<TitleScreen>();
            }, "消す", danger: true);
        }
    }

    /// <summary>
    /// 親木園・接ぎ木（SC-11 は Phase2 のため簡易版: 03 §1 v1.1）。
    /// 親木A・Bを選び、継承予測を見て、若葉候補3体から1体を迎える。
    /// </summary>
    public sealed class ParentTreeScreen : UiScreen
    {
        private RectTransform _trees;
        private Text _preview;
        private RectTransform _candidates;
        private string _a;
        private string _b;
        private List<GraftCandidate> _current;

        public ParentTreeScreen(MainUi ui) : base(ui)
        {
        }

        public override string Title => "親木園";

        protected override void OnBuild(RectTransform root)
        {
            var left = UiKit.Rect("TreesArea", root);
            left.anchorMin = new Vector2(0, 0);
            left.anchorMax = new Vector2(0.38f, 1);
            left.offsetMin = Vector2.zero;
            left.offsetMax = new Vector2(-10, 0);
            _trees = UiKit.ScrollList("Trees", left, 12);
            UiKit.Stretch((RectTransform)_trees.parent);

            var right = UiKit.VStack("Right", root, 14);
            right.anchorMin = new Vector2(0.38f, 0);
            right.anchorMax = new Vector2(1, 1);
            right.offsetMin = new Vector2(10, 0);
            right.offsetMax = Vector2.zero;
            var previewCard = UiKit.CardBox("Preview", right);
            _preview = UiKit.Label(previewCard, string.Empty, 26);
            UiKit.Button(right, "接ぎ木する", Graft, UiKit.Accent, 360, 80, 32);
            _candidates = UiKit.HStack("Candidates", right, 14, 360);
            _candidates.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = true;
        }

        public override void OnShow()
        {
            UiKit.Clear(_trees);
            UiKit.Clear(_candidates);
            _current = null;
            var trees = Services.Session.ParentTrees;
            if (trees.Count < 2)
            {
                UiKit.Label(_trees, "親木が2本以上あると接ぎ木できます。\n\n葉っぱを引退させると親木になります（ロストした子も親木になりますが、現役中に得た因子は残りません）。", 26, UiKit.SubText);
            }

            var lineage = Services.Lineage.Load();
            foreach (var tree in trees)
            {
                bool selected = tree.Id == _a || tree.Id == _b;
                var card = UiKit.CardBox($"Tree {tree.Id}", _trees, selected ? UiKit.CardHighlight : UiKit.Card, 14);
                string factors = tree.Factors.Count > 0 ? string.Join("・", tree.Factors.Select(f => $"{lineage.Factor(f.FactorId)?.Name}Lv{f.Level}")) : "因子なし";
                UiKit.Label(card, $"<b>{tree.Name}</b>{(tree.FromLost ? "（ロスト）" : string.Empty)}\n{HomeScreen.SpeciesName(tree.SpeciesId)} {tree.Generation}世代　{factors}", 24);
                var target = tree;
                var buttons = UiKit.HStack("Buttons", card, 12, 56);
                UiKit.Button(buttons, tree.Id == _a ? "親木A ✔" : "親木Aにする", () => { _a = target.Id; if (_b == _a) _b = null; OnShow(); }, UiKit.Muted, 220, 52, 24);
                UiKit.Button(buttons, tree.Id == _b ? "親木B ✔" : "親木Bにする", () => { _b = target.Id; if (_a == _b) _a = null; OnShow(); }, UiKit.Muted, 220, 52, 24);
            }

            _preview.text = BuildPreviewText();
        }

        private string BuildPreviewText()
        {
            if (_a == null || _b == null) return "左の一覧から親木Aと親木Bを選んでください。";
            var preview = Services.Grafting.Preview(Services.Session, _a, _b);
            var lineage = Services.Lineage.Load();
            var text = new StringBuilder();
            text.AppendLine($"<b>継承予測</b>　相性 {preview.Compatibility}");
            foreach (var pair in preview.StatRanges)
            {
                text.Append($"{NagaRace.Domain.Characters.CharacterParameter.Parse(pair.Key).Label} {pair.Value.min}〜{pair.Value.max}　");
            }

            text.AppendLine();
            if (preview.FactorChances.Count > 0)
            {
                text.AppendLine("受け継ぐかもしれない因子: " + string.Join("・", preview.FactorChances.Select(p => $"{lineage.Factor(p.Key)?.Name}（{(p.Value >= 0.6 ? "高" : p.Value >= 0.3 ? "中" : "低")}）")));
            }

            text.AppendLine(preview.MutationHint);
            return text.ToString();
        }

        private void Graft()
        {
            if (_a == null || _b == null)
            {
                Ui.Toast("親木を2本選んでください");
                return;
            }

            UiKit.Clear(_candidates);
            _current = Services.Grafting.CreateCandidates(Services.Session, _a, _b);
            var lineage = Services.Lineage.Load();
            var b = Services.Balance;
            for (int i = 0; i < _current.Count; i++)
            {
                var c = _current[i].Character;
                var card = UiKit.CardBox($"Candidate {i}", _candidates, UiKit.Card, 14);
                string factors = c.Factors.Count > 0 ? string.Join("・", c.Factors.Select(f => lineage.Factor(f.FactorId)?.Name)) : "因子なし";
                string mutation = c.MutationId != null ? $"\n<color=#ffd84a>特異葉: {lineage.Mutation(c.MutationId)?.Name}</color>" : string.Empty;
                UiKit.Label(card,
                    $"<b>候補{i + 1}</b>　{HomeScreen.SpeciesName(c.SpeciesId)}\n" +
                    $"流速 {UiKit.Rank(c.Stats.FlowSpeed, b)} 加速 {UiKit.Rank(c.Stats.Acceleration, b)} 浮力 {UiKit.Rank(c.Stats.Buoyancy, b)}\n安定 {UiKit.Rank(c.Stats.Stability, b)} 頑丈 {UiKit.Rank(c.Stats.Toughness, b)} 判断 {UiKit.Rank(c.Stats.Judgement, b)}\n" +
                    $"成長型 {NagaRace.Domain.Characters.GrowthProfile.Label(c.Growth.Type)}\n{factors}{mutation}", 24);
                int index = i;
                UiKit.Button(card, "この子を迎える", () => Adopt(index), UiKit.Accent, 260, 60, 26);
            }
        }

        /// <summary>テスト用: 親木A・Bを選ぶ。</summary>
        public void SelectForTests(string a, string b)
        {
            _a = a;
            _b = b;
            OnShow();
        }

        /// <summary>テスト用: 接ぎ木して候補を出す。</summary>
        public void GraftForTests() => Graft();

        public void Adopt(int index)
        {
            if (_current == null || index >= _current.Count) return;
            var candidate = _current[index];
            string name = NameFor(candidate.Character.SpeciesId, Services.Session.Roster.Active.Count + Services.Session.Roster.Album.Count);
            Services.Grafting.Adopt(Services.Session, candidate, name);
            Services.Settings.SelectedCharacterId = candidate.Character.Id;
            Services.SaveNow();
            Ui.Notify($"新しい若葉「{name}」を迎えた！（{candidate.Character.Generation}世代）", () => Ui.ShowRoot<HomeScreen>());
        }

        private static string NameFor(string speciesId, int count)
        {
            string[] names = { "モミジ丸", "ギンカ", "ハスネ", "サクラコ", "ドンタ", "クロハ", "コノハ", "ワカバ" };
            return names[count % names.Length] + (count / names.Length > 0 ? (count / names.Length + 1).ToString() : string.Empty);
        }
    }
}
