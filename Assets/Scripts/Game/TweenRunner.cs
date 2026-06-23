using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Proto.Game
{
    public static class Ease
    {
        public static float OutCubic(float t) => 1f - Mathf.Pow(1f - t, 3f);
        public static float InCubic(float t) => t * t * t;

        public static float OutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float u = t - 1f;
            return 1f + c3 * u * u * u + c1 * u * u;
        }

        /// <summary>A real settle-bounce: the value falls, hits the target, and hops a few diminishing
        /// times — always within [0,1] (unlike <see cref="OutBack"/>, which overshoots PAST the target).</summary>
        public static float OutBounce(float t)
        {
            const float n1 = 7.5625f, d1 = 2.75f;
            if (t < 1f / d1) return n1 * t * t;
            if (t < 2f / d1) { t -= 1.5f / d1; return n1 * t * t + 0.75f; }
            if (t < 2.5f / d1) { t -= 2.25f / d1; return n1 * t * t + 0.9375f; }
            t -= 2.625f / d1; return n1 * t * t + 0.984375f;
        }
    }

    /// <summary>
    /// The workhorse of the juice toolkit: a tiny coroutine-based tween host (no external tween library).
    /// One running tween per (target, channel) — starting a new one first cancels the old and restores that
    /// channel's baseline, so juice can never wander a transform off its base scale/rotation. All tweens run
    /// on UNSCALED time so they keep playing through a <see cref="HitStop"/> (timeScale 0).
    ///
    /// Why hand-rolled rather than DOTween: it's zero-dependency (no registry/asset-store install, smaller
    /// WebGL build), deterministic, and the channel-cancellation + baseline-restore is purpose-built for
    /// juice that must never desync. Add DOTween per-proto if a concept truly needs its feature set.
    /// </summary>
    public sealed class TweenRunner : MonoBehaviour
    {
        public enum Channel
        {
            Scale,
            Rotation,
            Position,
        }

        private sealed class Running
        {
            public Coroutine Coroutine;
            public Action Restore;
        }

        private readonly Dictionary<(Transform, Channel), Running> _running =
            new Dictionary<(Transform, Channel), Running>();

        /// <summary>Squash/punch to <paramref name="peak"/> × baseline, settling back with overshoot.</summary>
        public void PunchScale(Transform target, float peak, float duration)
        {
            if (target == null) return;
            Cancel(target, Channel.Scale);
            Vector3 baseline = target.localScale;
            StartChannel(target, Channel.Scale, () => { if (target != null) target.localScale = baseline; }, Punch());

            IEnumerator Punch()
            {
                yield return Animate(duration, target, t =>
                {
                    float scale = Mathf.LerpUnclamped(peak, 1f, Ease.OutBack(t));
                    target.localScale = baseline * scale;
                });
                if (target != null) target.localScale = baseline;
            }
        }

        /// <summary>Yaw jiggle around the current rotation (a refusal / rejected-input wobble).</summary>
        public void ShakeRotation(Transform target, float degrees, float duration)
        {
            if (target == null) return;
            Cancel(target, Channel.Rotation);
            Quaternion baseline = target.localRotation;
            StartChannel(target, Channel.Rotation, () => { if (target != null) target.localRotation = baseline; }, Shake());

            IEnumerator Shake()
            {
                yield return Animate(duration, target, t =>
                {
                    float falloff = 1f - t;
                    float angle = Mathf.Sin(t * 40f) * degrees * falloff;
                    target.localRotation = baseline * Quaternion.Euler(0f, angle, 0f);
                });
                if (target != null) target.localRotation = baseline;
            }
        }

        /// <summary>Local-position shake with falloff (impact, small bumps).</summary>
        public void ShakePosition(Transform target, float amplitude, float duration)
        {
            if (target == null) return;
            Cancel(target, Channel.Position);
            Vector3 baseline = target.localPosition;
            StartChannel(target, Channel.Position, () => { if (target != null) target.localPosition = baseline; }, Shake());

            IEnumerator Shake()
            {
                yield return Animate(duration, target, t =>
                {
                    float falloff = 1f - t;
                    target.localPosition = baseline + UnityEngine.Random.insideUnitSphere * (amplitude * falloff);
                });
                if (target != null) target.localPosition = baseline;
            }
        }

        /// <summary>Moves a transform along a parabolic world-space arc, then invokes onDone.</summary>
        public void ArcMove(Transform target, Vector3 from, Vector3 to, float height, float duration, Action onDone = null)
        {
            if (target == null) return;
            Cancel(target, Channel.Position);
            StartChannel(target, Channel.Position, null, Arc());

            IEnumerator Arc()
            {
                yield return Animate(duration, target, t =>
                {
                    float eased = Ease.OutCubic(t);
                    Vector3 pos = Vector3.LerpUnclamped(from, to, eased);
                    pos.y += height * 4f * eased * (1f - eased);
                    target.position = pos;
                });
                if (target != null) target.position = to;
                onDone?.Invoke();
            }
        }

        /// <summary>Spin + shrink to zero scale (a loss spin-out). Intentionally leaves the target shrunk.</summary>
        public void SpinOut(Transform target, float duration)
        {
            if (target == null) return;
            Cancel(target, Channel.Scale);
            Vector3 baseline = target.localScale;
            StartChannel(target, Channel.Scale, null, Spin());

            IEnumerator Spin()
            {
                yield return Animate(duration, target, t =>
                {
                    target.Rotate(0f, 1080f * Time.unscaledDeltaTime / duration, 0f, Space.World);
                    target.localScale = baseline * (1f - Ease.InCubic(t));
                });
                if (target != null) target.localScale = Vector3.zero;
            }
        }

        /// <summary>Runs an arbitrary routine outside the channel system (fire-and-forget sequences).</summary>
        public void Run(IEnumerator routine) => StartCoroutine(routine);

        /// <summary>Steps t over [0,1] on unscaled time; stops silently if the target is destroyed.</summary>
        public static IEnumerator Animate(float duration, Transform target, Action<float> step)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                if (target == null) yield break;
                step(Mathf.Clamp01(elapsed / duration));
                yield return null;
            }
        }

        public void Cancel(Transform target, Channel channel)
        {
            var key = (target, channel);
            if (!_running.TryGetValue(key, out Running running)) return;
            if (running.Coroutine != null) StopCoroutine(running.Coroutine);
            running.Restore?.Invoke();
            _running.Remove(key);
        }

        private void StartChannel(Transform target, Channel channel, Action restore, IEnumerator routine)
        {
            var key = (target, channel);
            var running = new Running { Restore = restore };
            _running[key] = running;
            running.Coroutine = StartCoroutine(Wrapped());

            IEnumerator Wrapped()
            {
                yield return routine;
                if (_running.TryGetValue(key, out Running current) && current == running)
                    _running.Remove(key);
            }
        }
    }
}
