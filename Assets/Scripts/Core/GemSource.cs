using System;

namespace Match3.Core
{
    /// <summary>
    /// Supplies the next gem color id for a column during board generation and refill. Abstracting this is
    /// what makes cascades deterministic and unit-testable: the runtime injects a seeded RNG; a test injects
    /// a scripted, per-column queue so it can author the exact gems a refill drops and thereby force a
    /// 2-step cascade.
    ///
    /// REFILL CONTRACT (relied on by tests): the resolver refills column-major (x ascending) and, within a
    /// column, bottom row first (y ascending), calling <see cref="Next"/> once per empty cell. A scripted
    /// source therefore keys its queues by column.
    /// </summary>
    public interface IGemSource
    {
        /// <summary>Next color id in <c>[0, colorCount)</c> for the given column.</summary>
        int Next(int column);
    }

    /// <summary>
    /// Runtime <see cref="IGemSource"/>: a seeded <see cref="System.Random"/> over <c>[0, colorCount)</c>.
    /// Column-agnostic. Seeding keeps a run reproducible (handy for a "daily board" or a deterministic
    /// replay) while still feeling random. Uses <c>System.Random</c>, never <c>UnityEngine.Random</c>, so
    /// it stays engine-free.
    /// </summary>
    public sealed class SeededGemSource : IGemSource
    {
        private readonly System.Random _rng;
        private readonly int _colorCount;

        public SeededGemSource(int colorCount, int seed)
            : this(colorCount, new System.Random(seed)) { }

        public SeededGemSource(int colorCount, System.Random rng)
        {
            if (colorCount <= 0) throw new ArgumentOutOfRangeException(nameof(colorCount));
            _colorCount = colorCount;
            _rng = rng ?? throw new ArgumentNullException(nameof(rng));
        }

        public int Next(int column) => _rng.Next(_colorCount);
    }
}
