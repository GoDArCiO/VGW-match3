using Proto.Core;
using UnityEngine;

namespace Proto.Game
{
    /// <summary>
    /// Looping background-music adapter: plays the first <see cref="AudioClip"/> it finds in
    /// <c>Resources/Music/</c> on a single persistent <see cref="AudioSource"/> for the whole session, at the
    /// level set by <see cref="VolumeSettings"/> (re-applied live when the settings panel moves the slider).
    /// Drop ANY one clip into <c>Assets/Resources/Music/</c> and it loops; an empty folder degrades to a
    /// console note and silence — music never crashes the slice (the build is fully playable with zero audio).
    /// </summary>
    public sealed class MusicPlayer : MonoBehaviour
    {
        private AudioSource _source;
        private VolumeSettings _settings;

        public void Play(VolumeSettings settings)
        {
            _settings = settings;

            _source = gameObject.AddComponent<AudioSource>();
            _source.loop = true;
            _source.playOnAwake = false;
            _source.spatialBlend = 0f; // 2D, non-positional
            _source.priority = 0;      // never voice-stolen by one-shot SFX
            ApplyVolume();

            AudioClip[] clips = Resources.LoadAll<AudioClip>("Music");
            if (clips == null || clips.Length == 0)
            {
                Debug.Log("[Proto] No music in Resources/Music — playing silent. Drop one loop clip in to score the slice.");
                return;
            }

            _source.clip = clips[0];
            _source.Play();
            if (_settings != null) _settings.Changed += ApplyVolume;
        }

        private void ApplyVolume()
        {
            if (_source != null) _source.volume = _settings != null ? _settings.Music : 0.6f;
        }

        private void OnDestroy()
        {
            if (_settings != null) _settings.Changed -= ApplyVolume;
        }
    }
}
