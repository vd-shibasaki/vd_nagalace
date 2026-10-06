using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace NagaRace.Presentation.App
{
    /// <summary>タップエフェクトの種類（意思決定 / 戻る・その他 / アイコン）。</summary>
    public enum TapKind
    {
        Decide,
        Back,
        Icon,
    }

    /// <summary>ボタンに付けて、タップエフェクトの種類を伝える。</summary>
    public sealed class TapTag : MonoBehaviour
    {
        [SerializeField] private TapKind _kind;

        public TapKind Kind
        {
            get => _kind;
            set => _kind = value;
        }
    }

    /// <summary>
    /// UI 部品集。配色は「白 70% / 緑 25% / アクセント 5%」を基本にする（参考: ウマ娘のUI調査）。
    /// - ボタンは緑（決定）と白（戻る・その他）の2種類。押せるものにはすべて影を付けて立体感を出す
    /// - 角丸・丸ゴシックで「ゆるい・かわいい」世界観に合わせる
    /// 正式 UI（M9 以降）では Prefab へ置き換える前提で、画面ロジックからは見た目の作り方を隠す。
    /// </summary>
    public static class UiKit
    {
        // ---- 配色 ----
        public static readonly Color Background = new Color32(0xF4, 0xF8, 0xF2, 0xF8);
        public static readonly Color Card = new Color32(0xFF, 0xFF, 0xFF, 0xF4);
        public static readonly Color CardHighlight = new Color32(0xE2, 0xF4, 0xE5, 0xFA);
        /// <summary>メイン（緑）。決定ボタン。</summary>
        public static readonly Color Accent = new Color32(0x3F, 0xAE, 0x5A, 0xFF);
        public static readonly Color AccentDark = new Color32(0x2C, 0x86, 0x44, 0xFF);
        /// <summary>アクセント（オレンジ）。レースの日・おすすめなど、ここぞという所だけ。</summary>
        public static readonly Color Warning = new Color32(0xF0, 0x8A, 0x24, 0xFF);
        public static readonly Color Danger = new Color32(0xE2, 0x55, 0x3C, 0xFF);
        /// <summary>白ボタン（戻る・その他）。</summary>
        public static readonly Color Muted = new Color32(0xFF, 0xFF, 0xFF, 0xFF);
        public static readonly Color TextColor = new Color32(0x2E, 0x3D, 0x36, 0xFF);
        public static readonly Color SubText = new Color32(0x6D, 0x7D, 0x76, 0xFF);
        public static readonly Color Line = new Color32(0xD5, 0xE3, 0xD8, 0xFF);

        // リッチテキスト用の色
        public const string HexAccent = "#2c8644";
        public const string HexHighlight = "#e0761a";
        public const string HexGood = "#2f9e4f";
        public const string HexWarn = "#e07a1f";
        public const string HexBad = "#d9442b";
        public const string HexInfo = "#3f72c4";
        public const string HexMuted = "#8a9891";

        /// <summary>UI の文字に使うフォント（Bootstrap が設定する）。</summary>
        public static Font Font { get; set; }

        public static (Canvas canvas, RectTransform safeRoot) CreateCanvas(string name, int sortingOrder, Transform parent = null)
        {
            var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            if (parent != null) go.transform.SetParent(parent, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<AdaptiveCanvasScaler>();
            var safe = Rect("SafeArea", go.transform);
            Stretch(safe);
            safe.gameObject.AddComponent<Race.SafeAreaFitter>();
            EnsureEventSystem();
            return (canvas, safe);
        }

        /// <summary>uGUI ボタンを Input System で押せるようにする。</summary>
        public static void EnsureEventSystem()
        {
            if (EventSystem.current != null || UnityEngine.Object.FindFirstObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            UnityEngine.Object.DontDestroyOnLoad(go);
        }

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        public static void Stretch(RectTransform rect, float left = 0, float top = 0, float right = 0, float bottom = 0)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }

        /// <summary>角丸のパネル。</summary>
        public static Image Panel(string name, Transform parent, Color color, bool rounded = true)
        {
            var rect = Rect(name, parent);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            if (rounded)
            {
                image.sprite = UiSprites.Rounded;
                image.type = Image.Type.Sliced;
                image.pixelsPerUnitMultiplier = 2.2f;
            }

            return image;
        }

        /// <summary>柔らかい影（押せるもの・カードの立体感）。</summary>
        public static void AddShadow(GameObject go, float distance = 5f, float alpha = 0.18f)
        {
            var shadow = go.AddComponent<Shadow>();
            shadow.effectColor = new Color(0.1f, 0.2f, 0.12f, alpha);
            shadow.effectDistance = new Vector2(0, -distance);
        }

        public static Text Text(string name, Transform parent, string text, int size, TextAnchor align = TextAnchor.MiddleLeft, Color? color = null)
        {
            var rect = Rect(name, parent);
            var t = rect.gameObject.AddComponent<Text>();
            t.font = Font;
            t.fontSize = size;
            t.alignment = align;
            t.color = color ?? TextColor;
            t.text = text;
            t.supportRichText = true;
            t.raycastTarget = false;
            t.lineSpacing = 1.05f;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        /// <summary>文字に合わせて高さが決まるラベル（縦並びの中で使う）。</summary>
        public static Text Label(Transform parent, string text, int size = 26, Color? color = null, TextAnchor align = TextAnchor.UpperLeft)
        {
            var t = Text("Label", parent, text, size, align, color);
            t.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return t;
        }

        /// <summary>
        /// ボタン。color が緑・オレンジ・赤なら「決定ボタン」（塗り＋白文字）、それ以外は「白ボタン」（白地＋緑文字＋緑の縁）。
        /// </summary>
        public static Button Button(Transform parent, string label, Action onClick, Color? color = null, float width = 280, float height = 72, int fontSize = 30)
        {
            var fill = color ?? Accent;
            bool decide = IsDecideColor(fill);
            var image = Panel($"Button {label}", parent, decide ? fill : Muted);
            image.raycastTarget = true;
            var button = image.gameObject.AddComponent<Button>();
            var colors = button.colors;
            colors.highlightedColor = new Color(0.96f, 0.96f, 0.96f, 1f);
            colors.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
            colors.disabledColor = new Color(0.75f, 0.75f, 0.75f, 0.7f);
            button.colors = colors;
            if (onClick != null) button.onClick.AddListener(() => onClick());
            AddShadow(image.gameObject, decide ? 6f : 4f, decide ? 0.25f : 0.16f);
            var outline = image.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(Accent.r, Accent.g, Accent.b, 0.55f);
            outline.effectDistance = new Vector2(2f, -2f);
            outline.enabled = !decide;

            image.gameObject.AddComponent<TapTag>().Kind = decide ? TapKind.Decide : TapKind.Back;
            var layout = image.gameObject.AddComponent<LayoutElement>();
            layout.preferredWidth = width;
            layout.preferredHeight = height;
            layout.minHeight = height;
            var text = Text("Text", image.transform, $"<b>{label}</b>", fontSize, TextAnchor.MiddleCenter, decide ? Color.white : AccentDark);
            Stretch(text.rectTransform, 8, 4, 8, 4);
            return button;
        }

        /// <summary>ボタンの見た目を決定 / 白に切り替える（状態で色が変わるボタン用）。</summary>
        public static void SetButtonColor(Button button, Color color)
        {
            bool decide = IsDecideColor(color);
            button.GetComponent<Image>().color = decide ? color : Muted;
            var text = button.GetComponentInChildren<Text>();
            if (text != null) text.color = decide ? Color.white : AccentDark;
            var outline = button.GetComponent<Outline>();
            if (outline != null) outline.enabled = !decide;
            var tag = button.GetComponent<TapTag>();
            if (tag != null) tag.Kind = decide ? TapKind.Decide : TapKind.Back;
        }

        public static void SetButtonLabel(Button button, string label)
        {
            button.GetComponentInChildren<Text>().text = $"<b>{label}</b>";
        }

        public static RectTransform VStack(string name, Transform parent, float spacing = 12, int padding = 0, bool expandWidth = true)
        {
            var rect = Rect(name, parent);
            var layout = rect.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.padding = new RectOffset(padding, padding, padding, padding);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = expandWidth;
            layout.childForceExpandHeight = false;
            layout.childAlignment = TextAnchor.UpperLeft;
            return rect;
        }

        public static RectTransform HStack(string name, Transform parent, float spacing = 12, float height = 72)
        {
            var rect = Rect(name, parent);
            var layout = rect.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = spacing;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;
            layout.childAlignment = TextAnchor.MiddleLeft;
            var element = rect.gameObject.AddComponent<LayoutElement>();
            element.preferredHeight = height;
            element.minHeight = height;
            // 縦並びの中で余った高さを受け取らない（タブやボタン列が引き伸ばされないように）
            element.flexibleHeight = 0;
            return rect;
        }

        /// <summary>縦スクロールのリスト。戻り値の content に子を追加する。</summary>
        public static RectTransform ScrollList(string name, Transform parent, float spacing = 12)
        {
            var viewport = Panel(name, parent, new Color(0, 0, 0, 0.001f), rounded: false);
            viewport.raycastTarget = true;
            viewport.gameObject.AddComponent<RectMask2D>();
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.scrollSensitivity = 40f;
            var content = VStack("Content", viewport.transform, spacing);
            content.GetComponent<VerticalLayoutGroup>().padding = new RectOffset(6, 6, 4, 12);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1);
            content.offsetMin = Vector2.zero;
            content.offsetMax = Vector2.zero;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = (RectTransform)viewport.transform;
            scroll.content = content;
            return content;
        }

        /// <summary>カード（白い角丸＋影の縦並び）。</summary>
        public static RectTransform CardBox(string name, Transform parent, Color? color = null, int padding = 22, float spacing = 6)
        {
            var image = Panel(name, parent, color ?? Card);
            AddShadow(image.gameObject, 4f, 0.12f);
            var layout = image.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.padding = new RectOffset(padding, padding, padding, padding);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            image.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return image.rectTransform;
        }

        /// <summary>顔つきの葉っぱアイコン（ちびキャラの代わり）。系統の色に染める。</summary>
        public static Image LeafIcon(Transform parent, string speciesId, float size)
        {
            var rect = Rect("LeafIcon", parent);
            rect.sizeDelta = new Vector2(size, size);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = UiSprites.LeafFace;
            image.color = SpeciesColor(speciesId);
            image.preserveAspect = true;
            image.raycastTarget = false;
            var element = rect.gameObject.AddComponent<LayoutElement>();
            element.preferredWidth = size;
            element.preferredHeight = size;
            element.minWidth = size;
            return image;
        }

        /// <summary>系統ごとの色（キャラごとに UI の色を変える）。</summary>
        public static Color SpeciesColor(string speciesId)
        {
            switch (speciesId)
            {
                case "maple": return new Color32(0xE5, 0x53, 0x3D, 0xFF);
                case "ginkgo": return new Color32(0xE9, 0xB5, 0x2F, 0xFF);
                case "lotus": return new Color32(0x4C, 0xAF, 0x6A, 0xFF);
                case "sakura": return new Color32(0xEC, 0x8F, 0xB0, 0xFF);
                case "acorn": return new Color32(0x9A, 0x6B, 0x3F, 0xFF);
                case "bad_leaf": return new Color32(0x7A, 0x5A, 0xA6, 0xFF);
                default: return Accent;
            }
        }

        /// <summary>系統色を薄めた背景色。</summary>
        public static Color SpeciesTint(string speciesId, float amount = 0.18f)
        {
            var c = SpeciesColor(speciesId);
            return new Color(Mathf.Lerp(1f, c.r, amount), Mathf.Lerp(1f, c.g, amount), Mathf.Lerp(1f, c.b, amount), 0.97f);
        }

        public static void Clear(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                UnityEngine.Object.Destroy(parent.GetChild(i).gameObject);
            }
        }

        /// <summary>能力値の S〜E 表示（02 §4）。</summary>
        public static string Rank(int value, NagaRace.Domain.Balance.BalanceConfig balance)
        {
            var t = balance.Stats.RankThresholds;
            if (value >= t["S"]) return "S";
            if (value >= t["A"]) return "A";
            if (value >= t["B"]) return "B";
            if (value >= t["C"]) return "C";
            if (value >= t["D"]) return "D";
            return "E";
        }

        public static string Plus(double expected)
        {
            if (expected >= 2.5) return "+++";
            if (expected >= 1.2) return "++";
            if (expected > 0.2) return "+";
            return "±";
        }

        /// <summary>決定ボタンの色か（緑・オレンジ・赤）。白・半透明は白ボタン扱い。</summary>
        private static bool IsDecideColor(Color c)
        {
            float max = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
            float min = Mathf.Min(c.r, Mathf.Min(c.g, c.b));
            return c.a > 0.9f && max - min > 0.25f;
        }
    }
}
