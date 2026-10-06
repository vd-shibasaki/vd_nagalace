using System;
using System.Collections.Generic;
using NagaRace.Presentation.Main;
using UnityEngine;

namespace NagaRace.Presentation.App
{
    /// <summary>
    /// 初めて見る画面で1回だけ出す短い説明（M10 tutorial）。第三者が説明なしで遊べること（09 M10 完了条件）を目指す。
    /// 既読はセーブに残し、設定画面からリセットできる。
    /// </summary>
    public static class Tutorial
    {
        public const string Race = "race";

        private static readonly Dictionary<string, string> Messages = new Dictionary<string, string>
        {
            ["home"] = "<b>ようこそ、川辺の拠点へ</b>\n\n迷ったら右の <b>「今日やること」</b> のボタンを押してください。\n\n育成・休養・大会のどれかをすると <b>1日</b> 進みます。\n毎週4日目が <b>レースの日</b> です。",
            ["training"] = "<b>ならし（育成）</b>\n\nカードを選ぶと右に効果が出ます。\n<color=#ffd84a>★おすすめ</color> が最初から選ばれているので、迷ったらそのまま下のボタンを押してください。",
            ["calendar"] = "<b>大会</b>\n\nレースの日には2〜3の大会から選べます。\n下の大会で勝つと、上の大会に出られるようになります。",
            ["entry"] = "<b>出走登録</b>\n\n<b>危険度</b> を確認しましょう。「危険」以上のレースではロストすることがあります。\n<b>おまかせ炉</b> を ON にすると、葉脈炉を AI が操作します。",
            ["result"] = "<b>リザルト</b>\n\nレースのあとは耐久と疲労が変わります。\n大きなダメージを受けると、最大耐久が下がることも。",
            ["detail"] = "<b>引退</b>\n\n紅葉期になったら引退を考えましょう。\n引退すると <b>親木</b> になり、能力や因子を次の世代へつなげます。",
            ["tree"] = "<b>親木園</b>\n\n親木を2本選んで <b>接ぎ木</b> すると、若葉の候補が3体生まれます。\n親の特徴を受け継ぎますが、まったく同じにはなりません。",
            [Race] = "<b>はじめてのレース</b>\n\n葉っぱは自動で流れます。あなたは <b>勝負所</b> だけ手を出します。\n\n<b>葉力</b> が50たまったら <b>Space / 葉脈炉ボタン</b> で始動。\n臨界までのカウントを見て、もう一度押して解除。\n引っ張りすぎると <color=#ff5040>OVER FLOW</color> で故障することも。\n\n<b>葉力チャンス</b> が出たら、バーの中央で <b>F / タップ</b>。",
        };

        public static bool IsSeen(string key)
        {
            var settings = GameServices.Instance?.Settings;
            return settings == null || settings.TutorialSeen.Contains(key);
        }

        public static string Message(string key) => Messages.TryGetValue(key, out var m) ? m : null;

        /// <summary>未読なら既読にして保存し、説明文を返す（既読なら null）。</summary>
        public static string Consume(string key)
        {
            var services = GameServices.Instance;
            if (services?.Session == null || IsSeen(key)) return null;
            services.Settings.TutorialSeen.Add(key);
            services.SaveNow();
            services.Log("tutorial_step", ("key", key));
            return Message(key);
        }

        /// <summary>画面を開いたときに呼ぶ。未読なら説明ダイアログを出す。</summary>
        public static void ShowOnce(MainUi ui, string key)
        {
            if (ui.IsDialogOpen) return;
            string message = Consume(key);
            if (message != null) ui.Notify(message);
        }

        public static void ResetAll()
        {
            GameServices.Instance?.Settings.TutorialSeen.Clear();
            GameServices.Instance?.SaveNow();
        }
    }

    /// <summary>プレイ時間（session_length）を記録する。シーンをまたいで1つだけ存在する。</summary>
    public sealed class SessionTracker : MonoBehaviour
    {
        private static SessionTracker _instance;
        private float _startedAt;

        public static void Ensure()
        {
            if (_instance != null) return;
            _instance = new GameObject("SessionTracker").AddComponent<SessionTracker>();
            DontDestroyOnLoad(_instance.gameObject);
        }

        private void Start()
        {
            _startedAt = Time.realtimeSinceStartup;
            GameServices.Instance?.Log("session_start", ("platform", UnityEngine.Application.platform.ToString()));
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused) LogEnd("pause");
        }

        private void OnApplicationQuit()
        {
            LogEnd("quit");
        }

        private void LogEnd(string reason)
        {
            GameServices.Instance?.Log("session_end", ("reason", reason), ("seconds", Math.Round(Time.realtimeSinceStartup - _startedAt)));
        }
    }
}
