using System;
using System.Collections.Generic;
using NagaRace.Domain.Characters;
using NagaRace.Domain.Training;
using NagaRace.Presentation.App;
using UnityEngine;
using UnityEngine.UI;

namespace NagaRace.Presentation.Main
{
    /// <summary>画面の基底クラス。Build は1回、Show は表示のたびに呼ばれる。</summary>
    public abstract class UiScreen
    {
        protected UiScreen(MainUi ui)
        {
            Ui = ui;
        }

        protected MainUi Ui { get; }
        protected GameServices Services => GameServices.Instance;
        public RectTransform Root { get; private set; }
        public abstract string Title { get; }
        /// <summary>ヘッダーに「戻る」を出すか。</summary>
        public virtual bool ShowBack => true;

        public void Build(RectTransform parent)
        {
            Root = UiKit.Rect(GetType().Name, parent);
            UiKit.Stretch(Root, 40, 120, 40, 30);
            OnBuild(Root);
            Root.gameObject.SetActive(false);
        }

        protected abstract void OnBuild(RectTransform root);

        public virtual void OnShow()
        {
        }
    }

    /// <summary>
    /// Main シーンの画面切り替え（03 §1 の SC-02〜SC-15 のうち Race 以外）。
    /// </summary>
    public sealed class MainUi : MonoBehaviour
    {
        private readonly Dictionary<Type, UiScreen> _screens = new Dictionary<Type, UiScreen>();
        private readonly Stack<UiScreen> _history = new Stack<UiScreen>();
        private RectTransform _screenRoot;
        private Text _headerTitle;
        private Text _headerInfo;
        private Button _backButton;
        private GameObject _header;
        private GameObject _dialog;
        private Text _dialogText;
        private Button _dialogOk;
        private Button _dialogCancel;
        private Action _dialogConfirm;
        private Text _toast;
        private float _toastRemaining;

        public UiScreen Current { get; private set; }
        public bool IsDialogOpen => _dialog != null && _dialog.activeSelf;

        /// <summary>画面を登録して組み立てる。</summary>
        public void Initialize(params UiScreen[] screens)
        {
            var (_, safe) = UiKit.CreateCanvas("MainCanvas", 0, transform);
            _screenRoot = safe;
            BuildHeader(safe);
            foreach (var screen in screens)
            {
                screen.Build(safe);
                _screens[screen.GetType()] = screen;
            }

            BuildDialog(safe);
            BuildToast(safe);
        }

        public T Get<T>() where T : UiScreen => (T)_screens[typeof(T)];

        public void Show<T>(bool remember = true) where T : UiScreen
        {
            var next = Get<T>();
            if (Current != null)
            {
                Current.Root.gameObject.SetActive(false);
                if (remember && Current != next) _history.Push(Current);
            }

            Current = next;
            Current.Root.gameObject.SetActive(true);
            _header.SetActive(Services_Ready());
            _backButton.gameObject.SetActive(Current.ShowBack && _history.Count > 0);
            Current.OnShow();
            RefreshHeader();
        }

        /// <summary>履歴を消してホームへ（レース後など）。</summary>
        public void ShowRoot<T>() where T : UiScreen
        {
            _history.Clear();
            if (Current != null) Current.Root.gameObject.SetActive(false);
            Current = null;
            Show<T>(remember: false);
        }

        public void Back()
        {
            if (_history.Count == 0) return;
            Current.Root.gameObject.SetActive(false);
            Current = _history.Pop();
            Current.Root.gameObject.SetActive(true);
            _backButton.gameObject.SetActive(Current.ShowBack && _history.Count > 0);
            Current.OnShow();
            RefreshHeader();
        }

        public void RefreshHeader()
        {
            _headerTitle.text = Current != null ? $"<b>{Current.Title}</b>" : string.Empty;
            var services = GameServices.Instance;
            if (services?.Session == null)
            {
                _headerInfo.text = string.Empty;
                return;
            }

            var b = services.Balance;
            var date = services.Session.Date(b);
            var weather = services.Session.TodayConditions(b);
            int nextRace = services.RaceDay.NextRaceDay(services.Session) - services.Session.Day;
            string race = nextRace == 0 ? "<color=#ffd84a>今日はレースの日</color>" : $"次の大会まで {nextRace}日";
            _headerInfo.text = $"{date.Label}　天気: {WeatherForecast.WeatherLabel(weather.Weather)}　水量: {WeatherForecast.WaterLabel(weather.WaterLevel)}　{race}";
        }

        /// <summary>確認ダイアログ（03 §4: 危険度が高い場合のみ確認）。</summary>
        public void Confirm(string message, Action onOk, string okLabel = "実行する", bool danger = false)
        {
            _dialogText.text = message;
            _dialogConfirm = onOk;
            UiKit.SetButtonLabel(_dialogOk, okLabel);
            _dialogOk.GetComponent<Image>().color = danger ? UiKit.Danger : UiKit.Accent;
            _dialogCancel.gameObject.SetActive(true);
            _dialog.SetActive(true);
        }

