using UnityEngine;

namespace Match3.Config
{
    /// <summary>
    /// The ordered list of levels the meta loop walks through as the player unlocks them. <see cref="At"/>
    /// clamps the level index, so once every level is cleared the player simply replays the last (hardest)
    /// one — a defined terminal behaviour for the slice.
    /// </summary>
    [CreateAssetMenu(fileName = "LevelSet", menuName = "Match3/Level Set")]
    public sealed class LevelSet : ScriptableObject
    {
        [SerializeField] private LevelDefinition[] levels = new LevelDefinition[0];

        public int Count => levels.Length;

        public LevelDefinition At(int levelIndex)
        {
            if (levels.Length == 0) return null;
            int i = Mathf.Clamp(levelIndex, 0, levels.Length - 1);
            return levels[i];
        }

        public void SetLevels(LevelDefinition[] value) => levels = value;
    }
}
