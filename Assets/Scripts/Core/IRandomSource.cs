namespace SlotGame.Core
{
    /// <summary>
    /// Minimal random-number abstraction used by every random decision in the game.
    /// Depending on an interface (instead of <see cref="UnityEngine.Random"/> or
    /// <see cref="System.Random"/> directly) lets the reels and the payout table be
    /// driven by a deterministic, seeded source in tests while still using real
    /// entropy at runtime.
    /// </summary>
    public interface IRandomSource
    {
        /// <summary>Returns a value in the half-open range [minInclusive, maxExclusive).</summary>
        int Range(int minInclusive, int maxExclusive);
    }
}
