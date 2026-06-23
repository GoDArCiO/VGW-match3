using System.Collections.Generic;
using Proto.Core;
using UnityEngine;

namespace Proto.Game
{
    /// <summary>
    /// Plays a one-shot clip for a named SFX hook. Clips live at <c>Resources/Sfx/&lt;hook&gt;</c> and are
    /// loaded by name — a missing clip is a SILENT no-op, so hooks without audio yet simply don't sound, and
    /// adding a cue is just dropping <c>Assets/Resources/Sfx/&lt;hook&gt;.wav</c> in. Misses are cached too, so
    /// an unmapped hook never re-hits disk. The view calls <see cref="PlayHook(string)"/> /
    /// <see cref="PlayHook(string,float)"/> directly so it controls cue timing and (for the cascade chain) pitch.
    ///
    /// BATCH-OVERLAP GOTCHA (learned the hard way): if one frame fires N identical hooks (a staged spawn),
    /// they sum in phase and clip. Stagger the cue or raise it once per step — never once-per-item in a batch.
    /// </summary>
    public sealed class SfxPlayer : MonoBehaviour
    {
        private AudioSource _source;
        private readonly Dictionary<string, AudioClip> _cache = new Dictionary<string, AudioClip>();
        private VolumeSettings _settings; // null until wired; full volume by default

        private void Awake()
        {
            _source = GetComponent<AudioSource>();
            if (_source == null) _source = gameObject.AddComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.spatialBlend = 0f; // 2D UI/feedback audio
        }

        /// <summary>Wire the audio mix so every cue scales by the SFX level (settings panel).</summary>
        public void UseSettings(VolumeSettings settings) => _settings = settings;

        /// <summary>Play a clip by hook name. Missing clip = silent no-op.</summary>
        public void PlayHook(string hook) => PlayInternal(hook, 1f);

        /// <summary>Play a clip with a pitch multiplier — drives the rising-pitch cascade chain.</summary>
        public void PlayHook(string hook, float pitch) => PlayInternal(hook, pitch);

        private void PlayInternal(string hook, float pitch)
        {
            if (string.IsNullOrEmpty(hook)) return;

            if (!_cache.TryGetValue(hook, out AudioClip clip))
            {
                clip = Resources.Load<AudioClip>($"Sfx/{hook}");
                _cache[hook] = clip; // cache misses (null) too, so unmapped hooks don't re-hit disk
            }
            if (clip == null) return;

            float level = _settings != null ? _settings.Sfx : 1f;
            if (level <= 0f) return; // muted — skip the voice entirely
            _source.pitch = Mathf.Clamp(pitch, 0.25f, 3f);
            _source.PlayOneShot(clip, level);
        }
    }
}
