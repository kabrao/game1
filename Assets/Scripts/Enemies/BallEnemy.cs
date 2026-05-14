using UnityEngine;

namespace BallShooter
{
    /// <summary>
    /// Rolling/sliding ball that chases the player and deals contact damage.
    /// Uses a CharacterController-free, gravity-aware Rigidbody approach.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(SphereCollider))]
    public class BallEnemy : MonoBehaviour, IDamageable
    {
        [Header("Stats")]
        public float maxHealth = 50f;
        public float moveSpeed = 4f;
        public float contactDamage = 12f;
        public float contactCooldown = 0.7f;
        public float scoreValue = 10f;

        [Header("Jump / hop")]
        public float minJumpInterval = 1.1f;
        public float maxJumpInterval = 2.4f;
        public float jumpForce = 7.5f;
        public float jumpForwardBoost = 3.5f;
        public float groundCheckDistance = 0.15f;

        [Header("Refs")]
        public Transform target;       // player

        [Header("FX")]
        public Color baseColor = new Color(0.85f, 0.2f, 0.2f);
        public Color hitFlash = Color.white;

        [Header("Anti-stack")]
        public float separationRadius = 3.0f;
        public float separationStrength = 4.5f;
        [Tooltip("Random tangent bias so enemies fan around the player instead of queueing in a line. -1..1 per enemy.")]
        public float tangentBias = 0f;
        public float tangentStrength = 0.35f;

        Rigidbody rb;
        SphereCollider col;
        Renderer rend;
        Material matInstance;
        float health;
        float lastContactTime;
        float flashTimer;
        float nextJumpTime;

        public float Health => health;
        public System.Action<BallEnemy> OnDied;

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            col = GetComponent<SphereCollider>();
            rend = GetComponentInChildren<Renderer>();
            if (rend != null)
            {
                matInstance = rend.material;       // instance
                matInstance.color = baseColor;
            }
            rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            health = maxHealth;
            nextJumpTime = Time.time + Random.Range(minJumpInterval * 0.3f, maxJumpInterval);
            tangentBias = Random.Range(-1f, 1f);
            EnemyRegistry.Register(transform);
            if (SoundFX.Instance != null) SoundFX.Instance.PlayEnemySpawn(transform.position);
        }

        void OnDestroy()
        {
            EnemyRegistry.Unregister(transform);
        }

        bool IsGrounded()
        {
            // Sphere-cast a small distance below the collider
            float r = col != null ? col.radius * transform.localScale.x : 0.5f;
            Vector3 origin = transform.position + Vector3.up * 0.02f;
            return Physics.SphereCast(origin, r * 0.9f, Vector3.down, out _, groundCheckDistance + 0.05f,
                ~0, QueryTriggerInteraction.Ignore);
        }

        public void Configure(float hp, float speed, float dmg, float score, Color color)
        {
            maxHealth = hp;
            health = hp;
            moveSpeed = speed;
            contactDamage = dmg;
            scoreValue = score;
            baseColor = color;
            if (matInstance != null) matInstance.color = color;
        }

        void FixedUpdate()
        {
            if (target == null) return;
            Vector3 toTarget = target.position - transform.position;
            toTarget.y = 0f;
            Vector3 dir = toTarget.sqrMagnitude > 0.001f ? toTarget.normalized : Vector3.zero;

            // Add per-enemy tangent so the swarm fans around the player
            Vector3 tangent = new Vector3(-dir.z, 0f, dir.x) * (tangentBias * tangentStrength);
            // Separation from other enemies
            Vector3 sep = EnemyRegistry.GetSeparation(transform.position, separationRadius) * separationStrength;

            Vector3 steer = (dir + tangent + sep);
            if (steer.sqrMagnitude > 1f) steer.Normalize();

            // Drive horizontal velocity, keep gravity on Y
            Vector3 v = rb.linearVelocity;
            Vector3 desired = steer * moveSpeed;
            v.x = Mathf.MoveTowards(v.x, desired.x, 40f * Time.fixedDeltaTime);
            v.z = Mathf.MoveTowards(v.z, desired.z, 40f * Time.fixedDeltaTime);
            rb.linearVelocity = v;

            // Face direction (yaw only)
            if (dir.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.LookRotation(dir);

            // Periodic hop toward the player
            if (Time.time >= nextJumpTime && IsGrounded())
            {
                Vector3 impulse = Vector3.up * jumpForce + dir * jumpForwardBoost;
                rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
                rb.AddForce(impulse, ForceMode.VelocityChange);
                nextJumpTime = Time.time + Random.Range(minJumpInterval, maxJumpInterval);
            }
        }

        void Update()
        {
            if (flashTimer > 0f)
            {
                flashTimer -= Time.deltaTime;
                if (matInstance != null)
                    matInstance.color = Color.Lerp(baseColor, hitFlash, Mathf.Clamp01(flashTimer * 6f));
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
            flashTimer = 0.12f;
            if (matInstance != null) matInstance.color = hitFlash;
            if (rb != null) rb.AddForce(-hitNormal * 3f, ForceMode.Impulse);
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
