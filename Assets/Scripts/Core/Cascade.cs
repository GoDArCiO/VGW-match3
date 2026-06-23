using System;
using System.Collections.Generic;

namespace Match3.Core
{
    /// <summary>A surviving gem sliding straight down under gravity, from <see cref="From"/> to <see cref="To"/>
    /// (same column, <c>To.Y &lt; From.Y</c>). The view tweens the existing gem object to its new cell.</summary>
    public readonly struct Movement
    {
        public readonly Cell From;
        public readonly Cell To;
        public readonly int Color;

        public Movement(Cell from, Cell to, int color)
        {
            From = from;
            To = to;
            Color = color;
        }
    }

    /// <summary>A freshly refilled gem. It LANDS at <see cref="To"/> (a real cell) and ENTERS from
    /// <see cref="FromRowVirtual"/>, an off-board row <c>&gt;= Rows</c> directly above the column. Within a
    /// column every spawn shares the same fall distance (<c>FromRowVirtual - To.Y</c> is constant), so the
    /// view can drop them all at one speed with no overlap.</summary>
    public readonly struct Spawn
    {
        public readonly Cell To;
        public readonly int Color;
        public readonly int FromRowVirtual;

        public Spawn(Cell to, int color, int fromRowVirtual)
        {
            To = to;
            Color = color;
            FromRowVirtual = fromRowVirtual;
        }
    }

    /// <summary>
    /// One link of a cascade: a clear, then the gravity slides it caused, then the refills that topped the
    /// column back up. The view animates a step as THREE ordered phases and must finish each before the next:
    /// <list type="number">
    ///   <item>clear every cell in <see cref="Cleared"/> (coordinates are PRE-gravity),</item>
    ///   <item>tween every <see cref="Movement"/> (targets are POST-gravity),</item>
    ///   <item>drop every <see cref="Spawn"/> from its off-board row to its landing cell.</item>
    /// </list>
    /// Steps are replayed strictly in order; the view keeps its own cell→gem-object map and mutates it per
    /// phase (destroy cleared, reassign moved, instantiate spawned). <see cref="Movements"/> and
    /// <see cref="Spawns"/> are never null (empty arrays for an untouched column).
    /// </summary>
    public sealed class CascadeStep
    {
        /// <summary>1-based cascade depth: 1 = the swap's own match, 2 = the match its fallout created, ...</summary>
        public int Chain { get; }
        public IReadOnlyList<MatchGroup> Cleared { get; }
        /// <summary>Score awarded per cleared group, parallel to <see cref="Cleared"/> (the cascade
        /// multiplier already applied). Computed once in the core so the view can render floating numbers
        /// without re-deriving the scoring formula; sums to <see cref="StepScore"/>.</summary>
        public IReadOnlyList<int> GroupScores { get; }
        /// <summary>Total cells cleared this step == sum of group sizes == size of the matched set.</summary>
        public int ClearedCount { get; }
        public IReadOnlyList<Movement> Movements { get; }
        public IReadOnlyList<Spawn> Spawns { get; }
        public int StepScore { get; }
        public int Multiplier { get; }

        public CascadeStep(int chain, IReadOnlyList<MatchGroup> cleared, int clearedCount,
                           IReadOnlyList<Movement> movements, IReadOnlyList<Spawn> spawns,
                           int stepScore, int multiplier, IReadOnlyList<int> groupScores)
        {
            Chain = chain;
            Cleared = cleared;
            ClearedCount = clearedCount;
            Movements = movements;
            Spawns = spawns;
            StepScore = stepScore;
            Multiplier = multiplier;
            GroupScores = groupScores;
        }
    }

    /// <summary>
    /// The full, ordered result of resolving one swap to a stable board. <see cref="Valid"/> is false for an
    /// illegal swap (non-adjacent, or it created no match) and the board is left unchanged. Otherwise
    /// <see cref="Steps"/> is the cascade to animate, and the invariant
    /// <c>MaxChain == Steps.Count</c> with <c>Steps[i].Chain == i + 1</c> holds.
    /// </summary>
    public sealed class ResolveOutcome
    {
        public bool Valid { get; }
        public IReadOnlyList<CascadeStep> Steps { get; }
        public int TotalScore { get; }
        public int MaxChain { get; }

        public ResolveOutcome(bool valid, IReadOnlyList<CascadeStep> steps, int totalScore, int maxChain)
        {
            Valid = valid;
            Steps = steps;
            TotalScore = totalScore;
            MaxChain = maxChain;
        }

        public static readonly ResolveOutcome Invalid =
            new ResolveOutcome(false, Array.Empty<CascadeStep>(), 0, 0);
    }
}
