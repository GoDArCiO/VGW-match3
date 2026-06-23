using System;
using System.Collections.Generic;

namespace Match3.Core
{
    public enum GameStatus { Playing, Won, Lost }

    /// <summary>
    /// The result of one attempted swap: whether it was legal, the full cascade to animate, and the
    /// committed session state afterward. The view replays <see cref="Steps"/>, counts the score up to
    /// <see cref="ScoreAfter"/>, and shows the win/lose banner for <see cref="StatusAfter"/> only after the
    /// last step has animated.
    /// </summary>
    public sealed class MoveResult
    {
        public bool Valid { get; }
        public IReadOnlyList<CascadeStep> Steps { get; }
        public int TotalScore { get; }
        public int MaxChain { get; }
        public int ScoreAfter { get; }
        public int GoalRemainingAfter { get; }
        public int MovesLeftAfter { get; }
        public GameStatus StatusAfter { get; }
        /// <summary>True if the board deadlocked after this move and was reshuffled in place (the view should
        /// re-sync its gem objects to <see cref="Match3Game.Board"/>).</summary>
        public bool Reshuffled { get; }

        public MoveResult(bool valid, IReadOnlyList<CascadeStep> steps, int totalScore, int maxChain,
                          int scoreAfter, int goalRemainingAfter, int movesLeftAfter,
                          GameStatus statusAfter, bool reshuffled)
        {
            Valid = valid;
            Steps = steps;
            TotalScore = totalScore;
            MaxChain = maxChain;
            ScoreAfter = scoreAfter;
            GoalRemainingAfter = goalRemainingAfter;
            MovesLeftAfter = movesLeftAfter;
            StatusAfter = statusAfter;
            Reshuffled = reshuffled;
        }

        public static readonly MoveResult Invalid =
            new MoveResult(false, Array.Empty<CascadeStep>(), 0, 0, 0, 0, 0, GameStatus.Playing, false);
    }

    /// <summary>
    /// The authoritative match-3 session: holds the board and the level's score / goal / moves, and resolves
    /// a swap into a committed <see cref="MoveResult"/>. Pure C# (no <c>UnityEngine</c>): a test plays whole
    /// levels headlessly and asserts on the returned results.
    ///
    /// Design: this is a SYNCHRONOUS planner. <see cref="TrySwap"/> resolves the entire cascade before
    /// judging win/lose — goal progress and score accrue across the whole cascade (including matches created
    /// by refill), then the goal is checked FIRST so a move that both meets the goal and exhausts moves is a
    /// Win. The view owns animation timing and "busy" state; the core never blocks.
    /// </summary>
    public sealed class Match3Game
    {
        private readonly LevelConfig _config;
        private readonly IGemSource _source;
        private readonly Random _rng;
        private readonly ScoringConfig _scoring;

        public Board Board { get; }
        public int Score { get; private set; }
        public int MovesLeft { get; private set; }
        public int GoalRemaining { get; private set; }
        public GameStatus Status { get; private set; } = GameStatus.Playing;

        public int GoalColorId => _config.GoalColorId;
        public int GoalCount => _config.GoalCount;
        public bool IsOver => Status != GameStatus.Playing;

        /// <summary>
        /// Constructs a session over a PRE-BUILT board (the testable seam — a test hands in a known
        /// <see cref="Board"/> and a scripted play source). <paramref name="rng"/> is used only for deadlock
        /// reshuffles; pass a seeded instance for reproducibility.
        /// </summary>
        public Match3Game(LevelConfig config, IGemSource playSource, Board board, Random rng = null, ScoringConfig scoring = null)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _source = playSource ?? throw new ArgumentNullException(nameof(playSource));
            Board = board ?? throw new ArgumentNullException(nameof(board));
            if (config.GoalCount < 1) throw new ArgumentOutOfRangeException(nameof(config), "GoalCount must be >= 1.");
            if (config.GoalColorId < 0 || config.GoalColorId >= config.ColorCount)
                throw new ArgumentOutOfRangeException(nameof(config), "GoalColorId out of color range.");
            if (config.MoveLimit < 1) throw new ArgumentOutOfRangeException(nameof(config), "MoveLimit must be >= 1.");

            _rng = rng ?? new Random();
            _scoring = scoring ?? ScoringConfig.Default; // remote-config / A-B can inject a different economy
            MovesLeft = config.MoveLimit;
            GoalRemaining = config.GoalCount;
        }

        /// <summary>Convenience factory that generates a fresh match-free board. <paramref name="genSource"/>
        /// MUST be a different instance from <paramref name="playSource"/> — generation consumes a
        /// data-dependent number of source values, which would otherwise desync scripted refills.</summary>
        public static Match3Game NewGame(LevelConfig config, IGemSource genSource, IGemSource playSource,
                                         Random rng = null, ScoringConfig scoring = null)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            var theRng = rng ?? new Random();
            var board = BoardGenerator.Generate(config.Cols, config.Rows, config.ColorCount, genSource);

            // Robustness: Generate almost always returns a board with a legal move, but its astronomically
            // rare last-resort fallback can return a dead one. Guarantee a playable start so the player is
            // never stuck before the first move (the in-TrySwap reshuffle only fires AFTER a legal move).
            if (!BoardGenerator.HasAnyLegalMove(board))
                BoardGenerator.Reshuffle(board, config.ColorCount, playSource, theRng);

            return new Match3Game(config, playSource, board, theRng, scoring);
        }

        /// <summary>
        /// Attempts to swap two cells. A no-op (returns <see cref="MoveResult.Invalid"/>, board and counters
        /// untouched) when the session is already over, or the swap is illegal (non-adjacent / forms no
        /// match). A legal swap consumes one move, banks the cascade score, advances the goal, resolves
        /// win/lose, and reshuffles if the resulting board is deadlocked.
        /// </summary>
        public MoveResult TrySwap(Cell a, Cell b)
        {
            if (Status != GameStatus.Playing) return MoveResult.Invalid;

            var outcome = CascadeResolver.Resolve(Board, a, b, _source, _scoring);
            if (!outcome.Valid) return MoveResult.Invalid; // illegal swap consumes no move

            int goalDelta = CountGoalColor(outcome.Steps);

            Score += outcome.TotalScore;
            GoalRemaining = Math.Max(0, GoalRemaining - goalDelta);
            MovesLeft -= 1;

            if (GoalRemaining == 0) Status = GameStatus.Won;       // goal is checked first
            else if (MovesLeft <= 0) Status = GameStatus.Lost;

            bool reshuffled = false;
            if (Status == GameStatus.Playing && !BoardGenerator.HasAnyLegalMove(Board))
            {
                BoardGenerator.Reshuffle(Board, _config.ColorCount, _source, _rng);
                reshuffled = true;
            }

            return new MoveResult(true, outcome.Steps, outcome.TotalScore, outcome.MaxChain,
                                  Score, GoalRemaining, MovesLeft, Status, reshuffled);
        }

        /// <summary>Goal-color cells cleared across the ENTIRE cascade (including refill-created matches in
        /// later steps). Group sizes sum to the cleared count because groups are disjoint.</summary>
        private int CountGoalColor(IReadOnlyList<CascadeStep> steps)
        {
            int count = 0;
            for (int s = 0; s < steps.Count; s++)
            {
                var groups = steps[s].Cleared;
                for (int g = 0; g < groups.Count; g++)
                    if (groups[g].Color == _config.GoalColorId)
                        count += groups[g].Size;
            }
            return count;
        }
    }
}
