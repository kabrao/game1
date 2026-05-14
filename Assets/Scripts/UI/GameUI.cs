using UnityEngine;
using UnityEngine.UI;

namespace BallShooter
{
    /// <summary>
    /// Procedurally built uGUI HUD: health bar, ammo, round, score, crosshair, game-over overlay.
    /// All elements are created at runtime so no prefabs are required.
    /// </summary>
    public class GameUI : MonoBehaviour
    {
        public PlayerHealth player;
        public PlayerShooter shooter;
        public WaveManager waves;

        Canvas canvas;
        Image hpFill;
        Text hpText;
        Text ammoText;
        Text roundText;
        Text scoreText;
        Text bigCenterText;
        Text reloadHintText;
        GameObject gameOverPanel;
        Text gameOverText;

        // Polish
        Image damageVignette;
        Image hitMarker;
        Image[] crosshairLines = new Image[4];
        Font hudFont;                  // cached so WorldKillPopup spawns can reuse it

        float roundBannerUntil;
        float damageFlashEnd;
        float damageFlashStart;
        float hitMarkerEnd;
        // True when the most recent hit was a confirmed kill — drives the hit
        // marker color (white otherwise, red on kill).
        bool hitMarkerKill;
        float crosshairKick;

        void Awake()
        {
            BuildCanvas();
        }

        void Start()
        {
            if (player != null)
            {
                player.OnHealthChanged += OnHealthChanged;
                player.OnDamaged += OnDamaged;
            }
            if (waves != null)
            {
                waves.OnRoundStart += OnRoundStart;
                waves.OnRoundEnd += OnRoundEnd;
                UpdateRound(waves.Round);
            }
            if (shooter != null)
            {
                shooter.OnFired += OnPlayerFired;
                shooter.OnEnemyHit += OnPlayerHitEnemy;
                shooter.OnEnemyKilledByShot += OnPlayerKilledEnemy;
            }
            // Subscribe to ANY enemy death (including AoE chains, splitters, etc)
            // so the world-space popup appears at every kill, not just direct shots.
            if (waves != null)
            {
                waves.OnEnemyKilled += OnAnyEnemyKilled;
            }
            if (player != null) OnHealthChanged(player.Health, player.MaxHealth);
        }

        void Update()
        {
            if (shooter != null)
            {
                string prefix = shooter.CurrentName;
                if (shooter.IsReloading)
                {
                    ammoText.text = $"{prefix}  RELOADING";
                    ammoText.color = new Color(1f, 0.6f, 0.2f);
                }
                else if (shooter.MagSize <= 0)
                {
                    // Melee or unlimited
                    ammoText.text = $"{prefix}";
                    ammoText.color = Color.white;
                }
                else
                {
                    ammoText.text = $"{prefix}  {shooter.AmmoInMag}/{shooter.MagSize}";
                    ammoText.color = shooter.AmmoInMag <= 3 ? new Color(1f, 0.3f, 0.3f) : Color.white;
                }
            }

            if (bigCenterText != null && bigCenterText.gameObject.activeSelf)
            {
                if (Time.time > roundBannerUntil)
                    bigCenterText.gameObject.SetActive(false);
            }

            UpdateDamageVignette();
            UpdateCrosshair();
            UpdateHitMarker();
            // (Floating score is now WORLD-space — each popup is a standalone
            //  WorldKillPopup component spawned at the kill location and self-managing.)
        }

        // ---- POLISH UPDATES ----

        void UpdateDamageVignette()
        {
            if (damageVignette == null) return;
            float t = Mathf.Clamp01((damageFlashEnd - Time.time) / Mathf.Max(0.01f, damageFlashEnd - damageFlashStart));
            float alpha = t * 0.45f;
            // Also gentle ongoing tint when HP is low
            if (player != null)
            {
                float hpFrac = player.MaxHealth > 0 ? player.Health / player.MaxHealth : 1f;
                if (hpFrac < 0.35f)
                    alpha = Mathf.Max(alpha, (0.35f - hpFrac) / 0.35f * 0.18f * (0.6f + 0.4f * Mathf.Sin(Time.time * 5f)));
            }
            var c = damageVignette.color;
            c.a = alpha;
            damageVignette.color = c;
        }

