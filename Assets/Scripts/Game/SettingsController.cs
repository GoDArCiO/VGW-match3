using System;
using Proto.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Proto.Game
{
    /// <summary>
    /// The deliberately-minimal settings panel (UI Toolkit) — the ONE allowed exception to the no-settings
    /// scope rule: an always-available gear that opens Music + SFX volume sliders and a Reset button, nothing
    /// more (no graphics/controls/save). The gear is visible during play; Reset rebuilds the run from the top.
    /// While the panel is open, gameplay input is suppressed (a slider drag is itself a pointer gesture).
    /// Defensive: missing UIDocument / elements degrade to a console error, never a crash.
    /// </summary>
    public sealed class SettingsController
    {
        private readonly VisualElement _overlay;
        private readonly Button _gear;
        private readonly Button _reset;
        private readonly Button _close;
        private readonly Slider _music;
        private readonly Slider _sfx;
        private readonly VolumeSettings _settings;
        private readonly Action _onReset;
        private readonly Action _onUiCue;
        private readonly bool _valid;

        public bool IsOpen => _valid && _overlay.style.display == DisplayStyle.Flex;

        public SettingsController(UIDocument document, VolumeSettings settings, Action onReset, Action onUiCue = null)
        {
            _settings = settings;
            _onReset = onReset;
            _onUiCue = onUiCue;

            VisualElement root = document != null ? document.rootVisualElement : null;
            if (root == null) { Debug.LogError("[Proto] Settings unavailable: UIDocument missing."); return; }

            _gear = root.Q<Button>("settings-button");
            _overlay = root.Q("settings-overlay");
            _reset = root.Q<Button>("settings-reset");
            _close = root.Q<Button>("settings-close");
            _music = root.Q<Slider>("music-slider");
            _sfx = root.Q<Slider>("sfx-slider");
            if (_gear == null || _overlay == null || _reset == null || _close == null || _music == null || _sfx == null)
            {
                Debug.LogError("[Proto] Settings unavailable: proto.uxml is missing settings elements.");
                return;
            }
            _valid = true;

            _music.lowValue = 0f; _music.highValue = 1f; _music.value = _settings.Music;
            _sfx.lowValue = 0f; _sfx.highValue = 1f; _sfx.value = _settings.Sfx;
            _music.RegisterValueChangedCallback(e => _settings.Music = e.newValue);
            _sfx.RegisterValueChangedCallback(e => _settings.Sfx = e.newValue);

            _gear.clicked += Open;
            _close.clicked += Close;
            _reset.clicked += () => { _onUiCue?.Invoke(); _onReset?.Invoke(); Close(); };

            Close();
        }

        private void Open()
        {
            if (!_valid) return;
            _onUiCue?.Invoke();
            _music.value = _settings.Music; // resync in case changed elsewhere
            _sfx.value = _settings.Sfx;
            _overlay.style.display = DisplayStyle.Flex;
        }

        private void Close()
        {
            if (!_valid) return;
            _overlay.style.display = DisplayStyle.None;
        }
    }
}
