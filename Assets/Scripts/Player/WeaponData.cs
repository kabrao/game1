using UnityEngine;

namespace BallShooter
{
    public enum WeaponType { SMG, Assault, Sniper, Pistol, Knife }

    /// <summary>
    /// Plain data holder for a weapon's tuning. PlayerShooter keeps an array
    /// of these and switches between them.
    /// </summary>
    [System.Serializable]
    public class WeaponData
    {
        public string displayName;
        public WeaponType type;

        // Damage / fire
        public float damage;
        public float fireRate;           // shots per second (or melee swings/sec)
        public int magazineSize;
        public float reloadTime;
        public float spreadDegrees;
        public float headshotMultiplier = 2f;

        // Behavior
        public bool isAutomatic = true;  // hold to fire vs click per shot
        public int pierce = 0;            // extra enemies the bullet can pass through
        public bool isMelee = false;
        public float meleeRange = 2.5f;

        // ADS
        public bool canADS;
        public float adsFov = 60f;
        public float adsSpreadMultiplier = 0.25f;
        public float adsRecoilMultiplier = 0.5f;

        // Feel
        public float shakeOnFire = 0.18f;
        public float recoilMultiplier = 1f;
        public Color tracerColor = new Color(1f, 0.85f, 0.2f);
        public Color gunColor = new Color(0.15f, 0.15f, 0.18f);
        public Vector3 gunScale = new Vector3(0.18f, 0.18f, 0.7f);

        // ----- Built-in presets -----
        public static WeaponData SMG() => new WeaponData
        {
            displayName = "SMG", type = WeaponType.SMG,
            damage = 22f, fireRate = 14f, magazineSize = 32, reloadTime = 1.5f,
            spreadDegrees = 2.8f, isAutomatic = true, pierce = 0, isMelee = false,
            canADS = false,
            shakeOnFire = 0.22f, recoilMultiplier = 1.3f,
            tracerColor = new Color(0.4f, 0.95f, 1f),
            gunColor = new Color(0.18f, 0.2f, 0.25f),
            gunScale = new Vector3(0.16f, 0.16f, 0.6f),
        };

        public static WeaponData Assault() => new WeaponData
        {
            displayName = "Assault", type = WeaponType.Assault,
            damage = 28f, fireRate = 9f, magazineSize = 26, reloadTime = 1.7f,
            spreadDegrees = 1.0f, isAutomatic = true, pierce = 0,
            canADS = true, adsFov = 60f, adsSpreadMultiplier = 0.25f, adsRecoilMultiplier = 0.5f,
            shakeOnFire = 0.18f, recoilMultiplier = 1f,
            tracerColor = new Color(1f, 0.85f, 0.2f),
            gunColor = new Color(0.18f, 0.16f, 0.15f),
            gunScale = new Vector3(0.18f, 0.18f, 0.85f),
        };

        public static WeaponData Sniper() => new WeaponData
        {
            displayName = "Sniper", type = WeaponType.Sniper,
            damage = 220f, fireRate = 0.9f, magazineSize = 5, reloadTime = 2.6f,
            spreadDegrees = 0f, isAutomatic = false, pierce = 5,   // shoots through 5 enemies
            canADS = true, adsFov = 28f, adsSpreadMultiplier = 0f, adsRecoilMultiplier = 0.4f,
            shakeOnFire = 0.55f, recoilMultiplier = 2.2f,
            tracerColor = new Color(1f, 0.4f, 1f),
            gunColor = new Color(0.12f, 0.12f, 0.15f),
            gunScale = new Vector3(0.15f, 0.15f, 1.15f),
        };

        public static WeaponData Pistol() => new WeaponData
        {
            displayName = "Pistol", type = WeaponType.Pistol,
            damage = 45f, fireRate = 5f, magazineSize = 14, reloadTime = 1.2f,
            spreadDegrees = 0.5f, isAutomatic = false, pierce = 0,
            canADS = true, adsFov = 70f, adsSpreadMultiplier = 0.3f, adsRecoilMultiplier = 0.6f,
            shakeOnFire = 0.14f, recoilMultiplier = 0.9f,
            tracerColor = new Color(1f, 0.95f, 0.7f),
            gunColor = new Color(0.22f, 0.22f, 0.25f),
            gunScale = new Vector3(0.14f, 0.14f, 0.42f),
        };

        public static WeaponData Knife() => new WeaponData
        {
            displayName = "Knife", type = WeaponType.Knife,
            damage = 120f, fireRate = 2.4f, magazineSize = 0, reloadTime = 0f,
            spreadDegrees = 0f, isAutomatic = false, pierce = 0,
            isMelee = true, meleeRange = 2.7f,
            canADS = false,
            shakeOnFire = 0.25f, recoilMultiplier = 0.5f,
            tracerColor = Color.white,
            gunColor = new Color(0.7f, 0.75f, 0.85f),
            gunScale = new Vector3(0.08f, 0.04f, 0.55f),
        };
    }
}
