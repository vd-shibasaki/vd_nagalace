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
        /// <summary>切り替え演出（フェード）用。</summary>
        public CanvasGroup Group { get; private set; }
        public abstract string Title { get; }
        /// <summary>ヘッダーに「戻る」を出すか。</summary>
        public virtual bool ShowBack => true;
        /// <summary>ヘッダーを出すか（必要なときだけ出す。ホームでは隠して縦の領域を使う）。</summary>
        public virtual bool ShowHeader => true;
        /// <summary>下部メニュー（グローバルナビ）を出すか。</summary>
        public virtual bool ShowNavigation => true;
        /// <summary>白い背景を敷くか（ホームは3Dの拠点を見せるため敷かない）。</summary>
        public virtual bool ShowBackground => true;

        public void Build(RectTransform parent)
        {
            Root = UiKit.Rect(GetType().Name, parent);
            Group = Root.gameObject.AddComponent<CanvasGroup>();
            Layout();
            OnBuild(Root);
            Root.gameObject.SetActive(false);
        }

        /// <summary>ヘッダー・下部メニューの有無に合わせて表示領域を決める。</summary>
        public void Layout()
        {
            UiKit.Stretch(Root, 40, ShowHeader ? 112 : 30, 40, ShowNavigation ? MainUi.NavigationHeight + 20 : 30);
        }

        protected abstract void OnBuild(RectTransform root);

        public virtual void OnShow()
        {
        }
    }

    /// <summary>
    /// Main シーンの画面切り替え（03 §1 の SC-02〜SC-15 のうち Race 以外）。
    /// 画面の骨格: 必要なときだけ出すヘッダー / 画面本体 / 下部のグローバルナビ / 2サイズに統一したダイアログ / タップエフェクト。
    /// </summary>
    public sealed class MainUi : MonoBehaviour
    {
        public const float NavigationHeight = 120f;

        private static readonly (string label, Type screen)[] NavigationItems =
        {
            ("ホーム", typeof(HomeScreen)),
            ("育成", typeof(TrainingScreen)),
            ("大会", typeof(CalendarScreen)),
            ("葉っぱ", typeof(CharactersScreen)),
            ("親木園", typeof(ParentTreeScreen)),
        };

        private readonly Dictionary<Type, UiScreen> _screens = new Dictionary<Type, UiScreen>();
        private readonly Stack<UiScreen> _history = new Stack<UiScreen>();
        private readonly List<(Button button, Image icon, Text text)> _navButtons = new List<(Button, Image, Text)>();
        private Image _background;
        private Text _headerTitle;
        private Text _headerInfo;
        private Button _backButton;
        private GameObject _header;
        private GameObject _navigation;
        private GameObject _dialog;
        private RectTransform _dialogBox;
        private Text _dialogText;
        private Button _dialogOk;
        private Button _dialogCancel;
        private Action _dialogConfirm;
        private Text _toast;
        private GameObject _toastBox;
        private float _toastRemaining;
        private int _activeNav = -1;

        // 切り替え演出（画面: スライド＋フェード / ダイアログ: ポップ / トースト: せり上げ）
        private const float ScreenTransitionSeconds = 0.24f;
        private const float DialogPopSeconds = 0.2f;
        private const float SlideDistance = 90f;
        private UiScreen _entering;
        private Vector2 _enteringBase;
        private float _enteringDirection;
        private float _enteringTime = -1f;
        private CanvasGroup _dialogGroup;
        private float _dialogTime = -1f;
        private RectTransform _toastRect;
        private float _toastTime = -1f;

        public UiScreen Current { get; private set; }
        public bool IsDialogOpen => _dialog != null && _dialog.activeSelf;
        public TapEffectLayer TapEffects { get; private set; }

        /// <summary>画面を登録して組み立てる。</summary>
        public void Initialize(params UiScreen[] screens)
        {
            var (_, safe) = UiKit.CreateCanvas("MainCanvas", 0, transform);
            _background = UiKit.Panel("Background", safe, UiKit.Background, rounded: false);
            UiKit.Stretch(_background.rectTransform, -200, -200, -200, -200);

            foreach (var screen in screens)
            {
                screen.Build(safe);
                _screens[screen.GetType()] = screen;
            }

            BuildHeader(safe);
            BuildNavigation(safe);
            BuildToast(safe);
            BuildDialog(safe);
            TapEffects = TapEffectLayer.Create(transform);
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

            Activate(next, Current == null ? 0f : 1f);
        }

        /// <summary>履歴を消して表示する（ホーム・タイトル・リザルト）。</summary>
        public void ShowRoot<T>() where T : UiScreen
        {
            _history.Clear();
            if (Current != null) Current.Root.gameObject.SetActive(false);
            Current = null;
            Show<T>(remember: false);
        }

        /// <summary>グローバルナビからの移動。履歴は「ホーム → 移動先」にする（戻るで必ずホームへ戻れる）。</summary>
        public void Navigate(Type screen)
        {
            _history.Clear();
            if (Current != null) Current.Root.gameObject.SetActive(false);
            var home = _screens[typeof(HomeScreen)];
            if (screen != typeof(HomeScreen)) _history.Push(home);
            // 下部メニューの並び順に合わせて、右のタブへは右から、左のタブへは左から入る
            int from = NavigationIndex(Current);
            int to = NavigationIndex(_screens[screen]);
            Activate(_screens[screen], from < 0 || to < 0 || from == to ? 0f : Mathf.Sign(to - from));
        }

        public void Back()
        {
            if (_history.Count == 0) return;
            Current.Root.gameObject.SetActive(false);
            Activate(_history.Pop(), -1f);
        }

        private void Activate(UiScreen screen, float direction = 0f)
        {
            // 前の画面の演出が途中なら終わらせる
            FinishScreenTransition();
            Current = screen;
            Current.Layout();
            Current.Root.gameObject.SetActive(true);
            BeginScreenTransition(Current, direction);
            bool ready = GameServices.Instance?.Session != null;
            _header.SetActive(ready && Current.ShowHeader);
            _navigation.SetActive(ready && Current.ShowNavigation);
            _background.gameObject.SetActive(Current.ShowBackground);
            _backButton.gameObject.SetActive(Current.ShowBack && _history.Count > 0);
            UpdateNavigation();
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

            _headerInfo.text = DateLine(services);
        }

        /// <summary>日付・天気・レースまでの日数（ヘッダーとホームで共通）。</summary>
        public static string DateLine(GameServices services)
        {
            var b = services.Balance;
            var date = services.Session.Date(b);
            var weather = services.Session.TodayConditions(b);
            int nextRace = services.RaceDay.NextRaceDay(services.Session) - services.Session.Day;
            string race = nextRace == 0 ? $"<color={UiKit.HexHighlight}><b>今日はレースの日</b></color>" : $"レースまで <b>{nextRace}日</b>";
            return $"{date.Label}　{WeatherForecast.WeatherLabel(weather.Weather)}・水量{WeatherForecast.WaterLabel(weather.WaterLevel)}　{race}";
        }

        /// <summary>確認ダイアログ（03 §4: 危険度が高い場合のみ確認）。</summary>
        public void Confirm(string message, Action onOk, string okLabel = "実行する", bool danger = false)
        {
            OpenDialog(message, onOk, okLabel, danger ? UiKit.Danger : UiKit.Accent, cancel: true);
        }

        /// <summary>OK だけのお知らせ。</summary>
        public void Notify(string message, Action onClose = null)
        {
            OpenDialog(message, onClose, "OK", UiKit.Accent, cancel: false);
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
            _toastBox.SetActive(true);
            _toastTime = 0f;
        }

        /// <summary>切り替え演出をすぐに終わらせる（テスト・スクリーンショット用）。</summary>
        public void SkipTransitions()
        {
            FinishScreenTransition();
            UpdateTransitions(10f);
        }

        /// <summary>direction: 1 = 右から入る（進む）、-1 = 左から入る（戻る）、0 = その場でフェード。</summary>
        private void BeginScreenTransition(UiScreen screen, float direction)
        {
            _entering = screen;
            _enteringBase = screen.Root.anchoredPosition;
            _enteringDirection = direction;
            _enteringTime = 0f;
            ApplyScreenTransition(0f);
        }

        private void FinishScreenTransition()
        {
            if (_entering == null) return;
            ApplyScreenTransition(1f);
            _entering = null;
            _enteringTime = -1f;
        }

        private void ApplyScreenTransition(float t)
        {
            float e = EaseOutCubic(t);
            _entering.Group.alpha = Mathf.Lerp(0.15f, 1f, e);
            // 方向なし（ホーム・タイトル）は少し下から浮かび上がる
            var offset = _enteringDirection != 0f
                ? new Vector2(_enteringDirection * SlideDistance * (1f - e), 0f)
                : new Vector2(0f, -SlideDistance * 0.4f * (1f - e));
            _entering.Root.anchoredPosition = _enteringBase + offset;
        }

        private void UpdateTransitions(float dt)
        {
            if (_entering != null && _enteringTime >= 0f)
            {
                _enteringTime += dt;
                if (_enteringTime >= ScreenTransitionSeconds) FinishScreenTransition();
                else ApplyScreenTransition(_enteringTime / ScreenTransitionSeconds);
            }

            if (_dialogTime >= 0f)
            {
                _dialogTime += dt;
                float t = Mathf.Clamp01(_dialogTime / DialogPopSeconds);
                _dialogGroup.alpha = EaseOutCubic(t);
                _dialogBox.localScale = Vector3.one * Mathf.LerpUnclamped(0.86f, 1f, EaseOutBack(t));
                if (t >= 1f) _dialogTime = -1f;
            }

            if (_toastTime >= 0f && _toastBox.activeSelf)
            {
                _toastTime += dt;
                float t = Mathf.Clamp01(_toastTime / DialogPopSeconds);
                _toastRect.anchoredPosition = new Vector2(0, NavigationHeight + 30 - 50f * (1f - EaseOutCubic(t)));
                if (t >= 1f) _toastTime = -1f;
            }
        }

        private static float EaseOutCubic(float t)
        {
            t = Mathf.Clamp01(t);
            float u = 1f - t;
            return 1f - u * u * u;
        }

        private static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float u = t - 1f;
            return 1f + c3 * u * u * u + c1 * u * u;
        }

        private void Update()
        {
            UpdateTransitions(Time.unscaledDeltaTime);
            if (_toastRemaining > 0)
            {
                _toastRemaining -= Time.unscaledDeltaTime;
                if (_toastRemaining <= 0) _toastBox.SetActive(false);
            }

            // アクティブなナビのアイコンを常にゆらす（現在地が一目でわかる）
            if (_activeNav >= 0 && _activeNav < _navButtons.Count)
            {
                float t = Time.unscaledTime;
                var icon = _navButtons[_activeNav].icon.rectTransform;
                icon.localScale = Vector3.one * (1f + 0.08f * Mathf.Sin(t * 5f));
                icon.localRotation = Quaternion.Euler(0, 0, 8f * Mathf.Sin(t * 2.5f));
            }
        }

        private void OpenDialog(string message, Action onOk, string okLabel, Color okColor, bool cancel)
        {
            _dialogText.text = message;
            _dialogConfirm = onOk;
            UiKit.SetButtonLabel(_dialogOk, okLabel);
            UiKit.SetButtonColor(_dialogOk, okColor);
            _dialogCancel.gameObject.SetActive(cancel);

            // 縦サイズは2種類だけ（ボタン位置を安定させる）
            // タグを除いた文字数と改行数で、おおよその行数を見積もる
            string plain = System.Text.RegularExpressions.Regex.Replace(message, "<[^>]+>", string.Empty);
            int lines = 0;
            foreach (var line in plain.Split('\n')) lines += 1 + line.Length / 30;
            _dialogBox.sizeDelta = lines <= 4 ? new Vector2(940, 440) : new Vector2(1120, 720);
            if (!_dialog.activeSelf) _dialogTime = 0f;
            _dialog.SetActive(true);
            if (_dialogTime == 0f)
            {
                _dialogGroup.alpha = 0f;
                _dialogBox.localScale = Vector3.one * 0.86f;
            }
        }

        private void UpdateNavigation()
        {
            _activeNav = NavigationIndex(Current);
            for (int i = 0; i < _navButtons.Count; i++)
            {
                var (button, icon, text) = _navButtons[i];
                bool active = i == _activeNav;
                UiKit.SetButtonColor(button, active ? UiKit.Accent : UiKit.Muted);
                text.color = active ? Color.white : UiKit.AccentDark;
                icon.color = active ? Color.white : UiKit.Accent;
                icon.rectTransform.localScale = Vector3.one;
                icon.rectTransform.localRotation = Quaternion.identity;
            }
        }

        private static int NavigationIndex(UiScreen screen)
        {
            switch (screen)
            {
                case HomeScreen _:
                case SettingsScreen _:
                    return 0;
                case TrainingScreen _:
                    return 1;
                case CalendarScreen _:
                case EntryScreen _:
                case ResultScreen _:
                    return 2;
                case CharactersScreen _:
                case CharacterDetailScreen _:
                    return 3;
                case ParentTreeScreen _:
                    return 4;
                default:
                    return -1;
            }
        }

        private void BuildHeader(RectTransform safe)
        {
            var bar = UiKit.Panel("Header", safe, new Color(1f, 1f, 1f, 0.97f), rounded: false);
            var rect = bar.rectTransform;
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(1, 1);
            rect.pivot = new Vector2(0.5f, 1);
            rect.sizeDelta = new Vector2(0, 92);
            UiKit.AddShadow(bar.gameObject, 3f, 0.12f);
            _header = bar.gameObject;

            // 緑の下線
            var line = UiKit.Panel("Line", rect, UiKit.Accent, rounded: false).rectTransform;
            line.anchorMin = new Vector2(0, 0);
            line.anchorMax = new Vector2(1, 0);
            line.pivot = new Vector2(0.5f, 0);
            line.sizeDelta = new Vector2(0, 5);

            _backButton = UiKit.Button(rect, "‹ 戻る", Back, UiKit.Muted, 170, 60, 28);
            var backRect = (RectTransform)_backButton.transform;
            backRect.anchorMin = backRect.anchorMax = new Vector2(0, 0.5f);
            backRect.pivot = new Vector2(0, 0.5f);
            backRect.anchoredPosition = new Vector2(28, 2);
            backRect.sizeDelta = new Vector2(170, 60);

            _headerTitle = UiKit.Text("Title", rect, string.Empty, 40, TextAnchor.MiddleLeft, UiKit.AccentDark);
            _headerTitle.rectTransform.anchorMin = new Vector2(0, 0);
            _headerTitle.rectTransform.anchorMax = new Vector2(0.45f, 1);
            _headerTitle.rectTransform.offsetMin = new Vector2(230, 0);
            _headerTitle.rectTransform.offsetMax = Vector2.zero;

            _headerInfo = UiKit.Text("Info", rect, string.Empty, 26, TextAnchor.MiddleRight, UiKit.SubText);
            _headerInfo.rectTransform.anchorMin = new Vector2(0.45f, 0);
            _headerInfo.rectTransform.anchorMax = new Vector2(1, 1);
            _headerInfo.rectTransform.offsetMin = Vector2.zero;
            _headerInfo.rectTransform.offsetMax = new Vector2(-34, 0);
            _header.SetActive(false);
        }

        private void BuildNavigation(RectTransform safe)
        {
            var bar = UiKit.Panel("Navigation", safe, new Color(1f, 1f, 1f, 0.97f), rounded: false);
            var rect = bar.rectTransform;
            rect.anchorMin = new Vector2(0, 0);
            rect.anchorMax = new Vector2(1, 0);
            rect.pivot = new Vector2(0.5f, 0);
            rect.sizeDelta = new Vector2(0, NavigationHeight);
            UiKit.AddShadow(bar.gameObject, -4f, 0.12f);
            _navigation = bar.gameObject;

            var row = UiKit.HStack("Items", rect, 14, NavigationHeight - 24);
            UiKit.Stretch(row, 40, 12, 40, 12);
            var layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.childForceExpandWidth = true;
            layout.childAlignment = TextAnchor.MiddleCenter;

            foreach (var (label, screen) in NavigationItems)
            {
                var target = screen;
                var button = UiKit.Button(row, label, () => Navigate(target), UiKit.Muted, 300, NavigationHeight - 24, 28);
                var icon = UiKit.LeafIcon(button.transform, null, 54);
                icon.sprite = UiSprites.Leaf;
                var iconRect = icon.rectTransform;
                iconRect.anchorMin = iconRect.anchorMax = new Vector2(0.5f, 0.5f);
                iconRect.anchoredPosition = new Vector2(-75, 0);
                icon.GetComponent<LayoutElement>().ignoreLayout = true;
                var text = button.GetComponentInChildren<Text>();
                text.rectTransform.offsetMin = new Vector2(56, 4);
                _navButtons.Add((button, icon, text));
            }

            _navigation.SetActive(false);
        }

        private void BuildDialog(RectTransform safe)
        {
            var shade = UiKit.Panel("Dialog", safe, new Color(0.1f, 0.2f, 0.15f, 0.45f), rounded: false);
            shade.raycastTarget = true;
            UiKit.Stretch(shade.rectTransform, -200, -200, -200, -200);
            _dialog = shade.gameObject;
            _dialogGroup = _dialog.AddComponent<CanvasGroup>();

            var box = UiKit.Panel("Box", shade.transform, Color.white);
            UiKit.AddShadow(box.gameObject, 8f, 0.25f);
            _dialogBox = box.rectTransform;
            _dialogBox.sizeDelta = new Vector2(940, 440);

            // 上部の緑の帯（ダイアログであることがすぐ分かるように）
            var band = UiKit.Panel("Band", box.transform, UiKit.Accent).rectTransform;
            band.anchorMin = new Vector2(0, 1);
            band.anchorMax = new Vector2(1, 1);
            band.pivot = new Vector2(0.5f, 1);
            band.sizeDelta = new Vector2(0, 16);

            _dialogText = UiKit.Text("Message", box.transform, string.Empty, 32, TextAnchor.MiddleCenter);
            UiKit.Stretch(_dialogText.rectTransform, 50, 44, 50, 140);

            var buttons = UiKit.HStack("Buttons", box.transform, 30, 84);
            buttons.anchorMin = new Vector2(0.5f, 0);
            buttons.anchorMax = new Vector2(0.5f, 0);
            buttons.pivot = new Vector2(0.5f, 0);
            buttons.anchoredPosition = new Vector2(0, 36);
            buttons.sizeDelta = new Vector2(640, 84);
            buttons.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;
            _dialogCancel = UiKit.Button(buttons, "やめる", () => _dialog.SetActive(false), UiKit.Muted, 290, 84);
            _dialogOk = UiKit.Button(buttons, "実行する", PressDialogOk, UiKit.Accent, 290, 84);
            _dialog.SetActive(false);
        }

        private void BuildToast(RectTransform safe)
        {
            var box = UiKit.Panel("Toast", safe, new Color(0.18f, 0.3f, 0.22f, 0.92f));
            var rect = box.rectTransform;
            _toastRect = rect;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0);
            rect.pivot = new Vector2(0.5f, 0);
            rect.anchoredPosition = new Vector2(0, NavigationHeight + 30);
            rect.sizeDelta = new Vector2(1300, 72);
            _toast = UiKit.Text("Text", rect, string.Empty, 30, TextAnchor.MiddleCenter, Color.white);
            UiKit.Stretch(_toast.rectTransform, 20, 0, 20, 0);
            _toastBox = box.gameObject;
            _toastBox.SetActive(false);
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
