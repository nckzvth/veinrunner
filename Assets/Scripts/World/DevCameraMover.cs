using UnityEngine;
using UnityEngine.InputSystem; // New Input System

namespace Game.World
{
    /// <summary>
    /// Lightweight WASD camera for exploring chunks.
    /// Works with the New Input System.
    /// </summary>
    [DisallowMultipleComponent]
    public class DevCameraMover : MonoBehaviour
    {
        [Header("Movement")]
        public float speed = 8f;
        public float fastSpeed = 16f;

        [Header("Zoom (Orthographic)")]
        public float zoomSpeed = 6f;
        public float minOrthoSize = 0.5f;
        public float maxOrthoSize = 50f;

        Camera _cam;

        void Awake()
        {
            _cam = GetComponent<Camera>();
            if (!_cam) _cam = Camera.main;
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return; // no keyboard available (editor focus?)

            // WASD movement
            Vector2 dir = Vector2.zero;
            if (kb.aKey.isPressed) dir.x -= 1f;
            if (kb.dKey.isPressed) dir.x += 1f;
            if (kb.sKey.isPressed) dir.y -= 1f;
            if (kb.wKey.isPressed) dir.y += 1f;
            if (dir.sqrMagnitude > 1f) dir.Normalize();

            float s = kb.leftShiftKey.isPressed ? fastSpeed : speed;
            transform.position += (Vector3)(dir * s * Time.unscaledDeltaTime);

            // Zoom: Q/E keys and mouse wheel (orthographic)
            if (_cam && _cam.orthographic)
            {
                float zoomDelta = 0f;
                if (kb.qKey.isPressed) zoomDelta += 1f;
                if (kb.eKey.isPressed) zoomDelta -= 1f;

                var mouse = Mouse.current;
                if (mouse != null)
                {
                    // Mouse scroll: positive up, negative down
                    zoomDelta -= mouse.scroll.ReadValue().y * 0.05f;
                }

                if (Mathf.Abs(zoomDelta) > 0.0001f)
                {
                    float size = _cam.orthographicSize + (-zoomDelta * zoomSpeed * Time.unscaledDeltaTime);
                    _cam.orthographicSize = Mathf.Clamp(size, minOrthoSize, maxOrthoSize);
                }
            }
        }
    }
}
