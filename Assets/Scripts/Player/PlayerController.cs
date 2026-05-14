using UnityEngine;
using UnityEngine.InputSystem;

namespace BallShooter
{
    /// <summary>
    /// First-person FPS controller with Quake/CS-style movement:
    /// ground friction, ground accelerate, air-accelerate for strafing,
    /// and auto-bunnyhop when Space is held on landing.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Ground movement")]
        public float walkSpeed = 7f;
        public float sprintSpeed = 11f;
        public float crouchSpeed = 4f;
        public float groundAccel = 90f;           // very fast — CS-style snappy ground accel
        public float groundFriction = 8f;         // higher = stops faster
        public float stopSpeed = 2.5f;            // friction floor

        [Header("Air movement (CS-style)")]
        public float airAccel = 90f;              // strong, so strafing feels responsive
        public float airWishCap = 1.4f;           // wishspeed cap during air accel — the key to bhop strafing
        public float maxAirSpeed = 22f;           // hard ceiling so it doesn't go infinite

        [Header("Jump / Gravity")]
        public float jumpHeight = 1.45f;
        public float gravity = -22f;

        [Header("Bunnyhop")]
        [Tooltip("When holding Space, auto-jump the instant you touch the ground. CS-style.")]
        public bool autoBhop = true;
        [Tooltip("Forgiving window after landing where pressing Space still counts as a perfect bhop.")]
        public float bhopBufferTime = 0.12f;

        [Header("Crouch")]
        public float standHeight = 1.9f;
        public float crouchHeight = 1.1f;
        public float crouchLerpSpeed = 12f;

        [Header("Slide")]
        public float slideSpeed = 18f;
        public float slideDuration = 0.75f;
        public float slideCooldown = 0.4f;
        public float slideFriction = 6f;

        [Header("Refs")]
        public Transform cameraPivot;
        public Transform cameraHolder;

        // ---- State ----
        CharacterController cc;
        Vector3 velocity;          // horizontal velocity (XZ used)
        float verticalVel;
        bool isGrounded;
        bool wasGrounded;
        bool isCrouching;
        bool isSliding;
        float slideTimer;
        float slideCooldownTimer;
        Vector3 slideDir;
        float lastGroundedExitTime = -999f;
        float lastJumpPressTime = -999f;
        float landTime = -999f;

        // Footstep distance accumulator (read by audio system)
        float footstepDistance;
        public float StepDistance => footstepDistance;

        public bool IsSliding => isSliding;
        public bool IsSprinting { get; private set; }
        public Vector3 Velocity => new Vector3(velocity.x, verticalVel, velocity.z);
        public bool IsGrounded => isGrounded;

