using System;

namespace SlotGame.Core
{
    /// <summary>
    /// Production <see cref="IRandomSource"/> backed by <see cref="System.Random"/>.
    /// A seed can be supplied to reproduce a session exactly (handy when a tester
    /// reports a suspicious spin).
    /// </summary>
    public sealed class SystemRandomSource : IRandomSource
    {
        private readonly Random _random;

        public SystemRandomSource()
            : this(Environment.TickCount ^ Guid.NewGuid().GetHashCode())
        {
        }

        public SystemRandomSource(int seed)
        {
            _random = new Random(seed);
        }

        public int Range(int minInclusive, int maxExclusive)
        {
            return maxExclusive <= minInclusive ? minInclusive : _random.Next(minInclusive, maxExclusive);
        }
    }
}
