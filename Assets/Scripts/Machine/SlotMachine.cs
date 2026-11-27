using System;
using System.Collections;
using System.Collections.Generic;
using SlotGame.Core;
using SlotGame.Data;
using SlotGame.Economy;
using UnityEngine;

namespace SlotGame.Machine
{
    /// <summary>
    /// The machine's brain: owns the wallet, decides outcomes, drives the reels and
    /// resolves payouts. It knows nothing about Unity UI - the view subscribes to the
    /// events below, so presentation can change without touching game rules.
    /// </summary>
    public sealed class SlotMachine : MonoBehaviour
    {
        [SerializeField] private GameConfig config;
        [SerializeField] private Reel[] reels;

        private Wallet _wallet;
        private SpinOutcomeGenerator _generator;
        private IRandomSource _random;
        private SymbolDefinition[,] _lastGrid;
        private SymbolDefinition[,] _forcedGrid;
        private Coroutine _autoSpinRoutine;

        private int _betIndex;
        private int _stoppedReels;
        private int _currentStake;
        private int _pendingWin;
        private int _gamblesUsed;
        private int _autoSpinsRemaining;
        private bool _busy;

        public event Action<int> CreditsChanged;
        public event Action<int> BetChanged;
        public event Action SpinStarted;
        public event Action<bool> BusyChanged;

        /// <summary>The resolved outcome of every spin, win or lose.</summary>
        public event Action<SpinOutcome> SpinResolved;

        /// <summary>True when the gamble was won (with the new stake), false when it was lost.</summary>
        public event Action<bool, int> GambleResolved;

        public event Action<int> WinCollected;
        public event Action<string> StatusMessage;

        /// <summary>Remaining autoplay spins; 0 means autoplay is idle.</summary>
        public event Action<int> AutoPlayChanged;

        public int Credits => _wallet != null ? _wallet.Credits : 0;
        public int CurrentBet => config != null ? config.BetLevels[_betIndex] : 0;
        public IReadOnlyList<int> BetLevels => config != null ? config.BetLevels : Array.Empty<int>();
        public IReadOnlyList<int> AutoSpinOptions => config != null ? config.AutoSpinOptions : Array.Empty<int>();
        public int PendingWin => _pendingWin;
        public bool IsBusy => _busy;
        public bool IsAutoPlaying => _autoSpinsRemaining > 0;

        /// <summary>Authoring helper: wires the machine to its data and reels.</summary>
        public void Configure(GameConfig gameConfig, Reel[] reelSet)
        {
            config = gameConfig;
            reels = reelSet;
        }

        private void Awake()
        {
            if (config == null)
            {
                Debug.LogError("[SlotMachine] No GameConfig assigned; machine disabled.", this);
                enabled = false;
                return;
            }

            _random = new SystemRandomSource();
            _generator = new SpinOutcomeGenerator(_random, config.Symbols);
            _wallet = new Wallet(config.StartingCredits);
            _wallet.CreditsChanged += HandleCreditsChanged;

            if (reels == null)
            {
                return;
            }

            foreach (Reel reel in reels)
            {
                if (reel == null)
                {
                    continue;
                }

                reel.Initialize(_generator);
                reel.Stopped += HandleReelStopped;
            }
        }

        private void Start()
        {
            // An empty cabinet looks broken, so the machine starts on a grid that is
            // guaranteed not to look like an unpaid win.
            ShowIdleGrid();

            CreditsChanged?.Invoke(Credits);
            BetChanged?.Invoke(CurrentBet);
            StatusMessage?.Invoke("PRESS SPIN");
        }

        private void OnDestroy()
        {
            if (_wallet != null)
            {
                _wallet.CreditsChanged -= HandleCreditsChanged;
            }

            if (reels == null)
            {
                return;
            }

            foreach (Reel reel in reels)
            {
                if (reel != null)
                {
                    reel.Stopped -= HandleReelStopped;
                }
            }
        }

        public void SetBetIndex(int index)
        {
            if (_busy || IsAutoPlaying || config == null || config.BetLevels.Count == 0)
            {
                return;
            }

            int clamped = Mathf.Clamp(index, 0, config.BetLevels.Count - 1);
            if (clamped == _betIndex)
            {
                return;
            }

            _betIndex = clamped;
            BetChanged?.Invoke(CurrentBet);
        }

