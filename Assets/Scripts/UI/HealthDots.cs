using UnityEngine;

namespace BallShooter
{
    /// <summary>
    /// Renders a row of small unlit dots above the enemy. Each dot represents
    /// one shot worth of HP at the player's current weapon damage. Bright dots =
    /// hits remaining; dim dots = hits already taken.
    ///
    /// Implementation notes:
    ///   - Dots are tiny primitive spheres in world space, NOT parented to the
    ///     enemy (so size/rotation don't inherit). Position is updated each
    ///     LateUpdate by following the enemy.
    ///   - Two shared materials (bright + dim) — zero per-enemy material allocation.
    ///   - Render shadows off; unlit shader; tight render cost.
    /// </summary>
    public class HealthDots : MonoBehaviour
    {
        public float yOffset = 1.3f;
        public float dotSize = 0.12f;
        public float dotSpacing = 0.2f;
        public int maxDots = 10;

        static float s_CachedPlayerDamage = -1f;
        static Material s_ActiveMat;
        static Material s_EmptyMat;
        static Camera s_Cam;
        static Shader s_Shader;

        Transform rowRoot;
        Renderer[] dotRenderers;
        int totalDots;
        float referenceDamage;

        public void Init(float maxHealth)
        {
            if (s_CachedPlayerDamage <= 0f)
            {
                var sh = FindFirstObjectByType<PlayerShooter>();
                s_CachedPlayerDamage = sh != null ? sh.damage : 35f;
            }
            referenceDamage = s_CachedPlayerDamage;
            int n = Mathf.Clamp(Mathf.CeilToInt(maxHealth / Mathf.Max(1f, referenceDamage)), 1, maxDots);
            BuildRow(n);
        }

        void EnsureMaterials()
        {
            if (s_ActiveMat != null && s_EmptyMat != null) return;
            if (s_Shader == null)
                s_Shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");

            s_ActiveMat = new Material(s_Shader);
            var ca = new Color(0.98f, 0.95f, 0.55f);
            s_ActiveMat.color = ca;
            if (s_ActiveMat.HasProperty("_BaseColor")) s_ActiveMat.SetColor("_BaseColor", ca);

            s_EmptyMat = new Material(s_Shader);
            var ce = new Color(0.18f, 0.18f, 0.22f);
            s_EmptyMat.color = ce;
            if (s_EmptyMat.HasProperty("_BaseColor")) s_EmptyMat.SetColor("_BaseColor", ce);
        }

        void BuildRow(int n)
        {
            EnsureMaterials();
            if (rowRoot != null) Destroy(rowRoot.gameObject);

            // World-space row (no parent — we follow manually so parent scale
            // and rotation don't affect dot size/orientation)
            rowRoot = new GameObject("HealthDotsRow").transform;
            totalDots = n;
            dotRenderers = new Renderer[n];

            float totalWidth = (n - 1) * dotSpacing;
            for (int i = 0; i < n; i++)
            {
                var d = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                d.name = "Dot" + i;
                var col = d.GetComponent<Collider>();
                if (col != null) Destroy(col);
                d.transform.SetParent(rowRoot, false);
                d.transform.localPosition = new Vector3(-totalWidth * 0.5f + i * dotSpacing, 0f, 0f);
                d.transform.localScale = Vector3.one * dotSize;

                var r = d.GetComponent<Renderer>();
                r.sharedMaterial = s_ActiveMat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
                dotRenderers[i] = r;
            }
        }

        public void Refresh(float currentHP)
        {
            if (dotRenderers == null) return;
            int hitsLeft = Mathf.Clamp(Mathf.CeilToInt(currentHP / Mathf.Max(1f, referenceDamage)), 0, totalDots);
            for (int i = 0; i < totalDots; i++)
                dotRenderers[i].sharedMaterial = (i < hitsLeft) ? s_ActiveMat : s_EmptyMat;
        }

        void LateUpdate()
        {
            if (rowRoot == null) return;

            // Position above the enemy, scaled by its size
            float yAbove = yOffset + transform.localScale.y * 0.55f;
            rowRoot.position = transform.position + Vector3.up * yAbove;

            // Billboard toward camera (yaw only — keeps the row level)
            if (s_Cam == null) s_Cam = Camera.main;
            if (s_Cam != null)
            {
                Vector3 dir = s_Cam.transform.position - rowRoot.position;
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.001f)
                    rowRoot.rotation = Quaternion.LookRotation(-dir);
            }
        }

        void OnDestroy()
        {
            if (rowRoot != null) Destroy(rowRoot.gameObject);
        }

        /// <summary>Convenience: attach + init in one call.</summary>
        public static HealthDots AttachTo(GameObject go, float maxHealth, float yOffsetOverride = -1f)
        {
            var dots = go.AddComponent<HealthDots>();
            if (yOffsetOverride > 0f) dots.yOffset = yOffsetOverride;
            dots.Init(maxHealth);
            return dots;
        }
    }
}
