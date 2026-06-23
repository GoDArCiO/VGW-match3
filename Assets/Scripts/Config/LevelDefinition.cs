using UnityEngine;
using Match3.Core;

namespace Match3.Config
{
    /// <summary>
    /// One authored match-3 level (board size, palette size, goal, move limit). Converts to the engine-free
    /// <see cref="LevelConfig"/> the pure rules consume, keeping designers in the inspector and the Core
    /// testable.
    /// </summary>
    [CreateAssetMenu(fileName = "Level", menuName = "Match3/Level Definition")]
    public sealed class LevelDefinition : ScriptableObject
    {
        [SerializeField] private string displayName = "Level";
        [SerializeField, Min(3)] private int cols = 7;
        [SerializeField, Min(3)] private int rows = 8;
        [SerializeField, Min(3)] private int colorCount = 6;
        [SerializeField, Min(0)] private int goalColorId = 1;
        [SerializeField, Min(1)] private int goalCount = 25;
        [SerializeField, Min(1)] private int moveLimit = 20;

        public string DisplayName => displayName;
        public int Cols => cols;
        public int Rows => rows;
        public int ColorCount => colorCount;
        public int GoalColorId => goalColorId;
        public int GoalCount => goalCount;
        public int MoveLimit => moveLimit;

        public LevelConfig ToConfig() => new LevelConfig
        {
            Cols = cols,
            Rows = rows,
            ColorCount = colorCount,
            GoalColorId = goalColorId,
            GoalCount = goalCount,
            MoveLimit = moveLimit
        };

        public void Configure(string name, int cols, int rows, int colorCount, int goalColorId, int goalCount, int moveLimit)
        {
            displayName = name;
            this.cols = cols;
            this.rows = rows;
            this.colorCount = colorCount;
            this.goalColorId = goalColorId;
            this.goalCount = goalCount;
            this.moveLimit = moveLimit;
        }
    }
}
