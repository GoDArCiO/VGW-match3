using NUnit.Framework;
using Match3.Core;

namespace Proto.Tests
{
    /// <summary>
    /// The pure meta-progression model: the looped board, dice spending, single-shot star unlocks, and the
    /// persistence round-trip (via an in-memory store fake). All engine-free.
    /// </summary>
    public class MetaTests
    {
        private static MetaTile[] Loop12()
        {
            var tiles = new MetaTile[12];
            for (int i = 0; i < tiles.Length; i++) tiles[i] = new MetaTile(RewardKind.Nothing, 0);
            return tiles;
        }

        [Test]
        public void MetaBoard_StepWrapsAroundTheLoop()
        {
            var board = new MetaBoard(Loop12());
            Assert.AreEqual(12, board.Count);
            Assert.AreEqual(0, board.Step(11, 1).newIndex);
            Assert.AreEqual(1, board.Step(10, 3).newIndex);
            Assert.AreEqual(5, board.Step(0, 5).newIndex);
        }

        [Test]
        public void SpendDie_MovesToken_AppliesReward_DecrementsDice()
        {
            var tiles = Loop12();
            tiles[2] = new MetaTile(RewardKind.Coins, 50);
            var board = new MetaBoard(tiles);
            var p = new MetaProgress(new MetaState { Dice = 1, TokenIndex = 0 });

            var outcome = p.SpendDie(board, new ScriptedDieRoller(2));

            Assert.IsTrue(outcome.Rolled);
            Assert.AreEqual(2, outcome.Face);
            Assert.AreEqual(2, outcome.ToIndex);
            Assert.AreEqual(50, outcome.CoinsGained);
            Assert.AreEqual(50, p.Coins);
            Assert.AreEqual(0, p.Dice);
            Assert.AreEqual(2, p.TokenIndex);
        }

        [Test]
        public void SpendDie_WithNoDice_IsANoOp()
        {
            var board = new MetaBoard(Loop12());
            var p = new MetaProgress(new MetaState { Dice = 0, TokenIndex = 0 });

            var outcome = p.SpendDie(board, new ScriptedDieRoller(4));

            Assert.IsFalse(outcome.Rolled);
            Assert.AreEqual(0, p.TokenIndex);
            Assert.AreEqual(0, p.Dice);
        }

        [Test]
        public void CollectingStars_UnlocksNextLevelOnce_AndConsumesThreshold()
        {
            var tiles = Loop12();
            tiles[3] = new MetaTile(RewardKind.Star, 1);
            tiles[5] = new MetaTile(RewardKind.Coins, 10);
            var board = new MetaBoard(tiles);
            var p = new MetaProgress(new MetaState { Stars = 2, Dice = 2, TokenIndex = 0 }, starsPerUnlock: 3);

            var first = p.SpendDie(board, new ScriptedDieRoller(3)); // 0 -> 3 : Star, stars 2->3 -> unlock
            Assert.IsTrue(first.Unlocked);
            Assert.AreEqual(1, first.LevelAfter);
            Assert.AreEqual(0, p.Stars, "Threshold is consumed so it can't re-trigger.");
            Assert.AreEqual(1, p.LevelIndex);

            var second = p.SpendDie(board, new ScriptedDieRoller(2)); // 3 -> 5 : Coins, no unlock
            Assert.IsFalse(second.Unlocked);
            Assert.AreEqual(1, p.LevelIndex);
            Assert.AreEqual(10, p.Coins);
        }

        [Test]
        public void AStarRewardCrossingThresholdTwice_UnlocksTwice()
        {
            var tiles = Loop12();
            tiles[2] = new MetaTile(RewardKind.Star, 6); // two thresholds at once
            var board = new MetaBoard(tiles);
            var p = new MetaProgress(new MetaState { Stars = 0, Dice = 1, TokenIndex = 0 }, starsPerUnlock: 3);

            var outcome = p.SpendDie(board, new ScriptedDieRoller(2));

            Assert.IsTrue(outcome.Unlocked);
            Assert.AreEqual(2, outcome.LevelAfter);
            Assert.AreEqual(2, p.LevelIndex);
            Assert.AreEqual(0, p.Stars);
        }

        [Test]
        public void AwardDice_AddsButNeverGoesNegative()
        {
            var p = new MetaProgress(MetaState.Defaults);
            p.AwardDice(3);
            p.AwardDice(0);
            p.AwardDice(-5);
            Assert.AreEqual(3, p.Dice);
        }

        [Test]
        public void Clear_WipesPersistedProgress_BackToFreshInstall()
        {
            // Reset is reachable through the IMetaStore interface (no concrete downcast), so it is unit-testable.
            var store = new InMemoryMetaStore();
            store.Save(new MetaState { Coins = 50, Dice = 3, LevelIndex = 2 });

            store.Clear();

            var after = store.Load();
            Assert.AreEqual(0, after.Coins + after.Dice + after.LevelIndex, "Clear must restore fresh-install defaults.");
            Assert.AreEqual(1, store.ClearCount);
        }

        [Test]
        public void Persistence_RoundTrips_AndFreshInstallIsAllZero()
        {
            var store = new InMemoryMetaStore();

            var fresh = store.Load();
            Assert.AreEqual(0, fresh.Coins + fresh.Stars + fresh.Dice + fresh.TokenIndex + fresh.LevelIndex);

            var p = new MetaProgress(new MetaState { Coins = 12, Stars = 2, Dice = 5, TokenIndex = 7, LevelIndex = 1 });
            store.Save(p.ToState());

            var loaded = store.Load();
            Assert.AreEqual(12, loaded.Coins);
            Assert.AreEqual(2, loaded.Stars);
            Assert.AreEqual(5, loaded.Dice);
            Assert.AreEqual(7, loaded.TokenIndex);
            Assert.AreEqual(1, loaded.LevelIndex);

            var restored = new MetaProgress(loaded);
            Assert.AreEqual(12, restored.Coins);
            Assert.AreEqual(1, restored.LevelIndex);
        }
    }
}
