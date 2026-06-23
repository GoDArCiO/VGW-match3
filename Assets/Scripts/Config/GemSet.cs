using System;
using UnityEngine;

namespace Match3.Config
{
    /// <summary>Distinct gem silhouettes — colour PLUS shape, so the board stays readable for colour-blind
    /// players (a readability/polish signal, not just a recolour).</summary>
    public enum GemShape
    {
        Circle,
        Diamond,
        Square,
        Triangle,
        Hexagon,
        Star
    }

    [Serializable]
    public struct GemTypeDef
    {
        public string Name;   // display name for the goal label — single source of truth with Color
        public Color Color;
        public GemShape Shape;
    }

    /// <summary>
    /// The palette of gem types. A board color id is an index into <see cref="Gems"/>: id <c>i</c> renders as
    /// <c>Gems[i]</c> (colour + shape). Authored as an asset (generated headlessly by the editor tool); the
    /// view reads it to build one procedural sprite per type.
    /// </summary>
    [CreateAssetMenu(fileName = "GemSet", menuName = "Match3/Gem Set")]
    public sealed class GemSet : ScriptableObject
    {
        [SerializeField] private GemTypeDef[] gems = Array.Empty<GemTypeDef>();

        public int Count => gems.Length;
        public GemTypeDef Get(int colorId) => gems[colorId];

        public void SetGems(GemTypeDef[] value) => gems = value;
    }
}