        /// <summary>OK だけのお知らせ。</summary>
        public void Notify(string message, Action onClose = null)
        {
            _dialogText.text = message;
            _dialogConfirm = onClose;
            UiKit.SetButtonLabel(_dialogOk, "OK");
            _dialogOk.GetComponent<Image>().color = UiKit.Accent;
            _dialogCancel.gameObject.SetActive(false);
            _dialog.SetActive(true);
        }

        /// <summary>テスト用: 開いているダイアログを OK で閉じる。</summary>
        public void PressDialogOk()
        {
            _dialog.SetActive(false);
            var action = _dialogConfirm;
            _dialogConfirm = null;
            action?.Invoke();
        }

        public void Toast(string message)
        {
            _toast.text = message;
            _toastRemaining = 2.5f;
            _toast.gameObject.SetActive(true);
        }

        private void Update()
        {
            if (_toastRemaining > 0)
            {
                _toastRemaining -= Time.unscaledDeltaTime;
                if (_toastRemaining <= 0) _toast.gameObject.SetActive(false);
            }
        }

        private static bool Services_Ready() => GameServices.Instance?.Session != null;

        private void BuildHeader(RectTransform safe)
        {
            var bar = UiKit.Panel("Header", safe, new Color(0.04f, 0.09f, 0.11f, 0.9f));
            var rect = bar.rectTransform;
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(1, 1);
            rect.pivot = new Vector2(0.5f, 1);
            rect.sizeDelta = new Vector2(0, 100);
            _header = bar.gameObject;

            _backButton = UiKit.Button(rect, "← 戻る", Back, UiKit.Muted, 180, 64, 28);
            var backRect = (RectTransform)_backButton.transform;
            backRect.anchorMin = backRect.anchorMax = new Vector2(0, 0.5f);
            backRect.pivot = new Vector2(0, 0.5f);
            backRect.anchoredPosition = new Vector2(24, 0);
            backRect.sizeDelta = new Vector2(180, 64);

            _headerTitle = UiKit.Text("Title", rect, string.Empty, 40);
            _headerTitle.rectTransform.anchorMin = new Vector2(0, 0);
            _headerTitle.rectTransform.anchorMax = new Vector2(0.35f, 1);
            _headerTitle.rectTransform.offsetMin = new Vector2(230, 0);
            _headerTitle.rectTransform.offsetMax = Vector2.zero;

            _headerInfo = UiKit.Text("Info", rect, string.Empty, 26, TextAnchor.MiddleRight, UiKit.SubText);
            _headerInfo.rectTransform.anchorMin = new Vector2(0.35f, 0);
            _headerInfo.rectTransform.anchorMax = new Vector2(1, 1);
            _headerInfo.rectTransform.offsetMin = Vector2.zero;
            _headerInfo.rectTransform.offsetMax = new Vector2(-30, 0);
            _header.SetActive(false);
        }

        private void BuildDialog(RectTransform safe)
        {
            var shade = UiKit.Panel("Dialog", safe, new Color(0, 0, 0, 0.6f));
            UiKit.Stretch(shade.rectTransform);
            _dialog = shade.gameObject;

            var box = UiKit.Panel("Box", shade.transform, UiKit.Card);
            box.rectTransform.sizeDelta = new Vector2(900, 460);
            _dialogText = UiKit.Text("Message", box.transform, string.Empty, 32, TextAnchor.MiddleCenter);
            UiKit.Stretch(_dialogText.rectTransform, 40, 30, 40, 130);

            var buttons = UiKit.HStack("Buttons", box.transform, 30, 80);
            buttons.anchorMin = new Vector2(0.5f, 0);
            buttons.anchorMax = new Vector2(0.5f, 0);
            buttons.pivot = new Vector2(0.5f, 0);
            buttons.anchoredPosition = new Vector2(0, 30);
            buttons.sizeDelta = new Vector2(620, 80);
            buttons.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;
            _dialogCancel = UiKit.Button(buttons, "やめる", () => _dialog.SetActive(false), UiKit.Muted, 280, 80);
            _dialogOk = UiKit.Button(buttons, "実行する", PressDialogOk, UiKit.Accent, 280, 80);
            _dialog.SetActive(false);
        }

        private void BuildToast(RectTransform safe)
        {
            _toast = UiKit.Text("Toast", safe, string.Empty, 34, TextAnchor.MiddleCenter);
            _toast.rectTransform.anchorMin = new Vector2(0.5f, 0);
            _toast.rectTransform.anchorMax = new Vector2(0.5f, 0);
            _toast.rectTransform.pivot = new Vector2(0.5f, 0);
            _toast.rectTransform.anchoredPosition = new Vector2(0, 60);
            _toast.rectTransform.sizeDelta = new Vector2(1200, 60);
            _toast.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.8f);
            _toast.gameObject.SetActive(false);
        }

        /// <summary>選択中の個体（いなければ先頭の現役）。</summary>
        public CharacterState SelectedCharacter
        {
            get
            {
                var s = GameServices.Instance;
                if (s?.Session == null) return null;
                var c = s.Settings.SelectedCharacterId != null ? s.Session.Roster.Find(s.Settings.SelectedCharacterId) : null;
                if (c != null && c.IsActive) return c;
                return s.Session.Roster.Active.Count > 0 ? s.Session.Roster.Active[0] : null;
            }
        }
    }
}
