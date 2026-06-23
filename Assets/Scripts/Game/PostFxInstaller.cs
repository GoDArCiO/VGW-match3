using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Proto.Game
{
    /// <summary>
    /// Installs the global URP post-processing stack in code (no manual editor steps): a base "look" —
    /// neutral tonemapping, gentle bloom, a soft vignette and a touch of colour grading — plus a zeroed
    /// chromatic-aberration channel that <see cref="PostFxJuice"/> drives on gameplay beats. Also turns
    /// camera post-processing on. Idempotent and crash-safe: a missing camera or post stack degrades to a
    /// logged warning, never a crash (stability first). Returns the persistent juice driver so the
    /// composition root can re-bind it to each run's events.
    /// </summary>
    public static class PostFxInstaller
    {
        private static PostFxJuice _juice;

        // WebGL reliability (boring-reliable doctrine): URP post-processing forces a full-screen blit through
        // Hidden/CoreSRP/CoreCopy, whose GLES3 variant URP shader-stripping drops — the blit then faults and
        // HALTS the WebGL build ("Uncaught exception from main loop → undefined") on a masked/limited GPU once the
        // post pass first runs (e.g. after a move). A flat 2D match-3 doesn't need screen post-FX, so it's OFF for
        // the slice; the tween/particle/shake/SFX juice is unaffected. To re-enable, flip EnablePostFx AND also
        // ship the CoreCopy variant (disable URP "Strip Unused Variants").
        private static readonly bool EnablePostFx = true;

        public static PostFxJuice Install(Camera camera)
        {
            // Force post-processing OFF on the camera so no screen blit ever runs, then bail before building the
            // volume. (Guarded by EnablePostFx so the full stack below is preserved for a future re-enable.)
            if (camera != null)
            {
                UniversalAdditionalCameraData data = camera.GetUniversalAdditionalCameraData();
                if (data != null) data.renderPostProcessing = EnablePostFx;
            }
            if (!EnablePostFx) return null;

            try
            {
                if (_juice != null) return _juice; // already installed — survives in-place rebuilds

                var profile = ScriptableObject.CreateInstance<VolumeProfile>();

                // Range-remap highlights so the bloom punch never clips to flat white (keeps colours true).
                Tonemapping tonemapping = profile.Add<Tonemapping>(overrides: true);
                tonemapping.mode.Override(TonemappingMode.Neutral);

                Bloom bloom = profile.Add<Bloom>(overrides: true);
                bloom.intensity.Override(1.0f);    // gentle glow — premium, not blown out
                bloom.threshold.Override(0.9f);    // only HDR emissives bloom; flat colours stay crisp
                bloom.scatter.Override(0.7f);
                bloom.tint.Override(Color.white);

                // A cozy static baseline grade — richer than the raw lit scene, still flat-low-poly clean.
                // These are the values PostFxJuice's reactive pulses spring back to.
                ColorAdjustments colour = profile.Add<ColorAdjustments>(overrides: true);
                colour.postExposure.Override(0f);  // base 0; driven by juice
                colour.contrast.Override(10f);
                colour.saturation.Override(12f);   // base; driven by juice (loss desaturate, win vividness)

                WhiteBalance white = profile.Add<WhiteBalance>(overrides: true);
                white.temperature.Override(10f);   // toward warm
                white.tint.Override(2f);

                Vignette vignette = profile.Add<Vignette>(overrides: true);
                vignette.intensity.Override(0.28f);
                vignette.smoothness.Override(0.45f);
                vignette.rounded.Override(false);

                ChromaticAberration chroma = profile.Add<ChromaticAberration>(overrides: true);
                chroma.intensity.Override(0f);     // zeroed at rest — purely a juice channel (loss glitch)

                var go = new GameObject("Global PostFx Volume");
                Object.DontDestroyOnLoad(go);      // survives in-place rebuilds
                Volume volume = go.AddComponent<Volume>();
                volume.isGlobal = true;
                volume.priority = 1f;              // over the URP default volume
                volume.profile = profile;

                _juice = go.AddComponent<PostFxJuice>();
                _juice.Init(bloom, vignette, colour, chroma);
                return _juice;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[Proto] Post-processing setup skipped: {e.Message}");
                return _juice;
            }
        }
    }
}
