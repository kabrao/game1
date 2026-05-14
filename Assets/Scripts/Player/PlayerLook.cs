using UnityEngine;
using UnityEngine.InputSystem;

namespace BallShooter
{
    /// <summary>
    /// Mouse look. Yaws the body (this transform) and pitches the camera holder.
    /// </summary>
    public class PlayerLook : MonoBehaviour
    {
        [Header("Look")]
        [Tooltip("Valorant-style sensitivity number. Most players use 0.2 - 1.0; default 0.4 matches mid-range Valorant feel.")]
        public float sensitivity = 0.4f;
        [Tooltip("Internal multiplier that converts the Valorant-style number to degrees-per-mouse-count. 0.063 mirrors Valorant 1.0 = ~0.063 deg/count.")]
        public float valorantScale = 0.063f;
        public float minPitch = -85f;
        public float maxPitch = 85f;

        [Header("Refs")]
        public Transform cameraHolder;        // pitched

        float yaw;
        float pitch;

        void Start()
        {
            yaw = transform.eulerAngles.y;
            if (cameraHolder != null) pitch = cameraHolder.localEulerAngles.x;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        void Update()
        {
            var mouse = Mouse.current;
            if (mouse == null) return;

            // Allow the player to free the cursor for debugging, but skip when paused
            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame && Time.timeScale > 0f)
            {
                bool locked = Cursor.lockState == CursorLockMode.Locked;
                Cursor.lockState = locked ? CursorLockMode.None : CursorLockMode.Locked;
                Cursor.visible = locked;
            }

            if (Cursor.lockState != CursorLockMode.Locked) return;

            Vector2 delta = mouse.delta.ReadValue();
            float effective = sensitivity * valorantScale;
            yaw += delta.x * effective;
            pitch -= delta.y * effective;
            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            if (cameraHolder != null)
                cameraHolder.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }
    }
}