        void UpdateCrosshair()
        {
            if (crosshairLines == null) return;
            // base offset + kick from firing/inaccuracy
            float baseOff = 8f;
            float spread = (shooter != null) ? shooter.AmmoInMag <= 0 ? 18f : 0f : 0f;
            float kick = crosshairKick;
            float off = baseOff + kick + spread;

            // Up, down, left, right
            if (crosshairLines[0] != null) crosshairLines[0].rectTransform.anchoredPosition = new Vector2(0, off);
            if (crosshairLines[1] != null) crosshairLines[1].rectTransform.anchoredPosition = new Vector2(0, -off);
            if (crosshairLines[2] != null)
            {
                crosshairLines[2].rectTransform.anchoredPosition = new Vector2(-off, 0);
                crosshairLines[2].rectTransform.sizeDelta = new Vector2(10, 2);
            }
            if (crosshairLines[3] != null)
            {
                crosshairLines[3].rectTransform.anchoredPosition = new Vector2(off, 0);
                crosshairLines[3].rectTransform.sizeDelta = new Vector2(10, 2);
            }

            crosshairKick = Mathf.MoveTowards(crosshairKick, 0f, 30f * Time.deltaTime);
        }

        // ---- Hit marker (the X overlaid on the crosshair) ----
        //
        // White X on any hit, RED X when the hit killed the enemy. Kill-state is
        // sticky for the duration of the marker so a kill on the last frame of a
        // burst still reads as red. The 4-line crosshair underneath is rendered
        // separately and its color is NOT touched here.
        void UpdateHitMarker()
        {
            if (hitMarker == null) return;
            float t = (hitMarkerEnd - Time.time);
            if (t <= 0f)
            {
                // Marker expired — hide the arms.
                for (int i = 0; i < hitMarker.transform.childCount; i++)
                {
                    var img = hitMarker.transform.GetChild(i).GetComponent<Image>();
                    if (img != null) { var cc = img.color; cc.a = 0f; img.color = cc; }
                }
                return;
            }

            float lifeT = Mathf.Clamp01(t / 0.35f);
            // Kill marker pops a little bigger so it reads instantly.
            float scale = (hitMarkerKill ? 1.35f : 1.0f) * Mathf.Lerp(0.85f, 1.0f, lifeT);
            hitMarker.rectTransform.localScale = Vector3.one * scale;

            // Faded by default so it doesn't fight the crosshair. Kills are slightly
            // brighter than plain hits.
            float maxAlpha = hitMarkerKill ? 0.85f : 0.55f;
            float alpha = lifeT * maxAlpha;
            Color col = hitMarkerKill
                ? new Color(1.0f, 0.25f, 0.22f, alpha)   // red on kill
                : new Color(1.0f, 1.0f,  1.0f,  alpha);  // white on hit

            // Parent is just a container; the two child arms are what's visible.
            for (int i = 0; i < hitMarker.transform.childCount; i++)
            {
                var img = hitMarker.transform.GetChild(i).GetComponent<Image>();
                if (img != null) img.color = col;
            }
        }

        // ---- EVENT HANDLERS ----

        void OnDamaged(float amount)
        {
            damageFlashStart = Time.time;
            damageFlashEnd = Time.time + 0.4f;
        }

        void OnPlayerFired()
        {
            crosshairKick = Mathf.Min(20f, crosshairKick + 6f);
        }

        /// <summary>
        /// A shot landed on an enemy but didn't kill it. Shows a white hit marker.
        /// Reset the kill flag so subsequent hits read as white again.
        /// </summary>
        void OnPlayerHitEnemy()
        {
            hitMarkerEnd = Time.time + 0.35f;
            hitMarkerKill = false;
        }