        void Awake()
        {
            cc = GetComponent<CharacterController>();
            cc.height = standHeight;
            cc.center = new Vector3(0, standHeight * 0.5f, 0);
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            wasGrounded = isGrounded;
            isGrounded = cc.isGrounded;
            if (isGrounded && !wasGrounded) landTime = Time.time;
            if (!isGrounded && wasGrounded) lastGroundedExitTime = Time.time;

            // ---- Input ----
            float x = (kb.dKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed ? 1f : 0f);
            float z = (kb.wKey.isPressed ? 1f : 0f) - (kb.sKey.isPressed ? 1f : 0f);
            Vector2 inp = new Vector2(x, z);
            if (inp.sqrMagnitude > 1f) inp.Normalize();

            bool sprintHeld = kb.leftShiftKey.isPressed;
            bool crouchHeld = kb.leftCtrlKey.isPressed || kb.cKey.isPressed;
            bool jumpHeld = kb.spaceKey.isPressed;
            bool jumpPressed = kb.spaceKey.wasPressedThisFrame;
            if (jumpPressed) lastJumpPressTime = Time.time;

            IsSprinting = sprintHeld && inp.y > 0.1f && !isCrouching;

            // ---- Slide ----
            slideCooldownTimer -= Time.deltaTime;
            bool wantSlide = sprintHeld && crouchHeld && isGrounded && inp.y > 0.1f && slideCooldownTimer <= 0f && !isSliding;
            if (wantSlide)
            {
                isSliding = true;
                slideTimer = slideDuration;
                slideCooldownTimer = slideDuration + slideCooldown;
                Vector3 fwd = cameraPivot ? cameraPivot.forward : transform.forward;
                fwd.y = 0f; fwd.Normalize();
                slideDir = fwd;
                velocity = slideDir * slideSpeed;
            }

            if (isSliding)
            {
                slideTimer -= Time.deltaTime;
                velocity = Vector3.MoveTowards(velocity, Vector3.zero, slideFriction * Time.deltaTime);
                if (slideTimer <= 0f || velocity.magnitude < walkSpeed) isSliding = false;
            }

            // ---- Crouch ----
            isCrouching = crouchHeld && !isSliding;
            float targetHeight = (isCrouching || isSliding) ? crouchHeight : standHeight;
            cc.height = Mathf.Lerp(cc.height, targetHeight, crouchLerpSpeed * Time.deltaTime);
            cc.center = new Vector3(0, cc.height * 0.5f, 0);
            if (cameraHolder != null)
            {
                float camY = cc.height - 0.25f;
                Vector3 lp = cameraHolder.localPosition;
                lp.y = Mathf.Lerp(lp.y, camY, crouchLerpSpeed * Time.deltaTime);
                cameraHolder.localPosition = lp;
            }

            // ---- Movement (skip if sliding — slide handles its own velocity) ----
            float wishSpeed = IsSprinting ? sprintSpeed : (isCrouching ? crouchSpeed : walkSpeed);
            Vector3 wishDir =
                (cameraPivot ? cameraPivot.right : transform.right) * inp.x +
                (cameraPivot ? cameraPivot.forward : transform.forward) * inp.y;
            wishDir.y = 0f;
            float wishLen = wishDir.magnitude;
            if (wishLen > 0.0001f) wishDir /= wishLen;
            else wishDir = Vector3.zero;

            // Bhop detection: if jump is held when we land (within buffer), skip ground friction
            bool bhopThisFrame = false;
            if (isGrounded && !isSliding)
            {
                bool spaceHeldOnLand = autoBhop && jumpHeld && Time.time - landTime < bhopBufferTime;
                bool perfectBhop = jumpPressed || spaceHeldOnLand;

                if (!perfectBhop)
                    ApplyFriction(groundFriction, Time.deltaTime);
                else
                    bhopThisFrame = true;

                Accelerate(wishDir, wishSpeed, groundAccel, Time.deltaTime);
            }
            else if (!isSliding)
            {
                // Air: CS-style strafing — clamp the contribution but use a big accel coefficient
                float airWish = Mathf.Min(wishSpeed, airWishCap * 10f);
                AirAccelerate(wishDir, airWish, airAccel, Time.deltaTime);
            }

            // ---- Jump (after friction so we keep speed) ----
            if (!isSliding)
            {
                bool wantJump = (jumpPressed || (autoBhop && jumpHeld)) && isGrounded;
                if (wantJump)
                {
                    verticalVel = Mathf.Sqrt(jumpHeight * -2f * gravity);
                    isGrounded = false;
                    // Cap horizontal speed so bhop can't go infinite, but allow chaining
                    Vector2 h = new Vector2(velocity.x, velocity.z);
                    if (h.magnitude > maxAirSpeed)
                    {
                        h = h.normalized * maxAirSpeed;
                        velocity.x = h.x; velocity.z = h.y;
                    }
                }
            }

            // ---- Gravity ----
            if (isGrounded && verticalVel < 0f) verticalVel = -2f;
            verticalVel += gravity * Time.deltaTime;

            // ---- Move ----
            Vector3 move = new Vector3(velocity.x, verticalVel, velocity.z);
            cc.Move(move * Time.deltaTime);

            // ---- Footstep accumulator (for audio) ----
            if (isGrounded && !isSliding)
            {
                float horiz = new Vector2(velocity.x, velocity.z).magnitude;
                footstepDistance += horiz * Time.deltaTime;
            }
            else
            {
                footstepDistance = 0f;
            }

            _ = bhopThisFrame; // reserved for future tuning hooks
        }

        // Quake-style ground friction
        void ApplyFriction(float friction, float dt)
        {
            float speed = new Vector2(velocity.x, velocity.z).magnitude;
            if (speed < 0.01f) { velocity.x = 0f; velocity.z = 0f; return; }
            float control = speed < stopSpeed ? stopSpeed : speed;
            float drop = control * friction * dt;
            float newSpeed = Mathf.Max(0f, speed - drop);
            float scale = newSpeed / speed;
            velocity.x *= scale;
            velocity.z *= scale;
        }

        // Quake-style accelerate (used on ground)
        void Accelerate(Vector3 wishDir, float wishSpeed, float accel, float dt)
        {
            if (wishDir.sqrMagnitude < 0.0001f) return;
            float currentSpeed = velocity.x * wishDir.x + velocity.z * wishDir.z;
            float addSpeed = wishSpeed - currentSpeed;
            if (addSpeed <= 0f) return;
            float accelSpeed = accel * dt * wishSpeed;
            if (accelSpeed > addSpeed) accelSpeed = addSpeed;
            velocity.x += wishDir.x * accelSpeed;
            velocity.z += wishDir.z * accelSpeed;
        }

        // CS-style air accelerate — small wishSpeed cap is what allows strafing
        void AirAccelerate(Vector3 wishDir, float wishSpeedCapped, float accel, float dt)
        {
            if (wishDir.sqrMagnitude < 0.0001f) return;
            float wishSpd = Mathf.Min(wishSpeedCapped, airWishCap * 8f); // hard cap
            float currentSpeed = velocity.x * wishDir.x + velocity.z * wishDir.z;
            float addSpeed = wishSpd - currentSpeed;
            if (addSpeed <= 0f) return;
            float accelSpeed = accel * dt * wishSpd;
            if (accelSpeed > addSpeed) accelSpeed = addSpeed;
            velocity.x += wishDir.x * accelSpeed;
            velocity.z += wishDir.z * accelSpeed;
        }

        /// <summary>Called by audio system to reset step accumulator each footstep.</summary>
        public void ConsumeStepDistance() { footstepDistance = 0f; }
    }
}
