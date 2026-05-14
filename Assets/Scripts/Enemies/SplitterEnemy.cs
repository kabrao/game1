using UnityEngine;

namespace BallShooter
{
    /// <summary>
    /// Green ball that splits into N smaller, faster regular balls on death.
    /// Same chase + jump behavior as BallEnemy, but visually distinct.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(SphereCollider))]
    public class SplitterEnemy : MonoBehaviour, IDamageable
    {
        [Header("Stats")]
        public float maxHealth = 60f;
        public float moveSpeed = 6.5f;        // was 3.8
        public float contactDamage = 11f;
        public float contactCooldown = 0.8f;
        public float scoreValue = 30f;

        [Header("Splitting")]
        public int splitCount = 3;
        public float splitChildHP = 18f;
        public float splitChildSpeed = 6f;

        [Header("Jump")]
        public float jumpForce = 6.5f;
        public float minJumpInterval = 1.3f;
        public float maxJumpInterval = 2.6f;

        [Header("Refs")]
        public Transform target;

        public Color baseColor = new Color(0.3f, 0.85f, 0.45f);
        public Color hitFlash = Color.white;

        [Header("Anti-stack")]
        public float separationRadius = 3f;
        public float separationStrength = 4f;
        public float tangentBias = 0f;
        public float tangentStrength = 0.35f;

        Rigidbody rb;
        SphereCollider col;
        Renderer rend;
        Material matInstance;
        float health;
        public float Health => health;       // exposed for kill detection
        float lastContactTime;
        float nextJumpTime;

        public System.Action<SplitterEnemy> OnDied;

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            col = GetComponent<SphereCollider>();
            rend = GetComponentInChildren<Renderer>();
            if (rend != null)
            {
                matInstance = rend.material;
                matInstance.color = baseColor;
                matInstance.EnableKeyword("_EMISSION");
                if (matInstance.HasProperty("_EmissionColor"))
                    matInstance.SetColor("_EmissionColor", baseColor * 1.1f);
            }
            rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            health = maxHealth;
            nextJumpTime = Time.time + Random.Range(0.4f, maxJumpInterval);
            tangentBias = Random.Range(-1f, 1f);
            EnemyRegistry.Register(transform);
            if (SoundFX.Instance != null) SoundFX.Instance.PlayEnemySpawn(transform.position);
        }

        void OnDestroy() { EnemyRegistry.Unregister(transform); }

        public void Configure(float hp, float speed, float dmg, float score, Color color, int split)
        {
            maxHealth = hp;
            health = hp;
            moveSpeed = speed;
            contactDamage = dmg;
            scoreValue = score;
            baseColor = color;
            splitCount = split;
            if (matInstance != null) matInstance.color = color;
        }

        void FixedUpdate()
        {
            if (target == null) return;
            Vector3 toTarget = target.position - transform.position;
            toTarget.y = 0f;
            Vector3 dir = toTarget.sqrMagnitude > 0.001f ? toTarget.normalized : Vector3.zero;

            Vector3 tangent = new Vector3(-dir.z, 0f, dir.x) * (tangentBias * tangentStrength);
            Vector3 sep = EnemyRegistry.GetSeparation(transform.position, separationRadius) * separationStrength;
            Vector3 steer = dir + tangent + sep;
            if (steer.sqrMagnitude > 1f) steer.Normalize();

            Vector3 v = rb.linearVelocity;
            v.x = Mathf.MoveTowards(v.x, steer.x * moveSpeed, 40f * Time.fixedDeltaTime);
            v.z = Mathf.MoveTowards(v.z, steer.z * moveSpeed, 40f * Time.fixedDeltaTime);
            rb.linearVelocity = v;

            if (dir.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.LookRotation(dir);

            bool grounded = Physics.SphereCast(transform.position + Vector3.up * 0.02f,
                (col != null ? col.radius * transform.localScale.x : 0.5f) * 0.9f,
                Vector3.down, out _, 0.2f, ~0, QueryTriggerInteraction.Ignore);
            if (grounded && Time.time >= nextJumpTime)
            {
                Vector3 impulse = Vector3.up * jumpForce + dir * 3f;
                rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
                rb.AddForce(impulse, ForceMode.VelocityChange);
                nextJumpTime = Time.time + Random.Range(minJumpInterval, maxJumpInterval);
            }
        }

        void OnCollisionStay(Collision c)
        {
            if (Time.time - lastContactTime < contactCooldown) return;
            var ph = c.collider.GetComponentInParent<PlayerHealth>();
            if (ph != null)
            {
                lastContactTime = Time.time;
                ph.TakeDamage(contactDamage, c.GetContact(0).point, c.GetContact(0).normal);
            }
        }

        public void TakeDamage(float dmg, Vector3 hitPoint, Vector3 hitNormal)
        {
            health -= dmg;
            if (matInstance != null) matInstance.color = hitFlash;
            if (rb != null) rb.AddForce(-hitNormal * 2.5f, ForceMode.Impulse);
            if (SoundFX.Instance != null && health > 0) SoundFX.Instance.PlayEnemyHit(hitPoint);
            var dots = GetComponent<HealthDots>();
            if (dots != null) dots.Refresh(health);
            if (health <= 0f) Split();
        }

        void Split()
        {
            // Spawn smaller balls via WaveManager
            var wm = FindFirstObjectByType<WaveManager>();
            if (wm != null && splitCount > 0)
            {
                for (int i = 0; i < splitCount; i++)
                {
                    float ang = (i / (float)splitCount) * Mathf.PI * 2f;
                    Vector3 offset = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * 1.6f;
                    Vector3 pos = transform.position + offset + Vector3.up * 0.5f;
                    var b = wm.SpawnBallAt(pos, splitChildHP, splitChildSpeed);
                    if (b != null)
                    {
                        // Eject child outward
                        var brb = b.GetComponent<Rigidbody>();
                        if (brb != null) brb.linearVelocity = offset.normalized * 6f + Vector3.up * 4f;
                    }
                }
            }
            if (SoundFX.Instance != null) SoundFX.Instance.PlayEnemyDeath(transform.position);
            OnDied?.Invoke(this);
            Destroy(gameObject);
        }
    }
}
