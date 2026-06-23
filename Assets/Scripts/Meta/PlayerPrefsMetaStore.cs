using UnityEngine;
using Match3.Core;

namespace Match3.Meta
{
    /// <summary>
    /// <see cref="IMetaStore"/> backed by <see cref="PlayerPrefs"/>. Versioned for forward-compatible
    /// migration: a fresh install (no version key) loads <see cref="MetaState.Defaults"/>; the version key is
    /// written LAST on save, so a crash mid-save leaves an older-but-consistent state rather than a
    /// half-written one. Persistence is in scope for this portfolio piece (unlike the 30-second proto
    /// template, which forbids it).
    /// </summary>
    public sealed class PlayerPrefsMetaStore : IMetaStore
    {
        private const string Prefix = "m3.";
        private const int CurrentVersion = 1;

        private const string KeyVersion = Prefix + "version";
        private const string KeyCoins = Prefix + "coins";
        private const string KeyStars = Prefix + "stars";
        private const string KeyDice = Prefix + "dice";
        private const string KeyToken = Prefix + "token";
        private const string KeyLevel = Prefix + "level";

        public MetaState Load()
        {
            if (!PlayerPrefs.HasKey(KeyVersion))
                return MetaState.Defaults; // fresh install

            int version = PlayerPrefs.GetInt(KeyVersion, CurrentVersion);
            if (version > CurrentVersion)
            {
                // Written by a newer build than this one understands — refuse to misread it as current.
                Debug.LogWarning($"[Match3] Save version {version} is newer than supported {CurrentVersion}; loading defaults.");
                return MetaState.Defaults;
            }
            // On a future schema bump, a (version < CurrentVersion) branch would upgrade fields here before the reads below.

            return new MetaState
            {
                Coins = PlayerPrefs.GetInt(KeyCoins, 0),
                Stars = PlayerPrefs.GetInt(KeyStars, 0),
                Dice = PlayerPrefs.GetInt(KeyDice, 0),
                TokenIndex = PlayerPrefs.GetInt(KeyToken, 0),
                LevelIndex = PlayerPrefs.GetInt(KeyLevel, 0)
            };
        }

        public void Save(MetaState state)
        {
            PlayerPrefs.SetInt(KeyCoins, state.Coins);
            PlayerPrefs.SetInt(KeyStars, state.Stars);
            PlayerPrefs.SetInt(KeyDice, state.Dice);
            PlayerPrefs.SetInt(KeyToken, state.TokenIndex);
            PlayerPrefs.SetInt(KeyLevel, state.LevelIndex);
            PlayerPrefs.SetInt(KeyVersion, CurrentVersion); // write version LAST
            PlayerPrefs.Save();
        }

        /// <summary>Wipes saved progress (drives the settings "Reset" button back to a fresh install).</summary>
        public void Clear()
        {
            foreach (var key in new[] { KeyVersion, KeyCoins, KeyStars, KeyDice, KeyToken, KeyLevel })
                PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
        }
    }
}
