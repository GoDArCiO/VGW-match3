using Proto.Core;
using UnityEngine;

namespace Proto.Game
{
    /// <summary>
    /// Trauma-based screen shake (the Vlambeer/GDC pattern): beats add trauma in [0,1]; the felt shake is
    /// trauma², so small knocks barely register while a big one slams, and it always decays smoothly back
    /// to the camera's rest pose. Position + rotation are offset from the pose captured at Awake, so the
    /// camera framing the proto authored is preserved. Runs on UNSCALED time, so a crash shake keeps moving
    /// through the hit-stop. <see cref="Bind"/> re-arms it on each session's beats (per-run event instance,
    /// so old subscriptions fall away — no leak).
    /// </summary>
    public sealed class CameraRig : MonoBehaviour
    {
        [SerializeField] private float traumaDecay = 1.6f;   // trauma units shed per second
        [SerializeField] private float maxOffset = 0.45f;    // world-units of positional shake at full trauma
        [SerializeField] private float maxYaw = 3.0f;        // degrees of rotational shake at full trauma
        [SerializeField] private float frequency = 26f;      // perlin sample rate — higher = jitterier

        private Vector3 _basePos;
        private Quaternion _baseRot;
        private float _trauma;
        private float _seed;

        private void Awake()
        {
            _basePos = transform.localPosition;
            _baseRot = transform.localRotation;
            _seed = Mathf.Repeat(transform.position.sqrMagnitude * 0.137f, 100f); // deterministic per-placement seed
        }

        /// <summary>Add a jolt. <paramref name="amount"/> ~0.1 = a tap, ~0.8 = a crash. Clamped to 1.</summary>
        public void AddTrauma(float amount) => _trauma = Mathf.Clamp01(_trauma + amount);

        /// <summary>Re-capture the rest pose (call after repositioning the camera for a new layout).</summary>
        public void SetBasePose()
        {
            _basePos = transform.localPosition;
            _baseRot = transform.localRotation;
        }

        public void Bind(GameEvents events)
        {
            if (events == null) return;
            events.OnCollected += (_, __) => AddTrauma(0.18f); // a small, satisfying knock per collect
            events.OnWon += _ => AddTrauma(0.45f);
            events.OnLost += _ => AddTrauma(0.8f);             // the headline slam
        }

        private void LateUpdate()
        {
            if (_trauma <= 0f)
            {
                transform.localPosition = _basePos;
                transform.localRotation = _baseRot;
                return;
            }

            float shake = _trauma * _trauma;
            float t = Time.unscaledTime * frequency;
            float ox = (Mathf.PerlinNoise(_seed, t) * 2f - 1f) * maxOffset * shake;
            float oy = (Mathf.PerlinNoise(_seed + 1.7f, t) * 2f - 1f) * maxOffset * shake;
            float yaw = (Mathf.PerlinNoise(_seed + 3.3f, t) * 2f - 1f) * maxYaw * shake;

            transform.localPosition = _basePos + new Vector3(ox, oy, 0f);
            transform.localRotation = _baseRot * Quaternion.Euler(0f, 0f, yaw);

            _trauma = Mathf.Max(0f, _trauma - traumaDecay * Time.unscaledDeltaTime);
        }
    }
}
