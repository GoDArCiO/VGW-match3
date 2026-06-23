namespace Match3.Core
{
    /// <summary>What a meta-board tile grants when the token lands on it.</summary>
    public enum RewardKind
    {
        Nothing,
        Coins,
        Star,
        Dice
    }

    /// <summary>One tile of the looped meta board (Monopoly-Match style): a reward kind and amount.</summary>
    public readonly struct MetaTile
    {
        public readonly RewardKind Kind;
        public readonly int Amount;

        public MetaTile(RewardKind kind, int amount)
        {
            Kind = kind;
            Amount = amount;
        }
    }
}
