using System;

namespace Match3.Core
{
    /// <summary>
    /// The looped meta board: a ring of <see cref="MetaTile"/>s the token hops around. Pure C#; built from a
    /// plain <see cref="MetaTile"/> array (a <c>MetaBoardLayout</c> ScriptableObject converts to this, so the
    /// Core never references the engine).
    /// </summary>
    public sealed class MetaBoard
    {
        private readonly MetaTile[] _tiles;

        public int Count => _tiles.Length;

        public MetaBoard(MetaTile[] tiles)
        {
            if (tiles == null) throw new ArgumentNullException(nameof(tiles));
            if (tiles.Length == 0) throw new ArgumentException("Meta board needs at least one tile.", nameof(tiles));
            _tiles = (MetaTile[])tiles.Clone();
        }

        public MetaTile TileAt(int index) => _tiles[Mod(index, Count)];

        /// <summary>
        /// Advances <paramref name="steps"/> tiles (wrapping the loop) from <paramref name="from"/> and
        /// returns the destination index and the tile landed on (never the start tile). <paramref name="steps"/>
        /// must be &gt;= 1.
        /// </summary>
        public (int newIndex, MetaTile landed) Step(int from, int steps)
        {
            if (steps < 1) throw new ArgumentOutOfRangeException(nameof(steps), "Must advance at least one tile.");
            int index = Mod(from + steps, Count);
            return (index, _tiles[index]);
        }

        private static int Mod(int a, int n) => ((a % n) + n) % n;
    }
}
