using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Proto.Game
{
    /// <summary>
    /// Pooled floating feedback labels ("+50") that rise and fade from a world position. Built on UI Toolkit
    /// (consistent with the HUD, no TextMesh dependency): a label is projected from world to panel space via
    /// <see cref="RuntimePanelUtils"/> and animated by the shared <see cref="TweenRunner"/>'s coroutine host.
    /// Generic toolkit piece — reused by the board and the meta view.
    /// </summary>
    public sealed class FloatingTextLayer
    {
        private readonly VisualElement _container;
        private readonly Camera _camera;
        private readonly TweenRunner _runner;
        private readonly Stack<Label> _pool = new Stack<Label>();

        public FloatingTextLayer(VisualElement root, Camera camera, TweenRunner runner)
        {
            _camera = camera;
            _runner = runner;

            _container = new VisualElement { name = "floaters", pickingMode = PickingMode.Ignore };
            _container.style.position = Position.Absolute;
            _container.style.left = 0;
            _container.style.top = 0;
            _container.style.right = 0;
            _container.style.bottom = 0;
            root.Add(_container);
        }

        /// <summary>Pop a label at a world position; it drifts up <paramref name="rise"/> px and fades over
        /// <paramref name="duration"/> s. No-op if the panel isn't ready or text is empty.</summary>
        public void Spawn(string text, Vector3 worldPos, Color color, float fontSize = 46f, float rise = 140f, float duration = 0.85f)
        {
            if (string.IsNullOrEmpty(text)) return;
            IPanel panel = _container.panel;
            if (panel == null || _camera == null) return;

            Vector2 p = RuntimePanelUtils.CameraTransformWorldToPanel(panel, worldPos, _camera);
            Label label = Rent();
            label.text = text;
            label.style.color = color;
            label.style.fontSize = fontSize;
            label.style.left = p.x;
            label.style.top = p.y;
            label.style.opacity = 1f;
            label.style.display = DisplayStyle.Flex;

            _runner.Run(Animate(label, p.y, rise, duration));
        }

        private IEnumerator Animate(Label label, float startTop, float rise, float duration)
        {
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / duration);
                label.style.top = startTop - rise * Ease.OutCubic(k);
                label.style.opacity = 1f - k * k;
                float s = 1f + 0.35f * (1f - Ease.OutCubic(Mathf.Clamp01(k * 2f)));
                label.style.scale = new StyleScale(new Scale(new Vector3(s, s, 1f)));
                yield return null;
            }
            Return(label);
        }

        private Label Rent()
        {
            Label label = _pool.Count > 0 ? _pool.Pop() : NewLabel();
            label.style.display = DisplayStyle.Flex;
            return label;
        }

        private void Return(Label label)
        {
            label.style.display = DisplayStyle.None;
            _pool.Push(label);
        }

        private Label NewLabel()
        {
            var label = new Label { pickingMode = PickingMode.Ignore };
            label.style.position = Position.Absolute;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.unityTextOutlineWidth = 1.5f;
            label.style.unityTextOutlineColor = new Color(0.04f, 0.05f, 0.08f, 0.9f);
            label.style.translate = new StyleTranslate(new Translate(Length.Percent(-50f), Length.Percent(-50f)));
            _container.Add(label);
            return label;
        }
    }
}
