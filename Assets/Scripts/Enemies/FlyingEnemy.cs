using UnityEngine;

namespace BallShooter
{
    /// <summary>
    /// Hovering ball. Ignores gravity, orbits/chases the player at altitude,
    /// and periodically dive-bombs the player's last known position.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(SphereCollider))]
    public class FlyingEnemy : MonoBehaviour, IDamageable
    {
        [Header("Stats")]
        public float maxHealth = 25f;
        public float chaseSpeed = 10f;       // was 6.5
        public float contactDamage = 14f;
        public float contactCooldown = 0.55f;
        public float scoreValue = 25f;

        [Header("Flight")]
        public float hoverHeight = 5.5f;
        public float hoverHeightJitter = 1.5f;
        public float orbitRadius = 8f;
        public float orbitSpeed = 1.6f;       // was 1.0 — orbit faster
        public float verticalLerp = 4f;

        [Header("Dive bomb")]
        public float diveInterval = 3.5f;    // more frequent (was 5.5)
        public float diveTelegraph = 0.4f;   // shorter (was 0.6)
        public float diveSpeed = 28f;        // way faster (was 18)
        public float diveDuration = 0.9f;

        [Header("Refs")]
        public Transform target;

        Rigidbody rb;
        Renderer rend;
        Material matInstance;
        float health;
        public float Health => health;       // exposed for kill detection
        float lastContactTime;
        float nextDiveTime;
        float diveUntil;
        float telegraphUntil;
        Vector3 diveDir;
        float orbitAngle;
        float personalHoverHeight;

        public Color baseColor = new Color(0.2f, 0.7f, 1f);
        public Color hitFlash = Color.white;
        float flashTimer;

        public System.Action<FlyingEnemy> OnDied;

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            rb.useGravity = false;
            rb.linearDamping = 1.5f;
            rb.angularDamping = 2f;
            rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.interpolation = RigidbodyInterpolation.Interpolate;

            rend = GetComponentInChildren<Renderer>();
            if (rend != null)
            {
                matInstance = rend.material;
                matInstance.color = baseColor;
                matInstance.EnableKeyword("_EMISSION");
                if (matInstance.HasProperty("_EmissionColor"))
                    matInstance.SetColor("_EmissionColor", baseColor * 1.3f);
            }

            health = maxHealth;
            personalHoverHeight = hoverHeight + Random.Range(-hoverHeightJitter, hoverHeightJitter);
            orbitAngle = Random.Range(0f, Mathf.PI * 2f);
            nextDiveTime = Time.time + Random.Range(diveInterval * 0.4f, diveInterval);
            EnemyRegistry.Register(transform);
            if (SoundFX.Instance != null) SoundFX.Instance.PlayEnemySpawn(transform.position);
        }

        void OnDestroy() { EnemyRegistry.Unregister(transform); }

        public void Configure(float hp, float speed, float dmg, float score, Color color)
        {
            maxHealth = hp;
            health = hp;
            chaseSpeed = speed;
            contactDamage = dmg;
            scoreValue = score;
            baseColor = color;
            if (matInstance != null)
            {
                matInstance.color = color;
                if (matInstance.HasProperty("_EmissionColor"))
                    matInstance.SetColor("_EmissionColor", color * 1.3f);
            }
        }

        void FixedUpdate()
        {
            if (target == null) return;

            bool diving = Time.time < diveUntil;
            bool telegraphing = Time.time < telegraphUntil;

            if (diving)
            {
                // Carry the dive
                rb.linearVelocity = diveDir * diveSpeed;
                return;
            }

            if (!telegraphing && Time.time >= nextDiveTime)
            {
                // Start telegraph (hover briefly) then dive
                telegraphUntil = Time.time + diveTelegraph;
                nextDiveTime = Time.time + diveInterval;
            }

            if (telegraphing)
            {
                // Stop and aim at player
                rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, Vector3.zero, 8f * Time.fixedDeltaTime);
                Vector3 toTarget = target.position - transform.position;
                if (toTarget.sqrMagnitude > 0.01f)
                    diveDir = toTarget.normalized;
                // Trigger the actual dive at end of telegraph
                if (Time.time + Time.fixedDeltaTime >= telegraphUntil)
                {
                    diveUntil = Time.time + diveDuration;
                }
                if (matInstance != null && matInstance.HasProperty("_EmissionColor"))
                    matInstance.SetColor("_EmissionColor", Color.white * (1.4f + Mathf.Sin(Time.time * 22f) * 0.6f));
                return;
            }

            // Restore emission to normal
            if (matInstance != null && matInstance.HasProperty("_EmissionColor"))
                matInstance.SetColor("_EmissionColor", baseColor * 1.3f);

            // Normal orbit-chase: pick a point on a circle around the player at hoverHeight
            orbitAngle += orbitSpeed * Time.fixedDeltaTime;
            Vector3 orbitOffset = new Vector3(Mathf.Cos(orbitAngle), 0f, Mathf.Sin(orbitAngle)) * orbitRadius;
            Vector3 desiredPos = target.position + orbitOffset;
            desiredPos.y = target.position.y + personalHoverHeight;

            Vector3 toDesired = desiredPos - transform.position;
            Vector3 horiz = new Vector3(toDesired.x, 0, toDesired.z);
            Vector3 wishHoriz = Vector3.ClampMagnitude(horiz, 1f) * chaseSpeed;

            Vector3 v = rb.linearVelocity;
            v.x = Mathf.MoveTowards(v.x, wishHoriz.x, 18f * Time.fixedDeltaTime);
            v.z = Mathf.MoveTowards(v.z, wishHoriz.z, 18f * Time.fixedDeltaTime);
            v.y = Mathf.Lerp(v.y, toDesired.y * verticalLerp, verticalLerp * Time.fixedDeltaTime);
            v.y = Mathf.Clamp(v.y, -8f, 8f);
            rb.linearVelocity = v;

            // Hard altitude cap so flyers can't drift over the walls
            if (transform.position.y > 10.5f)
            {
                var p = transform.position;
                p.y = 10.5f;
                transform.position = p;
                if (v.y > 0f) { v.y = 0f; rb.linearVelocity = v; }
            }

            if (horiz.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.LookRotation(horiz);
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
            var ph = c.collider.GetComponentInParent<PlayerHealth>();
            if (ph != null)
            {
                lastContactTime = Time.time;
                ph.TakeDamage(contactDamage, c.GetContact(0).point, c.GetContact(0).normal);
                // After contact, end the dive and pop back up
                diveUntil = 0f;
                rb.linearVelocity = new Vector3(rb.linearVelocity.x, 6f, rb.linearVelocity.z);
            }
        }

        public void TakeDamage(float dmg, Vector3 hitPoint, Vector3 hitNormal)
        {
            health -= dmg;
            flashTimer = 0.12f;
            if (matInstance != null) matInstance.color = hitFlash;
            if (rb != null) rb.AddForce(-hitNormal * 2f, ForceMode.Impulse);
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
