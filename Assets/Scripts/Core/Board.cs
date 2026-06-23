using System;
using System.Text;

namespace Match3.Core
{
    /// <summary>
    /// The mutable grid of gem color ids — the single source of truth for board state. A cell holds a color
    /// id in <c>[0, colorCount)</c> or <see cref="Empty"/> while a cascade is mid-resolve. Pure C# (no
    /// <c>UnityEngine</c>): the view layer renders it and never mutates grid math itself.
    ///
    /// Coordinate convention: <c>cells[x, y]</c> with <c>x</c> = column, <c>y</c> = row, <c>y = 0</c> the
    /// bottom row. See <see cref="Cell"/>.
    /// </summary>
    public sealed class Board
    {
        /// <summary>Sentinel color id for a cell with no gem (only ever present transiently during a resolve,
        /// between clear and refill). A settled board has no <see cref="Empty"/> cells.</summary>
        public const int Empty = -1;

        private readonly int[,] _cells; // [x, y]

        public int Cols { get; }
        public int Rows { get; }

        /// <summary>An all-<see cref="Empty"/> board of the given size.</summary>
        public Board(int cols, int rows)
        {
            if (cols <= 0) throw new ArgumentOutOfRangeException(nameof(cols));
            if (rows <= 0) throw new ArgumentOutOfRangeException(nameof(rows));
            Cols = cols;
            Rows = rows;
            _cells = new int[cols, rows];
            for (int x = 0; x < cols; x++)
                for (int y = 0; y < rows; y++)
                    _cells[x, y] = Empty;
        }

        /// <summary>Wraps an existing <c>[x, y]</c> grid (copied, so the caller can't mutate behind our back).</summary>
        public Board(int[,] cells)
        {
            if (cells == null) throw new ArgumentNullException(nameof(cells));
            Cols = cells.GetLength(0);
            Rows = cells.GetLength(1);
            _cells = (int[,])cells.Clone();
        }

        /// <summary>
        /// Builds a board from human-readable rows given TOP-to-BOTTOM (natural reading order), each
        /// character a single-digit color id (<c>'0'..'9'</c>) or <c>'.'</c> for <see cref="Empty"/>.
        /// The last string is row <c>y = 0</c>. Makes EditMode tests legible, e.g.:
        /// <code>Board.FromRows("001", "221", "111")</code>
        /// </summary>
        public static Board FromRows(params string[] rowsTopToBottom)
        {
            if (rowsTopToBottom == null || rowsTopToBottom.Length == 0)
                throw new ArgumentException("Need at least one row.", nameof(rowsTopToBottom));

            int rows = rowsTopToBottom.Length;
            int cols = rowsTopToBottom[0].Length;
            var board = new Board(cols, rows);

            for (int r = 0; r < rows; r++)
            {
                string line = rowsTopToBottom[r];
                if (line.Length != cols)
                    throw new ArgumentException($"Row {r} has length {line.Length}, expected {cols}.");

                int y = rows - 1 - r; // top string is the highest row
                for (int x = 0; x < cols; x++)
                {
                    char ch = line[x];
                    board._cells[x, y] = ch == '.' ? Empty : ch - '0';
                }
            }
            return board;
        }

        public int Get(int x, int y) => _cells[x, y];
        public int Get(Cell c) => _cells[c.X, c.Y];
        public void Set(int x, int y, int color) => _cells[x, y] = color;
        public void Set(Cell c, int color) => _cells[c.X, c.Y] = color;

        public bool InBounds(int x, int y) => x >= 0 && x < Cols && y >= 0 && y < Rows;
        public bool InBounds(Cell c) => InBounds(c.X, c.Y);

        /// <summary>Swaps the contents of two cells in place (used by the resolver to apply a player swap and
        /// to revert an illegal one).</summary>
        public void Swap(Cell a, Cell b)
        {
            int tmp = _cells[a.X, a.Y];
            _cells[a.X, a.Y] = _cells[b.X, b.Y];
            _cells[b.X, b.Y] = tmp;
        }

        public Board Clone() => new Board(_cells);

        public bool HasEmpty()
        {
            for (int x = 0; x < Cols; x++)
                for (int y = 0; y < Rows; y++)
                    if (_cells[x, y] == Empty) return true;
            return false;
        }

        /// <summary>Top-to-bottom dump (matching <see cref="FromRows"/>) for test failure messages.</summary>
        public override string ToString()
        {
            var sb = new StringBuilder();
            for (int y = Rows - 1; y >= 0; y--)
            {
                for (int x = 0; x < Cols; x++)
                {
                    int c = _cells[x, y];
                    sb.Append(c == Empty ? '.' : (char)('0' + c));
                }
                if (y > 0) sb.Append('\n');
            }
            return sb.ToString();
        }
    }
}
