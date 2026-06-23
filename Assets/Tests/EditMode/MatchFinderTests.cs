using System.Linq;
using NUnit.Framework;
using Match3.Core;

namespace Proto.Tests
{
    /// <summary>
    /// Match detection on hand-built boards (read top-to-bottom via <see cref="Board.FromRows"/>, digits are
    /// color ids, '.' is empty). Pure logic, no Unity.
    /// </summary>
    public class MatchFinderTests
    {
        private static int GroupSizeOfColor(System.Collections.Generic.List<MatchGroup> groups, int color)
            => groups.Where(g => g.Color == color).Sum(g => g.Size);

        [Test]
        public void DetectsHorizontalRunOfThree()
        {
            var b = Board.FromRows(
                "012",
                "345",
                "111");
            var m = MatchFinder.FindMatchedCells(b);
            Assert.AreEqual(3, m.Count);
            Assert.IsTrue(m.Contains(new Cell(0, 0)) && m.Contains(new Cell(1, 0)) && m.Contains(new Cell(2, 0)));
        }

        [Test]
        public void DetectsVerticalRunOfThree()
        {
            var b = Board.FromRows(
                "200",
                "234",
                "256");
            var m = MatchFinder.FindMatchedCells(b);
            Assert.AreEqual(3, m.Count);
            Assert.IsTrue(m.Contains(new Cell(0, 0)) && m.Contains(new Cell(0, 1)) && m.Contains(new Cell(0, 2)));
        }

        [Test]
        public void NoMatchOnAStableBoard()
        {
            var b = Board.FromRows(
                "012",
                "120",
                "201");
            Assert.IsEmpty(MatchFinder.FindMatchedCells(b));
            Assert.IsFalse(MatchFinder.HasMatch(b));
        }

        [Test]
        public void RunOfTwoNeverMatches()
        {
            var b = Board.FromRows(
                "1123",
                "4567");
            Assert.IsEmpty(MatchFinder.FindMatchedCells(b));
        }

        [Test]
        public void RunOfFour_IsOneLongGroup()
        {
            var b = Board.FromRows(
                "1111",
                "2345");
            var m = MatchFinder.FindMatchedCells(b);
            Assert.AreEqual(4, m.Count);
            var groups = MatchFinder.GroupRuns(b, m);
            Assert.AreEqual(1, groups.Count);
            Assert.AreEqual(4, groups[0].Size);
            Assert.IsTrue(groups[0].IsLong);
        }

        [Test]
        public void RunOfFive_IsOneLongGroup()
        {
            var b = Board.FromRows(
                "11111",
                "23456");
            var groups = MatchFinder.GroupRuns(b, MatchFinder.FindMatchedCells(b));
            Assert.AreEqual(1, groups.Count);
            Assert.AreEqual(5, groups[0].Size);
            Assert.IsTrue(groups[0].IsLong);
        }

        [Test]
        public void LShape_IsExactlyOneGroupOfFive()
        {
            // Vertical arm (col0) + horizontal arm (row0) of color 1, sharing corner (0,0).
            var b = Board.FromRows(
                "122",
                "133",
                "111");
            var m = MatchFinder.FindMatchedCells(b);
            Assert.AreEqual(5, m.Count);
            var groups = MatchFinder.GroupRuns(b, m);
            Assert.AreEqual(1, groups.Count, "L-shape must be ONE connected component, not two overlapping runs.");
            Assert.AreEqual(5, groups[0].Size);
            Assert.AreEqual(1, groups[0].Color);
            Assert.AreEqual(m.Count, groups.Sum(g => g.Size), "Groups must partition the matched set (disjoint).");
        }

        [Test]
        public void StackedParallelRuns_MergeIntoOneGroupOfSix()
        {
            // Two horizontal runs of color 1, vertically adjacent -> one 2x3 connected block.
            var b = Board.FromRows(
                "242",
                "111",
                "111",
                "353");
            var m = MatchFinder.FindMatchedCells(b);
            Assert.AreEqual(6, m.Count);
            var groups = MatchFinder.GroupRuns(b, m);
            Assert.AreEqual(1, groups.Count, "Parallel adjacent same-color runs must MERGE via cell-level CC.");
            Assert.AreEqual(6, groups[0].Size);
        }

        [Test]
        public void TwoRunsSeparatedByAGap_AreTwoGroups()
        {
            var b = Board.FromRows("1110111"); // single row, gap at col3
            var m = MatchFinder.FindMatchedCells(b);
            Assert.AreEqual(6, m.Count);
            var groups = MatchFinder.GroupRuns(b, m);
            Assert.AreEqual(2, groups.Count);
            Assert.IsTrue(groups.All(g => g.Size == 3 && g.Color == 1));
        }

        [Test]
        public void EmptyCellsBreakRunsAndNeverMatch()
        {
            var b = Board.FromRows("111.1"); // first three match; the '.' breaks the rest
            var m = MatchFinder.FindMatchedCells(b);
            Assert.AreEqual(3, m.Count);
            Assert.IsTrue(m.Contains(new Cell(0, 0)) && m.Contains(new Cell(2, 0)));
            Assert.IsFalse(m.Contains(new Cell(4, 0)));

            Assert.IsEmpty(MatchFinder.FindMatchedCells(Board.FromRows("...", "...")));
        }

        [Test]
        public void TwoDisjointColorRuns_AreSeparateGroups()
        {
            // col0 vertical of 2s, row0 horizontal of 1s (touching but different color => not merged).
            var b = Board.FromRows(
                "2345",
                "2367",
                "2111");
            var m = MatchFinder.FindMatchedCells(b);
            Assert.AreEqual(6, m.Count);
            var groups = MatchFinder.GroupRuns(b, m);
            Assert.AreEqual(2, groups.Count);
            Assert.AreEqual(3, GroupSizeOfColor(groups, 2));
            Assert.AreEqual(3, GroupSizeOfColor(groups, 1));
        }
    }
}
