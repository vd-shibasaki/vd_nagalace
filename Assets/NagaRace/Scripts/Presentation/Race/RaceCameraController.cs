using System.Collections.Generic;
using UnityEngine;

namespace NagaRace.Presentation.Race
{
    public enum RaceCameraMode
    {
        /// <summary>自キャラの後方から追う（既定）。</summary>
        PlayerFollow,
        /// <summary>集団全体が入るように引く。</summary>
        WidePack,
        /// <summary>自キャラのゴール後。ゴール付近を見下ろす。</summary>
        Finish,
    }

    /// <summary>
    /// レースカメラ（07 §13）。MVP は簡易追従。演出過多で順位が見えなくならないよう、
    /// どのモードでも自キャラを画面内に収めることを優先する。
    /// </summary>
    public sealed class RaceCameraController : MonoBehaviour
    {
        [SerializeField] private Camera _camera;
        [SerializeField] private float _followDistance = 15f;
        [SerializeField] private float _followHeight = 8f;
        [SerializeField] private float _lookAhead = 10f;
        [SerializeField] private float _smoothing = 4f;

        private Vector3 _velocity;
        private float _punchRemaining;
        private float _punchDuration;

        public RaceCameraMode Mode { get; set; } = RaceCameraMode.PlayerFollow;
        public Camera Camera => _camera;

        /// <summary>葉脈炉始動時の短いカメラ寄り（03 §7）。</summary>
        public void Punch(float duration)
        {
            _punchDuration = duration;
            _punchRemaining = duration;
        }

        public void SetCamera(Camera camera)
        {
            _camera = camera;
        }

        /// <summary>即座にカメラを目標位置へ移す（レース開始時など）。</summary>
        public void Snap(Vector3 playerPosition, Vector3 playerForward)
        {
            if (_camera == null) return;
            ComputeFollow(playerPosition, playerForward, out var position, out var lookAt);
            _camera.transform.position = position;
            _camera.transform.LookAt(lookAt);
            _velocity = Vector3.zero;
        }

        public void UpdateCamera(Vector3 playerPosition, Vector3 playerForward, IReadOnlyList<Vector3> packPositions, Vector3 goalPosition, float deltaTime)
        {
            if (_camera == null) return;
            _punchRemaining = Mathf.Max(0f, _punchRemaining - deltaTime);

            Vector3 position;
            Vector3 lookAt;
            switch (Mode)
            {
                case RaceCameraMode.WidePack:
                    ComputeWide(playerPosition, playerForward, packPositions, out position, out lookAt);
                    break;
                case RaceCameraMode.Finish:
                    position = goalPosition + Vector3.up * 14f - playerForward * 6f + Vector3.Cross(Vector3.up, playerForward) * 10f;
                    lookAt = Vector3.Lerp(goalPosition, playerPosition, 0.5f);
                    break;
                default:
                    ComputeFollow(playerPosition, playerForward, out position, out lookAt);
                    break;
            }

            float smoothTime = 1f / Mathf.Max(0.01f, _smoothing);
            _camera.transform.position = Vector3.SmoothDamp(_camera.transform.position, position, ref _velocity, smoothTime, Mathf.Infinity, deltaTime);
            var targetRotation = Quaternion.LookRotation(lookAt - _camera.transform.position, Vector3.up);
            _camera.transform.rotation = Quaternion.Slerp(_camera.transform.rotation, targetRotation, 1f - Mathf.Exp(-_smoothing * 1.5f * deltaTime));
        }

        private void ComputeFollow(Vector3 player, Vector3 forward, out Vector3 position, out Vector3 lookAt)
        {
            var flatForward = new Vector3(forward.x, 0f, forward.z).normalized;
            // 寄りは最大 30%。キャラを見失わないよう距離を詰めるだけにする
            float punch = _punchDuration > 0 ? Mathf.Sin(Mathf.Clamp01(_punchRemaining / _punchDuration) * Mathf.PI) * 0.3f : 0f;
            position = player - flatForward * _followDistance * (1f - punch) + Vector3.up * _followHeight * (1f - punch * 0.5f);
            lookAt = player + flatForward * _lookAhead;
        }

        private void ComputeWide(Vector3 player, Vector3 forward, IReadOnlyList<Vector3> pack, out Vector3 position, out Vector3 lookAt)
        {
            var bounds = new Bounds(player, Vector3.one);
            foreach (var p in pack)
            {
                bounds.Encapsulate(p);
            }

            var flatForward = new Vector3(forward.x, 0f, forward.z).normalized;
            float spread = Mathf.Max(bounds.size.x, bounds.size.z);
            float distance = Mathf.Clamp(spread * 0.6f + 14f, 20f, 60f);
            lookAt = bounds.center;
            position = bounds.center - flatForward * distance * 0.85f + Vector3.up * distance * 0.55f;
        }
    }
}
