using Proto.Core;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Proto.Game
{
    /// <summary>
    /// Drives the global URP post stack AS juice: each beat nudges a channel — an exposure/bloom flash on a
    /// collect, a big bloom + saturation swell on a win, a desaturate + vignette + chromatic-aberration
    /// glitch on a loss — and every channel springs back to its installed baseline. Runs on UNSCALED time so
    /// the flashes keep playing through the loss hit-stop. <see cref="Bind"/> re-subscribes to each run's
    /// events and resets every channel, so a mid-effect rebuild never carries grading over.
    /// </summary>
    public sealed class PostFxJuice : MonoBehaviour
    {
        /// <summary>An additive impulse on one post channel that springs back to zero each frame.</summary>
        private struct Pulse
        {
            public float Value;
            public float Decay; // larger = snappier recovery

            public Pulse(float decay) { Value = 0f; Decay = decay; }

            /// <summary>Set to <paramref name="v"/> unless a stronger impulse is already in flight
            /// (so a small tick never cuts a big glitch short).</summary>
            public void Kick(float v) { if (Mathf.Abs(v) >= Mathf.Abs(Value)) Value = v; }

            /// <summary>Accumulate — repeated beats (a rapid collect chain) stack into a swell.</summary>
            public void Add(float v) => Value += v;

            public void Tick(float dt) => Value = Mathf.Lerp(Value, 0f, 1f - Mathf.Exp(-Decay * dt));
        }

        private Bloom _bloom;
        private Vignette _vignette;
        private ColorAdjustments _colour;
        private ChromaticAberration _chroma;

        private float _bloomBase, _vignetteBase, _saturationBase, _exposureBase;

        private Pulse _bloomPulse = new Pulse(2.5f);      // lingering glow (win)
        private Pulse _exposurePulse = new Pulse(6f);     // snappy brightness flashes (collect, win)
        private Pulse _saturationPulse = new Pulse(3f);   // loss desaturate / win vividness
        private Pulse _vignettePulse = new Pulse(4f);     // loss framing punch
        private Pulse _chromaPulse = new Pulse(5f);       // loss glitch

        public void Init(Bloom bloom, Vignette vignette, ColorAdjustments colour, ChromaticAberration chroma)
        {
            _bloom = bloom;
            _vignette = vignette;
            _colour = colour;
            _chroma = chroma;
            _bloomBase = bloom.intensity.value;
            _vignetteBase = vignette.intensity.value;
            _saturationBase = colour.saturation.value;
            _exposureBase = colour.postExposure.value;
        }

        /// <summary>(Re)bind to a run's events. Each run builds a fresh <see cref="GameEvents"/>, so the old
        /// subscriptions fall away with it (no leak).</summary>
        public void Bind(GameEvents events)
        {
            ResetChannels();
            if (events == null) return;
            events.OnCollected += (_, __) => Collect();
            events.OnWon += _ => Win();
            events.OnLost += _ => Lose();
        }

        private void Collect() { _exposurePulse.Add(0.08f); _bloomPulse.Add(0.14f); }
        private void Win()     { _bloomPulse.Kick(1.70f); _saturationPulse.Kick(28f); _exposurePulse.Kick(0.60f); }
        private void Lose()    { _chromaPulse.Kick(0.85f); _vignettePulse.Kick(0.32f); _saturationPulse.Kick(-75f); _exposurePulse.Kick(-0.30f); }

        private void ResetChannels()
        {
            _bloomPulse.Value = 0f;
            _exposurePulse.Value = 0f;
            _saturationPulse.Value = 0f;
            _vignettePulse.Value = 0f;
            _chromaPulse.Value = 0f;
            Apply();
        }

        private void Update()
        {
            if (_bloom == null) return; // install failed — nothing to drive
            float dt = Time.unscaledDeltaTime;
            _bloomPulse.Tick(dt);
            _exposurePulse.Tick(dt);
            _saturationPulse.Tick(dt);
            _vignettePulse.Tick(dt);
            _chromaPulse.Tick(dt);
            Apply();
        }

        private void Apply()
        {
            _bloom.intensity.Override(Mathf.Max(0f, _bloomBase + _bloomPulse.Value));
            _colour.postExposure.Override(_exposureBase + _exposurePulse.Value);
            _colour.saturation.Override(Mathf.Clamp(_saturationBase + _saturationPulse.Value, -100f, 100f));
            _vignette.intensity.Override(Mathf.Clamp01(_vignetteBase + _vignettePulse.Value));
            _chroma.intensity.Override(Mathf.Clamp01(_chromaPulse.Value));
        }
    }
}