        /// <summary>Starts a spin if the machine is idle and the player can afford the bet.</summary>
        public void Spin()
        {
            if (_busy || _pendingWin > 0)
            {
                return;
            }

            if (!_wallet.CanAfford(CurrentBet))
            {
                StatusMessage?.Invoke("NOT ENOUGH CREDIT");
                StopAutoPlay();
                return;
            }

            _wallet.TrySpend(CurrentBet);
            _currentStake = CurrentBet;
            _busy = true;
            _stoppedReels = 0;
            BusyChanged?.Invoke(true);
            SpinStarted?.Invoke();

            // Outcome first: the reels are only ever told where to land.
            _lastGrid = _forcedGrid ?? _generator.Generate(config.Reels, config.Rows);
            _forcedGrid = null;

            bool anticipate = HasAnticipation(_lastGrid);

            for (int i = 0; i < reels.Length; i++)
            {
                bool lastReel = i == reels.Length - 1;
                float duration = config.SpinDuration + i * config.ReelStopStagger;
                float delay = i * config.ReelStartStagger;
                float deceleration = SpinCurve.DecelerationFraction;

                // When the first two reels already match, the last one is given a longer,
                // lazier tail so the player gets a moment of suspense before it lands.
                if (anticipate && lastReel)
                {
                    duration += config.AnticipationExtraDuration;
                    deceleration = SpinCurve.AnticipationDecelerationFraction;
                }

                reels[i].Spin(_lastGrid[i, 0], _lastGrid[i, 1], _lastGrid[i, 2], duration, delay, deceleration);
            }
        }

        /// <summary>Adds the pending win to the balance.</summary>
        public void CollectWin()
        {
            if (_pendingWin <= 0)
            {
                return;
            }

            int amount = _pendingWin;
            _pendingWin = 0;
            _gamblesUsed = 0;
            _wallet.Add(amount);
            StatusMessage?.Invoke($"COLLECTED {amount} G");
            WinCollected?.Invoke(amount);
        }

        /// <summary>Double-or-nothing on the pending win, up to <see cref="GameConfig.MaxGambles"/> times.</summary>
        public void Gamble()
        {
            if (_pendingWin <= 0 || config == null)
            {
                return;
            }

            if (_gamblesUsed >= config.MaxGambles)
            {
                StatusMessage?.Invoke("MAX GAMBLES REACHED");
                return;
            }

            _gamblesUsed++;
            bool won = _random.Range(0, 2) == 0;

            if (won)
            {
                _pendingWin *= 2;
                GambleResolved?.Invoke(true, _pendingWin);
            }
            else
            {
                _pendingWin = 0;
                _gamblesUsed = 0;
                GambleResolved?.Invoke(false, 0);
            }
        }

        /// <summary>Runs <paramref name="spins"/> spins back to back, collecting small wins automatically.</summary>
        public void StartAutoPlay(int spins)
        {
            if (spins <= 0 || config == null)
            {
                return;
            }

            StopAutoPlay();
            _autoSpinsRemaining = spins;
            AutoPlayChanged?.Invoke(_autoSpinsRemaining);

            if (!_busy && _pendingWin <= 0)
            {
                Spin();
            }
        }

        public void StopAutoPlay()
        {
            if (_autoSpinRoutine != null)
            {
                StopCoroutine(_autoSpinRoutine);
                _autoSpinRoutine = null;
            }

            if (_autoSpinsRemaining == 0)
            {
                return;
            }

            _autoSpinsRemaining = 0;
            AutoPlayChanged?.Invoke(0);
        }

        /// <summary>
        /// Diagnostics hook: makes the next spin land on a full-board match so the mega
        /// win can be screenshotted without waiting for a one-in-three-thousand spin.
        /// </summary>
        public void ForceMegaRound()
        {
            if (config == null || config.Symbols.Count == 0)
            {
                return;
            }

            SymbolDefinition best = config.Symbols[0];
            foreach (SymbolDefinition symbol in config.Symbols)
            {
                if (symbol != null && symbol.PayoutMultiplier > best.PayoutMultiplier)
                {
                    best = symbol;
                }
            }

            _forcedGrid = new SymbolDefinition[config.Reels, config.Rows];
            for (int reel = 0; reel < config.Reels; reel++)
            {
                for (int row = 0; row < config.Rows; row++)
                {
                    _forcedGrid[reel, row] = best;
                }
            }
        }

