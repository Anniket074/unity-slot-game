using System.Collections.Generic;
using UnityEngine;

namespace SlotGame.Data
{
    /// <summary>
    /// Single source of truth for machine layout, economy and timing.
    /// Everything a designer may want to tweak lives here rather than in code.
    /// </summary>
    [CreateAssetMenu(fileName = "GameConfig", menuName = "Slot Game/Game Config", order = 1)]
    public sealed class GameConfig : ScriptableObject
    {
        [Header("Symbols")]
        [SerializeField] private SymbolDefinition[] symbols = new SymbolDefinition[0];

        [Header("Machine layout")]
        [SerializeField, Min(1)] private int reels = 3;
        [SerializeField, Min(1)] private int rows = 3;

        [Tooltip("Random symbols placed above the outcome on each reel so the strip scrolls past a believable amount of content.")]
        [SerializeField, Min(3)] private int fillerSymbolsPerReel = 12;

        [Header("Economy")]
        [SerializeField, Min(1)] private int startingCredits = 500;
        [SerializeField] private int[] betLevels = { 10, 50, 100 };
        [SerializeField, Min(0)] private int maxGambles = 3;
        [SerializeField] private int[] autoSpinOptions = { 10, 25 };

        [Header("Bonus rules")]
        [Tooltip("Pays when all nine cells match. Replaces the line wins rather than stacking.")]
        [SerializeField, Min(1)] private int megaWinMultiplier = 200;

        [Tooltip("Autoplay stops and hands the win over when it reaches this multiple of the bet.")]
        [SerializeField, Min(1)] private int bigWinMultiplier = 10;

        [Tooltip("Anticipation only teases symbols worth at least this much, so a cherry pair stays quiet.")]
        [SerializeField, Min(1)] private int anticipationMinPayout = 10;

        [Tooltip("Extra seconds added to the final reel when the first two match.")]
        [SerializeField, Min(0f)] private float anticipationExtraDuration = 0.8f;

        [Header("Timing (seconds)")]
        [SerializeField, Min(0.1f)] private float spinDuration = 1.4f;
        [SerializeField, Min(0f)] private float reelStopStagger = 0.16f;
        [SerializeField, Min(0f)] private float reelStartStagger = 0.06f;

        public IReadOnlyList<SymbolDefinition> Symbols => symbols;
        public int Reels => reels;
        public int Rows => rows;
        public int FillerSymbolsPerReel => fillerSymbolsPerReel;
        public int StartingCredits => startingCredits;
        public IReadOnlyList<int> BetLevels => betLevels;
        public int MaxGambles => maxGambles;
        public IReadOnlyList<int> AutoSpinOptions => autoSpinOptions;
        public int MegaWinMultiplier => megaWinMultiplier;
        public int BigWinMultiplier => bigWinMultiplier;
        public int AnticipationMinPayout => anticipationMinPayout;
        public float AnticipationExtraDuration => anticipationExtraDuration;
        public float SpinDuration => spinDuration;
        public float ReelStopStagger => reelStopStagger;
        public float ReelStartStagger => reelStartStagger;

        /// <summary>Asset-creation helper used by the project builder and tests.</summary>
        public void Configure(SymbolDefinition[] symbolSet)
        {
            symbols = symbolSet;
        }
    }
}
