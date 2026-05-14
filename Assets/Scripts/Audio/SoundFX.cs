using UnityEngine;

namespace BallShooter
{
    /// <summary>
    /// Central sound playback. Lazily generates AudioClips on first request,
    /// then plays them via PlayClipAtPoint or via cached AudioSources.
    /// </summary>
    public class SoundFX : MonoBehaviour
    {
        public static SoundFX Instance { get; private set; }

        AudioClip footstep;
        AudioClip enemySpawn;
        AudioClip enemyHit;
        AudioClip enemyDeath;
        AudioClip explosion;
        AudioClip gunshot;
        AudioClip bossSpawn;
        AudioClip killConfirm;       // 2D player-feedback cue on confirmed kill

        [Header("Master")]
        public float masterVolume = 0.8f;
        public float footstepVolume = 0.35f;
        public float enemyVolume = 0.7f;
        public float explosionVolume = 0.9f;
        public float killConfirmVolume = 0.55f;

        void Awake()
        {
            Instance = this;
            footstep = ProceduralAudio.Footstep();
            enemySpawn = ProceduralAudio.EnemySpawn();
            enemyHit = ProceduralAudio.EnemyHit();
            enemyDeath = ProceduralAudio.EnemyDeath();
            explosion = ProceduralAudio.Explosion();
            gunshot = ProceduralAudio.Gunshot();
            bossSpawn = ProceduralAudio.BossSpawn();
            killConfirm = ProceduralAudio.KillConfirm();
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        // ----------------- Helpers -----------------

        public void Play(AudioClip clip, Vector3 pos, float volume = 1f, float pitch = 1f)
        {
            if (clip == null) return;
            var go = new GameObject("SFX_" + clip.name);
            go.transform.position = pos;
            var src = go.AddComponent<AudioSource>();
            src.clip = clip;
            src.volume = Mathf.Clamp01(volume * masterVolume);
            src.pitch = pitch;
            src.spatialBlend = 1f;          // 3D
            src.minDistance = 4f;
            src.maxDistance = 60f;
            src.rolloffMode = AudioRolloffMode.Linear;
            src.dopplerLevel = 0f;
            src.Play();
            Destroy(go, clip.length / Mathf.Max(0.01f, pitch) + 0.1f);
        }

        public void Play2D(AudioClip clip, float volume = 1f, float pitch = 1f)
        {
            if (clip == null) return;
            var go = new GameObject("SFX2D_" + clip.name);
            var src = go.AddComponent<AudioSource>();
            src.clip = clip;
            src.volume = Mathf.Clamp01(volume * masterVolume);
            src.pitch = pitch;
            src.spatialBlend = 0f;
            src.Play();
            Destroy(go, clip.length / Mathf.Max(0.01f, pitch) + 0.1f);
        }

        // ----------------- Game events -----------------

        public void PlayFootstep(Vector3 pos)
        {
            Play(footstep, pos, footstepVolume, Random.Range(0.85f, 1.15f));
        }

        public void PlayEnemySpawn(Vector3 pos)
        {
            Play(enemySpawn, pos, enemyVolume * 0.6f, Random.Range(0.85f, 1.2f));
        }

        public void PlayEnemyHit(Vector3 pos)
        {
            Play(enemyHit, pos, enemyVolume * 0.5f, Random.Range(0.9f, 1.15f));
        }

        public void PlayEnemyDeath(Vector3 pos)
        {
            Play(enemyDeath, pos, enemyVolume, Random.Range(0.85f, 1.1f));
        }

        public void PlayExplosion(Vector3 pos)
        {
            Play(explosion, pos, explosionVolume, Random.Range(0.95f, 1.05f));
        }

        public void PlayGunshot(Vector3 pos)
        {
            Play(gunshot, pos, 0.5f, Random.Range(0.95f, 1.05f));
        }

        public void PlayBossSpawn(Vector3 pos)
        {
            Play(bossSpawn, pos, enemyVolume * 1.1f, 1f);
        }

        /// <summary>
        /// Plays the 2D kill-confirmation cue — a short bright "ding" that gives the
        /// player satisfying feedback when their shot kills an enemy. Distinct from
        /// PlayEnemyDeath, which is the 3D world-positioned death sound.
        /// </summary>
        public void PlayKillConfirm()
        {
            // Small random pitch variation so back-to-back kills don't sound identical
            Play2D(killConfirm, killConfirmVolume, Random.Range(0.96f, 1.06f));
        }
    }
}
