using System.Linq;
using NUnit.Framework;
using Match3.Core;

namespace Proto.Tests
{
    public class GravityRefillTests
    {
        [Test]
        public void Gravity_CompactsScatteredHolesInAColumn()
        {
            // col0 top->bottom: 1, ., 2, ., 3  =>  bottom-up gems 3,2,1 compact to y0,y1,y2.
            var b = Board.FromRows("1", ".", "2", ".", "3");
            var moves = CascadeResolver.ApplyGravity(b);

            Assert.AreEqual(3, b.Get(0, 0));
            Assert.AreEqual(2, b.Get(0, 1));
            Assert.AreEqual(1, b.Get(0, 2));
            Assert.AreEqual(Board.Empty, b.Get(0, 3));
            Assert.AreEqual(Board.Empty, b.Get(0, 4));
            Assert.AreEqual(2, moves.Length); // the '3' was already at the bottom and didn't move
        }

        [Test]
        public void Gravity_PreservesVerticalOrder_AndOnlyMovesDown()
        {
            var b = Board.FromRows("1.", "2.", ".3");
            var moves = CascadeResolver.ApplyGravity(b);

            Assert.AreEqual(2, b.Get(0, 0)); // 2 was below 1, stays below
            Assert.AreEqual(1, b.Get(0, 1));
            Assert.AreEqual(3, b.Get(1, 0));
            foreach (var m in moves)
            {
                Assert.AreEqual(m.From.X, m.To.X, "Gravity never changes column.");
                Assert.Less(m.To.Y, m.From.Y, "Gravity only moves gems down.");
            }
        }

        [Test]
        public void Gravity_NoOpOnSettledBoard()
        {
            var b = Board.FromRows("12", "34");
            var before = b.ToString();
            var moves = CascadeResolver.ApplyGravity(b);
            Assert.IsEmpty(moves);
            Assert.AreEqual(before, b.ToString());
        }

        [Test]
        public void Refill_FillsExactlyTheEmpties_Deterministically_NoEmptyLeft()
        {
            // col0: one empty at top (y2). col1: two empties at top (y1, y2).
            var b = Board.FromRows("..", "1.", "23");
            var src = new ScriptedGemSource(new System.Collections.Generic.Dictionary<int, int[]>
            {
                { 0, new[] { 5 } },        // col0 y2
                { 1, new[] { 6, 7 } },     // col1 y1, then y2 (bottom-first)
            });

            var spawns = CascadeResolver.Refill(b, src);

            Assert.AreEqual(3, spawns.Length);
            Assert.IsFalse(b.HasEmpty());
            Assert.AreEqual(5, b.Get(0, 2));
            Assert.AreEqual(6, b.Get(1, 1));
            Assert.AreEqual(7, b.Get(1, 2));
        }

        [Test]
        public void Refill_FallDistanceIsConstantPerColumn_AndOffBoard()
        {
            var b = Board.FromRows("..", "1.", "23");
            var src = new ScriptedGemSource(new System.Collections.Generic.Dictionary<int, int[]>
            {
                { 0, new[] { 5 } },
                { 1, new[] { 6, 7 } },
            });

            var spawns = CascadeResolver.Refill(b, src);

            foreach (var grp in spawns.GroupBy(s => s.To.X))
            {
                var distances = grp.Select(s => s.FromRowVirtual - s.To.Y).Distinct().ToArray();
                Assert.AreEqual(1, distances.Length, "All spawns in a column share one fall distance.");
            }
            Assert.IsTrue(spawns.All(s => s.FromRowVirtual >= b.Rows), "Spawns enter from off-board rows.");
        }

        [Test]
        public void Refill_NoEmpties_ReturnsEmptyArray()
        {
            var b = Board.FromRows("12", "34");
            var spawns = CascadeResolver.Refill(b, new UniqueGemSource());
            Assert.IsEmpty(spawns);
        }

        [Test]
        public void Refill_DrawsColumnMajorBottomFirst()
        {
            var b = Board.FromRows("..", "1.", "23");
            var rec = new RecordingGemSource(new UniqueGemSource());
            CascadeResolver.Refill(b, rec);
            // col0 (one empty) then col1 (two empties).
            CollectionAssert.AreEqual(new[] { 0, 1, 1 }, rec.Calls);
        }
    }
}
