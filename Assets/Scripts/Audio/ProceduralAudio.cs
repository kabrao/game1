using UnityEngine;

namespace BallShooter
{
    /// <summary>
    /// Generates AudioClips at runtime — no audio assets required.
    /// Each method synthesises a short PCM buffer (mono, 44.1kHz).
    /// </summary>
    public static class ProceduralAudio
    {
        const int SR = 44100;

        // ---------- Footstep: short low-pass noise burst with quick decay ----------
        public static AudioClip Footstep()
        {
            float dur = 0.12f;
            int n = (int)(SR * dur);
            var samples = new float[n];
            float prev = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                float envelope = Mathf.Exp(-t * 18f); // fast attack/decay
                float noise = Random.Range(-1f, 1f);
                // simple low-pass IIR
                prev = Mathf.Lerp(prev, noise, 0.18f);
                samples[i] = prev * envelope * 0.45f;
            }
            return MakeClip("Footstep", samples);
        }

        // ---------- Enemy spawn: short rising blip ----------
        public static AudioClip EnemySpawn(float baseHz = 220f)
        {
            float dur = 0.22f;
            int n = (int)(SR * dur);
            var samples = new float[n];
            double phase = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                float envelope = Mathf.Sin(t * Mathf.PI) * Mathf.Exp(-t * 2f);
                float freq = baseHz + 380f * t;             // sweep up
                double inc = 2.0 * System.Math.PI * freq / SR;
                phase += inc;
                float wave = (float)System.Math.Sin(phase);
                samples[i] = wave * envelope * 0.4f;
            }
            return MakeClip("EnemySpawn", samples);
        }

        // ---------- Enemy hit: short hard pluck ----------
        public static AudioClip EnemyHit()
        {
            float dur = 0.09f;
            int n = (int)(SR * dur);
            var samples = new float[n];
            double phase = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                float env = Mathf.Exp(-t * 22f);
                float freq = 620f - 320f * t;
                double inc = 2.0 * System.Math.PI * freq / SR;
                phase += inc;
                float wave = (float)System.Math.Sin(phase);
                float noise = Random.Range(-1f, 1f) * 0.25f;
                samples[i] = (wave + noise) * env * 0.42f;
            }
            return MakeClip("EnemyHit", samples);
        }

        // ---------- Enemy death: pop with descending pitch + noise ----------
        public static AudioClip EnemyDeath(float baseHz = 380f)
        {
            float dur = 0.32f;
            int n = (int)(SR * dur);
            var samples = new float[n];
            double phase = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                float env = Mathf.Exp(-t * 5f);
                float freq = baseHz * Mathf.Lerp(1f, 0.35f, t);
                double inc = 2.0 * System.Math.PI * freq / SR;
                phase += inc;
                float wave = (float)System.Math.Sin(phase);
                float noise = Random.Range(-1f, 1f) * Mathf.Exp(-t * 8f) * 0.6f;
                samples[i] = (wave * 0.6f + noise) * env * 0.55f;
            }
            return MakeClip("EnemyDeath", samples);
        }

        // ---------- Explosion: low rumble + noise crash ----------
        public static AudioClip Explosion()
        {
            float dur = 0.7f;
            int n = (int)(SR * dur);
            var samples = new float[n];
            double phase = 0;
            float prev = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                float env = Mathf.Exp(-t * 4.5f);
                float freq = 80f * Mathf.Lerp(1f, 0.5f, t);
                double inc = 2.0 * System.Math.PI * freq / SR;
                phase += inc;
                float wave = (float)System.Math.Sin(phase);
                float noise = Random.Range(-1f, 1f);
                prev = Mathf.Lerp(prev, noise, 0.35f); // low-passed noise
                samples[i] = (wave * 0.5f + prev * 0.7f) * env * 0.7f;
            }
            return MakeClip("Explosion", samples);
        }

        // ---------- Kill confirm: short two-tone "ding" — clean, satisfying, brief ----------
        //
        // This is a 2D feedback cue, separate from the 3D enemy-death sound. It plays
        // when YOUR shot kills an enemy. Two stacked sine tones (high root + perfect
        // fifth above it) with a fast attack and short exponential decay.
        public static AudioClip KillConfirm()
        {
            float dur = 0.18f;
            int n = (int)(SR * dur);
            var samples = new float[n];
            double p1 = 0, p2 = 0;
            const float root = 1320f;          // ~E6 — bright but not piercing
            const float fifth = 1980f;         // perfect fifth above the root
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                // Fast attack (first 5% of clip ramps up), then exponential decay.
                float attack = Mathf.Clamp01(t / 0.05f);
                float decay = Mathf.Exp(-t * 9f);
                float env = attack * decay;

                p1 += 2.0 * System.Math.PI * root / SR;
                p2 += 2.0 * System.Math.PI * fifth / SR;
                float wave = (float)(System.Math.Sin(p1) + 0.55f * System.Math.Sin(p2));
                samples[i] = wave * env * 0.45f;
            }
            return MakeClip("KillConfirm", samples);
        }

        // ---------- Gunshot: fast click + noise tail ----------
        public static AudioClip Gunshot()
        {
            float dur = 0.18f;
            int n = (int)(SR * dur);
            var samples = new float[n];
            float prev = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                float env = Mathf.Exp(-t * 14f);
                float noise = Random.Range(-1f, 1f);
                prev = Mathf.Lerp(prev, noise, 0.55f);
                // initial click via low-frequency thump
                float thump = Mathf.Sin(t * 30f) * Mathf.Exp(-t * 60f) * 0.6f;
                samples[i] = (prev * 0.8f + thump) * env * 0.5f;
            }
            return MakeClip("Gunshot", samples);
        }

        // ---------- Boss roar: low growling tone ----------
        public static AudioClip BossSpawn()
        {
            float dur = 0.9f;
            int n = (int)(SR * dur);
            var samples = new float[n];
            double p1 = 0, p2 = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                float env = Mathf.Sin(t * Mathf.PI) * Mathf.Exp(-Mathf.Abs(t - 0.5f) * 1.5f);
                float f1 = 60f + 40f * Mathf.Sin(t * 8f);
                float f2 = 110f + 60f * Mathf.Sin(t * 6f + 1f);
                p1 += 2.0 * System.Math.PI * f1 / SR;
                p2 += 2.0 * System.Math.PI * f2 / SR;
                float wave = (float)(System.Math.Sin(p1) + 0.6f * System.Math.Sin(p2));
                samples[i] = wave * env * 0.5f;
            }
            return MakeClip("BossSpawn", samples);
        }

        static AudioClip MakeClip(string name, float[] samples)
        {
            var clip = AudioClip.Create(name, samples.Length, 1, SR, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
