using System.Collections.Generic;
using UnityEngine;

namespace Proto.Game
{
    /// <summary>
    /// One flat-shaded URP material per colour, cached — no per-object material instances, no texture
    /// pipeline (the flat low-poly pillar). Also mints HDR-emissive variants so the bloom post-process
    /// makes chosen objects bleed light.
    /// </summary>
    public sealed class MaterialFactory
    {
        private readonly Dictionary<Color, Material> _cache = new Dictionary<Color, Material>();
        private readonly Dictionary<(Color, float), Material> _emissiveCache = new Dictionary<(Color, float), Material>();
        private Shader _shader;

        // Resolved lazily, never in the constructor: Shader.Find is illegal from a MonoBehaviour
        // constructor / field initializer, so a MaterialFactory built as a field of a MonoBehaviour would
        // throw. The first material request happens during Awake/Start/play, where Find is allowed.
        // Fall back through a couple of always-present shaders so a stripped URP/Lit can't produce a null
        // shader → `new Material(null)` ArgumentNullException (which HALTS a WebGL build). The build force-
        // includes URP/Lit (BuildScript), so the primary find normally succeeds; this is defence in depth.
        private Shader Shader => _shader != null ? _shader : (_shader =
            UnityEngine.Shader.Find("Universal Render Pipeline/Lit")
            ?? UnityEngine.Shader.Find("Universal Render Pipeline/Unlit")
            ?? UnityEngine.Shader.Find("Sprites/Default"));

        public Material Get(Color colour)
        {
            if (_cache.TryGetValue(colour, out Material material)) return material;
            Shader sh = Shader;
            if (sh == null) { Debug.LogError("[Proto] MaterialFactory: no usable shader found — material skipped (check Always-Included shaders)."); return null; }
            material = new Material(sh) { color = colour };
            material.SetFloat("_Smoothness", 0.1f);
            _cache[colour] = material;
            return material;
        }

        /// <summary>
        /// A glowing variant of <paramref name="colour"/>: HDR emission at <paramref name="intensity"/> so
        /// the bloom post-process makes it bleed light. Cached per (colour, intensity) so we never spawn
        /// per-object material instances (flat low-poly pillar).
        /// </summary>
        public Material GetEmissive(Color colour, float intensity = 2.4f)
        {
            var key = (colour, intensity);
            if (_emissiveCache.TryGetValue(key, out Material material)) return material;
            Shader sh = Shader;
            if (sh == null) { Debug.LogError("[Proto] MaterialFactory: no usable shader found — emissive material skipped."); return null; }
            material = new Material(sh) { color = colour };
            material.SetFloat("_Smoothness", 0.35f);
            material.EnableKeyword("_EMISSION");
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            material.SetColor("_EmissionColor", colour * intensity);
            _emissiveCache[key] = material;
            return material;
        }

        /// <summary>Maps friendly colour ids to display colours. Unknown ids render magenta — visible, never a crash.</summary>
        public static Color ColourOf(string colourId) => colourId switch
        {
            "red" => new Color(0.91f, 0.25f, 0.24f),
            "blue" => new Color(0.23f, 0.51f, 0.93f),
            "yellow" => new Color(0.98f, 0.83f, 0.18f),
            "green" => new Color(0.30f, 0.78f, 0.36f),
            "purple" => new Color(0.66f, 0.36f, 0.90f),
            "orange" => new Color(0.98f, 0.58f, 0.16f),
            "white" => new Color(0.95f, 0.96f, 0.98f),
            "dark" => new Color(0.12f, 0.13f, 0.17f),
            _ => Color.magenta,
        };
    }
}
