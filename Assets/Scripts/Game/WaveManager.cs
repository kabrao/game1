using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BallShooter
{
    /// <summary>
    /// Zombies-style round system. Each round spawns N balls scaled by round.
    /// Every bossInterval rounds the wave is a boss round.
    /// </summary>
    public class WaveManager : MonoBehaviour
    {
        [Header("Refs")]
        public Transform player;
        public Transform[] spawnPoints;     // optional. If empty, spawns in a ring around the player.

        [Header("Round pacing")]
        public int startingBalls = 8;
        public int ballsPerRound = 4;        // additional per round
        public float timeBetweenRounds = 2.5f;
        public float spawnInterval = 0.22f;  // was 0.45 — faster trickle
        public int bossInterval = 5;

        [Header("Scaling")]
        public float baseBallHP = 30f;
        public float ballHPPerRound = 8f;
        public float baseBallSpeed = 7.5f;      // was 4
        public float ballSpeedPerRound = 0.45f; // was 0.25
        public float maxBallSpeed = 14f;        // was 9
        public float baseBallDamage = 10f;
        public float ballDamagePerRound = 1.2f;

        [Header("Boss scaling")]
        public float baseBossHP = 900f;
        public float bossHPPerCycle = 450f;

        [Header("Flying enemies")]
        public int flyerStartRound = 1;
        public int flyerStartCount = 3;       // more pressure
        public int flyerPerRound = 1;
        public int maxFlyersPerRound = 14;

        [Header("Explosive enemies")]
        public int explosiveStartRound = 3;
        public int explosiveStartCount = 1;
        public int explosivePerRound = 1;
        public int maxExplosivesPerRound = 6;

        [Header("Splitter enemies")]
        public int splitterStartRound = 4;
        public int splitterStartCount = 1;
        public int splitterPerRound = 1;
        public int maxSplittersPerRound = 5;

        [Header("Aim-trainer enemies")]
        public int staticTargetStartRound = 1;
        public int staticTargetStartCount = 3;
        public int staticTargetPerRound = 1;
        public int maxStaticPerRound = 8;

        public int trackerStartRound = 2;
        public int trackerStartCount = 2;
        public int trackerPerRound = 1;
        public int maxTrackersPerRound = 6;

        public int tinyFastStartRound = 2;
        public int tinyFastStartCount = 2;
        public int tinyFastPerRound = 1;
        public int maxTinyPerRound = 10;

        [Header("Size variation")]
        [Tooltip("Random scale range applied to every regular ball spawn.")]
        public Vector2 ballScaleRange = new Vector2(0.7f, 1.7f);
        [Tooltip("Random scale range applied to every flying enemy spawn.")]
        public Vector2 flyerScaleRange = new Vector2(0.7f, 1.4f);
        [Tooltip("Random scale range applied to bosses.")]
        public Vector2 bossScaleRange = new Vector2(0.9f, 1.25f);
        [Tooltip("HP/damage scales by size^this exponent. 0 = ignore size, 2 = strong (bigger = much tankier).")]
        public float sizeStatExponent = 1.4f;
        public float baseFlyerHP = 22f;
        public float flyerHPPerRound = 5f;
        public float baseFlyerSpeed = 6.5f;
        public float flyerSpeedPerRound = 0.15f;
        public float maxFlyerSpeed = 11f;
        public float baseFlyerDamage = 12f;
        public float flyerDamagePerRound = 1f;

        // Runtime
        public int Round { get; private set; }
        public int Alive => alive.Count;

        readonly HashSet<GameObject> alive = new HashSet<GameObject>();
        bool running;

        public System.Action<int> OnRoundStart;
        public System.Action<int> OnRoundEnd;
        /// <summary>
        /// Fired whenever any enemy dies, for ANY reason (player shot, AoE chain,
        /// lifetime expiry on a static target, etc). Passes the score awarded and
        /// the world position where the enemy died — UI uses both for the floating
        /// kill popup.
        /// </summary>
        public System.Action<float, Vector3> OnEnemyKilled;

        void Start()
        {
            StartCoroutine(RunRounds());
        }

        IEnumerator RunRounds()
        {
            running = true;
            yield return new WaitForSeconds(1.5f);

            while (running)
            {
                Round++;
                OnRoundStart?.Invoke(Round);

                bool boss = (Round % bossInterval == 0);
                if (boss)
                {
                    SpawnBoss();
                    // also a couple of small adds
                    int adds = Mathf.Min(8, 3 + Round / 2);
                    for (int i = 0; i < adds; i++)
                    {
                        SpawnBall();
                        yield return new WaitForSeconds(spawnInterval);
                    }
                }
                else
                {
                    int total = startingBalls + (Round - 1) * ballsPerRound;
                    int flyers = (Round >= flyerStartRound)
                        ? Mathf.Min(maxFlyersPerRound, flyerStartCount + (Round - flyerStartRound) * flyerPerRound) : 0;
                    int explosives = (Round >= explosiveStartRound)
                        ? Mathf.Min(maxExplosivesPerRound, explosiveStartCount + (Round - explosiveStartRound) * explosivePerRound) : 0;
                    int splitters = (Round >= splitterStartRound)
                        ? Mathf.Min(maxSplittersPerRound, splitterStartCount + (Round - splitterStartRound) * splitterPerRound) : 0;
                    int statics = (Round >= staticTargetStartRound)
                        ? Mathf.Min(maxStaticPerRound, staticTargetStartCount + (Round - staticTargetStartRound) * staticTargetPerRound) : 0;
                    int trackers = (Round >= trackerStartRound)
                        ? Mathf.Min(maxTrackersPerRound, trackerStartCount + (Round - trackerStartRound) * trackerPerRound) : 0;
                    int tinies = (Round >= tinyFastStartRound)
                        ? Mathf.Min(maxTinyPerRound, tinyFastStartCount + (Round - tinyFastStartRound) * tinyFastPerRound) : 0;

                    // Build a queue of enemy "kind" ids
                    var queue = new System.Collections.Generic.List<int>();
                    for (int i = 0; i < total; i++) queue.Add(0);
                    for (int i = 0; i < flyers; i++) queue.Add(1);
                    for (int i = 0; i < explosives; i++) queue.Add(2);
                    for (int i = 0; i < splitters; i++) queue.Add(3);
                    for (int i = 0; i < statics; i++) queue.Add(4);
                    for (int i = 0; i < trackers; i++) queue.Add(5);
                    for (int i = 0; i < tinies; i++) queue.Add(6);
                    Shuffle(queue);

                    foreach (var kind in queue)
                    {
                        switch (kind)
                        {
                            case 0: SpawnBall(); break;
                            case 1: SpawnFlyer(); break;
                            case 2: SpawnExplosive(); break;
                            case 3: SpawnSplitter(); break;
                            case 4: SpawnStaticTarget(); break;
                            case 5: SpawnTracker(); break;
                            case 6: SpawnTinyFast(); break;
                        }
                        yield return new WaitForSeconds(spawnInterval);
                    }
                }

                // Wait for all enemies dead. Also clean up stale entries so a
                // stuck / fallen / lost enemy can't soft-lock the round.
                float waitStart = Time.time;
                while (true)
                {
                    CleanupStaleEnemies();
                    if (alive.Count == 0) break;
                    // Hard safety: if a round drags past 90s of waiting,
                    // nuke whatever's left and move on.
                    if (Time.time - waitStart > 90f)
                    {
                        ForceKillAll();
                        break;
                    }
                    yield return null;
                }

                OnRoundEnd?.Invoke(Round);
                yield return new WaitForSeconds(timeBetweenRounds);
            }
        }

        /// <summary>
        /// Removes destroyed / out-of-bounds / stuck enemies from the alive set
        /// so a round can't soft-lock if one gets wedged in geometry or knocked away.
        /// </summary>
        void CleanupStaleEnemies()
        {
            if (alive.Count == 0) return;

            // Collect first; can't modify the HashSet while iterating
            _toRemove.Clear();
            foreach (var go in alive)
            {
                if (go == null) { _toRemove.Add(go); continue; }

                // Fell below the arena
                if (go.transform.position.y < -10f)
                {
                    _toRemove.Add(go);
                    Destroy(go);
                    continue;
                }

                // Way outside the play zone
                Vector3 origin = player != null ? player.position : Vector3.zero;
                Vector3 toOrigin = go.transform.position - origin;
                toOrigin.y = 0f;
                if (toOrigin.sqrMagnitude > 60f * 60f)
                {
                    _toRemove.Add(go);
                    Destroy(go);
                    continue;
                }

                // Stuck check (no measurable movement for several seconds)
                var rb = go.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    if (!_stuckTimers.TryGetValue(go, out float t)) t = 0f;
                    if (rb.linearVelocity.sqrMagnitude < 0.05f) t += Time.deltaTime;
                    else t = 0f;
                    _stuckTimers[go] = t;
                    if (t > 6f)
                    {
                        // Give the enemy one big nudge toward the player before giving up
                        Vector3 dir = (origin - go.transform.position);
                        dir.y = 0.5f;
                        rb.AddForce(dir.normalized * 6f, ForceMode.VelocityChange);
                        _stuckTimers[go] = 3f; // reset partway so we'll retry, then despawn if still stuck
                    }
                    if (t > 12f)
                    {
                        _toRemove.Add(go);
                        Destroy(go);
                    }
                }
            }

            foreach (var go in _toRemove)
            {
                alive.Remove(go);
                _stuckTimers.Remove(go);
            }
            _toRemove.Clear();
        }

        void ForceKillAll()
        {
            foreach (var go in alive)
            {
                if (go != null) Destroy(go);
            }
            alive.Clear();
            _stuckTimers.Clear();
        }

        readonly List<GameObject> _toRemove = new List<GameObject>();
        readonly Dictionary<GameObject, float> _stuckTimers = new Dictionary<GameObject, float>();

        Vector3 PickSpawnPosition()
        {
            if (spawnPoints != null && spawnPoints.Length > 0)
            {
                var p = spawnPoints[Random.Range(0, spawnPoints.Length)];
                if (p != null) return p.position;
            }
            // ring around player at radius ~ 22
            float angle = Random.Range(0f, Mathf.PI * 2f);
            float radius = Random.Range(18f, 25f);
            Vector3 origin = player != null ? player.position : Vector3.zero;
            return new Vector3(origin.x + Mathf.Cos(angle) * radius, 1.2f, origin.z + Mathf.Sin(angle) * radius);
        }

        public BallEnemy SpawnBall()
        {
            float hp = baseBallHP + ballHPPerRound * (Round - 1);
            float spd = Mathf.Min(maxBallSpeed, baseBallSpeed + ballSpeedPerRound * (Round - 1));
            float dmg = baseBallDamage + ballDamagePerRound * (Round - 1);
            return SpawnBallAt(PickSpawnPosition(), hp, spd, dmg);
        }

        public BallEnemy SpawnBallAt(Vector3 position, float hp, float speed)
        {
            return SpawnBallAt(position, hp, speed, baseBallDamage + ballDamagePerRound * (Round - 1));
        }

        public BallEnemy SpawnBallAt(Vector3 position, float hp, float speed, float damage)
        {
            // Random size + size-scaled stats
            float sizeMul = Random.Range(ballScaleRange.x, ballScaleRange.y);
            float statMul = Mathf.Pow(sizeMul, sizeStatExponent);

            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "BallEnemy";
            // Lift spawn slightly so larger sizes don't clip the floor
            position.y = Mathf.Max(position.y, 1.0f * sizeMul);
            go.transform.position = position;
            go.transform.localScale = Vector3.one * (1.2f * sizeMul);
            ApplyEnemyMaterial(go, new Color(0.85f, 0.2f, 0.2f), false);

            var rb = go.AddComponent<Rigidbody>();
            rb.mass = 1.5f * statMul;

            var enemy = go.AddComponent<BallEnemy>();
            enemy.target = player;
            // Smaller balls jump more often, big ones less
            enemy.minJumpInterval = Mathf.Lerp(0.8f, 1.6f, Mathf.InverseLerp(0.7f, 1.7f, sizeMul));
            enemy.maxJumpInterval = enemy.minJumpInterval + 1.1f;
            enemy.jumpForce = Mathf.Lerp(9f, 6.5f, Mathf.InverseLerp(0.7f, 1.7f, sizeMul));

            enemy.Configure(hp * statMul, speed / Mathf.Sqrt(sizeMul),
                            damage * statMul, (10f + Round) * statMul, ColorByRound());
            enemy.OnDied += OnBallDied;
            HealthDots.AttachTo(go, hp * statMul);
            alive.Add(go);
            return enemy;
        }

        public FlyingEnemy SpawnFlyer()
        {
            // Spawn in air above a ring around the player
            float angle = Random.Range(0f, Mathf.PI * 2f);
            float radius = Random.Range(14f, 22f);
            Vector3 origin = player != null ? player.position : Vector3.zero;
            Vector3 pos = new Vector3(
                origin.x + Mathf.Cos(angle) * radius,
                origin.y + Random.Range(5f, 8f),
                origin.z + Mathf.Sin(angle) * radius);

            float sizeMul = Random.Range(flyerScaleRange.x, flyerScaleRange.y);
            float statMul = Mathf.Pow(sizeMul, sizeStatExponent);

            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "FlyingEnemy";
            go.transform.position = pos;
            go.transform.localScale = Vector3.one * (0.9f * sizeMul);
            var color = FlyerColorByRound();
            ApplyEnemyMaterial(go, color, true);

            var rb = go.AddComponent<Rigidbody>();
            rb.mass = 0.8f * statMul;

            var flyer = go.AddComponent<FlyingEnemy>();
            flyer.target = player;
            // Smaller flyers are more erratic
            flyer.orbitRadius = Random.Range(6f, 10f) * sizeMul;
            flyer.orbitSpeed = Random.Range(0.7f, 1.6f) / sizeMul;
            flyer.hoverHeight = Random.Range(4.5f, 7f);
            float hp = baseFlyerHP + flyerHPPerRound * (Round - 1);
            float spd = Mathf.Min(maxFlyerSpeed, baseFlyerSpeed + flyerSpeedPerRound * (Round - 1));
            float dmg = baseFlyerDamage + flyerDamagePerRound * (Round - 1);
            flyer.Configure(hp * statMul, spd / Mathf.Sqrt(sizeMul),
                            dmg * statMul, (20f + Round * 2f) * statMul, color);
            flyer.OnDied += OnFlyerDied;
            HealthDots.AttachTo(go, hp * statMul);
            alive.Add(go);
            return flyer;
        }

        Color FlyerColorByRound()
        {
            float t = Mathf.Clamp01((Round - 1) / 20f);
            return Color.Lerp(new Color(0.2f, 0.7f, 1f), new Color(0.85f, 0.3f, 1f), t);
        }

        public ExplosiveEnemy SpawnExplosive()
        {
            float sizeMul = Random.Range(0.9f, 1.3f);
            float statMul = Mathf.Pow(sizeMul, sizeStatExponent);
            Vector3 pos = PickSpawnPosition();
            pos.y = Mathf.Max(pos.y, 1.0f * sizeMul);

            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "ExplosiveEnemy";
            go.transform.position = pos;
            go.transform.localScale = Vector3.one * (1.1f * sizeMul);
            var color = new Color(1f, 0.65f, 0.1f);
            ApplyEnemyMaterial(go, color, true);

            var rb = go.AddComponent<Rigidbody>();
            rb.mass = 1.3f * statMul;

            var en = go.AddComponent<ExplosiveEnemy>();
            en.target = player;
            float explHP = (35f + 6f * (Round - 1)) * statMul;
            en.Configure(
                explHP,
                Mathf.Min(maxBallSpeed, 4.2f + 0.25f * (Round - 1)),
                (35f + 4f * (Round - 1)) * statMul,
                (30f + Round * 3f) * statMul,
                color);
            en.OnDied += OnExplosiveDied;
            HealthDots.AttachTo(go, explHP);
            alive.Add(go);
            return en;
        }

        public SplitterEnemy SpawnSplitter()
        {
            float sizeMul = Random.Range(1.1f, 1.5f);
            float statMul = Mathf.Pow(sizeMul, sizeStatExponent);
            Vector3 pos = PickSpawnPosition();
            pos.y = Mathf.Max(pos.y, 1.0f * sizeMul);

            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "SplitterEnemy";
            go.transform.position = pos;
            go.transform.localScale = Vector3.one * (1.3f * sizeMul);
            var color = new Color(0.25f, 0.85f, 0.4f);
            ApplyEnemyMaterial(go, color, true);

            var rb = go.AddComponent<Rigidbody>();
            rb.mass = 1.5f * statMul;

            var en = go.AddComponent<SplitterEnemy>();
            en.target = player;
            float spHP = (55f + 9f * (Round - 1)) * statMul;
            en.Configure(
                spHP,
                Mathf.Min(maxBallSpeed, 3.6f + 0.2f * (Round - 1)),
                (10f + 1f * (Round - 1)) * statMul,
                (28f + Round * 2.5f) * statMul,
                color,
                Mathf.Clamp(2 + Round / 5, 2, 5));
            en.OnDied += OnSplitterDied;
            HealthDots.AttachTo(go, spHP);
            alive.Add(go);
            return en;
        }

        void Shuffle<T>(System.Collections.Generic.IList<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        void OnExplosiveDied(ExplosiveEnemy e)
        {
            if (e == null) return;
            Vector3 pos = e.transform.position;
            alive.Remove(e.gameObject);
            OnEnemyKilled?.Invoke(e.scoreValue, pos);
        }

        void OnSplitterDied(SplitterEnemy s)
        {
            if (s == null) return;
            Vector3 pos = s.transform.position;
            alive.Remove(s.gameObject);
            OnEnemyKilled?.Invoke(s.scoreValue, pos);
        }

        public StaticTargetEnemy SpawnStaticTarget()
        {
            // Float somewhere on a ring at chest height
            float angle = Random.Range(0f, Mathf.PI * 2f);
            float radius = Random.Range(12f, 22f);
            Vector3 origin = player != null ? player.position : Vector3.zero;
            Vector3 pos = new Vector3(
                origin.x + Mathf.Cos(angle) * radius,
                origin.y + Random.Range(1.5f, 4.5f),
                origin.z + Mathf.Sin(angle) * radius);

            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "StaticTarget";
            go.transform.position = pos;
            go.transform.localScale = Vector3.one * 0.75f;
            var color = new Color(0.98f, 0.92f, 0.4f);
            ApplyEnemyMaterial(go, color, true);

            var en = go.AddComponent<StaticTargetEnemy>();
            en.Configure(1f, 40f + Round * 2f, color);
            en.OnDied += OnStaticDied;
            HealthDots.AttachTo(go, 1f);
            alive.Add(go);
            return en;
        }

        public TrackerEnemy SpawnTracker()
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);
            float radius = Random.Range(14f, 20f);
            Vector3 origin = player != null ? player.position : Vector3.zero;
            Vector3 pos = new Vector3(
                origin.x + Mathf.Cos(angle) * radius,
                origin.y + 3.5f,
                origin.z + Mathf.Sin(angle) * radius);

            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Tracker";
            go.transform.position = pos;
            go.transform.localScale = Vector3.one * 0.7f;
            var color = new Color(0.3f, 1f, 0.85f);
            ApplyEnemyMaterial(go, color, true);

            var en = go.AddComponent<TrackerEnemy>();
            en.target = player;
            en.Configure(1f, 60f + Round * 3f, color,
                Random.Range(12f, 18f),
                Random.Range(0.5f, 1.1f) + 0.05f * (Round - 1));
            en.OnDied += OnTrackerDied;
            HealthDots.AttachTo(go, 1f);
            alive.Add(go);
            return en;
        }

        public BallEnemy SpawnTinyFast()
        {
            // Use BallEnemy but tiny + fast + 1-shot
            Vector3 pos = PickSpawnPosition();
            pos.y = 0.6f;

            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "TinyFast";
            go.transform.position = pos;
            go.transform.localScale = Vector3.one * 0.45f;
            var color = new Color(1f, 0.3f, 0.55f);
            ApplyEnemyMaterial(go, color, true);

            var rb = go.AddComponent<Rigidbody>();
            rb.mass = 0.5f;

            var en = go.AddComponent<BallEnemy>();
            en.target = player;
            // Very nimble: aggressive jump, high speed, fragile
            en.minJumpInterval = 0.5f;
            en.maxJumpInterval = 1.0f;
            en.jumpForce = 9f;
            en.separationRadius = 2.2f;
            en.separationStrength = 5f;
            en.tangentStrength = 0.5f;
            en.Configure(1f,
                Mathf.Min(maxBallSpeed + 4f, 12f + 0.4f * (Round - 1)),
                7f + 0.6f * (Round - 1),
                15f + Round * 1.5f,
                color);
            en.OnDied += OnBallDied;
            HealthDots.AttachTo(go, 1f);
            alive.Add(go);
            return en;
        }

        void OnStaticDied(StaticTargetEnemy s)
        {
            if (s == null) return;
            Vector3 pos = s.transform.position;
            alive.Remove(s.gameObject);
            OnEnemyKilled?.Invoke(s.scoreValue, pos);
        }

        void OnTrackerDied(TrackerEnemy t)
        {
            if (t == null) return;
            Vector3 pos = t.transform.position;
            alive.Remove(t.gameObject);
            OnEnemyKilled?.Invoke(t.scoreValue, pos);
        }

        public BossEnemy SpawnBoss()
        {
            float bossSize = Random.Range(bossScaleRange.x, bossScaleRange.y);
            float bossStat = Mathf.Pow(bossSize, sizeStatExponent);
            Vector3 pos = PickSpawnPosition();
            pos.y = 2.2f * bossSize;
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "BossBall";
            go.transform.position = pos;
            go.transform.localScale = Vector3.one * (3.5f * bossSize);
            ApplyEnemyMaterial(go, new Color(0.55f, 0.1f, 0.75f), true);

            var rb = go.AddComponent<Rigidbody>();
            rb.mass = 8f * bossStat;

            var boss = go.AddComponent<BossEnemy>();
            boss.target = player;
            int cycle = Round / bossInterval;
            float hp = (baseBossHP + bossHPPerCycle * (cycle - 1)) * bossStat;
            boss.Configure(hp, 3.2f + 0.2f * cycle, (25f + 2f * cycle) * bossStat, (200f + 50f * cycle) * bossStat);
            boss.OnDied += OnBossDied;
            // Boss has way more HP — give the dots a higher y offset and a larger cap
            var bossDots = HealthDots.AttachTo(go, hp, 2.5f);
            bossDots.maxDots = 14;
            alive.Add(go);
            return boss;
        }

        Color ColorByRound()
        {
            // Visual tells: color shifts toward yellow/orange as rounds get harder
            float t = Mathf.Clamp01((Round - 1) / 20f);
            return Color.Lerp(new Color(0.85f, 0.2f, 0.2f), new Color(1f, 0.7f, 0.1f), t);
        }

        void ApplyEnemyMaterial(GameObject go, Color color, bool emissive)
        {
            var r = go.GetComponent<Renderer>();
            if (r == null) return;
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var mat = new Material(shader);
            mat.color = color;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (emissive)
            {
                mat.EnableKeyword("_EMISSION");
                if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", color * 1.6f);
            }
            r.sharedMaterial = mat;
        }

        void OnBallDied(BallEnemy b)
        {
            if (b == null) return;
            Vector3 pos = b.transform.position;
            alive.Remove(b.gameObject);
            OnEnemyKilled?.Invoke(b.scoreValue, pos);
        }

        void OnFlyerDied(FlyingEnemy f)
        {
            if (f == null) return;
            Vector3 pos = f.transform.position;
            alive.Remove(f.gameObject);
            OnEnemyKilled?.Invoke(f.scoreValue, pos);
        }

        void OnBossDied(BossEnemy b)
        {
            if (b == null) return;
            Vector3 pos = b.transform.position;
            alive.Remove(b.gameObject);
            OnEnemyKilled?.Invoke(b.scoreValue, pos);
        }

        public void StopAll()
        {
            running = false;
            StopAllCoroutines();
        }
    }
}
