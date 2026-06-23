namespace Match3.Core
{
    /// <summary>
    /// Tuning for one match-3 level — the only knobs the pure rules read. The presentation layer owns the
    /// values (authored as a <c>LevelDefinition</c> ScriptableObject that converts to this engine-free POCO),
    /// so the same <see cref="Match3Game"/> can be driven at any difficulty in a test without the engine.
    /// </summary>
    public sealed class LevelConfig
    {
        public int Cols = 7;
        public int Rows = 8;
        public int ColorCount = 6;

        /// <summary>The gem color the level asks the player to collect.</summary>
        public int GoalColorId = 1;

        /// <summary>How many of <see cref="GoalColorId"/> must be cleared to win.</summary>
        public int GoalCount = 25;

        /// <summary>Swaps allowed before the level is lost (if the goal isn't met).</summary>
        public int MoveLimit = 20;
    }
}
