using System.Collections.Generic;

namespace SlotGame.Core
{
    /// <summary>
    /// Picks an index from a list of integer weights.
    /// The probability of index i is weights[i] / sum(weights), which is how the
    /// machine's symbol frequency (and therefore its return-to-player) is configured.
    /// </summary>
    public static class WeightedPicker
    {
        public static int PickIndex(IRandomSource random, IReadOnlyList<int> weights)
        {
            if (weights == null || weights.Count == 0)
            {
                return 0;
            }

            int total = 0;
            for (int i = 0; i < weights.Count; i++)
            {
                if (weights[i] > 0)
                {
                    total += weights[i];
                }
            }

            if (total <= 0)
            {
                return 0;
            }

            int roll = random.Range(0, total);
            int cumulative = 0;
            for (int i = 0; i < weights.Count; i++)
            {
                int weight = weights[i];
                if (weight <= 0)
                {
                    continue;
                }

                cumulative += weight;
                if (roll < cumulative)
                {
                    return i;
                }
            }

            // Only reachable through integer overflow; fall back to the last weighted entry.
            for (int i = weights.Count - 1; i >= 0; i--)
            {
                if (weights[i] > 0)
                {
                    return i;
                }
            }

            return 0;
        }
    }
}
