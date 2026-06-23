using System;
using System.Collections.Generic;

namespace Match3.Core
{
    /// <summary>
    /// The heart of the game: a pure, static, deterministic planner. Given a board, a swap, and a gem
    /// source it returns the ENTIRE cascade (matches → gravity → refill → re-check, until stable) as an
    /// ordered <see cref="ResolveOutcome"/>. No <c>UnityEngine</c>, no timing, no animation — the view
    /// replays the returned steps over time. This is what makes the cascade unit-testable on a hand-built
    /// board with a scripted gem source.
    /// </summary>
    public static class CascadeResolver
    {
        /// <summary>Hard cap on cascade depth. Far above any humanly reachable chain on a real board, it
        /// exists purely to GUARANTEE termination under any <see cref="IGemSource"/> — including an
        /// adversarial/scripted one that keeps completing a match on every refill. Without it
        /// <see cref="Resolve"/> could spin forever (a hang is a zero-value slice).</summary>
        public const int MaxCascade = 64;

        /// <summary>
        /// Applies the swap and resolves the full cascade in place. Returns <see cref="ResolveOutcome.Invalid"/>
        /// (board unchanged) if the swap is non-adjacent / out of bounds, or if it forms no match. Otherwise
        /// the board is left in its settled post-cascade state and every step is recorded for the view.
        /// </summary>
        public static ResolveOutcome Resolve(Board board, Cell a, Cell b, IGemSource source, ScoringConfig scoring = null)
        {
            if (board == null) throw new ArgumentNullException(nameof(board));
            if (source == null) throw new ArgumentNullException(nameof(source));
            scoring ??= ScoringConfig.Default;

            if (!board.InBounds(a) || !board.InBounds(b) || !a.IsAdjacent(b))
                return ResolveOutcome.Invalid;

            board.Swap(a, b);
            if (!MatchFinder.HasMatch(board))
            {
                board.Swap(a, b); // revert: an illegal swap leaves the board (and the move count) untouched
                return ResolveOutcome.Invalid;
            }

            var steps = new List<CascadeStep>();
            int chain = 0;
            int total = 0;

            // chain++ and steps.Add are paired exactly once per iteration, so the invariant
            // MaxChain == steps.Count and steps[i].Chain == i+1 always holds. The cap is checked here
            // (before chain++) so a capped resolve never records a half-built step.
            while (chain < MaxCascade)
            {
                var matched = MatchFinder.FindMatchedCells(board);
                if (matched.Count == 0) break;

                chain++;
                var groups = MatchFinder.GroupRuns(board, matched); // read colors BEFORE clearing
                int multiplier = scoring.MultiplierFor(chain);

                // Per-group scores are computed here, in the core, and carried on the step so the view never
                // re-derives the scoring formula — one source of truth, safe to remote-config / A-B tune.
                var groupScores = new int[groups.Count];
                int stepScore = 0;
                for (int g = 0; g < groups.Count; g++)
                {
                    groupScores[g] = scoring.GroupScore(groups[g].Size) * multiplier;
                    stepScore += groupScores[g];
                }

                foreach (var cell in matched) board.Set(cell, Board.Empty);   // 1. clear
                var movements = ApplyGravity(board);                          // 2. gravity
                var spawns = Refill(board, source);                           // 3. refill

                steps.Add(new CascadeStep(chain, groups, matched.Count, movements, spawns, stepScore, multiplier, groupScores));
                total += stepScore;
            }

            return new ResolveOutcome(true, steps, total, chain);
        }

        /// <summary>
        /// Compacts every column downward (a single bottom-to-top pass per column), recording a
        /// <see cref="Movement"/> for each gem that actually moved. After this, all empties in a column are
        /// the top contiguous block, which is what <see cref="Refill"/> relies on. Invariant: the write
        /// slot always trails the read row, so writing never clobbers an unread gem; gems only move down.
        /// </summary>
        public static Movement[] ApplyGravity(Board board)
        {
            var moves = new List<Movement>();
            for (int x = 0; x < board.Cols; x++)
            {
                int write = 0; // next slot to fill from the bottom
                for (int y = 0; y < board.Rows; y++)
                {
                    int color = board.Get(x, y);
                    if (color == Board.Empty) continue;

                    if (write != y)
                    {
                        board.Set(x, write, color);
                        board.Set(x, y, Board.Empty);
                        moves.Add(new Movement(new Cell(x, y), new Cell(x, write), color));
                    }
                    write++;
                }
            }
            return moves.ToArray();
        }

        /// <summary>
        /// Fills every <see cref="Board.Empty"/> cell with a fresh gem from <paramref name="source"/>.
        /// Order (the test contract): column-major (x ascending), bottom row first (y ascending), one
        /// <c>source.Next(x)</c> per empty. New gems enter from off-board rows <c>&gt;= Rows</c>, stacked so
        /// each column's spawns share a constant fall distance.
        /// </summary>
        public static Spawn[] Refill(Board board, IGemSource source)
        {
            var spawns = new List<Spawn>();
            for (int x = 0; x < board.Cols; x++)
            {
                int virtualRow = board.Rows; // first new gem sits just above the visible top row
                for (int y = 0; y < board.Rows; y++)
                {
                    if (board.Get(x, y) != Board.Empty) continue;

                    int color = source.Next(x);
                    board.Set(x, y, color);
                    spawns.Add(new Spawn(new Cell(x, y), color, virtualRow));
                    virtualRow++;
                }
            }
            return spawns.ToArray();
        }

    }
}
