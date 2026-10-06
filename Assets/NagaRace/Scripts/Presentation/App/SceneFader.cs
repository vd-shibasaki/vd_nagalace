using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace NagaRace.Presentation.App
{
    /// <summary>
    /// シーン切り替えのワイプ演出（ホーム ⇔ レース）。緑の幕が横から閉じ、葉っぱが揺れている間に読み込み、反対側へ抜けていく。
    /// シーンをまたいで1つだけ存在する。時間は Time.unscaledDeltaTime ではなく Time.deltaTime を使う（テストで時間を固定できるように）。
    /// </summary>
    public sealed class SceneFader : MonoBehaviour
    {
        private const float CoverSeconds = 0.32f;
        private const float HoldSeconds = 0.12f;
        private const float RevealSeconds = 0.38f;

        private static SceneFader _instance;
        private RectTransform _curtain;
        private RectTransform _canvasRect;
        private Image _icon;
        private string _pendingScene;
        private Action _pendingAction;
        private float _time;
        private Phase _phase = Phase.Idle;

        private enum Phase
        {
            Idle,
            Covering,
            Holding,
            Revealing,
        }

        /// <summary>演出中か（テスト・入力の抑止用）。</summary>
        public static bool IsBusy => _instance != null && _instance._phase != Phase.Idle;

        /// <summary>幕を閉じてからシーンを読み込み、新しいシーンで幕を開ける。</summary>
        public static void LoadScene(string sceneName)
        {
            Ensure().Begin(sceneName, null);
        }

        /// <summary>幕を閉じてから処理を実行し、幕を開ける（同じシーン内の大きな切り替え用）。</summary>
        public static void Cover(Action whileCovered)
        {
            Ensure().Begin(null, whileCovered);
        }

        private static SceneFader Ensure()
        {
            if (_instance != null) return _instance;
            var (canvas, _) = UiKit.CreateCanvas("SceneFader", 500);
            DontDestroyOnLoad(canvas.gameObject);
            _instance = canvas.gameObject.AddComponent<SceneFader>();
            _instance._canvasRect = (RectTransform)canvas.transform;
            _instance.Build();
            return _instance;
        }

        private void Build()
        {
            var curtain = UiKit.Panel("Curtain", transform, UiKit.Accent, rounded: false);
            curtain.raycastTarget = true; // 演出中は下の UI を押せない
            _curtain = curtain.rectTransform;
            _curtain.anchorMin = Vector2.zero;
            _curtain.anchorMax = Vector2.one;
            _curtain.offsetMin = Vector2.zero;
            _curtain.offsetMax = Vector2.zero;

            _icon = UiKit.LeafIcon(_curtain, "maple", 150);
            _icon.color = Color.white;
            _icon.rectTransform.anchorMin = _icon.rectTransform.anchorMax = new Vector2(0.5f, 0.55f);
            var label = UiKit.Text("Label", _curtain, "<b>ながレース</b>", 44, TextAnchor.MiddleCenter, Color.white);
            label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(0.5f, 0.38f);
            label.rectTransform.sizeDelta = new Vector2(600, 80);
            _curtain.gameObject.SetActive(false);
        }

        private void Begin(string sceneName, Action action)
        {
            if (_phase != Phase.Idle)
            {
                // 演出中に重ねて呼ばれたら、すぐに実行する（取りこぼさない）
                if (sceneName != null) SceneManager.LoadScene(sceneName);
                action?.Invoke();
                return;
            }

            _pendingScene = sceneName;
            _pendingAction = action;
            _phase = Phase.Covering;
            _time = 0;
            _curtain.gameObject.SetActive(true);
            SetCurtain(1f);
        }

        private void Update()
        {
            if (_phase == Phase.Idle) return;
            _time += Time.deltaTime;
            _icon.rectTransform.localRotation = Quaternion.Euler(0, 0, 12f * Mathf.Sin(Time.time * 8f));

            switch (_phase)
            {
                case Phase.Covering:
                    SetCurtain(1f - EaseOut(_time / CoverSeconds));
                    if (_time >= CoverSeconds)
                    {
                        SetCurtain(0f);
                        _phase = Phase.Holding;
                        _time = 0;
                        var action = _pendingAction;
                        _pendingAction = null;
                        action?.Invoke();
                        if (_pendingScene != null)
                        {
                            string scene = _pendingScene;
                            _pendingScene = null;
                            SceneManager.LoadScene(scene);
                        }
                    }

                    break;
                case Phase.Holding:
                    if (_time >= HoldSeconds)
                    {
                        _phase = Phase.Revealing;
                        _time = 0;
                    }

                    break;
                case Phase.Revealing:
                    SetCurtain(-EaseIn(_time / RevealSeconds));
                    if (_time >= RevealSeconds)
                    {
                        _phase = Phase.Idle;
                        _curtain.gameObject.SetActive(false);
                    }

                    break;
            }
        }

        /// <summary>幕の位置: 1 = 右の画面外、0 = 全面、-1 = 左の画面外。</summary>
        private void SetCurtain(float position)
        {
            float width = _canvasRect.rect.width + 200f;
            _curtain.anchoredPosition = new Vector2(position * width, 0);
        }

        private static float EaseOut(float t)
        {
            t = Mathf.Clamp01(t);
            return 1f - (1f - t) * (1f - t) * (1f - t);
        }

        private static float EaseIn(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * t;
        }
    }
}
