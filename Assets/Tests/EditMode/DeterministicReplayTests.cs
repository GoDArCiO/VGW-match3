using NUnit.Framework;
using Match3.Core;

namespace Proto.Tests
{
    /// <summary>
    /// Determinism is a load-bearing property for a live match-3, not a nicety: daily/shared boards,
    /// server-side replay validation (anti-cheat), and economy regression all require that a fixed seed
    /// plus a fixed move list reproduce the EXACT same outcome. The engine is built for this — pure C#,
    /// seeded <see cref="System.Random"/>, no <c>UnityEngine.Random</c>, no wall-clock — so these tests
    /// pin the guarantee.
    /// </summary>
    public class DeterministicReplayTests
    {
        [Test]
        public void SameSeedAndMoves_ReplayBitIdentical_ScoreAndBoard()
        {
            // Two independent runs from the same seed, driven by the same deterministic move-picker, must
            // land on byte-identical score AND board state. This is the seam daily boards / server replay
            // / golden-master regression all build on.
            string runA = PlaySeededRun(seed: 73);
            string runB = PlaySeededRun(seed: 73);

            Assert.AreEqual(runA, runB,
                "A fixed seed + fixed moves must reproduce an identical score and board.");
        }

        [Test]
        public void SettledBoard_AfterAReplay_HasNoEmpties()
        {
            // A replayed run leaves a fully settled board (sanity that the deterministic path resolves cleanly).
            var game = DriveSeededRun(seed: 21);
            Assert.IsFalse(game.Board.HasEmpty(), "A settled board must hold no Empty cells.");
        }

        private static string PlaySeededRun(int seed)
        {
            var game = DriveSeededRun(seed);
            return $"score={game.Score}\n{game.Board}";
        }

        private static Match3Game DriveSeededRun(int seed)
        {
            var cfg = new LevelConfig
            {
                Cols = 7, Rows = 8, ColorCount = 6,
                GoalColorId = 1, GoalCount = 9999, // unreachable: keep playing the whole move budget
                MoveLimit = 12
            };
            var game = Match3Game.NewGame(cfg,
                new SeededGemSource(6, seed),     // board generation
                new SeededGemSource(6, seed + 1), // refills
                new System.Random(seed + 2));     // deadlock-reshuffle rng

            for (int move = 0; move < cfg.MoveLimit && !game.IsOver; move++)
            {
                if (!TryFirstLegalSwap(game.Board, out Cell from, out Cell to)) break;
                game.TrySwap(from, to);
            }
            return game;
        }

        // Deterministic move-picker: the first legal swap in a fixed scan order (so the "move list" is itself
        // a pure function of the board — no randomness in the harness).
        private static bool TryFirstLegalSwap(Board b, out Cell a, out Cell c)
        {
            for (int x = 0; x < b.Cols; x++)
                for (int y = 0; y < b.Rows; y++)
                {
                    if (x + 1 < b.Cols && Makes(b, new Cell(x, y), new Cell(x + 1, y))) { a = new Cell(x, y); c = new Cell(x + 1, y); return true; }
                    if (y + 1 < b.Rows && Makes(b, new Cell(x, y), new Cell(x, y + 1))) { a = new Cell(x, y); c = new Cell(x, y + 1); return true; }
                }
            a = default; c = default; return false;
        }

        private static bool Makes(Board b, Cell a, Cell c)
        {
            b.Swap(a, c);
            bool m = MatchFinder.HasMatch(b);
            b.Swap(a, c);
            return m;
        }
    }
}
