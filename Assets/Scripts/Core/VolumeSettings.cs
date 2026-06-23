using System;

namespace Proto.Core
{
    /// <summary>
    /// The audio mix for the slice: a Music level and an SFX level, each in [0,1]. Engine-free so it's
    /// unit-testable; the presentation layer (SfxPlayer / MusicPlayer / SettingsController) reads it and
    /// subscribes to <see cref="Changed"/>. In-memory only — a proto has no persistence (scope exclusion),
    /// so the mix resets on reload, which is fine for a 30-second slice.
    /// </summary>
    public sealed class VolumeSettings
    {
        public event Action Changed;

        private float _music;
        private float _sfx;

        public VolumeSettings(float music = 0.6f, float sfx = 0.85f)
        {
            _music = Clamp01(music);
            _sfx = Clamp01(sfx);
        }

        public float Music
        {
            get => _music;
            set { float v = Clamp01(value); if (v != _music) { _music = v; Changed?.Invoke(); } }
        }

        public float Sfx
        {
            get => _sfx;
            set { float v = Clamp01(value); if (v != _sfx) { _sfx = v; Changed?.Invoke(); } }
        }

        private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
    }
}
