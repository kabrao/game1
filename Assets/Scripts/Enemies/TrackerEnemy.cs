using UnityEngine;

namespace BallShooter
{
    /// <summary>
    /// Aimlab-tracking-style enemy. Floats in the air, drifts on a noise-based
    /// random path (strafes around the player at a fixed distance). One-shot HP
    /// by default but worth more score for landing the kill on a moving target.
    /// </summary>
    [RequireComponent(typeof(SphereCollider))]
    public class TrackerEnemy : MonoBehaviour, IDamageable
    {
        [Header("Stats")]
        public float maxHealth = 1f;
        public float scoreValue = 60f;
        public float lifetime = 12f;

        [Header("Motion")]
        public float orbitRadius = 14f;
        public float orbitSpeed = 0.55f;       // radians/sec base
        public float jitterAmount = 3.2f;
        public float jitterSpeed = 1.4f;
        public float verticalOscillation = 1.1f;

        [Header("Refs")]
        public Transform target;

        public Color baseColor = new Color(0.3f, 1f, 0.85f);

        Renderer rend;
        Material matInstance;
        float health;
        public float Health => health;       // exposed for kill detection
        float spawnTime;
        float angle;
        float noiseSeed;
        Vector3 baseY;

        public System.Action<TrackerEnemy> OnDied;

        void Awake()
        {
            rend = GetComponentInChildren<Renderer>();
            if (rend != null)
            {
                matInstance = rend.material;
                matInstance.color = baseColor;
                matInstance.EnableKeyword("_EMISSION");
                if (matInstance.HasProperty("_EmissionColor"))
                    matInstance.SetColor("_EmissionColor", baseColor * 1.7f);
            }
            var rb = GetComponent<Rigidbody>();
            if (rb != null) Destroy(rb);

            health = maxHealth;
            spawnTime = Time.time;
            angle = Random.Range(0f, Mathf.PI * 2f);
            noiseSeed = Random.value * 100f;
            EnemyRegistry.Register(transform);
            if (SoundFX.Instance != null) SoundFX.Instance.PlayEnemySpawn(transform.position);
        }

        void OnDestroy() { EnemyRegistry.Unregister(transform); }

        public void Configure(float hp, float score, Color color, float radius, float speed)
        {
            maxHealth = hp;
            health = hp;
            scoreValue = score;
            orbitRadius = radius;
            orbitSpeed = speed;
            baseColor = color;
            if (matInstance != null) matInstance.color = color;
        }

        void Update()
        {
            if (Time.time - spawnTime > lifetime) { Die(); return; }
            if (target == null) return;

            angle += orbitSpeed * Time.deltaTime * (Mathf.Sin(noiseSeed + Time.time * 0.3f) * 0.5f + 1.2f);
            // Strafe along a circle, plus noise jitter to make it erratic
            float jx = (Mathf.PerlinNoise(noiseSeed, Time.time * jitterSpeed) - 0.5f) * 2f;
            float jz = (Mathf.PerlinNoise(noiseSeed + 7.3f, Time.time * jitterSpeed) - 0.5f) * 2f;
            Vector3 pos = target.position + new Vector3(
                Mathf.Cos(angle) * orbitRadius + jx * jitterAmount,
                4f + Mathf.Sin(Time.time * 0.7f + noiseSeed) * verticalOscillation,
                Mathf.Sin(angle) * orbitRadius + jz * jitterAmount);

            transform.position = Vector3.Lerp(transform.position, pos, 6f * Time.deltaTime);

            if (matInstance != null && matInstance.HasProperty("_EmissionColor"))
            {
                float pulse = 1.5f + Mathf.Sin(Time.time * 8f + spawnTime) * 0.5f;
                matInstance.SetColor("_EmissionColor", baseColor * pulse);
            }
        }

        public void TakeDamage(float dmg, Vector3 hitPoint, Vector3 hitNormal)
        {
            health -= dmg;
            if (matInstance != null) matInstance.color = Color.white;
            if (SoundFX.Instance != null && health > 0) SoundFX.Instance.PlayEnemyHit(hitPoint);
            var dots = GetComponent<HealthDots>();
            if (dots != null) dots.Refresh(health);
            if (health <= 0f) Die();
        }

        public void Die()
        {
            if (SoundFX.Instance != null) SoundFX.Instance.PlayEnemyDeath(transform.position);
            OnDied?.Invoke(this);
            Destroy(gameObject);
        }
    }
}
