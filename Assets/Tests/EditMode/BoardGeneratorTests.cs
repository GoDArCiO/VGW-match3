using NUnit.Framework;
using Match3.Core;

namespace Proto.Tests
{
    public class BoardGeneratorTests
    {
        [Test]
        public void Generate_ProducesNoPreexistingMatch_AndAtLeastOneMove()
        {
            for (int seed = 1; seed <= 15; seed++)
            {
                var b = BoardGenerator.Generate(7, 8, 6, new SeededGemSource(6, seed));
                Assert.IsFalse(MatchFinder.HasMatch(b), $"seed {seed} generated a board with a pre-existing match.");
                Assert.IsTrue(BoardGenerator.HasAnyLegalMove(b), $"seed {seed} generated a board with no legal move.");
            }
        }

        [Test]
        public void HasAnyLegalMove_TrueWhenASwapWouldMatch()
        {
            // (2,0)<->(2,1) would complete the bottom row.
            Assert.IsTrue(BoardGenerator.HasAnyLegalMove(Board.FromRows("345", "671", "112")));
        }

        [Test]
        public void HasAnyLegalMove_FalseOnADeadlock()
        {
            // Single row of three distinct colors: no swap can ever make a run of three.
            Assert.IsFalse(BoardGenerator.HasAnyLegalMove(Board.FromRows("012")));
        }

        [Test]
        public void Reshuffle_YieldsAPlayableBoard()
        {
            var b = BoardGenerator.Generate(7, 8, 6, new SeededGemSource(6, 5));
            BoardGenerator.Reshuffle(b, 6, new SeededGemSource(6, 9), new System.Random(3));

            Assert.IsFalse(MatchFinder.HasMatch(b), "Reshuffled board must have no pre-existing match.");
            Assert.IsTrue(BoardGenerator.HasAnyLegalMove(b), "Reshuffled board must have a legal move.");
        }
    }
}