        /// <summary>True when the first two reels already line up on something worth teasing.</summary>
        private bool HasAnticipation(SymbolDefinition[,] grid)
        {
            if (config.Reels < 3)
            {
                return false;
            }

            for (int row = 0; row < config.Rows; row++)
            {
                SymbolDefinition first = grid[0, row];
                if (first != null
                    && ReferenceEquals(first, grid[1, row])
                    && first.PayoutMultiplier >= config.AnticipationMinPayout)
                {
                    return true;
                }
            }

            return false;
        }

        private void HandleReelStopped(Reel reel)
        {
            _stoppedReels++;
            if (_stoppedReels < reels.Length)
            {
                return;
            }

            ResolveSpin();
        }

        private void ResolveSpin()
        {
            SpinOutcome outcome = SpinRules.Evaluate(
                _lastGrid,
                config.Reels,
                config.Rows,
                _currentStake,
                config.MegaWinMultiplier);

            _busy = false;
            _gamblesUsed = 0;
            _pendingWin = outcome.TotalWin;
            BusyChanged?.Invoke(false);
            SpinResolved?.Invoke(outcome);

            if (outcome.IsMegaWin)
            {
                StatusMessage?.Invoke($"MEGA WIN {outcome.TotalWin} G");
            }
            else
            {
                StatusMessage?.Invoke(outcome.TotalWin > 0 ? $"WIN {outcome.TotalWin} G" : "NO WIN");
            }

            HandleAutoPlayStep(outcome);
        }

        private void HandleAutoPlayStep(SpinOutcome outcome)
        {
            if (_autoSpinsRemaining <= 0)
            {
                return;
            }

            _autoSpinsRemaining--;
            AutoPlayChanged?.Invoke(_autoSpinsRemaining);

            bool bigWin = outcome.TotalWin >= _currentStake * config.BigWinMultiplier;
            if (bigWin)
            {
                // Hand the big win to the player rather than swallowing it: autoplay stops
                // and the normal collect-or-gamble popup takes over.
                StopAutoPlay();
                return;
            }

            if (outcome.TotalWin > 0)
            {
                CollectWin();
            }

            if (_autoSpinsRemaining <= 0)
            {
                StopAutoPlay();
                return;
            }

            if (!_wallet.CanAfford(CurrentBet))
            {
                StatusMessage?.Invoke("AUTOPLAY STOPPED - OUT OF CREDIT");
                StopAutoPlay();
                return;
            }

            _autoSpinRoutine = StartCoroutine(NextAutoSpinAfterDelay());
        }

        private IEnumerator NextAutoSpinAfterDelay()
        {
            yield return new WaitForSeconds(0.45f);
            _autoSpinRoutine = null;

            if (_autoSpinsRemaining > 0 && !_busy && _pendingWin <= 0)
            {
                Spin();
            }
        }

        private void ShowIdleGrid()
        {
            if (reels == null || reels.Length == 0)
            {
                return;
            }

            SymbolDefinition[,] grid = _generator.Generate(config.Reels, config.Rows);
            for (int row = 0; row < config.Rows; row++)
            {
                SymbolDefinition first = grid[0, row];
                bool matches = true;
                for (int reel = 1; reel < config.Reels; reel++)
                {
                    if (!ReferenceEquals(grid[reel, row], first))
                    {
                        matches = false;
                        break;
                    }
                }

                if (matches)
                {
                    grid[config.Reels - 1, row] = _generator.NextSymbolExcluding(first);
                }
            }

            for (int i = 0; i < reels.Length; i++)
            {
                reels[i].ShowInstant(grid[i, 0], grid[i, 1], grid[i, 2]);
            }
        }

        private void HandleCreditsChanged(int credits)
        {
            CreditsChanged?.Invoke(credits);
        }
    }
}
