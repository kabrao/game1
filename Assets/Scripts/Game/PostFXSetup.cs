using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace BallShooter
{
    /// <summary>
    /// Builds a runtime URP post-processing volume with Bloom, Vignette,
    /// Color Adjustments and Film Grain. No project profile asset required.
    /// </summary>
    public static class PostFXSetup
    {
        public static void Install()
        {
            // Don't add a second volume if one already exists
            if (Object.FindFirstObjectByType<Volume>() != null) return;

            var go = new GameObject("PostFXVolume");
            var v = go.AddComponent<Volume>();
            v.isGlobal = true;
            v.priority = 10f;

            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            v.sharedProfile = profile;

            // Bloom — pops emissive enemies and muzzle flashes
            var bloom = GetOrAdd<Bloom>(profile);
            if (bloom != null)
            {
                bloom.intensity.overrideState = true;
                bloom.intensity.value = 0.55f;
                bloom.threshold.overrideState = true;
                bloom.threshold.value = 0.9f;
                bloom.scatter.overrideState = true;
                bloom.scatter.value = 0.7f;
                bloom.tint.overrideState = true;
                bloom.tint.value = new Color(1f, 0.9f, 0.8f);
            }

            // Vignette — frame the scene
            var vig = GetOrAdd<Vignette>(profile);
            if (vig != null)
            {
                vig.intensity.overrideState = true;
                vig.intensity.value = 0.32f;
                vig.smoothness.overrideState = true;
                vig.smoothness.value = 0.45f;
                vig.color.overrideState = true;
                vig.color.value = new Color(0.03f, 0.02f, 0.05f);
            }

            // Color adjustments — slight contrast + warmth
            var ca = GetOrAdd<ColorAdjustments>(profile);
            if (ca != null)
            {
                ca.postExposure.overrideState = true;
                ca.postExposure.value = 0.1f;
                ca.contrast.overrideState = true;
                ca.contrast.value = 8f;
                ca.saturation.overrideState = true;
                ca.saturation.value = 4f;
                ca.colorFilter.overrideState = true;
                ca.colorFilter.value = new Color(1f, 0.97f, 0.92f);
            }

            // Subtle film grain
            var fg = GetOrAdd<FilmGrain>(profile);
            if (fg != null)
            {
                fg.type.overrideState = true;
                fg.type.value = FilmGrainLookup.Thin1;
                fg.intensity.overrideState = true;
                fg.intensity.value = 0.18f;
                fg.response.overrideState = true;
                fg.response.value = 0.8f;
            }

            // Tonemap for HDR pop
            var tm = GetOrAdd<Tonemapping>(profile);
            if (tm != null)
            {
                tm.mode.overrideState = true;
                tm.mode.value = TonemappingMode.ACES;
            }
        }

        static T GetOrAdd<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (profile.TryGet<T>(out var c)) return c;
            return profile.Add<T>();
        }
    }
}
