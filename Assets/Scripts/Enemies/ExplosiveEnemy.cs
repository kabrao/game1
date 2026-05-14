using UnityEngine;

namespace BallShooter
{
    /// <summary>
    /// Pulsing orange ball. Chases the player, deals contact damage, but on death
    /// (or proximity to player) explodes for AoE damage and visual/light flash.
    ///
    /// Performance-hardened: uses a static collider buffer for the explosion query,
    /// a cached/shared flash material, and a one-shot `exploded` guard so chain
    /// reactions can't recurse or double-trigger.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(SphereCollider))]
    public class ExplosiveEnemy : MonoBehaviour, IDamageable
    {
        [Header("Stats")]
        public float maxHealth = 45f;
        public float moveSpeed = 7.5f;       // was 4.5
        public float contactDamage = 6f;
        public float contactCooldown = 1.2f;
        public float scoreValue = 35f;

        [Header("Explosion")]
        public float explosionRadius = 6.5f;
        public float explosionDamage = 60f;
        public float enemyDamageMultiplier = 1.5f;
        public float proximityFuseRadius = 2.6f;
        public float proximityFuseTime = 0.6f;
        public Color explosionColor = new Color(1f, 0.7f, 0.1f);

        [Header("Jump")]
        public float jumpForce = 7.5f;
        public float minJumpInterval = 1.2f;
        public float maxJumpInterval = 2.2f;

        [Header("Refs")]
        public Transform target;

        public Color baseColor = new Color(1f, 0.65f, 0.1f);

        [Header("Anti-stack")]
        public float separationRadius = 3.2f;
        public float separationStrength = 4.5f;
        public float tangentBias = 0f;
        public float tangentStrength = 0.4f;

        Rigidbody rb;
        SphereCollider col;
        Renderer rend;
        Material matInstance;
        float health;
        public float Health => health;       // exposed so PlayerShooter can detect kills
        float lastContactTime;
        float nextJumpTime;
        float fuseEnd = -1f;
        bool exploded;

        // Shared, allocated once. Used by every Explode() call.
        static readonly Collider[] s_HitBuffer = new Collider[48];
        static Shader s_UnlitShader;
        static Material s_FlashMaterial;
        static int s_ActiveFlashes;
        const int MaxConcurrentFlashLights = 4;

