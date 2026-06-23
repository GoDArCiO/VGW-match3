using System;

namespace Match3.Core
{
    /// <summary>
    /// Board setup and analysis: generate a fair starting board (no pre-existing matches, at least one legal
    /// move), test for a legal move, and reshuffle out of a deadlock. All pure C#.
    /// </summary>
    public static class BoardGenerator
    {
        private const int MaxAttempts = 64;

        /// <summary>
        /// A fresh board with NO matches already present and (almost surely) a legal move available. Each
        /// cell is drawn from <paramref name="source"/>, re-rolling any pick that would complete a run of 3
        /// with the two cells to its left or below. Needs <c>colorCount &gt;= 3</c> so a non-completing pick
        /// always exists (at most two colors are ever forbidden for a cell).
        /// </summary>
        public static Board Generate(int cols, int rows, int colorCount, IGemSource source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (colorCount < 3) throw new ArgumentOutOfRangeException(nameof(colorCount), "Need >= 3 colors to generate a match-free board.");

            for (int attempt = 0; attempt < MaxAttempts; attempt++)
            {
                var board = BuildMatchFree(cols, rows, colorCount, source);
                if (HasAnyLegalMove(board)) return board;
            }
            // Astronomically unlikely on a real board; return the last match-free board regardless — the
            // session will reshuffle on the first move if it truly has no move.
            return BuildMatchFree(cols, rows, colorCount, source);
        }

        private static Board BuildMatchFree(int cols, int rows, int colorCount, IGemSource source)
        {
            var board = new Board(cols, rows);
            for (int y = 0; y < rows; y++)
            {
                for (int x = 0; x < cols; x++)
                {
                    int color;
                    int tries = 0;
                    do { color = source.Next(x) % colorCount; tries++; }
                    while (CompletesRun(board, x, y, color) && tries < MaxAttempts);
                    board.Set(x, y, color);
                }
            }
            return board;
        }

        private static bool CompletesRun(Board board, int x, int y, int color)
        {
            bool horizontal = x >= 2 && board.Get(x - 1, y) == color && board.Get(x - 2, y) == color;
            bool vertical = y >= 2 && board.Get(x, y - 1) == color && board.Get(x, y - 2) == color;
            return horizontal || vertical;
        }

        /// <summary>True if any single adjacent swap would create a match. O(Cols*Rows) trial swaps, each
        /// restored — cheap for the small boards a match-3 slice uses.</summary>
        public static bool HasAnyLegalMove(Board board)
        {
            for (int x = 0; x < board.Cols; x++)
            {
                for (int y = 0; y < board.Rows; y++)
                {
                    if (x + 1 < board.Cols && SwapMakesMatch(board, new Cell(x, y), new Cell(x + 1, y))) return true;
                    if (y + 1 < board.Rows && SwapMakesMatch(board, new Cell(x, y), new Cell(x, y + 1))) return true;
                }
            }
            return false;
        }

        private static bool SwapMakesMatch(Board board, Cell a, Cell b)
        {
            board.Swap(a, b);
            bool match = MatchFinder.HasMatch(board);
            board.Swap(a, b);
            return match;
        }

        /// <summary>
        /// Re-lays the board out of a deadlock. Preserves the existing color multiset (a true shuffle),
        /// retrying until the layout has no pre-existing match AND a legal move. As a last-resort degenerate
        /// fallback (effectively never with &gt;= 4 colors) it regenerates a fresh board so the player always
        /// has a move. Returns true if the multiset-preserving shuffle succeeded, false if it fell back.
        /// </summary>
        public static bool Reshuffle(Board board, int colorCount, IGemSource source, Random rng)
        {
            if (rng == null) throw new ArgumentNullException(nameof(rng));

            int n = board.Cols * board.Rows;
            var colors = new int[n];
            int i = 0;
            for (int x = 0; x < board.Cols; x++)
                for (int y = 0; y < board.Rows; y++)
                    colors[i++] = board.Get(x, y);

            for (int attempt = 0; attempt < MaxAttempts; attempt++)
            {
                Shuffle(colors, rng);
                Lay(board, colors);
                if (!MatchFinder.HasMatch(board) && HasAnyLegalMove(board)) return true;
            }

            // Degenerate multiset: regenerate fresh so play can always continue.
            var fresh = Generate(board.Cols, board.Rows, colorCount, source);
            for (int x = 0; x < board.Cols; x++)
                for (int y = 0; y < board.Rows; y++)
                    board.Set(x, y, fresh.Get(x, y));
            return false;
        }

        private static void Lay(Board board, int[] colors)
        {
            int k = 0;
            for (int x = 0; x < board.Cols; x++)
                for (int y = 0; y < board.Rows; y++)
                    board.Set(x, y, colors[k++]);
        }

        private static void Shuffle(int[] a, Random rng)
        {
            for (int i = a.Length - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (a[i], a[j]) = (a[j], a[i]);
            }
        }
    }
}
