using System;
using System.Collections.Generic;
using SlotGame.Core;
using SlotGame.Data;

namespace SlotGame.Machine
{
    /// <summary>
    /// Decides the full result of a spin *before* any reel moves.
    ///
    /// This ordering is deliberate: the outcome is generated once, the reels are then
    /// told where to stop, and the payout table evaluates exactly the same 2D array the
    /// player sees. There is no code path where a reel could land on something the
    /// evaluator did not intend, which keeps the RNG and the visible result honest.
    /// </summary>
    public sealed class SpinOutcomeGenerator
    {
        private readonly IRandomSource _random;
        private readonly SymbolDefinition[] _symbols;
        private readonly int[] _weights;

        public SpinOutcomeGenerator(IRandomSource random, IReadOnlyList<SymbolDefinition> symbols)
        {
            if (symbols == null || symbols.Count == 0)
            {
                throw new ArgumentException("A spin needs at least one symbol definition.", nameof(symbols));
            }

            _random = random ?? throw new ArgumentNullException(nameof(random));
            _symbols = new SymbolDefinition[symbols.Count];
            _weights = new int[symbols.Count];

            for (int i = 0; i < symbols.Count; i++)
            {
                _symbols[i] = symbols[i];
                _weights[i] = symbols[i] != null ? symbols[i].Weight : 0;
            }
        }

        /// <summary>
        /// Generates a [reels, rows] grid. Column index is the reel, row index is the
        /// vertical position (0 = top row).
        /// </summary>
        public SymbolDefinition[,] Generate(int reels, int rows)
        {
            var grid = new SymbolDefinition[reels, rows];
            for (int reel = 0; reel < reels; reel++)
            {
                for (int row = 0; row < rows; row++)
                {
                    grid[reel, row] = NextSymbol();
                }
            }

            return grid;
        }

        /// <summary>A weighted random symbol, used to fill the strip above the outcome.</summary>
        public SymbolDefinition NextSymbol()
        {
            return _symbols[WeightedPicker.PickIndex(_random, _weights)];
        }

        /// <summary>A weighted random symbol that is guaranteed not to match <paramref name="avoid"/>.</summary>
        public SymbolDefinition NextSymbolExcluding(SymbolDefinition avoid)
        {
            for (int attempt = 0; attempt < 12; attempt++)
            {
                SymbolDefinition candidate = NextSymbol();
                if (!ReferenceEquals(candidate, avoid))
                {
                    return candidate;
                }
            }

            foreach (SymbolDefinition symbol in _symbols)
            {
                if (!ReferenceEquals(symbol, avoid))
                {
                    return symbol;
                }
            }

            return avoid;
        }
    }
}
