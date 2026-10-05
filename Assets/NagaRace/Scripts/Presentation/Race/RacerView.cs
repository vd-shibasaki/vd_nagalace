using UnityEngine;

namespace NagaRace.Presentation.Race
{
    /// <summary>
    /// 出走者1体の見た目（仮モデル: 平たい楕円体の葉 + 自キャラの目印）。
    /// 正式モデル（06）に差し替えるときは、このコンポーネントの子だけを置き換える。
    /// </summary>
    public sealed class RacerView : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private Transform _body;
        private Transform _marker;
        private Transform _ring;
        private float _bobPhase;

        public bool IsPlayer { get; private set; }
        public Color Color { get; private set; }

        public static RacerView Create(string name, Color color, bool isPlayer, Material bodyMaterial, Material markerMaterial, int index)
        {
            var root = new GameObject(name);
            var view = root.AddComponent<RacerView>();
            view.IsPlayer = isPlayer;
            view.Color = color;
            view._bobPhase = index * 0.7f;

            // 葉っぱ本体（平たい楕円）。自キャラは一回り大きい
            float scale = isPlayer ? 1.35f : 1f;
            view._body = CreatePart(PrimitiveType.Sphere, "Body", root.transform, bodyMaterial, color);
            view._body.localScale = new Vector3(1.2f, 0.35f, 1.6f) * scale;
            view._body.localPosition = new Vector3(0f, 0.2f, 0f);

            // 葉先（進行方向が分かるように）
            var tip = CreatePart(PrimitiveType.Cube, "Tip", view._body, bodyMaterial, color * 0.85f);
            tip.localScale = new Vector3(0.35f, 0.6f, 0.35f);
            tip.localPosition = new Vector3(0f, 0f, 0.55f);
            tip.localRotation = Quaternion.Euler(0f, 45f, 0f);

            if (isPlayer)
            {
                // H-01: 自キャラは常に識別できる（頭上の目印 + 足元のリング）
                view._marker = CreatePart(PrimitiveType.Cube, "PlayerMarker", root.transform, markerMaterial, new Color(1f, 0.85f, 0.1f));
                view._marker.localScale = new Vector3(0.7f, 0.7f, 0.7f);
                view._ring = CreatePart(PrimitiveType.Cylinder, "PlayerRing", root.transform, markerMaterial, new Color(1f, 0.85f, 0.1f, 0.8f));
                view._ring.localScale = new Vector3(3.2f, 0.02f, 3.2f);
                view._ring.localPosition = new Vector3(0f, 0.02f, 0f);
            }

            return view;
        }

        public void SetPose(Vector3 position, Vector3 forward)
        {
            transform.position = position;
            if (forward.sqrMagnitude > 1e-6f)
            {
                transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
            }
        }

        private void Update()
        {
            // 水に浮いているゆるい上下動（06 §9 FloatIdle の代わり）
            float t = Time.time * 2.2f + _bobPhase;
            if (_body != null)
            {
                _body.localPosition = new Vector3(0f, 0.2f + Mathf.Sin(t) * 0.08f, 0f);
                _body.localRotation = Quaternion.Euler(Mathf.Sin(t * 0.8f) * 4f, 0f, Mathf.Cos(t) * 5f);
            }

            if (_marker != null)
            {
                _marker.localPosition = new Vector3(0f, 3.2f + Mathf.Sin(Time.time * 4f) * 0.25f, 0f);
                _marker.localRotation = Quaternion.Euler(45f, Time.time * 120f, 45f);
            }
        }

        private static Transform CreatePart(PrimitiveType type, string name, Transform parent, Material material, Color color)
        {
            var go = GameObject.CreatePrimitive(type);
            Destroy(go.GetComponent<Collider>());
            go.name = name;
            go.transform.SetParent(parent, false);
            var renderer = go.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            var block = new MaterialPropertyBlock();
            block.SetColor(BaseColorId, color);
            renderer.SetPropertyBlock(block);
            return go.transform;
        }
    }
}
