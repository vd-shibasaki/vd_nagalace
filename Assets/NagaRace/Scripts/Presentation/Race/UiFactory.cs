using UnityEngine;
using UnityEngine.UI;

namespace NagaRace.Presentation.Race
{
    /// <summary>仮 HUD 用の uGUI 部品をコードで組み立てる小さなヘルパー。正式 UI（M8）では Prefab に置き換える。</summary>
    internal static class UiFactory
    {
        public static RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        public static Image Panel(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size, Color color)
        {
            var rect = Rect(name, parent, anchor, pivot, position, size);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        public static Text Label(string name, Transform parent, Font font, int fontSize, TextAnchor alignment, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size, Color color)
        {
            var rect = Rect(name, parent, anchor, pivot, position, size);
            var text = rect.gameObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = color;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.supportRichText = true;
            var outline = rect.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.6f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);
            return text;
        }

        /// <summary>2点を結ぶ細い線（Image を回転させて表現）。</summary>
        public static Image Line(string name, Transform parent, Vector2 from, Vector2 to, float thickness, Color color)
        {
            var delta = to - from;
            var rect = Rect(name, parent, Vector2.zero, new Vector2(0f, 0.5f), from, new Vector2(delta.magnitude, thickness));
            rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }
    }
}
