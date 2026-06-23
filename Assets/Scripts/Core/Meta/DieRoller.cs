using System;

namespace Match3.Core
{
    /// <summary>Rolls a die face in <c>[1, Faces]</c> — the number of tiles a spent die advances the token.
    /// Abstracted so tests can script exact landings.</summary>
    public interface IDieRoller
    {
        int Roll();
    }

    /// <summary>Runtime die: a seeded <see cref="System.Random"/> over <c>[1, faces]</c>. Engine-free.</summary>
    public sealed class RandomDieRoller : IDieRoller
    {
        private readonly System.Random _rng;
        private readonly int _faces;

        public RandomDieRoller(System.Random rng, int faces = 6)
        {
            _rng = rng ?? throw new ArgumentNullException(nameof(rng));
            if (faces < 1) throw new ArgumentOutOfRangeException(nameof(faces));
            _faces = faces;
        }

        public RandomDieRoller(int seed, int faces = 6) : this(new System.Random(seed), faces) { }

        public int Roll() => _rng.Next(1, _faces + 1);
    }
}
