using UnityEngine;

namespace BallShooter
{
    /// <summary>
    /// Drives weapon procedural feel: idle sway, walk bob, sprint bob,
    /// slide-tilt, fire recoil kick. Attach to the weapon root (child of camera).
    /// </summary>
    public class WeaponBob : MonoBehaviour
    {
        [Header("Refs")]
        public PlayerController player;          // for velocity / sprint / slide state

        [Header("Idle sway")]
        public float idleAmount = 0.008f;
        public float idleSpeed = 1.3f;

        [Header("Walk bob")]
        public float walkBobAmount = 0.022f;
        public float walkBobSpeed = 9f;
        public float sprintBobMultiplier = 1.5f;

        [Header("Slide")]
        public float slideTilt = 12f;            // degrees
        public float slideDip = 0.18f;

        [Header("Recoil")]
        public float recoilKickBack = 0.07f;
        public float recoilKickUp = 0.04f;
        public float recoilRotKick = 4f;
        public float recoilRecover = 18f;

        Vector3 basePos;
        Quaternion baseRot;
        float bobT;
        Vector3 recoilPos;
        Vector3 recoilRot;

        void Awake()
        {
            basePos = transform.localPosition;
            baseRot = transform.localRotation;
        }

        void Update()
        {
            if (player == null) player = GetComponentInParent<PlayerController>();

            // ---- target offsets ----
            Vector3 v = player != null ? player.Velocity : Vector3.zero;
            float horiz = new Vector2(v.x, v.z).magnitude;
            bool sprinting = player != null && player.IsSprinting;
            bool sliding = player != null && player.IsSliding;

            float speedT = Mathf.Clamp01(horiz / 8f);
            float bobMul = sprinting ? sprintBobMultiplier : 1f;
            bobT += Time.deltaTime * walkBobSpeed * bobMul * speedT;

            Vector3 bob = new Vector3(
                Mathf.Cos(bobT) * walkBobAmount,
                Mathf.Abs(Mathf.Sin(bobT)) * walkBobAmount * 0.5f,
                0f) * speedT;

            Vector3 idle = new Vector3(
                Mathf.Sin(Time.time * idleSpeed) * idleAmount,
                Mathf.Cos(Time.time * idleSpeed * 0.6f) * idleAmount * 0.6f,
                0f);

            Vector3 slidePos = sliding ? new Vector3(0f, -slideDip, 0f) : Vector3.zero;
            Quaternion slideRot = sliding ? Quaternion.Euler(0f, 0f, slideTilt) : Quaternion.identity;

            // ---- recoil decay ----
            recoilPos = Vector3.Lerp(recoilPos, Vector3.zero, recoilRecover * Time.deltaTime);
            recoilRot = Vector3.Lerp(recoilRot, Vector3.zero, recoilRecover * Time.deltaTime);

            // ---- apply ----
            transform.localPosition = basePos + bob + idle + slidePos + recoilPos;
            transform.localRotation = baseRot * slideRot * Quaternion.Euler(recoilRot);
        }

        public void AddRecoil(float multiplier = 1f)
        {
            recoilPos += new Vector3(
                Random.Range(-0.01f, 0.01f),
                recoilKickUp * multiplier,
                -recoilKickBack * multiplier);
            recoilRot += new Vector3(
                -recoilRotKick * multiplier,
                Random.Range(-recoilRotKick * 0.3f, recoilRotKick * 0.3f) * multiplier,
                Random.Range(-recoilRotKick * 0.5f, recoilRotKick * 0.5f) * multiplier);
        }
    }
}
