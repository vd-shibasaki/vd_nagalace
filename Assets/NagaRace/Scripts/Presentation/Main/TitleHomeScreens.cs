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
}
