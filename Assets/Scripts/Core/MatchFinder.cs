using System.Collections.Generic;

namespace Match3.Core
{
    /// <summary>
    /// One cleared group of same-color gems — a maximal connected component of matched cells (4-neighbour,
    /// same color). A straight run is one group; an L/T/+ junction is ONE group (its union), not two
    /// overlapping runs, which keeps score and cleared-count in sync.
    /// </summary>
    public readonly struct MatchGroup
    {
        public readonly int Color;
        public readonly Cell[] Cells;

        public MatchGroup(int color, Cell[] cells)
        {
            Color = color;
            Cells = cells;
        }

        public int Size => Cells.Length;

        /// <summary>"Long" = worth a bonus. A non-straight (L/T/+) junction has Size &gt;= 5 by construction
        /// (each arm is an independent run of &gt;= 3 sharing &gt;= 1 cell), so <c>Size &gt;= 4</c> captures
        /// runs-of-4 and every junction with no separate shape detection.</summary>
        public bool IsLong => Cells.Length >= 4;
    }

    /// <summary>
    /// Pure match detection over a <see cref="Board"/>: horizontal and vertical runs of 3+ equal,
    /// non-empty gems. Stateless — no <c>UnityEngine</c>, fully unit-testable.
    /// </summary>
    public static class MatchFinder
    {
        /// <summary>
        /// Every cell that is part of a run of 3+ (horizontal or vertical), de-duplicated — a cell shared by
        /// a horizontal and a vertical run appears once. <see cref="Board.Empty"/> never matches (it breaks
        /// any run).
        /// </summary>
        public static HashSet<Cell> FindMatchedCells(Board board)
        {
            var matched = new HashSet<Cell>();

            // Horizontal: sweep each row with a sentinel one past the right edge so the final run flushes.
            for (int y = 0; y < board.Rows; y++)
            {
                int runStart = 0;
                for (int x = 1; x <= board.Cols; x++)
                {
                    bool continues = x < board.Cols
                                     && board.Get(x, y) != Board.Empty
                                     && board.Get(x, y) == board.Get(x - 1, y);
                    if (!continues)
                    {
                        int runLen = x - runStart;
                        // The `!= Empty` guard is defensive: a run of len>=3 can never START on Empty
                        // because `continues` requires non-empty equal neighbours.
                        if (runLen >= 3 && board.Get(runStart, y) != Board.Empty)
                            for (int k = runStart; k < x; k++) matched.Add(new Cell(k, y));
                        runStart = x;
                    }
                }
            }

            // Vertical: same sweep down each column.
            for (int x = 0; x < board.Cols; x++)
            {
                int runStart = 0;
                for (int y = 1; y <= board.Rows; y++)
                {
                    bool continues = y < board.Rows
                                     && board.Get(x, y) != Board.Empty
                                     && board.Get(x, y) == board.Get(x, y - 1);
                    if (!continues)
                    {
                        int runLen = y - runStart;
                        if (runLen >= 3 && board.Get(x, runStart) != Board.Empty)
                            for (int k = runStart; k < y; k++) matched.Add(new Cell(x, k));
                        runStart = y;
                    }
                }
            }

            return matched;
        }

        /// <summary>Cheap "is the board currently matched?" — used to validate a swap and to test legal moves.</summary>
        public static bool HasMatch(Board board) => FindMatchedCells(board).Count > 0;

        /// <summary>
        /// Partitions a matched set into connected components (4-neighbour, same color). Must be called while
        /// the board STILL holds the matched gems (before they are cleared) — it derives each group's color
        /// and adjacency from <c>board.Get</c>. The components are pairwise disjoint and their union is
        /// exactly <paramref name="matched"/>, so the group sizes sum to <c>matched.Count</c>.
        /// </summary>
        public static List<MatchGroup> GroupRuns(Board board, HashSet<Cell> matched)
        {
            var groups = new List<MatchGroup>();
            var visited = new HashSet<Cell>();
            var stack = new Stack<Cell>();

            foreach (var start in matched)
            {
                if (!visited.Add(start)) continue;
                int color = board.Get(start);
                var component = new List<Cell>();
                stack.Push(start);

                while (stack.Count > 0)
                {
                    var c = stack.Pop();
                    component.Add(c);
                    TryVisit(new Cell(c.X + 1, c.Y));
                    TryVisit(new Cell(c.X - 1, c.Y));
                    TryVisit(new Cell(c.X, c.Y + 1));
                    TryVisit(new Cell(c.X, c.Y - 1));
                }

                groups.Add(new MatchGroup(color, component.ToArray()));

                void TryVisit(Cell n)
                {
                    if (matched.Contains(n) && board.Get(n) == color && visited.Add(n))
                        stack.Push(n);
                }
            }

            return groups;
        }
    }
}
