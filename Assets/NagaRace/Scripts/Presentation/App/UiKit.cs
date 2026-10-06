using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace NagaRace.Presentation.App
{
    /// <summary>
    /// 仮 UI（M8）をコードで組み立てる部品集。正式 UI（M9 以降）では Prefab へ置き換える前提で、
    /// 画面ロジック（Screen クラス）からは見た目の作り方を隠す。
    /// </summary>
    public static class UiKit
    {
        public static readonly Color Background = new Color(0.07f, 0.13f, 0.16f, 0.92f);
        public static readonly Color Card = new Color(0.13f, 0.22f, 0.26f, 0.95f);
        public static readonly Color CardHighlight = new Color(0.22f, 0.36f, 0.30f, 0.98f);
        public static readonly Color Accent = new Color(0.36f, 0.72f, 0.42f);
        public static readonly Color Warning = new Color(0.95f, 0.55f, 0.2f);
        public static readonly Color Danger = new Color(0.9f, 0.25f, 0.2f);
        public static readonly Color Muted = new Color(0.32f, 0.38f, 0.40f);
        public static readonly Color TextColor = new Color(0.95f, 0.97f, 0.95f);
        public static readonly Color SubText = new Color(0.75f, 0.85f, 0.82f);

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

        public static Image Panel(string name, Transform parent, Color color)
        {
            var rect = Rect(name, parent);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            return image;
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

        public static Button Button(Transform parent, string label, Action onClick, Color? color = null, float width = 280, float height = 72, int fontSize = 30)
        {
            var image = Panel($"Button {label}", parent, color ?? Accent);
            var button = image.gameObject.AddComponent<Button>();
            var colors = button.colors;
            colors.highlightedColor = new Color(1f, 1f, 1f, 0.9f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.6f);
            button.colors = colors;
            if (onClick != null) button.onClick.AddListener(() => onClick());
            var layout = image.gameObject.AddComponent<LayoutElement>();
            layout.preferredWidth = width;
            layout.preferredHeight = height;
            layout.minHeight = height;
            var text = Text("Text", image.transform, label, fontSize, TextAnchor.MiddleCenter);
            Stretch(text.rectTransform, 8, 4, 8, 4);
            return button;
        }

        public static void SetButtonLabel(Button button, string label)
        {
            button.GetComponentInChildren<Text>().text = label;
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
            var viewport = Panel(name, parent, new Color(0, 0, 0, 0.001f));
            viewport.gameObject.AddComponent<RectMask2D>();
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.scrollSensitivity = 40f;
            var content = VStack("Content", viewport.transform, spacing);
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

        /// <summary>カード（背景付きの縦並び）。</summary>
        public static RectTransform CardBox(string name, Transform parent, Color? color = null, int padding = 18, float spacing = 6)
        {
            var image = Panel(name, parent, color ?? Card);
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
    }
}
