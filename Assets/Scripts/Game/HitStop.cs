using System.Collections;
using UnityEngine;

namespace Proto.Game
{
    /// <summary>
    /// A brief time-freeze on a big beat (a hit, a win) — the cheapest way to make an impact land.
    /// Tweens/UI run on unscaled time so the juice keeps playing through the freeze. <see cref="OnDestroy"/>
    /// restores timeScale, so a restart mid-hit-stop can never leave the game frozen (stability first).
    /// </summary>
    public sealed class HitStop : MonoBehaviour
    {
        public void Trigger(float duration)
        {
            StopAllCoroutines();
            StartCoroutine(Routine(duration));
        }

        private static IEnumerator Routine(float duration)
        {
            Time.timeScale = 0f;
            yield return new WaitForSecondsRealtime(duration);
            Time.timeScale = 1f;
        }

        private void OnDestroy()
        {
            Time.timeScale = 1f;
        }
    }
}
