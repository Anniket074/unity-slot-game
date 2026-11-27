using System.Collections.Generic;
using SlotGame.Data;
using UnityEngine;

namespace SlotGame.Machine
{
    /// <summary>A single winning payline: which row won, with what symbol, for how much.</summary>
    public readonly struct PaylineWin
    {
        public PaylineWin(int row, SymbolDefinition symbol, int payout)
        {
            Row = row;
            Symbol = symbol;
            Payout = payout;
        }

        public int Row { get; }
        public SymbolDefinition Symbol { get; }
        public int Payout { get; }
    }

    /// <summary>
    /// Evaluates a landing grid against the game's win rule:
    /// a payline wins when every reel shows the same symbol on that row.
    /// </summary>
    public static class PaylineEvaluator
    {
        public static List<PaylineWin> Evaluate(
            SymbolDefinition[,] grid,
            int reels,
            int rows,
            float lineBet)
        {
            var wins = new List<PaylineWin>();
            if (grid == null)
            {
                return wins;
            }

            for (int row = 0; row < rows; row++)
            {
                SymbolDefinition first = grid[0, row];
                if (first == null)
                {
                    continue;
                }

                bool allMatch = true;
                for (int reel = 1; reel < reels; reel++)
                {
                    if (!ReferenceEquals(grid[reel, row], first))
                    {
                        allMatch = false;
                        break;
                    }
                }

                if (!allMatch)
                {
                    continue;
                }

                int payout = Mathf.RoundToInt(first.PayoutMultiplier * lineBet);
                if (payout > 0)
                {
                    wins.Add(new PaylineWin(row, first, payout));
                }
            }

            return wins;
        }
    }
}