        public System.Action<ExplosiveEnemy> OnDied;

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
                    matInstance.SetColor("_EmissionColor", baseColor * 1.5f);
            }
            rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            health = maxHealth;
            nextJumpTime = Time.time + Random.Range(0.5f, maxJumpInterval);
            tangentBias = Random.Range(-1f, 1f);
            EnemyRegistry.Register(transform);
            if (SoundFX.Instance != null) SoundFX.Instance.PlayEnemySpawn(transform.position);
        }

        void OnDestroy() { EnemyRegistry.Unregister(transform); }

        public void Configure(float hp, float speed, float dmg, float score, Color color)
        {
            maxHealth = hp;
            health = hp;
            moveSpeed = speed;
            explosionDamage = dmg;
            scoreValue = score;
            baseColor = color;
            if (matInstance != null) matInstance.color = color;
        }

        void FixedUpdate()
        {
            if (exploded || target == null) return;
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

            float dist = Vector3.Distance(target.position, transform.position);
            if (fuseEnd < 0 && dist < proximityFuseRadius)
                fuseEnd = Time.time + proximityFuseTime;

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

        void Update()
        {
            if (exploded) return;
            if (matInstance != null && matInstance.HasProperty("_EmissionColor"))
            {
                float pulse = 1.3f + Mathf.Sin(Time.time * 6f) * 0.6f;
                if (fuseEnd > 0) pulse = 1.6f + Mathf.Sin(Time.time * 26f) * 1.2f;
                matInstance.SetColor("_EmissionColor", baseColor * pulse);
            }
            if (fuseEnd > 0 && Time.time >= fuseEnd) Explode();
        }

        void OnCollisionStay(Collision c)
        {
            if (exploded) return;
            if (Time.time - lastContactTime < contactCooldown) return;
            var ph = c.collider.GetComponentInParent<PlayerHealth>();
            if (ph != null)
            {
                lastContactTime = Time.time;
                ph.TakeDamage(contactDamage, c.GetContact(0).point, c.GetContact(0).normal);
                if (fuseEnd < 0) fuseEnd = Time.time + 0.05f;
            }
        }

        public void TakeDamage(float dmg, Vector3 hitPoint, Vector3 hitNormal)
        {
            if (exploded) return;
            health -= dmg;
            if (matInstance != null) matInstance.color = Color.white;
            if (rb != null) rb.AddForce(-hitNormal * 2.5f, ForceMode.Impulse);
            if (SoundFX.Instance != null && health > 0) SoundFX.Instance.PlayEnemyHit(hitPoint);
            var dots = GetComponent<HealthDots>();
            if (dots != null) dots.Refresh(health);
            if (health <= 0f) Explode();
        }

        public void Explode()
        {
            if (exploded) return;
            exploded = true;

            // Stop further interactions IMMEDIATELY so chain reactions can't
            // pick this collider back up and overlap-query it.
            if (col != null) col.enabled = false;
            if (rb != null) rb.linearVelocity = Vector3.zero;

            SpawnFlash();

            // Damage player if in radius
            if (target != null)
            {
                float d = Vector3.Distance(target.position, transform.position);
                if (d <= explosionRadius)
                {
                    float falloff = 1f - (d / explosionRadius);
                    var ph = target.GetComponentInParent<PlayerHealth>();
                    if (ph != null)
                        ph.TakeDamage(explosionDamage * falloff, transform.position, Vector3.up);
                }
            }

            // Damage / push other entities. Use non-alloc query and a local set
            // to skip duplicates from multi-collider enemies.
            int count = Physics.OverlapSphereNonAlloc(transform.position, explosionRadius,
                s_HitBuffer, ~0, QueryTriggerInteraction.Ignore);
            var damaged = HashSetPool.Get();
            for (int i = 0; i < count; i++)
            {
                var h = s_HitBuffer[i];
                if (h == null || h.gameObject == gameObject) continue;

                var er = h.attachedRigidbody;
                if (er != null && er.gameObject != gameObject)
                    er.AddExplosionForce(1100f, transform.position, explosionRadius, 1.4f);

                var dmg = h.GetComponentInParent<IDamageable>();
                if (dmg == null || dmg is PlayerHealth || damaged.Contains(dmg)) continue;
                damaged.Add(dmg);

                float d = Vector3.Distance(transform.position, h.transform.position);
                float falloff = Mathf.Lerp(0.5f, 1f, Mathf.Clamp01(1f - d / explosionRadius));
                dmg.TakeDamage(explosionDamage * enemyDamageMultiplier * falloff,
                               transform.position, Vector3.up);
            }
            HashSetPool.Release(damaged);

            CameraShake.Kick(0.45f);
            if (SoundFX.Instance != null) SoundFX.Instance.PlayExplosion(transform.position);
            OnDied?.Invoke(this);
            Destroy(gameObject);
        }

        void SpawnFlash()
        {
            // Cached shader (Shader.Find is slow — only do it once per game)
            if (s_UnlitShader == null)
                s_UnlitShader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            if (s_FlashMaterial == null)
            {
                s_FlashMaterial = new Material(s_UnlitShader);
                s_FlashMaterial.color = explosionColor;
                if (s_FlashMaterial.HasProperty("_BaseColor"))
                    s_FlashMaterial.SetColor("_BaseColor", explosionColor);
            }

            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "ExplosionFlash";
            var c = go.GetComponent<Collider>();
            if (c != null) Destroy(c);
            go.transform.position = transform.position;
            go.transform.localScale = Vector3.one * 0.6f;
            var r = go.GetComponent<Renderer>();
            if (r != null)
            {
                r.sharedMaterial = s_FlashMaterial;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
            // Smaller end-scale + shorter lifetime = far less screen-fill + bloom cost
            go.AddComponent<FlashScale>().Init(Mathf.Min(explosionRadius * 0.9f, 4.5f), 0.22f);

            // Only spawn the point light if there aren't already several active.
            // URP forward+ can handle many but bloom amplifies cost dramatically.
            if (s_ActiveFlashes < MaxConcurrentFlashLights)
            {
                var lgo = new GameObject("FlashLight");
                lgo.transform.SetParent(go.transform, false);
                var lg = lgo.AddComponent<Light>();
                lg.type = LightType.Point;
                lg.color = explosionColor;
                lg.intensity = 4.5f;                         // was 12 — dramatically reduces bloom cost
                lg.range = Mathf.Min(explosionRadius * 1.2f, 8f);
                lg.shadows = LightShadows.None;
                s_ActiveFlashes++;
                go.AddComponent<FlashLightTracker>();
            }

            Destroy(go, 0.25f);
        }

        public static void OnFlashLightDestroyed()
        {
            if (s_ActiveFlashes > 0) s_ActiveFlashes--;
        }
    }

    /// <summary>Scales a transform up over its lifetime. Uses sharedMaterial — no per-flash GC.</summary>
    public class FlashScale : MonoBehaviour
    {
        Vector3 startScale;
        Vector3 endScale;
        float lifetime;
        float t;

        public void Init(float targetSize, float life)
        {
            startScale = transform.localScale;
            endScale = Vector3.one * targetSize;
            lifetime = life;
        }

        void Update()
        {
            t += Time.deltaTime / Mathf.Max(0.001f, lifetime);
            float u = Mathf.Clamp01(t);
            transform.localScale = Vector3.Lerp(startScale, endScale, u);
        }
    }

    /// <summary>Decrements the active-flash counter when the flash GameObject is destroyed.</summary>
    public class FlashLightTracker : MonoBehaviour
    {
        void OnDestroy() => ExplosiveEnemy.OnFlashLightDestroyed();
    }

    /// <summary>Tiny pool to avoid HashSet GC on every explosion.</summary>
    internal static class HashSetPool
    {
        static readonly System.Collections.Generic.Stack<System.Collections.Generic.HashSet<IDamageable>> _pool =
            new System.Collections.Generic.Stack<System.Collections.Generic.HashSet<IDamageable>>();

        public static System.Collections.Generic.HashSet<IDamageable> Get()
        {
            return _pool.Count > 0 ? _pool.Pop() : new System.Collections.Generic.HashSet<IDamageable>();
        }

        public static void Release(System.Collections.Generic.HashSet<IDamageable> set)
        {
            set.Clear();
            _pool.Push(set);
        }
    }
}
