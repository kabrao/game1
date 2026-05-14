using UnityEngine;

namespace BallShooter
{
    /// <summary>
    /// Big slow tanky ball. On death spawns several small balls.
    /// Periodically does a "lunge" — short burst of speed toward the player.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(SphereCollider))]
    public class BossEnemy : MonoBehaviour, IDamageable
    {
        [Header("Stats")]
        public float maxHealth = 1200f;
        public float moveSpeed = 6f;          // was 3.2
        public float lungeSpeed = 22f;        // was 12
        public float lungeInterval = 3.0f;    // was 4.5
        public float lungeDuration = 0.7f;
        public float contactDamage = 28f;
        public float contactCooldown = 0.8f;
        public float scoreValue = 250f;

        [Header("Spawn on death")]
        public int spawnOnDeath = 6;
        public float spawnChildHP = 30f;
        public float spawnChildSpeed = 5.5f;

        [Header("Refs")]
        public Transform target;

        Rigidbody rb;
        Renderer rend;
        Material matInstance;
        float health;
        public float Health => health;       // exposed for kill detection
        float lastContactTime;
        float nextLungeTime;
        float lungeUntil;

        public Color baseColor = new Color(0.6f, 0.1f, 0.7f);

        public System.Action<BossEnemy> OnDied;

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            rend = GetComponentInChildren<Renderer>();
            if (rend != null)
            {
                matInstance = rend.material;
                matInstance.color = baseColor;
                matInstance.EnableKeyword("_EMISSION");
                matInstance.SetColor("_EmissionColor", baseColor * 1.4f);
            }
            rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            health = maxHealth;
            nextLungeTime = Time.time + lungeInterval;
            EnemyRegistry.Register(transform);
            if (SoundFX.Instance != null) SoundFX.Instance.PlayBossSpawn(transform.position);
        }

        void OnDestroy() { EnemyRegistry.Unregister(transform); }

        public void Configure(float hp, float speed, float dmg, float score)
        {
            maxHealth = hp;
            health = hp;
            moveSpeed = speed;
            contactDamage = dmg;
            scoreValue = score;
        }

        void FixedUpdate()
        {
            if (target == null) return;
            Vector3 toTarget = target.position - transform.position;
            toTarget.y = 0f;
            Vector3 dir = toTarget.sqrMagnitude > 0.001f ? toTarget.normalized : Vector3.zero;

            bool lunging = Time.time < lungeUntil;
            float spd = lunging ? lungeSpeed : moveSpeed;

            Vector3 v = rb.linearVelocity;
            Vector3 desired = dir * spd;
            v.x = Mathf.MoveTowards(v.x, desired.x, 25f * Time.fixedDeltaTime);
            v.z = Mathf.MoveTowards(v.z, desired.z, 25f * Time.fixedDeltaTime);
            rb.linearVelocity = v;

            if (dir.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.LookRotation(dir);

            if (!lunging && Time.time >= nextLungeTime)
            {
                lungeUntil = Time.time + lungeDuration;
                nextLungeTime = Time.time + lungeInterval;
            }
        }

        void OnCollisionStay(Collision c)
        {
            if (Time.time - lastContactTime < contactCooldown) return;
            var dmgable = c.collider.GetComponentInParent<PlayerHealth>();
            if (dmgable != null)
            {
                lastContactTime = Time.time;
                dmgable.TakeDamage(contactDamage, c.GetContact(0).point, c.GetContact(0).normal);
            }
        }

        public void TakeDamage(float dmg, Vector3 hitPoint, Vector3 hitNormal)
        {
            health -= dmg;
            if (matInstance != null)
            {
                float t = Mathf.Clamp01(health / maxHealth);
                Color c = Color.Lerp(Color.white, baseColor, t);
                matInstance.color = c;
                matInstance.SetColor("_EmissionColor", c * 1.4f);
            }
            if (SoundFX.Instance != null && health > 0) SoundFX.Instance.PlayEnemyHit(hitPoint);
            var dots = GetComponent<HealthDots>();
            if (dots != null) dots.Refresh(health);
            if (health <= 0f) Die();
        }

        public void Die()
        {
            // Spawn small balls
            if (spawnOnDeath > 0)
            {
                var wm = FindFirstObjectByType<WaveManager>();
                for (int i = 0; i < spawnOnDeath; i++)
                {
                    Vector3 offset = new Vector3(
                        Mathf.Cos(i * (Mathf.PI * 2f / spawnOnDeath)) * 2f,
                        0.5f,
                        Mathf.Sin(i * (Mathf.PI * 2f / spawnOnDeath)) * 2f);
                    Vector3 pos = transform.position + offset;
                    if (wm != null)
                        wm.SpawnBallAt(pos, spawnChildHP, spawnChildSpeed);
                }
            }
            if (SoundFX.Instance != null) SoundFX.Instance.PlayExplosion(transform.position);
            CameraShake.Kick(0.4f);
            OnDied?.Invoke(this);
            Destroy(gameObject);
        }
    }
}
