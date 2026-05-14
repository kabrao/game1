using UnityEngine;

namespace BallShooter
{
    /// <summary>
    /// Gridshot-style stationary target. Floats at fixed height, doesn't chase
    /// or damage the player, dies in one shot (or in 1-2 dots' worth).
    /// Awards bonus score for fast clears.
    /// </summary>
    [RequireComponent(typeof(SphereCollider))]
    public class StaticTargetEnemy : MonoBehaviour, IDamageable
    {
        [Header("Stats")]
        public float maxHealth = 1f;          // one-tap by default
        public float lifetime = 8f;            // self-despawn so they don't pile up
        public float scoreValue = 40f;

        public Color baseColor = new Color(0.95f, 0.9f, 0.55f);

        Renderer rend;
        Material matInstance;
        float health;
        public float Health => health;       // exposed for kill detection
        float spawnTime;

        public System.Action<StaticTargetEnemy> OnDied;

        void Awake()
        {
            rend = GetComponentInChildren<Renderer>();
            if (rend != null)
            {
                matInstance = rend.material;
                matInstance.color = baseColor;
                matInstance.EnableKeyword("_EMISSION");
                if (matInstance.HasProperty("_EmissionColor"))
                    matInstance.SetColor("_EmissionColor", baseColor * 1.6f);
            }
            // No Rigidbody — static. No gravity. The collider on the sphere is enough.
            // Make sure any leftover Rigidbody is removed (in case the primitive spawn-helper added one).
            var rb = GetComponent<Rigidbody>();
            if (rb != null) Destroy(rb);

            health = maxHealth;
            spawnTime = Time.time;
            EnemyRegistry.Register(transform);
            if (SoundFX.Instance != null) SoundFX.Instance.PlayEnemySpawn(transform.position);
        }

        void OnDestroy() { EnemyRegistry.Unregister(transform); }

        public void Configure(float hp, float score, Color color)
        {
            maxHealth = hp;
            health = hp;
            scoreValue = score;
            baseColor = color;
            if (matInstance != null) matInstance.color = color;
        }

        void Update()
        {
            // Pulsate gently so the player can see it from a distance
            if (matInstance != null && matInstance.HasProperty("_EmissionColor"))
            {
                float pulse = 1.3f + Mathf.Sin(Time.time * 3f + spawnTime) * 0.5f;
                matInstance.SetColor("_EmissionColor", baseColor * pulse);
            }
            if (Time.time - spawnTime > lifetime) Die();
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
