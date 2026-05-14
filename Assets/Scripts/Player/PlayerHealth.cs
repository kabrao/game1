using UnityEngine;

namespace BallShooter
{
    public class PlayerHealth : MonoBehaviour, IDamageable
    {
        [Header("Health")]
        public float maxHealth = 120f;
        public float regenDelay = 3f;        // seconds without damage before regen starts
        public float regenPerSecond = 16f;

        float health;
        float lastHitTime = -999f;

        public float Health => health;
        public float MaxHealth => maxHealth;
        public bool IsDead { get; private set; }

        public System.Action<float, float> OnHealthChanged;  // (current, max)
        public System.Action<float> OnDamaged;               // (amount)
        public System.Action OnDeath;

        void Awake()
        {
            health = maxHealth;
        }

        void Update()
        {
            if (IsDead) return;
            if (Time.time - lastHitTime > regenDelay && health < maxHealth)
            {
                health = Mathf.Min(maxHealth, health + regenPerSecond * Time.deltaTime);
                OnHealthChanged?.Invoke(health, maxHealth);
            }
        }

        public void TakeDamage(float dmg, Vector3 hitPoint, Vector3 hitNormal)
        {
            if (IsDead) return;
            lastHitTime = Time.time;
            health -= dmg;
            OnHealthChanged?.Invoke(Mathf.Max(0, health), maxHealth);
            OnDamaged?.Invoke(dmg);
            CameraShake.Kick(Mathf.Clamp01(dmg / 30f) * 0.6f);
            if (health <= 0f)
            {
                IsDead = true;
                OnDeath?.Invoke();
            }
        }

        public void Heal(float amount)
        {
            if (IsDead) return;
            health = Mathf.Min(maxHealth, health + amount);
            OnHealthChanged?.Invoke(health, maxHealth);
        }
    }
}
