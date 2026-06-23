using System;

namespace Match3.Core
{
    /// <summary>
    /// An immutable grid coordinate. <c>X</c> is the column, <c>Y</c> the row, with <c>Y == 0</c> the
    /// BOTTOM row (gravity pulls toward 0; refilled gems enter from above the top row). Engine-free so the
    /// whole board model stays unit-testable without Unity.
    /// </summary>
    public readonly struct Cell : IEquatable<Cell>
    {
        public readonly int X;
        public readonly int Y;

        public Cell(int x, int y)
        {
            X = x;
            Y = y;
        }

        /// <summary>True when <paramref name="other"/> is exactly one orthogonal step away (the only legal
        /// swap relationship — diagonals and self never count).</summary>
        public bool IsAdjacent(Cell other)
        {
            int dx = Math.Abs(X - other.X);
            int dy = Math.Abs(Y - other.Y);
            return dx + dy == 1;
        }

        public bool Equals(Cell other) => X == other.X && Y == other.Y;
        public override bool Equals(object obj) => obj is Cell c && Equals(c);
        public override int GetHashCode() => HashCode.Combine(X, Y);
        public static bool operator ==(Cell a, Cell b) => a.Equals(b);
        public static bool operator !=(Cell a, Cell b) => !a.Equals(b);
        public override string ToString() => $"({X},{Y})";
    }
}
