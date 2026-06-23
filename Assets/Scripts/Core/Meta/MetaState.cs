namespace Match3.Core
{
    /// <summary>
    /// The persisted meta-progress snapshot — a flat DTO of primitives, deliberately engine-free so the Core
    /// owns the shape and any storage backend (PlayerPrefs, a file, a test fake) just serializes these
    /// fields. <see cref="Defaults"/> is the fresh-install state (everything zero, level 0).
    /// </summary>
    public struct MetaState
    {
        public int Coins;
        public int Stars;
        public int Dice;
        public int TokenIndex;
        public int LevelIndex;

        public static MetaState Defaults => new MetaState
        {
            Coins = 0,
            Stars = 0,
            Dice = 0,
            TokenIndex = 0,
            LevelIndex = 0
        };
    }

    /// <summary>
    /// Persistence seam for meta progress. The Core defines the contract; the presentation layer supplies a
    /// concrete store (PlayerPrefs at runtime, an in-memory fake in tests). Keeping the interface in the Core
    /// keeps the persistence policy testable without touching the engine.
    /// </summary>
    public interface IMetaStore
    {
        MetaState Load();
        void Save(MetaState state);
        /// <summary>Wipe persisted progress back to a fresh install (drives the settings "Reset" button).
        /// On the interface so the composition root never has to downcast to a concrete store to reset.</summary>
        void Clear();
    }
}
