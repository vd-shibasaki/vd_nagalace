using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace NagaRace.Presentation.Race
{
    /// <summary>
    /// レース中の入力（07 §12 Race Action Map）。スマホのタッチ（画面上のボタン）とキーボード・ゲームパッドを同じ操作へ束ねる。
    /// 直接ステアリングは扱わない（CLAUDE.md §1）。
    /// TODO(M8): InputActionAsset（Assets/NagaRace/Settings/InputSystem_Actions）へ移し、キー設定を可能にする。
    /// </summary>
    public sealed class RaceInputController : MonoBehaviour
    {
        private InputAction _furnace;
        private InputAction _charge;

        public event Action FurnacePressed;
        public event Action ChargePressed;

        private void Awake()
        {
            _furnace = new InputAction("FurnaceToggle", InputActionType.Button);
            _furnace.AddBinding("<Keyboard>/space");
            _furnace.AddBinding("<Gamepad>/buttonSouth");
            _furnace.performed += _ => FurnacePressed?.Invoke();

            _charge = new InputAction("ChargeTap", InputActionType.Button);
            _charge.AddBinding("<Keyboard>/f");
            _charge.AddBinding("<Gamepad>/buttonWest");
            _charge.performed += _ => ChargePressed?.Invoke();

            EnsureEventSystem();
        }

        private void OnEnable()
        {
            _furnace?.Enable();
            _charge?.Enable();
        }

        private void OnDisable()
        {
            _furnace?.Disable();
            _charge?.Disable();
        }

        private void OnDestroy()
        {
            _furnace?.Dispose();
            _charge?.Dispose();
        }

        /// <summary>uGUI ボタンを Input System で押せるようにする。</summary>
        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null || FindFirstObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            DontDestroyOnLoad(go);
        }
    }
}