        /// <summary>
        /// Player's bullet killed an enemy. Flip the hit marker red. The marker
        /// timer is also refreshed so the red pulse always plays out fully.
        /// </summary>
        void OnPlayerKilledEnemy()
        {
            hitMarkerEnd = Time.time + 0.35f;
            hitMarkerKill = true;
        }

        /// <summary>
        /// Any enemy died — including AoE chain kills and lifetime-expired statics.
        /// Spawns a world-space "+score" popup at the death position.
        /// </summary>
        void OnAnyEnemyKilled(float score, Vector3 worldPos)
        {
            if (canvas == null || hudFont == null) return;
            WorldKillPopup.Spawn(canvas.transform, hudFont, worldPos, Mathf.RoundToInt(score));
        }

        void BuildCanvas()
        {
            var go = new GameObject("HUD_Canvas");
            go.transform.SetParent(transform, false);
            canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null) font = Resources.FindObjectsOfTypeAll<Font>().Length > 0 ? Resources.FindObjectsOfTypeAll<Font>()[0] : null;
            hudFont = font;     // cached for spawning WorldKillPopups later

            // --- Damage vignette (full-screen red flash overlay) ---
            damageVignette = CreateUI("DamageVignette", canvas.transform);
            var dvRT = damageVignette.rectTransform;
            dvRT.anchorMin = Vector2.zero; dvRT.anchorMax = Vector2.one;
            dvRT.offsetMin = dvRT.offsetMax = Vector2.zero;
            damageVignette.color = new Color(0.7f, 0.05f, 0.08f, 0f);

            // --- Crosshair: 4 lines forming a cross, animatable ---
            for (int i = 0; i < 4; i++)
            {
                var ln = CreateUI("CrosshairLine" + i, canvas.transform);
                ln.rectTransform.sizeDelta = new Vector2(2, 10);
                ln.rectTransform.anchorMin = ln.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                ln.color = new Color(1f, 1f, 1f, 0.9f);
                crosshairLines[i] = ln;
            }
            // Center dot
            var dot = CreateUI("CrosshairDot", canvas.transform);
            dot.rectTransform.sizeDelta = new Vector2(2.5f, 2.5f);
            dot.rectTransform.anchorMin = dot.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            dot.color = new Color(1f, 1f, 1f, 0.9f);

            // --- Hit marker (X shape, made of two rotated lines) ---
            hitMarker = CreateUI("HitMarker", canvas.transform);
            hitMarker.rectTransform.sizeDelta = new Vector2(28, 28);
            hitMarker.rectTransform.anchorMin = hitMarker.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            hitMarker.color = new Color(1f, 1f, 1f, 0f);
            // Two children for the X
            for (int i = 0; i < 2; i++)
            {
                var arm = CreateUI("HMArm" + i, hitMarker.transform);
                arm.rectTransform.sizeDelta = new Vector2(2.2f, 26);
                arm.rectTransform.anchorMin = arm.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                arm.rectTransform.localRotation = Quaternion.Euler(0, 0, i == 0 ? 45f : -45f);
                arm.color = Color.white;
            }

            // Note: the old screen-space "+N" popup near the HUD score was removed —
            // replaced by world-space WorldKillPopup instances spawned at each kill
            // location. See OnAnyEnemyKilled() and WorldKillPopup.cs.

            // --- Health bar (bottom-left) ---
            var hpBg = CreateUI("HPBG", canvas.transform);
            hpBg.rectTransform.sizeDelta = new Vector2(360, 28);
            hpBg.rectTransform.anchorMin = hpBg.rectTransform.anchorMax = new Vector2(0, 0);
            hpBg.rectTransform.pivot = new Vector2(0, 0);
            hpBg.rectTransform.anchoredPosition = new Vector2(28, 28);
            hpBg.color = new Color(0, 0, 0, 0.55f);

