using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Match3.Core;

namespace Proto.Tests
{
    /// <summary>
    /// The heart of the game: resolving a swap to a stable board. Cascades are made deterministic by either
    /// a <see cref="UniqueGemSource"/> (refills can never match — isolates gravity-driven chains) or a
    /// <see cref="ScriptedGemSource"/> (per-column refills author a chain).
    /// </summary>
    public class CascadeResolverTests
    {
        [Test]
        public void IllegalSwap_FormsNoMatch_ReturnsInvalid_BoardUnchanged()
        {
            var b = Board.FromRows("012", "345", "678");
            string before = b.ToString();

            var outcome = CascadeResolver.Resolve(b, new Cell(0, 0), new Cell(1, 0), new UniqueGemSource());

            Assert.IsFalse(outcome.Valid);
            Assert.AreEqual(before, b.ToString(), "An illegal swap must leave the board untouched.");
        }

        [Test]
        public void NonAdjacentSwap_ReturnsInvalid()
        {
            var b = Board.FromRows("012", "345", "678");
            var outcome = CascadeResolver.Resolve(b, new Cell(0, 0), new Cell(2, 0), new UniqueGemSource());
            Assert.IsFalse(outcome.Valid);
        }

        [Test]
        public void SingleMatch_IsOneStepChainOne()
        {
            // Swapping (2,0)<->(2,1) brings a 1 down to complete the bottom row [1,1,1].
            var b = Board.FromRows("345", "671", "112");
            var outcome = CascadeResolver.Resolve(b, new Cell(2, 0), new Cell(2, 1), new UniqueGemSource());

            Assert.IsTrue(outcome.Valid);
            Assert.AreEqual(1, outcome.Steps.Count);
            Assert.AreEqual(1, outcome.MaxChain);
            Assert.AreEqual(1, outcome.Steps[0].Chain);
            Assert.AreEqual(1, outcome.Steps[0].Multiplier);
            Assert.AreEqual(3, outcome.Steps[0].ClearedCount);
            Assert.AreEqual(30, outcome.Steps[0].StepScore); // 3 gems * 10, mult 1
            Assert.AreEqual(30, outcome.TotalScore);
        }

        [Test]
        public void TwoStepCascade_GravityDriven_MultiplierGrows()
        {
            // Step 1: swap (1,2)<->(2,2) makes a vertical 3 of color 1 in col1. Clearing it drops col1's
            // top gem (a 2) to the bottom row, which lines up [2,2,2] across row 0 -> step 2.
            var b = Board.FromRows(
                "524",
                "401",
                "315",
                "212");
            var outcome = CascadeResolver.Resolve(b, new Cell(1, 2), new Cell(2, 2), new UniqueGemSource());

            Assert.IsTrue(outcome.Valid);
            Assert.AreEqual(2, outcome.Steps.Count);
            Assert.AreEqual(2, outcome.MaxChain);
            Assert.AreEqual(1, outcome.Steps[0].Chain);
            Assert.AreEqual(2, outcome.Steps[1].Chain);
            Assert.Greater(outcome.Steps[1].Multiplier, outcome.Steps[0].Multiplier);
            Assert.AreEqual(30, outcome.Steps[0].StepScore); // 3*10 * mult(1)=1
            Assert.AreEqual(60, outcome.Steps[1].StepScore); // 3*10 * mult(2)=2
            Assert.AreEqual(90, outcome.TotalScore);
        }

        [Test]
        public void TwoStepCascade_RefillDriven_ViaScriptedColumns()
        {
            // Step 1: swap (1,0)<->(1,1) makes row 0 = [2,2,2]. After clear+drop, col0 is [3,5,5,_];
            // the scripted col0 refill drops a 5 on top -> vertical [5,5,5] in col0 = step 2.
            var b = Board.FromRows(
                "501",
                "543",
                "324",
                "212");
            var src = new ScriptedGemSource(new Dictionary<int, int[]>
            {
                { 0, new[] { 5, 7, 6, 7 } }, // step1 top (5), then step2's three refills
                { 1, new[] { 9 } },
                { 2, new[] { 8 } },
            });

            var outcome = CascadeResolver.Resolve(b, new Cell(1, 0), new Cell(1, 1), src);

            Assert.IsTrue(outcome.Valid);
            Assert.AreEqual(2, outcome.Steps.Count);
            Assert.AreEqual(2, outcome.Steps[0].Cleared[0].Color); // step 1 cleared the 2s
            Assert.AreEqual(5, outcome.Steps[1].Cleared[0].Color); // step 2 cleared the refilled 5s
            Assert.AreEqual(90, outcome.TotalScore);               // 30*1 + 30*2
        }

