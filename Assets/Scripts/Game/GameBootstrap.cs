using UnityEngine;

namespace BallShooter
{
    /// <summary>
    /// Auto-runs at scene load. Shows the main menu first; the arena/player/UI
    /// are only built when the player clicks PLAY (via StartGame()).
    /// </summary>
    public static class GameBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Run()
        {
            // Don't double-bootstrap if a player already exists
            if (Object.FindFirstObjectByType<PlayerController>() != null) return;
            if (Object.FindFirstObjectByType<MainMenu>() != null) return;
            MainMenu.Show();
        }

        /// <summary>Called by the main menu Play button to build the actual game.</summary>
        public static void StartGame()
        {
            if (Object.FindFirstObjectByType<PlayerController>() != null) return;

            BuildEnvironment();
            BuildLighting();
            PostFXSetup.Install();
            BuildAudio();
            var player = BuildPlayer();
            var ui = BuildUI(player);
            BuildGameManager(player, ui);
        }

        static void BuildAudio()
        {
            if (Object.FindFirstObjectByType<SoundFX>() != null) return;
            var go = new GameObject("SoundFX");
            go.AddComponent<SoundFX>();
        }

        // ---------------- ENVIRONMENT ----------------

        static Shader LitShader()
        {
            return Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        }

        static Material MakeMat(Color c, bool emissive = false, float smoothness = 0.2f)
        {
            var m = new Material(LitShader());
            m.color = c;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smoothness);
            if (emissive)
            {
                m.EnableKeyword("_EMISSION");
                if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", c * 1.5f);
            }
            return m;
        }

        static GameObject MakePrimitive(PrimitiveType type, string name, Vector3 pos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.position = pos;
            go.transform.localScale = scale;
            var r = go.GetComponent<Renderer>();
            if (r != null) r.sharedMaterial = mat;
            return go;
        }

        static void BuildEnvironment()
        {
            // Arena root
            var root = new GameObject("Arena");

            // Floor — procedurally-textured plane with a subtle grid pattern
            var floorMat = MakeMat(new Color(0.22f, 0.23f, 0.27f), false, 0.05f);
            var gridTex = MakeGridTexture(512, 32,
                new Color(0.18f, 0.19f, 0.22f),
                new Color(0.32f, 0.34f, 0.42f));
            if (floorMat.HasProperty("_BaseMap")) floorMat.SetTexture("_BaseMap", gridTex);
            if (floorMat.HasProperty("_MainTex")) floorMat.SetTexture("_MainTex", gridTex);
            if (floorMat.HasProperty("_BaseMap_ST"))
                floorMat.SetTextureScale("_BaseMap", new Vector2(20, 20));
            else
                floorMat.mainTextureScale = new Vector2(20, 20);

            var floor = MakePrimitive(PrimitiveType.Plane, "Floor", Vector3.zero, new Vector3(8, 1, 8), floorMat);
            floor.transform.SetParent(root.transform, true);

            // Outer walls — tall enough that flyers can't escape over the top
            var wallMat = MakeMat(new Color(0.32f, 0.34f, 0.4f));
            BuildWall(root.transform, new Vector3(0, 6f, 40), new Vector3(80, 12, 1), wallMat, "WallN");
            BuildWall(root.transform, new Vector3(0, 6f, -40), new Vector3(80, 12, 1), wallMat, "WallS");
            BuildWall(root.transform, new Vector3(40, 6f, 0), new Vector3(1, 12, 80), wallMat, "WallE");
            BuildWall(root.transform, new Vector3(-40, 6f, 0), new Vector3(1, 12, 80), wallMat, "WallW");

            // Cover / pillars / crates (a bit of vertical interest + tactical movement)
            var crateMat = MakeMat(new Color(0.55f, 0.4f, 0.25f));
            var pillarMat = MakeMat(new Color(0.45f, 0.45f, 0.5f));

            // Symmetric crates
            for (int sx = -1; sx <= 1; sx += 2)
            for (int sz = -1; sz <= 1; sz += 2)
            {
                MakePrimitive(PrimitiveType.Cube, "Crate",
                    new Vector3(8f * sx, 1f, 8f * sz),
                    new Vector3(2.5f, 2f, 2.5f), crateMat).transform.SetParent(root.transform, true);
                MakePrimitive(PrimitiveType.Cube, "CrateSmall",
                    new Vector3(15f * sx, 0.75f, 4f * sz),
                    new Vector3(1.5f, 1.5f, 1.5f), crateMat).transform.SetParent(root.transform, true);
            }

            // Pillars at the four mid-distance points
            for (int i = 0; i < 4; i++)
            {
                float ang = i * Mathf.PI * 0.5f + Mathf.PI * 0.25f;
                Vector3 p = new Vector3(Mathf.Cos(ang) * 18f, 2.5f, Mathf.Sin(ang) * 18f);
                MakePrimitive(PrimitiveType.Cylinder, "Pillar", p, new Vector3(1.6f, 2.5f, 1.6f), pillarMat)
                    .transform.SetParent(root.transform, true);
            }

            // A few elevated platforms to enable slide-jumping fun
            var platMat = MakeMat(new Color(0.3f, 0.5f, 0.6f));
            MakePrimitive(PrimitiveType.Cube, "Platform1", new Vector3(0, 1.0f, 22), new Vector3(8, 0.4f, 4), platMat)
                .transform.SetParent(root.transform, true);
            MakePrimitive(PrimitiveType.Cube, "Platform2", new Vector3(0, 1.0f, -22), new Vector3(8, 0.4f, 4), platMat)
                .transform.SetParent(root.transform, true);
            MakePrimitive(PrimitiveType.Cube, "RampL", new Vector3(-22, 1.0f, 0), new Vector3(4, 0.4f, 8), platMat)
                .transform.SetParent(root.transform, true);
            MakePrimitive(PrimitiveType.Cube, "RampR", new Vector3(22, 1.0f, 0), new Vector3(4, 0.4f, 8), platMat)
                .transform.SetParent(root.transform, true);

            // Skybox-ish ambient
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.55f, 0.6f, 0.75f);
            RenderSettings.ambientEquatorColor = new Color(0.35f, 0.35f, 0.4f);
            RenderSettings.ambientGroundColor = new Color(0.1f, 0.1f, 0.12f);
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.18f, 0.2f, 0.26f);
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 25f;
            RenderSettings.fogEndDistance = 80f;

            Camera.main?.gameObject.SetActive(false); // disable any leftover scene camera; we'll spawn our own
        }

        static void BuildWall(Transform parent, Vector3 pos, Vector3 scale, Material mat, string name)
        {
            MakePrimitive(PrimitiveType.Cube, name, pos, scale, mat).transform.SetParent(parent, true);
        }

        /// <summary>
        /// Procedural grid texture. Used on the floor to give the arena scale.
        /// </summary>
        static Texture2D MakeGridTexture(int size, int cellSize, Color baseColor, Color lineColor)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true);
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Bilinear;
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool onLine = (x % cellSize) < 1 || (y % cellSize) < 1;
                    // Soft tint variation so it doesn't look like flat tile
                    float n = Mathf.PerlinNoise(x * 0.02f, y * 0.02f) * 0.06f;
                    Color c = onLine ? lineColor : baseColor + new Color(n, n, n, 0);
                    pixels[y * size + x] = c;
                }
            }
            tex.SetPixels(pixels);
            tex.Apply(true);
            return tex;
        }

        // ---------------- LIGHTING ----------------

        static void BuildLighting()
        {
            // Remove any existing directional lights so our own controls the look
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (l != null && l.type == LightType.Directional) Object.Destroy(l.gameObject);
            }

            // Key (sun) — warm directional, soft shadows
            var sun = new GameObject("Sun");
            sun.transform.rotation = Quaternion.Euler(48f, 35f, 0f);
            var sunLight = sun.AddComponent<Light>();
            sunLight.type = LightType.Directional;
            sunLight.color = new Color(1f, 0.93f, 0.8f);
            sunLight.intensity = 1.1f;
            sunLight.shadows = LightShadows.Soft;
            sunLight.shadowStrength = 0.85f;

            // Four colored point-light accents at the corners for mood
            var palette = new Color[]
            {
                new Color(0.4f, 0.8f, 1.2f),    // cyan
                new Color(1.2f, 0.4f, 0.6f),    // magenta
                new Color(0.5f, 1.2f, 0.7f),    // mint
                new Color(1.2f, 0.7f, 0.3f),    // amber
            };
            Vector3[] corners =
            {
                new Vector3(-30, 7,  30),
                new Vector3( 30, 7, -30),
                new Vector3(-30, 7, -30),
                new Vector3( 30, 7,  30),
            };
            for (int i = 0; i < 4; i++)
            {
                var lgo = new GameObject("AccentLight_" + i);
                lgo.transform.position = corners[i];
                var pl = lgo.AddComponent<Light>();
                pl.type = LightType.Point;
                pl.color = palette[i];
                pl.intensity = 4f;
                pl.range = 28f;
                pl.shadows = LightShadows.None;
            }
        }

        // ---------------- PLAYER ----------------

        static PlayerController BuildPlayer()
        {
            var playerGO = new GameObject("Player");
            playerGO.tag = "Player";
            playerGO.transform.position = new Vector3(0, 1f, 0);

            var cc = playerGO.AddComponent<CharacterController>();
            cc.height = 1.9f;
            cc.radius = 0.4f;
            cc.center = new Vector3(0, 0.95f, 0);
            cc.slopeLimit = 50f;
            cc.stepOffset = 0.4f;
            cc.skinWidth = 0.05f;

            // Camera holder
            var camHolder = new GameObject("CameraHolder").transform;
            camHolder.SetParent(playerGO.transform, false);
            camHolder.localPosition = new Vector3(0, 1.65f, 0);

            // Camera
            var camGO = new GameObject("FirstPersonCamera");
            camGO.tag = "MainCamera";
            camGO.transform.SetParent(camHolder, false);
            var cam = camGO.AddComponent<Camera>();
            cam.fieldOfView = 90f;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 300f;
            camGO.AddComponent<AudioListener>();

            // Camera shake on the camera (so it stacks on top of PlayerLook rotation)
            camGO.AddComponent<CameraShake>();

            // Scripts
            var pc = playerGO.AddComponent<PlayerController>();
            pc.cameraPivot = playerGO.transform;
            pc.cameraHolder = camHolder;

            var look = playerGO.AddComponent<PlayerLook>();
            look.cameraHolder = camHolder;

            var shooter = playerGO.AddComponent<PlayerShooter>();
            shooter.fpCamera = cam;

            var hp = playerGO.AddComponent<PlayerHealth>();

            // Footstep audio driver
            var fs = playerGO.AddComponent<FootstepAudio>();
            fs.player = pc;

            // Weapon root — its own transform that WeaponBob animates
            var weaponRoot = new GameObject("WeaponRoot").transform;
            weaponRoot.SetParent(camGO.transform, false);
            weaponRoot.localPosition = new Vector3(0.32f, -0.28f, 0.55f);

            var bob = weaponRoot.gameObject.AddComponent<WeaponBob>();
            bob.player = pc;

            // Gun body
            var gunMat = MakeMat(new Color(0.15f, 0.15f, 0.18f), false, 0.6f);
            var gun = MakePrimitive(PrimitiveType.Cube, "Gun",
                Vector3.zero, new Vector3(0.18f, 0.18f, 0.7f), gunMat);
            gun.transform.SetParent(weaponRoot, false);
            gun.transform.localPosition = Vector3.zero;
            var gc = gun.GetComponent<Collider>();
            if (gc != null) Object.Destroy(gc);

            // Barrel
            var barrelMat = MakeMat(new Color(0.05f, 0.05f, 0.06f), false, 0.7f);
            var barrel = MakePrimitive(PrimitiveType.Cylinder, "Barrel",
                Vector3.zero, new Vector3(0.06f, 0.18f, 0.06f), barrelMat);
            barrel.transform.SetParent(gun.transform, false);
            barrel.transform.localPosition = new Vector3(0, 0, 0.5f);
            barrel.transform.localRotation = Quaternion.Euler(90f, 0, 0);
            var bc = barrel.GetComponent<Collider>();
            if (bc != null) Object.Destroy(bc);

            // Muzzle point at barrel tip
            var muzzlePoint = new GameObject("Muzzle").transform;
            muzzlePoint.SetParent(gun.transform, false);
            muzzlePoint.localPosition = new Vector3(0, 0, 0.95f);

            // Muzzle light (very brief flash)
            var muzzleLightGO = new GameObject("MuzzleLight");
            muzzleLightGO.transform.SetParent(muzzlePoint, false);
            var muzzleLight = muzzleLightGO.AddComponent<Light>();
            muzzleLight.type = LightType.Point;
            muzzleLight.color = new Color(1f, 0.85f, 0.4f);
            muzzleLight.intensity = 6f;
            muzzleLight.range = 4f;
            muzzleLight.enabled = false;

            // Wire shooter to weapon feel components
            shooter.weaponBob = bob;
            shooter.muzzleLight = muzzleLight;
            shooter.muzzlePoint = muzzlePoint;
            shooter.gunMesh = gun.transform;
            shooter.gunRenderer = gun.GetComponent<Renderer>();
            shooter.weaponRoot = weaponRoot;

            // ---- Default loadout: chosen primary + pistol + knife ----
            int saved = PlayerPrefs.GetInt("BallShooter_PrimaryWeapon", (int)WeaponType.Assault);
            WeaponData primary;
            switch ((WeaponType)saved)
            {
                case WeaponType.SMG:    primary = WeaponData.SMG(); break;
                case WeaponType.Sniper: primary = WeaponData.Sniper(); break;
                default:                primary = WeaponData.Assault(); break;
            }
            shooter.SetWeapons(new[] { primary, WeaponData.Pistol(), WeaponData.Knife() });

            return pc;
        }

        // ---------------- UI + MANAGERS ----------------

        static GameUI BuildUI(PlayerController player)
        {
            var go = new GameObject("UIRoot");
            var ui = go.AddComponent<GameUI>();
            ui.player = player.GetComponent<PlayerHealth>();
            ui.shooter = player.GetComponent<PlayerShooter>();
            return ui;
        }

        static void BuildGameManager(PlayerController player, GameUI ui)
        {
            var go = new GameObject("GameManager");

            var waves = go.AddComponent<WaveManager>();
            waves.player = player.transform;

            var gm = go.AddComponent<GameManager>();
            gm.player = player.GetComponent<PlayerHealth>();
            gm.waves = waves;
            gm.ui = ui;

            // Hand WaveManager to the UI
            ui.waves = waves;

            // Pause / settings menu, toggled with X. Wired to PlayerLook, the FP camera,
            // and SoundFX so it can adjust sensitivity, FOV, and volume live.
            var pauseGO = new GameObject("PauseMenu");
            var pause = pauseGO.AddComponent<PauseMenu>();
            pause.look = player.GetComponent<PlayerLook>();
            var shooter = player.GetComponent<PlayerShooter>();
            if (shooter != null) pause.fpCamera = shooter.fpCamera;
            pause.sfx = Object.FindFirstObjectByType<SoundFX>();
        }
    }
}