            // Fill bar uses left-anchored scaling instead of Image.Filled
            // (Image.Filled needs an actual sprite to render — pure color fills don't).
            var hpFillImg = CreateUI("HPFill", hpBg.transform);
            hpFillImg.rectTransform.anchorMin = new Vector2(0, 0);
            hpFillImg.rectTransform.anchorMax = new Vector2(1, 1);
            hpFillImg.rectTransform.pivot = new Vector2(0, 0.5f);
            hpFillImg.rectTransform.offsetMin = new Vector2(3, 3);
            hpFillImg.rectTransform.offsetMax = new Vector2(-3, -3);
            hpFillImg.color = new Color(0.85f, 0.2f, 0.25f);
            hpFill = hpFillImg;

            hpText = CreateText("HPText", hpBg.transform, font, 18, TextAnchor.MiddleCenter, "100 / 100");
            hpText.rectTransform.anchorMin = new Vector2(0, 0);
            hpText.rectTransform.anchorMax = new Vector2(1, 1);
            hpText.rectTransform.offsetMin = hpText.rectTransform.offsetMax = Vector2.zero;
            hpText.color = Color.white;

            // --- Ammo (bottom-right) ---
            ammoText = CreateText("Ammo", canvas.transform, font, 42, TextAnchor.LowerRight, "15/15");
            ammoText.rectTransform.anchorMin = ammoText.rectTransform.anchorMax = new Vector2(1, 0);
            ammoText.rectTransform.pivot = new Vector2(1, 0);
            ammoText.rectTransform.anchoredPosition = new Vector2(-32, 28);
            ammoText.rectTransform.sizeDelta = new Vector2(300, 60);

            reloadHintText = CreateText("ReloadHint", canvas.transform, font, 16, TextAnchor.LowerRight, "R to reload");
            reloadHintText.rectTransform.anchorMin = reloadHintText.rectTransform.anchorMax = new Vector2(1, 0);
            reloadHintText.rectTransform.pivot = new Vector2(1, 0);
            reloadHintText.rectTransform.anchoredPosition = new Vector2(-32, 92);
            reloadHintText.rectTransform.sizeDelta = new Vector2(260, 24);
            reloadHintText.color = new Color(1, 1, 1, 0.6f);

            // --- Round (top-center) ---
            roundText = CreateText("Round", canvas.transform, font, 36, TextAnchor.UpperCenter, "ROUND 1");
            roundText.rectTransform.anchorMin = roundText.rectTransform.anchorMax = new Vector2(0.5f, 1);
            roundText.rectTransform.pivot = new Vector2(0.5f, 1);
            roundText.rectTransform.anchoredPosition = new Vector2(0, -28);
            roundText.rectTransform.sizeDelta = new Vector2(400, 50);
            roundText.color = new Color(1f, 0.95f, 0.7f);

            // --- Score (top-right) ---
            scoreText = CreateText("Score", canvas.transform, font, 28, TextAnchor.UpperRight, "Score: 0");
            scoreText.rectTransform.anchorMin = scoreText.rectTransform.anchorMax = new Vector2(1, 1);
            scoreText.rectTransform.pivot = new Vector2(1, 1);
            scoreText.rectTransform.anchoredPosition = new Vector2(-28, -28);
            scoreText.rectTransform.sizeDelta = new Vector2(360, 40);

            // --- Big center banner ---
            bigCenterText = CreateText("Banner", canvas.transform, font, 72, TextAnchor.MiddleCenter, "");
            bigCenterText.rectTransform.anchorMin = bigCenterText.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            bigCenterText.rectTransform.anchoredPosition = new Vector2(0, 180);
            bigCenterText.rectTransform.sizeDelta = new Vector2(1200, 120);
            bigCenterText.color = new Color(1f, 0.95f, 0.4f);
            bigCenterText.gameObject.SetActive(false);

