using System;

namespace Proto.Core
{
    /// <summary>Default <see cref="IGameEvents"/>: fans each juice beat out to C# events the view-side
    /// services subscribe to. Interface methods are explicitly implemented so only the holder of the
    /// <see cref="IGameEvents"/> reference (the view's cascade sequencer) can raise them.</summary>
    public sealed class GameEvents : IGameEvents
    {
        public event Action<int, Vec2> OnCollected;
        public event Action<int> OnWon;
        public event Action<int> OnLost;

        void IGameEvents.Collected(int points, Vec2 at) => OnCollected?.Invoke(points, at);
        void IGameEvents.Won(int finalScore) => OnWon?.Invoke(finalScore);
        void IGameEvents.Lost(int finalScore) => OnLost?.Invoke(finalScore);
    }
}
