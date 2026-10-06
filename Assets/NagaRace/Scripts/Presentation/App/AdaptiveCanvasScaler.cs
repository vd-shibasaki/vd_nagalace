using UnityEngine;
using UnityEngine.UI;

namespace NagaRace.Presentation.App
{
    /// <summary>
    /// 画面比に合わせて CanvasScaler の基準を切り替える（01 §4 横画面 16:9 基準 / 03 §12 レスポンシブ）。
    /// - 16:9 より縦長（4:3・16:10 など）: 横幅を基準にする → 基準幅 1920 が常に確保され、左右のパネルが重ならない
    /// - 16:9 より横長（21:9 など）: 高さを基準にする → 基準高さ 1080 が常に確保され、上下が詰まらない
    /// 画面サイズが変わったときだけ再計算する。
    /// </summary>
    [RequireComponent(typeof(CanvasScaler))]
    public sealed class AdaptiveCanvasScaler : MonoBehaviour
    {
        public const float ReferenceAspect = 16f / 9f;

        private CanvasScaler _scaler;
        private Canvas _canvas;
        private Vector2 _lastSize;

        public float Aspect { get; private set; } = ReferenceAspect;

        private void Awake()
        {
            _scaler = GetComponent<CanvasScaler>();
            _canvas = GetComponent<Canvas>();
            Apply();
        }

        private void Update()
        {
            Apply();
        }

        /// <summary>現在の表示サイズで基準を決め直す（サイズが変わっていなければ何もしない）。</summary>
        public void Apply(bool force = false)
        {
            if (_scaler == null) _scaler = GetComponent<CanvasScaler>();
            if (_canvas == null) _canvas = GetComponent<Canvas>();
            var size = _canvas != null && _canvas.pixelRect.width > 0 ? _canvas.pixelRect.size : new Vector2(Screen.width, Screen.height);
            if (!force && size == _lastSize) return;
            _lastSize = size;
            if (size.x <= 0 || size.y <= 0) return;

            Aspect = size.x / size.y;
            _scaler.matchWidthOrHeight = Aspect < ReferenceAspect ? 0f : 1f;
        }
    }
}
