using System;

namespace Match3.Core
{
    /// <summary>The outcome of spending one die: the face rolled, where the token moved, what it gained, and
    /// whether that pushed the player over an unlock. <see cref="None"/> (Rolled = false) means the roll was
    /// refused (no dice).</summary>
    public readonly struct RollOutcome
    {
        public readonly bool Rolled;
        public readonly int Face;
        public readonly int FromIndex;
        public readonly int ToIndex;
        public readonly MetaTile Landed;
        public readonly int CoinsGained;
        public readonly int StarsGained;
        public readonly int DiceGained;
        public readonly bool Unlocked;
        public readonly int LevelAfter;

        public RollOutcome(bool rolled, int face, int fromIndex, int toIndex, MetaTile landed,
                           int coinsGained, int starsGained, int diceGained, bool unlocked, int levelAfter)
        {
            Rolled = rolled;
            Face = face;
            FromIndex = fromIndex;
            ToIndex = toIndex;
            Landed = landed;
            CoinsGained = coinsGained;
            StarsGained = starsGained;
            DiceGained = diceGained;
            Unlocked = unlocked;
            LevelAfter = levelAfter;
        }

        public static readonly RollOutcome None = default;
    }

    /// <summary>
    /// The meta-progression model: coins, stars, dice, the token's position on the loop, and the unlocked
    /// level. Pure C#. A match-3 win awards dice (<see cref="AwardDice"/>); spending a die
    /// (<see cref="SpendDie"/>) hops the token and grants the tile's reward, and collecting enough stars
    /// unlocks the next level. Persists via <see cref="ToState"/> / the <see cref="MetaState"/> ctor.
    /// </summary>
    public sealed class MetaProgress
    {
        public int Coins { get; private set; }
        public int Stars { get; private set; }
        public int Dice { get; private set; }
        public int TokenIndex { get; private set; }
        public int LevelIndex { get; private set; }

        /// <summary>Stars consumed to unlock each successive level (config, not persisted progress).</summary>
        public int StarsPerUnlock { get; }

        public MetaProgress(MetaState state, int starsPerUnlock = 3)
        {
            if (starsPerUnlock < 1) throw new ArgumentOutOfRangeException(nameof(starsPerUnlock));
            Coins = state.Coins;
            Stars = state.Stars;
            Dice = state.Dice;
            TokenIndex = state.TokenIndex;
            LevelIndex = state.LevelIndex;
            StarsPerUnlock = starsPerUnlock;
        }

        public MetaState ToState() => new MetaState
        {
            Coins = Coins,
            Stars = Stars,
            Dice = Dice,
            TokenIndex = TokenIndex,
            LevelIndex = LevelIndex
        };

        /// <summary>Grants dice for clearing a level (the bridge from match-3 win to the meta loop).</summary>
        public void AwardDice(int amount)
        {
            if (amount > 0) Dice += amount;
        }

        /// <summary>
        /// Spends one die: rolls a face, hops the token that many tiles, applies the landed tile's reward,
        /// and consumes stars into level unlocks. Computed as one all-or-nothing transform (no partial
        /// mutation), so persisting <see cref="ToState"/> immediately after always reflects a completed roll.
        /// A no-op returning <see cref="RollOutcome.None"/> when there are no dice.
        /// </summary>
        public RollOutcome SpendDie(MetaBoard board, IDieRoller roller)
        {
            if (board == null) throw new ArgumentNullException(nameof(board));
            if (roller == null) throw new ArgumentNullException(nameof(roller));
            if (Dice <= 0) return RollOutcome.None;

            int face = roller.Roll();
            var (toIndex, tile) = board.Step(TokenIndex, face);

            // Build the candidate next-state locally; commit only at the end.
            int dice = Dice - 1;
            int coins = Coins, stars = Stars, level = LevelIndex;
            int coinsGained = 0, starsGained = 0, diceGained = 0;

            switch (tile.Kind)
            {
                case RewardKind.Coins: coins += tile.Amount; coinsGained = tile.Amount; break;
                case RewardKind.Star: stars += tile.Amount; starsGained = tile.Amount; break;
                case RewardKind.Dice: dice += tile.Amount; diceGained = tile.Amount; break;
                case RewardKind.Nothing: break;
            }

            // Single-shot consume: each StarsPerUnlock stars buys one level, and the leftover can never sit
            // at/above the threshold (which would re-trigger the unlock on the next, unrelated roll).
            bool unlocked = false;
            while (stars >= StarsPerUnlock)
            {
                stars -= StarsPerUnlock;
                level++;
                unlocked = true;
            }

            int fromIndex = TokenIndex;
            Dice = dice;
            Coins = coins;
            Stars = stars;
            TokenIndex = toIndex;
            LevelIndex = level;

            return new RollOutcome(true, face, fromIndex, toIndex, tile,
                                   coinsGained, starsGained, diceGained, unlocked, level);
        }
    }
}
