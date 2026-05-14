using UnityEngine;
using UnityEngine.UI;

namespace BallShooter
{
    /// <summary>
    /// One-shot "+score" popup anchored to a world-space position (where the
    /// enemy died). Each frame it projects that world point back to screen
    /// space, drifts it upward in WORLD space (so it feels like it rises from
    /// the kill site even as the camera moves), and fades the text alpha.
    /// Destroys itself when its lifetime expires.
    ///
    /// USAGE
    ///   Call WorldKillPopup.Spawn(parentCanvas, font, worldPos, score)
    ///   to spawn one. The component creates its own Text child and animates
    ///   it from there — no prefab needed.
    ///
    /// TUNING — feel free to tweak the public fields at the top.
    /// </summary>
    public class WorldKillPopup : MonoBehaviour
    {
        // -------- Tunable feel --------
        public float lifetime    = 1.0f;      // seconds before the popup is destroyed
        public float riseSpeed   = 1.6f;      // world-space rise rate (units/sec)
        public float fadeStartT  = 0.35f;     // 0..1 of lifetime when alpha begins fading
        public Color color       = new Color(1f, 0.92f, 0.35f);
        public int   fontSize    = 38;

        // -------- Runtime state --------
        Text text;
        Camera cam;
        Vector3 worldAnchor;       // where the enemy died
        float   bornAt;

        /// <summary>
        /// Spawns a "+score" popup at <paramref name="worldPos"/>, parented to the
        /// supplied screen-space canvas. Returns the new popup component.
        /// </summary>
        public static WorldKillPopup Spawn(Transform canvasParent, Font font,
                                           Vector3 worldPos, int score)
        {
            // The popup is a single GameObject that holds both this component
            // and the visible Text. Keeping it as one object means cleanup is
            // a single Destroy() call.
            var go = new GameObject("WorldKillPopup");
            go.transform.SetParent(canvasParent, false);

            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(180, 50);

            var t = go.AddComponent<Text>();
            t.font = font;
            t.fontSize = 38;
            t.alignment = TextAnchor.MiddleCenter;
            t.text = "+" + score;
            t.raycastTarget = false;

            // Thin drop-shadow outline so the popup is readable against any background.
            var outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(0, 0, 0, 0.85f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);

            var popup = go.AddComponent<WorldKillPopup>();
            popup.text = t;
            popup.cam = Camera.main;
            popup.worldAnchor = worldPos;
            popup.bornAt = Time.time;
            // Apply default color
            t.color = popup.color;
            return popup;
        }

        void LateUpdate()
        {
            if (text == null) return;
            float age = Time.time - bornAt;
            if (age >= lifetime) { Destroy(gameObject); return; }

            // ---- World-space rise ----
            // Compute where the popup "really is" in world space this frame.
            Vector3 wpos = worldAnchor + Vector3.up * (riseSpeed * age);

            // ---- Project to screen ----
            // Lazily re-grab the camera in case the game restarted or the
            // bootstrap rebuilt it.
            if (cam == null) cam = Camera.main;
            if (cam == null) return;

            Vector3 screen = cam.WorldToScreenPoint(wpos);
            if (screen.z <= 0f)
            {
                // Behind the camera — hide rather than render in a weird position.
                SetAlpha(0f);
                return;
            }
            // ScreenSpaceOverlay canvas: rectTransform.position takes raw pixel coords.
            text.rectTransform.position = screen;

            // ---- Fade ----
            float fade = 1f;
            float lifeT = age / lifetime;
            if (lifeT > fadeStartT)
                fade = 1f - (lifeT - fadeStartT) / (1f - fadeStartT);
            SetAlpha(Mathf.Clamp01(fade));
        }

        void SetAlpha(float a)
        {
            if (text == null) return;
            var c = color; c.a = a;
            text.color = c;
        }
    }
}