            // --- Game-over overlay ---
            gameOverPanel = new GameObject("GameOver");
            gameOverPanel.transform.SetParent(canvas.transform, false);
            var goImg = gameOverPanel.AddComponent<Image>();
            goImg.color = new Color(0, 0, 0, 0.75f);
            var goRT = goImg.rectTransform;
            goRT.anchorMin = Vector2.zero;
            goRT.anchorMax = Vector2.one;
            goRT.offsetMin = goRT.offsetMax = Vector2.zero;

            gameOverText = CreateText("GOText", gameOverPanel.transform, font, 64, TextAnchor.MiddleCenter,
                "GAME OVER\nPress R to restart");
            gameOverText.rectTransform.anchorMin = Vector2.zero;
            gameOverText.rectTransform.anchorMax = Vector2.one;
            gameOverText.rectTransform.offsetMin = gameOverText.rectTransform.offsetMax = Vector2.zero;
            gameOverText.color = Color.white;
            gameOverPanel.SetActive(false);
        }

        static Image CreateUI(string name, Transform parent)
        {
            var g = new GameObject(name);
            g.transform.SetParent(parent, false);
            var img = g.AddComponent<Image>();
            img.raycastTarget = false;
            return img;
        }

        static Text CreateText(string name, Transform parent, Font font, int size, TextAnchor anchor, string content)
        {
            var g = new GameObject(name);
            g.transform.SetParent(parent, false);
            var t = g.AddComponent<Text>();
            t.font = font;
            t.fontSize = size;
            t.alignment = anchor;
            t.text = content;
            t.color = Color.white;
            t.raycastTarget = false;
            var outline = g.AddComponent<Outline>();
            outline.effectColor = new Color(0, 0, 0, 0.85f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);
            return t;
        }

        void OnHealthChanged(float current, float max)
        {
            float t = Mathf.Clamp01(current / Mathf.Max(1f, max));
            if (hpFill != null)
            {
                var rt = hpFill.rectTransform;
                rt.localScale = new Vector3(t, 1f, 1f);
                hpFill.color = Color.Lerp(new Color(0.85f, 0.15f, 0.2f), new Color(0.3f, 0.85f, 0.3f), t);
            }
            if (hpText != null) hpText.text = $"{Mathf.CeilToInt(current)} / {Mathf.CeilToInt(max)}";
        }

        public void UpdateScore(int score)
        {
            if (scoreText != null) scoreText.text = $"Score: {score}";
            // The "+score" popup is now world-space and is spawned by
            // OnAnyEnemyKilled directly — UpdateScore no longer triggers it.
        }

        public void UpdateRound(int round)
        {
            if (roundText != null) roundText.text = $"ROUND {round}";
        }

        void OnRoundStart(int round)
        {
            UpdateRound(round);
            if (bigCenterText != null)
            {
                bool boss = waves != null && (round % waves.bossInterval == 0);
                bigCenterText.text = boss ? $"BOSS ROUND {round}" : $"ROUND {round}";
                bigCenterText.color = boss ? new Color(1f, 0.4f, 0.5f) : new Color(1f, 0.95f, 0.4f);
                bigCenterText.gameObject.SetActive(true);
                roundBannerUntil = Time.time + 2.2f;
            }
        }

        void OnRoundEnd(int round)
        {
            if (bigCenterText != null)
            {
                bigCenterText.text = "ROUND CLEARED";
                bigCenterText.color = new Color(0.6f, 1f, 0.6f);
                bigCenterText.gameObject.SetActive(true);
                roundBannerUntil = Time.time + 2.0f;
            }
        }

        public void ShowGameOver(int finalScore, int round)
        {
            if (gameOverPanel != null) gameOverPanel.SetActive(true);
            if (gameOverText != null)
                gameOverText.text = $"GAME OVER\nReached round {round}\nScore: {finalScore}\n\nPress R to restart";
        }
    }
}
