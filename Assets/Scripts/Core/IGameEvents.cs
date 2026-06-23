namespace Proto.Core
{
    /// <summary>
    /// The core/view → juice seam. The presentation layer raises these semantic beats with correct animation
    /// timing, and the reusable juice services (camera-shake rig, post-fx) subscribe. Engine-free so it stays
    /// in the pure-core assembly; the concrete fan-out is <see cref="GameEvents"/>.
    /// </summary>
    public interface IGameEvents
    {
        /// <summary>A scoring beat at a world-ish point (drives a camera knock + a post-fx bloom swell that
        /// accumulates over a cascade). Raised once per cleared group by the view's cascade sequencer.</summary>
        void Collected(int points, Vec2 at);

        void Won(int finalScore);  // hook: win
        void Lost(int finalScore); // hook: lose
    }
}
