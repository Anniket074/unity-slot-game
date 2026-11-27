using System.Collections.Generic;
using SlotGame.Data;
using UnityEngine;

namespace SlotGame.Machine
{
    /// <summary>The full result of one spin: which lines paid, the total, and whether the board was a clean sweep.</summary>
    public readonly struct SpinOutcome
    {
        public SpinOutcome(IReadOnlyList<PaylineWin> lines, int totalWin, bool isMegaWin)
        {
            Lines = lines;
            TotalWin = totalWin;
            IsMegaWin = isMegaWin;
        }

        /// <summary>Winning paylines. When the whole board matches, all rows are listed for highlighting.</summary>
        public IReadOnlyList<PaylineWin> Lines { get; }

        public int TotalWin { get; }
        public bool IsMegaWin { get; }
    }

    /// <summary>
    /// The payout rules, in one place.
    ///
    /// Both <see cref="SlotMachine"/> and the RTP simulator call this, so the number
    /// quoted in the README is produced by exactly the code that pays the player.
    /// </summary>
    public static class SpinRules
    {
        public static SpinOutcome Evaluate(
            SymbolDefinition[,] grid,
            int reels,
            int rows,
            int totalBet,
            int megaWinMultiplier)
        {
            var lines = PaylineEvaluator.Evaluate(grid, reels, rows, LineBet(totalBet, rows));

            int lineTotal = 0;
            foreach (PaylineWin line in lines)
            {
                lineTotal += line.Payout;
            }

            if (!IsFullBoardMatch(grid, reels, rows))
            {
                return new SpinOutcome(lines, lineTotal, false);
            }

            // A clean sweep already pays three lines; the mega prize replaces that total
            // rather than stacking on top of it, so the RTP stays predictable.
            int megaPayout = Mathf.RoundToInt(megaWinMultiplier * LineBet(totalBet, rows));
            return new SpinOutcome(lines, Mathf.Max(lineTotal, megaPayout), true);
        }

        /// <summary>True when every cell on the board shows the same symbol.</summary>
        public static bool IsFullBoardMatch(SymbolDefinition[,] grid, int reels, int rows)
        {
            if (grid == null || reels <= 0 || rows <= 0)
            {
                return false;
            }

            SymbolDefinition first = grid[0, 0];
            if (first == null)
            {
                return false;
            }

            for (int reel = 0; reel < reels; reel++)
            {
                for (int row = 0; row < rows; row++)
                {
                    if (!ReferenceEquals(grid[reel, row], first))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        /// <summary>The stake is split evenly across the paylines.</summary>
        public static float LineBet(int totalBet, int rows)
        {
            return totalBet / (float)Mathf.Max(1, rows);
        }
    }
}
