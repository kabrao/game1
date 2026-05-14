using UnityEngine;

namespace BallShooter
{
    /// <summary>
    /// Simple additive camera shake. Sits on the FP camera and applies a decaying
    /// random local-position + rotation offset on top of normal look rotation.
    /// </summary>
    public class CameraShake : MonoBehaviour
    {
        public static CameraShake Instance { get; private set; }

        [Header("Tuning")]
        public float positionAmount = 0.08f;
        public float rotationAmount = 1.3f;     // degrees
        public float frequency = 22f;
        public float decay = 6f;

        Vector3 basePos;
        Quaternion baseRot;
        float trauma;       // 0..1 - shake severity
        float noiseSeed;

        void Awake()
        {
            Instance = this;
            basePos = transform.localPosition;
            baseRot = transform.localRotation;
            noiseSeed = Random.value * 1000f;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void LateUpdate()
        {
            // Always restore base position so offsets don't compound across frames
            transform.localPosition = basePos;
            transform.localRotation = baseRot;

            if (trauma <= 0f) return;

            float t = Time.time * frequency + noiseSeed;
            float ox = (Mathf.PerlinNoise(t, 0.1f) - 0.5f) * 2f;
            float oy = (Mathf.PerlinNoise(0.3f, t) - 0.5f) * 2f;
            float oz = (Mathf.PerlinNoise(t, t * 0.5f) - 0.5f) * 2f;
            float oRoll = (Mathf.PerlinNoise(t * 0.8f, 1.7f) - 0.5f) * 2f;
            float oPitch = (Mathf.PerlinNoise(2.3f, t * 0.9f) - 0.5f) * 2f;

            float k = trauma * trauma;
            Vector3 posOffset = new Vector3(ox, oy, oz) * (positionAmount * k);
            transform.localPosition = basePos + posOffset;
            transform.localRotation = baseRot *
                Quaternion.Euler(oPitch * rotationAmount * k, 0f, oRoll * rotationAmount * k);

            trauma = Mathf.Max(0f, trauma - decay * Time.deltaTime);
        }

        public void Add(float amount)
        {
            trauma = Mathf.Clamp01(trauma + amount);
        }

        public static void Kick(float amount)
        {
            if (Instance != null) Instance.Add(amount);
        }
    }
}
