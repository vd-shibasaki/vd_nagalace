using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace NagaRace.Presentation.App
{
    /// <summary>
    /// タップエフェクト（参考: ウマ娘のUI調査「タップエフェクトは種類ごとに変える」）。
    /// - 決定ボタン: 黄色い葉っぱが散る
    /// - 戻る・その他の白ボタン: 白い葉っぱが散る
    /// - アイコン: 葉っぱのみ
    /// - 何もない所: 小さなキラキラ
    /// 一番手前の Canvas に描き、操作の邪魔をしない（raycast しない）。
    /// </summary>
    public sealed class TapEffectLayer : MonoBehaviour
    {
        private const float Lifetime = 0.45f;

        private readonly List<Particle> _particles = new List<Particle>();
        private readonly List<RaycastResult> _hits = new List<RaycastResult>();
        private RectTransform _root;

        private sealed class Particle
        {
            public RectTransform Rect;
            public Image Image;
            public Vector2 Velocity;
            public float Spin;
            public float Age;
            public float Scale;
        }

        public static TapEffectLayer Create(Transform parent)
        {
            var (canvas, _) = UiKit.CreateCanvas("TapEffects", 200, parent);
            canvas.GetComponent<GraphicRaycaster>().enabled = false;
            var layer = canvas.gameObject.AddComponent<TapEffectLayer>();
            layer._root = (RectTransform)canvas.transform;
            return layer;
        }

        private void Update()
        {
            var pointer = Pointer.current;
            if (pointer != null && pointer.press.wasPressedThisFrame)
            {
                Spawn(pointer.position.ReadValue());
            }

            float dt = Time.unscaledDeltaTime;
            for (int i = _particles.Count - 1; i >= 0; i--)
            {
                var p = _particles[i];
                p.Age += dt;
                float t = p.Age / Lifetime;
                if (t >= 1f)
                {
                    Destroy(p.Rect.gameObject);
                    _particles.RemoveAt(i);
                    continue;
                }

                p.Rect.anchoredPosition += p.Velocity * dt;
                p.Velocity *= 1f - 4f * dt;
                p.Rect.localRotation = Quaternion.Euler(0, 0, p.Rect.localEulerAngles.z + p.Spin * dt);
                p.Rect.localScale = Vector3.one * p.Scale * (1f - 0.4f * t);
                var c = p.Image.color;
                c.a = 1f - t * t;
                p.Image.color = c;
            }
        }

        /// <summary>画面座標の位置にエフェクトを出す（テストからも呼べる）。</summary>
        public void Spawn(Vector2 screenPosition)
        {
            var kind = KindAt(screenPosition);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, screenPosition, null, out var local);
            switch (kind)
            {
                case TapKind.Decide:
                    Burst(local, UiSprites.Leaf, new Color(1f, 0.85f, 0.25f), 7, 420f, 34f);
                    break;
                case TapKind.Back:
                    Burst(local, UiSprites.Leaf, Color.white, 6, 360f, 30f);
                    break;
                case TapKind.Icon:
                    Burst(local, UiSprites.Leaf, new Color(0.6f, 0.9f, 0.6f), 5, 320f, 28f);
                    break;
                default:
                    Burst(local, UiSprites.Star, new Color(1f, 0.97f, 0.7f), 4, 220f, 24f);
                    break;
            }
        }

        private TapKind? KindAt(Vector2 screenPosition)
        {
            var eventSystem = EventSystem.current;
            if (eventSystem == null) return null;
            _hits.Clear();
            eventSystem.RaycastAll(new PointerEventData(eventSystem) { position = screenPosition }, _hits);
            foreach (var hit in _hits)
            {
                var tag = hit.gameObject.GetComponentInParent<TapTag>();
                if (tag != null) return tag.Kind;
                if (hit.gameObject.GetComponentInParent<Selectable>() != null) return TapKind.Icon;
            }

            return null;
        }

        private void Burst(Vector2 center, Sprite sprite, Color color, int count, float speed, float size)
        {
            for (int i = 0; i < count; i++)
            {
                float angle = (i / (float)count) * Mathf.PI * 2f + Random.Range(-0.3f, 0.3f);
                var rect = UiKit.Rect("Tap", _root);
                rect.sizeDelta = new Vector2(size, size);
                rect.anchoredPosition = center;
                rect.localRotation = Quaternion.Euler(0, 0, Random.Range(0f, 360f));
                var image = rect.gameObject.AddComponent<Image>();
                image.sprite = sprite;
                image.color = color;
                image.raycastTarget = false;
                // 白い葉っぱも白い背景で見えるよう薄い縁を付ける
                var outline = rect.gameObject.AddComponent<Outline>();
                outline.effectColor = new Color(0.2f, 0.45f, 0.25f, 0.35f);
                _particles.Add(new Particle
                {
                    Rect = rect,
                    Image = image,
                    Velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed * Random.Range(0.7f, 1.1f),
                    Spin = Random.Range(-360f, 360f),
                    Scale = Random.Range(0.8f, 1.2f),
                });
            }
        }
    }
}
