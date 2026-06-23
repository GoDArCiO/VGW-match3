using System.Collections.Generic;
using NUnit.Framework;
using Match3.Core;

namespace Proto.Tests
{
    /// <summary>
    /// The session rules: goal/move accounting, win/lose resolution, and the illegal-move and terminal-state
    /// guards. Driven headlessly on hand-built boards.
    /// </summary>
    public class Match3GameTests
    {
        private static LevelConfig Cfg(int goalColor, int goalCount, int moveLimit, int colors = 6)
            => new LevelConfig { Cols = 3, Rows = 3, ColorCount = colors, GoalColorId = goalColor, GoalCount = goalCount, MoveLimit = moveLimit };

        // Bottom row completes to [1,1,1] when (2,0)<->(2,1) is swapped; clears three color-1 gems.
        private static Board SingleMatchBoard() => Board.FromRows("345", "671", "112");

        [Test]
        public void NewGame_StartsMatchFree_WithALegalMove_AndFullCounters()
        {
            var cfg = new LevelConfig { Cols = 7, Rows = 8, ColorCount = 6, GoalColorId = 1, GoalCount = 25, MoveLimit = 20 };
            var game = Match3Game.NewGame(cfg, new SeededGemSource(6, 1), new SeededGemSource(6, 2));

            Assert.IsFalse(MatchFinder.HasMatch(game.Board));
            Assert.IsTrue(BoardGenerator.HasAnyLegalMove(game.Board));
            Assert.AreEqual(20, game.MovesLeft);
            Assert.AreEqual(25, game.GoalRemaining);
            Assert.AreEqual(GameStatus.Playing, game.Status);
        }

        [Test]
        public void ClearingGoalColor_ToZero_Wins_AndConsumesOneMove()
        {
            var game = new Match3Game(Cfg(goalColor: 1, goalCount: 3, moveLimit: 5), new UniqueGemSource(), SingleMatchBoard());

            var res = game.TrySwap(new Cell(2, 0), new Cell(2, 1));

            Assert.IsTrue(res.Valid);
            Assert.AreEqual(GameStatus.Won, res.StatusAfter);
            Assert.AreEqual(GameStatus.Won, game.Status);
            Assert.AreEqual(0, game.GoalRemaining);
            Assert.AreEqual(30, game.Score);
            Assert.AreEqual(4, game.MovesLeft);
        }

        [Test]
        public void RunningOutOfMovesBeforeGoal_Loses()
        {
            // Goal is a color this move never clears; with one move it must end Lost.
            var game = new Match3Game(Cfg(goalColor: 4, goalCount: 10, moveLimit: 1), new UniqueGemSource(), SingleMatchBoard());

            var res = game.TrySwap(new Cell(2, 0), new Cell(2, 1));

            Assert.IsTrue(res.Valid);
            Assert.AreEqual(GameStatus.Lost, res.StatusAfter);
            Assert.AreEqual(GameStatus.Lost, game.Status);
        }

        [Test]
        public void GoalMetByARefillCreatedMatchLaterInTheCascade_StillWins()
        {
            // Goal color 5 is only cleared in cascade step 2 (formed by the scripted refill).
            var cfg = new LevelConfig { Cols = 3, Rows = 4, ColorCount = 10, GoalColorId = 5, GoalCount = 3, MoveLimit = 5 };
            var board = Board.FromRows("501", "543", "324", "212");
            var src = new ScriptedGemSource(new Dictionary<int, int[]>
            {
                { 0, new[] { 5, 7, 6, 7 } }, { 1, new[] { 9 } }, { 2, new[] { 8 } },
            });
            var game = new Match3Game(cfg, src, board);

            var res = game.TrySwap(new Cell(1, 0), new Cell(1, 1));

            Assert.AreEqual(GameStatus.Won, res.StatusAfter);
            Assert.AreEqual(0, game.GoalRemaining);
            Assert.AreEqual(90, game.Score);
        }

        [Test]
        public void IllegalSwap_ConsumesNoMove_AndDoesNotScore()
        {
            var game = new Match3Game(Cfg(0, 5, 5), new UniqueGemSource(), Board.FromRows("012", "345", "678"));

            var noMatch = game.TrySwap(new Cell(0, 0), new Cell(1, 0));
            var nonAdjacent = game.TrySwap(new Cell(0, 0), new Cell(2, 0));

            Assert.IsFalse(noMatch.Valid);
            Assert.IsFalse(nonAdjacent.Valid);
            Assert.AreEqual(5, game.MovesLeft);
            Assert.AreEqual(0, game.Score);
            Assert.AreEqual(GameStatus.Playing, game.Status);
        }

        [Test]
        public void SwappingAfterTheGameIsOver_IsANoOp()
        {
            var game = new Match3Game(Cfg(goalColor: 1, goalCount: 3, moveLimit: 5), new UniqueGemSource(), SingleMatchBoard());
            game.TrySwap(new Cell(2, 0), new Cell(2, 1)); // win
            int movesAfterWin = game.MovesLeft;
            int scoreAfterWin = game.Score;

            var res = game.TrySwap(new Cell(0, 0), new Cell(0, 1));

            Assert.IsFalse(res.Valid);
            Assert.AreEqual(GameStatus.Won, game.Status);
            Assert.AreEqual(movesAfterWin, game.MovesLeft);
            Assert.AreEqual(scoreAfterWin, game.Score);
        }
    }
}