        [Test]
        public void OneSwap_MakingTwoMatches_ClearsBothInOneStep()
        {
            // Swapping (1,1)<->(2,1): col1 becomes a vertical 1-run AND row 1 becomes a 2-run at x2..x4.
            var b = Board.FromRows(
                "31456",
                "02122",
                "31546");
            var outcome = CascadeResolver.Resolve(b, new Cell(1, 1), new Cell(2, 1), new UniqueGemSource());

            Assert.IsTrue(outcome.Valid);
            Assert.AreEqual(1, outcome.Steps[0].Chain);
            Assert.AreEqual(2, outcome.Steps[0].Cleared.Count, "Two disjoint matches must clear as two groups in one step.");
            Assert.AreEqual(6, outcome.Steps[0].ClearedCount);
        }

        [Test]
        public void Resolve_PreservesInvariants_OnGeneratedBoards()
        {
            // Brute-force a legal move on a real generated board and assert the structural invariants
            // for ANY cascade (independent of hand-computed values).
            for (int seed = 1; seed <= 20; seed++)
            {
                var board = BoardGenerator.Generate(7, 8, 6, new SeededGemSource(6, seed));
                if (!TryFindLegalSwap(board, out var a, out var c)) continue;

                var outcome = CascadeResolver.Resolve(board, a, c, new SeededGemSource(6, seed + 1000));
                Assert.IsTrue(outcome.Valid);
                Assert.AreEqual(outcome.Steps.Count, outcome.MaxChain, "MaxChain == step count.");

                int runningScore = 0;
                int prevMult = 0;
                for (int i = 0; i < outcome.Steps.Count; i++)
                {
                    var step = outcome.Steps[i];
                    Assert.AreEqual(i + 1, step.Chain, "Steps[i].Chain == i+1.");
                    Assert.GreaterOrEqual(step.Multiplier, prevMult, "Multiplier is monotonic non-decreasing.");
                    prevMult = step.Multiplier;
                    Assert.IsNotNull(step.Movements);
                    Assert.IsNotNull(step.Spawns);
                    Assert.AreEqual(step.ClearedCount, step.Cleared.Sum(g => g.Size), "Groups partition the cleared set.");
                    runningScore += step.StepScore;
                }
                Assert.AreEqual(outcome.TotalScore, runningScore, "TotalScore == sum of step scores.");
                Assert.IsFalse(board.HasEmpty(), "Settled board has no empties.");
            }
        }

        [Test]
        public void MultiplierTable_GrowsWithDepth_AndScoringBonuses()
        {
            var scoring = new ScoringConfig(); // defaults reproduce the original baked economy
            Assert.AreEqual(1, scoring.MultiplierFor(1));
            Assert.AreEqual(2, scoring.MultiplierFor(2));
            Assert.AreEqual(3, scoring.MultiplierFor(3));
            Assert.AreEqual(5, scoring.MultiplierFor(4));
            Assert.AreEqual(8, scoring.MultiplierFor(5));
            Assert.AreEqual(12, scoring.MultiplierFor(6));
            Assert.AreEqual(12, scoring.MultiplierFor(7)); // clamps to the last entry for deeper chains

            Assert.AreEqual(0, scoring.LongBonus(3));
            Assert.AreEqual(30, scoring.LongBonus(4));
            Assert.AreEqual(60, scoring.LongBonus(5));
        }

        [Test]
        public void InjectedScoringConfig_RetunesTheEconomy_WithoutTouchingTheResolver()
        {
            // The live-ops seam: a remote-config value / A-B bucket supplies a different ScoringConfig at
            // runtime. Same board + same swap, only the injected economy differs -> different banked score.
            var board = Board.FromRows("345", "671", "112"); // swap (2,0)<->(2,1) clears three color-1 gems
            var doubled = new ScoringConfig { BasePerGem = 20 };

            var outcome = CascadeResolver.Resolve(board, new Cell(2, 0), new Cell(2, 1), new UniqueGemSource(), doubled);

            Assert.IsTrue(outcome.Valid);
            Assert.AreEqual(60, outcome.TotalScore);                 // 3 gems * 20 * mult(1) — vs 30 by default
            Assert.AreEqual(60, outcome.Steps[0].GroupScores[0]);    // per-group score is carried, not recomputed
        }

        private static bool TryFindLegalSwap(Board b, out Cell a, out Cell c)
        {
            for (int x = 0; x < b.Cols; x++)
            {
                for (int y = 0; y < b.Rows; y++)
                {
                    if (x + 1 < b.Cols && Makes(b, new Cell(x, y), new Cell(x + 1, y))) { a = new Cell(x, y); c = new Cell(x + 1, y); return true; }
                    if (y + 1 < b.Rows && Makes(b, new Cell(x, y), new Cell(x, y + 1))) { a = new Cell(x, y); c = new Cell(x, y + 1); return true; }
                }
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
