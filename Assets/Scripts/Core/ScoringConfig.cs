using System;

namespace Match3.Core
{
    /// <summary>
    /// The tunable scoring economy: base points per gem, the cascade-multiplier curve, and the long-match
    /// bonuses. Pure data + pure functions, injected into <see cref="CascadeResolver"/> via
    /// <see cref="Match3Game"/> (defaulting to <see cref="Default"/>).
    ///
    /// This is the LIVE-OPS seam. In a shipped title the values that move session length and conversion —
    /// score-per-gem, the combo curve, big-match bonuses — are exactly what gets A/B-tested. Because they
    /// live in an engine-free POCO behind an injection point (not as <c>const</c> in the resolver), a
    /// remote-config provider can hand a different instance to each experiment bucket WITHOUT a client
    /// release — the same data-driven pattern the level and meta economies already use. The defaults below
    /// reproduce the original baked values, so behaviour is unchanged until something overrides them.
    /// </summary>
    public sealed class ScoringConfig
    {
        /// <summary>Points per cleared gem, before bonuses and the cascade multiplier.</summary>
        public int BasePerGem = 10;

        /// <summary>Combo multiplier indexed by cascade depth (index = chain - 1), clamped to the last
        /// entry for deeper chains. Authored as data so the curve is a config value, not a code branch.</summary>
        public int[] Multipliers = { 1, 2, 3, 5, 8, 12 };

        /// <summary>Bonus for clearing a group of exactly 4.</summary>
        public int LongBonusFour = 30;

        /// <summary>Bonus for clearing a group of 5 or more.</summary>
        public int LongBonusFivePlus = 60;

        /// <summary>Shared default economy (the original baked values). A single instance avoids a
        /// per-resolve allocation; treat it as read-only.</summary>
        public static readonly ScoringConfig Default = new ScoringConfig();

        /// <summary>Combo multiplier for a 1-based cascade depth. Monotonic, always &gt;= 1, clamps to the
        /// last table entry for chains beyond the table.</summary>
        public int MultiplierFor(int chain)
        {
            if (chain < 1 || Multipliers == null || Multipliers.Length == 0) return 1;
            int i = Math.Min(chain - 1, Multipliers.Length - 1);
            return Multipliers[i];
        }

        /// <summary>Bonus on top of per-gem points for a longer group: <see cref="LongBonusFour"/> for 4,
        /// <see cref="LongBonusFivePlus"/> for 5+.</summary>
        public int LongBonus(int groupSize) =>
            groupSize >= 5 ? LongBonusFivePlus : groupSize == 4 ? LongBonusFour : 0;

        /// <summary>Score for one cleared group BEFORE the cascade multiplier is applied.</summary>
        public int GroupScore(int groupSize) => groupSize * BasePerGem + LongBonus(groupSize);
    }
}
