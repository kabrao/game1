using UnityEngine;

namespace BallShooter
{
    /// <summary>
    /// Plays a footstep every N units of horizontal travel while grounded.
    /// Reads accumulated step distance from PlayerController.
    /// </summary>
    public class FootstepAudio : MonoBehaviour
    {
        public PlayerController player;
        public float walkStrideDistance = 1.8f;
        public float sprintStrideDistance = 2.2f;
        public float minSpeed = 1.5f;

        void Reset()
        {
            player = GetComponent<PlayerController>();
        }

        void Update()
        {
            if (player == null || SoundFX.Instance == null) return;
            if (!player.IsGrounded) return;

            float horiz = new Vector2(player.Velocity.x, player.Velocity.z).magnitude;
            if (horiz < minSpeed) return;

            float stride = player.IsSprinting ? sprintStrideDistance : walkStrideDistance;
            if (player.StepDistance >= stride)
            {
                SoundFX.Instance.PlayFootstep(transform.position);
                player.ConsumeStepDistance();
            }
        }
    }
}
