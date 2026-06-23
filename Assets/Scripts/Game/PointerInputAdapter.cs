using System;
using Proto.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Proto.Game
{
    /// <summary>
    /// Thin input adapter — the only place that reads the device. Reads press→release pointer deltas (mouse
    /// and touch via <see cref="Pointer.current"/>) and forwards a player intent: a short press is a TAP (at
    /// its screen position), a longer drag is a SWIPE (a board-space direction). The composition root maps
    /// these to core calls; the core never sees Unity input. Most casual hooks need exactly one of these.
    /// </summary>
    public sealed class PointerInputAdapter : MonoBehaviour
    {
        [SerializeField] private float minSwipeDp = 24f; // density-independent pixels; below this is a tap

        private Camera _camera;
        private Action<Vector2> _onTap;     // screen-space position of the tap
        private Action<Vec2> _onSwipe;      // board-space (XZ) swipe direction
        private Action<Vector2, Vector2> _onSwipeFrom; // (press-start screen pos, screen-space delta)
        private Func<bool> _isBlocked;      // gameplay input suppressed while this returns true (e.g. a modal)
        private Vector2 _pressStart;
        private bool _tracking;

        public void Init(Camera camera, Action<Vector2> onTap, Action<Vec2> onSwipe = null,
                         Func<bool> isBlocked = null, Action<Vector2, Vector2> onSwipeFrom = null)
        {
            _camera = camera;
            _onTap = onTap;
            _onSwipe = onSwipe;
            _isBlocked = isBlocked;
            _onSwipeFrom = onSwipeFrom;
        }

        private void Update()
        {
            if (_isBlocked != null && _isBlocked()) { _tracking = false; return; }
            Pointer pointer = Pointer.current;
            if (pointer == null) return;

            if (pointer.press.wasPressedThisFrame)
            {
                _pressStart = pointer.position.ReadValue();
                _tracking = true;
            }
            else if (_tracking && pointer.press.wasReleasedThisFrame)
            {
                _tracking = false;
                Vector2 end = pointer.position.ReadValue();
                Vector2 delta = end - _pressStart;
                float dpScale = Screen.dpi > 0f ? Screen.dpi / 160f : 1f;

                if (delta.magnitude < minSwipeDp * dpScale)
                    _onTap?.Invoke(end);
                else if (_onSwipeFrom != null)
                    _onSwipeFrom(_pressStart, delta); // consumer needs the grabbed point (e.g. match-3 swap)
                else
                    _onSwipe?.Invoke(ScreenToBoard(delta));
            }
        }

        /// <summary>Maps a screen-space delta into board (XZ) space using the camera's yaw.</summary>
        private Vec2 ScreenToBoard(Vector2 screenDelta)
        {
            if (_camera == null) return new Vec2(screenDelta.x, screenDelta.y);
            Transform t = _camera.transform;
            Vector3 right = t.right; right.y = 0f; right.Normalize();
            Vector3 up = t.up; up.y = 0f;
            if (up.sqrMagnitude < 1e-4f) up = Vector3.ProjectOnPlane(t.forward, Vector3.up);
            up.Normalize();

            Vector3 world = right * screenDelta.x + up * screenDelta.y;
            return new Vec2(world.x, world.z);
        }
    }
}
