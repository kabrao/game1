using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BallShooter
{
    /// <summary>
    /// Anything the player can shoot. The Health property lets the shooter
    /// detect "did that hit kill it?" by comparing pre/post HP.
    /// </summary>
    public interface IDamageable
    {
        void TakeDamage(float dmg, Vector3 hitPoint, Vector3 hitNormal);
        float Health { get; }
    }

    /// <summary>
    /// Multi-weapon FPS shooter. Holds an array of WeaponData; left-click fires,
    /// right-click ADSes (where supported), R reloads, 1/2/3 switches.
    /// Sniper supports raycast pierce; Knife is melee. Backwards-compatible with
    /// existing UI events (OnFired, OnEnemyHit) and public properties used by GameUI.
    /// </summary>
    public class PlayerShooter : MonoBehaviour
    {
        [Header("Camera / Aim")]
        public Camera fpCamera;
        public float range = 300f;
        public LayerMask hitMask = ~0;

        [Header("Reference damage (used by HealthDots for the dot count)")]
        [Tooltip("Read by HealthDots — set to the primary weapon's per-shot damage so dots reflect that weapon. We update this when switching.")]
        public float damage = 28f;

        [Header("Weapons")]
        public WeaponData[] weapons;        // [0]=primary, [1]=pistol, [2]=knife
        public int currentWeaponIndex = 0;

        [Header("FX")]
        public float tracerDuration = 0.06f;
        public float shakeOnFire = 0.18f;

        [Header("Refs (auto-wired by bootstrap)")]
        public WeaponBob weaponBob;
        public Light muzzleLight;
        public Transform muzzlePoint;
        public Transform gunMesh;            // the visible gun cube (for color / scale swap)
        public Renderer gunRenderer;
        public Transform weaponRoot;

        // ---- Events read by GameUI ----
        //   OnFired           — every time the player pulls the trigger / swings
        //   OnEnemyHit        — a shot/swing connected with an enemy (no kill required)
        //   OnEnemyKilledByShot — that connection reduced the enemy from >0 HP to 0
        //
        // Note: enemies in this game are balls — there is no headshot distinction,
        // so the previous Action<bool wasHeadshot> signature was simplified to a
        // plain Action. GameUI uses OnEnemyKilledByShot to flip the hit marker red.
        public System.Action OnFired;
        public System.Action OnEnemyHit;
        public System.Action OnEnemyKilledByShot;

        // Per-weapon state — kept in parallel arrays so each weapon remembers its ammo / cooldown
        int[] ammoInMag;
        bool[] reloading;
        float[] reloadEndTime;

        float nextFireTime;
        float muzzleFlashUntil;
        bool ads;
        [Tooltip("Non-ADS FOV target. PauseMenu's FOV slider writes to this.")]
        public float baseFov = 90f;
        float pistolGunRotation = -3f;

        // Used to suppress autoclick on semi-auto / sniper
        bool fireWasPressedPrevFrame;

        // Static buffer for sniper RaycastAll non-alloc usage
        static RaycastHit[] s_HitBuf = new RaycastHit[32];

        public WeaponData Current => (weapons != null && currentWeaponIndex < weapons.Length) ? weapons[currentWeaponIndex] : null;
        public int AmmoInMag => Current == null ? 0 : (ammoInMag != null ? ammoInMag[currentWeaponIndex] : 0);
        public int MagSize => Current == null ? 0 : Current.magazineSize;
        public bool IsReloading => Current != null && reloading != null && reloading[currentWeaponIndex];
        public bool IsADS => ads;
        public string CurrentName => Current != null ? Current.displayName : "";

        void Awake()
        {
            if (fpCamera == null) fpCamera = GetComponentInChildren<Camera>();
            if (fpCamera != null) baseFov = fpCamera.fieldOfView;
            EnsureWeaponState();
            ApplyWeaponVisual();
            SyncReferenceDamage();
        }

        public void SetWeapons(WeaponData[] w)
        {
            weapons = w;
            currentWeaponIndex = 0;
            EnsureWeaponState();
            ApplyWeaponVisual();
            SyncReferenceDamage();
        }

        void EnsureWeaponState()
        {
            if (weapons == null || weapons.Length == 0) return;
            ammoInMag = new int[weapons.Length];
            reloading = new bool[weapons.Length];
            reloadEndTime = new float[weapons.Length];
            for (int i = 0; i < weapons.Length; i++)
                ammoInMag[i] = weapons[i].magazineSize;
        }

        void Update()
        {
            UpdateMuzzleFlash();
            HandleSwitchInput();

            var mouse = Mouse.current;
            var kb = Keyboard.current;
            if (mouse == null || Current == null) return;

            // ---- ADS ----
            bool wantADS = Current.canADS && mouse.rightButton.isPressed && Cursor.lockState == CursorLockMode.Locked && !IsReloading;
            SetADS(wantADS);

            // ---- Reload ----
            if (IsReloading)
            {
                if (Time.time >= reloadEndTime[currentWeaponIndex])
                {
                    ammoInMag[currentWeaponIndex] = Current.magazineSize;
                    reloading[currentWeaponIndex] = false;
                }
                fireWasPressedPrevFrame = mouse.leftButton.isPressed;
                return;
            }

            if (kb != null && kb.rKey.wasPressedThisFrame && Current.magazineSize > 0 && ammoInMag[currentWeaponIndex] < Current.magazineSize)
            {
                StartReload();
                fireWasPressedPrevFrame = mouse.leftButton.isPressed;
                return;
            }

            // ---- Fire ----
            bool firePressed = mouse.leftButton.isPressed && Cursor.lockState == CursorLockMode.Locked;
            bool canTriggerFire = Current.isAutomatic ? firePressed : (firePressed && !fireWasPressedPrevFrame);
            if (canTriggerFire && Time.time >= nextFireTime)
            {
                if (Current.isMelee) { MeleeSwing(); }
                else if (Current.magazineSize > 0 && ammoInMag[currentWeaponIndex] <= 0) { StartReload(); }
                else { Fire(); }
            }

            fireWasPressedPrevFrame = mouse.leftButton.isPressed;
        }

        // ----- INPUT / SWITCHING -----

        void HandleSwitchInput()
        {
            var kb = Keyboard.current;
            if (kb == null || weapons == null) return;
            int newIndex = currentWeaponIndex;
            if (kb.digit1Key.wasPressedThisFrame && weapons.Length > 0) newIndex = 0;
            if (kb.digit2Key.wasPressedThisFrame && weapons.Length > 1) newIndex = 1;
            if (kb.digit3Key.wasPressedThisFrame && weapons.Length > 2) newIndex = 2;

            if (newIndex != currentWeaponIndex)
            {
                currentWeaponIndex = newIndex;
                SetADS(false);
                ApplyWeaponVisual();
                SyncReferenceDamage();
            }
        }

        void SetADS(bool down)
        {
            ads = down;
            if (fpCamera != null)
            {
                float target = down && Current != null ? Current.adsFov : baseFov;
                fpCamera.fieldOfView = Mathf.Lerp(fpCamera.fieldOfView, target, 18f * Time.deltaTime);
            }
        }

        void SyncReferenceDamage()
        {
            // Public 'damage' is read by HealthDots to compute dot count.
            // Use the primary's damage so the dots reflect the main weapon.
            if (weapons != null && weapons.Length > 0)
                damage = weapons[0].damage;
        }

        // ----- FIRE / MELEE -----

        void StartReload()
        {
            reloading[currentWeaponIndex] = true;
            reloadEndTime[currentWeaponIndex] = Time.time + Current.reloadTime;
        }

        void Fire()
        {
            var w = Current;
            ammoInMag[currentWeaponIndex]--;
            nextFireTime = Time.time + 1f / w.fireRate;

            Vector3 dir = fpCamera.transform.forward;
            float effectiveSpread = w.spreadDegrees * (ads ? w.adsSpreadMultiplier : 1f);
            if (effectiveSpread > 0f)
            {
                Quaternion s = Quaternion.Euler(
                    Random.Range(-effectiveSpread, effectiveSpread),
                    Random.Range(-effectiveSpread, effectiveSpread), 0f);
                dir = s * dir;
            }

            Vector3 origin = fpCamera.transform.position;
            Vector3 end = origin + dir * range;
            // Counters used after the raycast loop. We fire OnEnemyHit at most once
            // per trigger pull (any hit landed), and OnEnemyKilledByShot once per kill
            // so multi-kills from sniper pierce get multiple satisfying cues.
            bool hitAny = false;
            int kills = 0;

            if (w.pierce > 0)
            {
                // Sniper-style: bullet can damage up to (pierce + 1) enemies in a line.
                int count = Physics.RaycastNonAlloc(origin, dir, s_HitBuf, range, hitMask, QueryTriggerInteraction.Ignore);
                System.Array.Sort(s_HitBuf, 0, count, RaycastHitComparer.Instance);
                int remaining = w.pierce + 1;
                end = origin + dir * range;
                var hitSet = new HashSet<IDamageable>();
                for (int i = 0; i < count && remaining > 0; i++)
                {
                    var hit = s_HitBuf[i];
                    var dmgable = hit.collider.GetComponentInParent<IDamageable>();
                    if (dmgable == null || dmgable is PlayerHealth || hitSet.Contains(dmgable))
                    {
                        // Solid wall (non-damageable) stops the bullet.
                        if (dmgable == null)
                        {
                            end = hit.point;
                            SpawnImpactSpark(hit.point, hit.normal);
                            break;
                        }
                        continue;
                    }
                    hitSet.Add(dmgable);

                    // Cache HP before TakeDamage so we can tell if this shot was the kill.
                    float hpBefore = dmgable.Health;
                    dmgable.TakeDamage(w.damage, hit.point, hit.normal);
                    if (hpBefore > 0f && dmgable.Health <= 0f) kills++;

                    SpawnImpactSpark(hit.point, hit.normal);
                    end = hit.point;
                    hitAny = true;
                    remaining--;
                }
            }
            else
            {
                if (Physics.Raycast(origin, dir, out RaycastHit hit, range, hitMask, QueryTriggerInteraction.Ignore))
                {
                    end = hit.point;
                    var dmgable = hit.collider.GetComponentInParent<IDamageable>();
                    if (dmgable != null && !(dmgable is PlayerHealth))
                    {
                        // Pre/post HP delta tells us whether this hit killed the enemy.
                        float hpBefore = dmgable.Health;
                        dmgable.TakeDamage(w.damage, hit.point, hit.normal);
                        if (hpBefore > 0f && dmgable.Health <= 0f) kills++;
                        hitAny = true;
                    }
                    SpawnImpactSpark(hit.point, hit.normal);
                }
            }

            Vector3 muzzlePos = muzzlePoint != null ? muzzlePoint.position : origin + dir * 0.5f;
            SpawnTracer(muzzlePos, end, w.tracerColor);

            muzzleFlashUntil = Time.time + 0.05f;
            float recoilMul = w.recoilMultiplier * (ads ? w.adsRecoilMultiplier : 1f);
            if (weaponBob != null) weaponBob.AddRecoil(recoilMul);
            CameraShake.Kick(w.shakeOnFire * (ads ? w.adsRecoilMultiplier : 1f));
            if (SoundFX.Instance != null) SoundFX.Instance.PlayGunshot(muzzlePos);

            OnFired?.Invoke();
            if (hitAny) OnEnemyHit?.Invoke();
            // Fire kill events + sound once per kill. Multi-kill from sniper pierce
            // gets multiple cues — feels rewarding.
            for (int k = 0; k < kills; k++)
            {
                OnEnemyKilledByShot?.Invoke();
                if (SoundFX.Instance != null) SoundFX.Instance.PlayKillConfirm();
            }
        }

        void MeleeSwing()
        {
            var w = Current;
            nextFireTime = Time.time + 1f / w.fireRate;

            Vector3 origin = fpCamera.transform.position;
            Vector3 dir = fpCamera.transform.forward;
            bool hitAny = false;
            int kills = 0;

            // Sphere-cast so the swing has some lateral reach.
            if (Physics.SphereCast(origin, 0.5f, dir, out RaycastHit hit, w.meleeRange, hitMask, QueryTriggerInteraction.Ignore))
            {
                var dmgable = hit.collider.GetComponentInParent<IDamageable>();
                if (dmgable != null && !(dmgable is PlayerHealth))
                {
                    float hpBefore = dmgable.Health;
                    dmgable.TakeDamage(w.damage, hit.point, hit.normal);
                    if (hpBefore > 0f && dmgable.Health <= 0f) kills++;
                    hitAny = true;
                }
                SpawnImpactSpark(hit.point, hit.normal);
            }

            if (weaponBob != null) weaponBob.AddRecoil(w.recoilMultiplier);
            CameraShake.Kick(w.shakeOnFire);
            // Short snappy gunshot sound is also fine for melee — placeholder for now.
            if (SoundFX.Instance != null) SoundFX.Instance.PlayGunshot(transform.position);
            OnFired?.Invoke();
            if (hitAny) OnEnemyHit?.Invoke();
            for (int k = 0; k < kills; k++)
            {
                OnEnemyKilledByShot?.Invoke();
                if (SoundFX.Instance != null) SoundFX.Instance.PlayKillConfirm();
            }
        }

        // ----- VISUAL -----

        void ApplyWeaponVisual()
        {
            var w = Current;
            if (w == null) return;
            if (gunMesh != null) gunMesh.localScale = w.gunScale;
            if (gunRenderer != null)
            {
                if (gunRenderer.material != null) gunRenderer.material.color = w.gunColor;
            }
            // Knife: rotate the gun mesh so it looks like a blade angled forward
            if (gunMesh != null)
            {
                if (w.type == WeaponType.Knife)
                    gunMesh.localRotation = Quaternion.Euler(0, 0, -25f);
                else if (w.type == WeaponType.Pistol)
                    gunMesh.localRotation = Quaternion.Euler(pistolGunRotation, 0, 0);
                else
                    gunMesh.localRotation = Quaternion.identity;
            }
        }

        void UpdateMuzzleFlash()
        {
            if (muzzleLight == null) return;
            muzzleLight.enabled = (Time.time < muzzleFlashUntil) && (Current == null || !Current.isMelee);
        }

        void SpawnImpactSpark(Vector3 pos, Vector3 normal)
        {
            var go = new GameObject("Impact");
            go.transform.position = pos + normal * 0.02f;
            var lr = go.AddComponent<LineRenderer>();
            var mat = new Material(Shader.Find("Sprites/Default"));
            lr.material = mat;
            lr.useWorldSpace = false;
            lr.positionCount = 2;
            lr.SetPosition(0, Vector3.zero);
            lr.SetPosition(1, normal * 0.25f);
            lr.startWidth = 0.04f; lr.endWidth = 0.0f;
            lr.startColor = new Color(1f, 0.95f, 0.6f, 1f);
            lr.endColor = new Color(1f, 0.5f, 0.1f, 0f);
            Destroy(go, 0.08f);
        }

        void SpawnTracer(Vector3 from, Vector3 to, Color color)
        {
            var go = new GameObject("Tracer");
            var lr = go.AddComponent<LineRenderer>();
            var mat = new Material(Shader.Find("Sprites/Default"));
            lr.material = mat;
            lr.positionCount = 2;
            lr.SetPosition(0, from);
            lr.SetPosition(1, to);
            lr.startWidth = 0.03f; lr.endWidth = 0.005f;
            lr.startColor = color;
            lr.endColor = new Color(color.r, color.g, color.b, 0f);
            lr.useWorldSpace = true;
            lr.numCapVertices = 2;
            Destroy(go, tracerDuration);
        }

        class RaycastHitComparer : IComparer<RaycastHit>
        {
            public static readonly RaycastHitComparer Instance = new RaycastHitComparer();
            public int Compare(RaycastHit a, RaycastHit b) => a.distance.CompareTo(b.distance);
        }
    }
}
