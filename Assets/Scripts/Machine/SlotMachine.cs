using System;
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

        private int _betIndex;
        private int _stoppedReels;
        private int _pendingWin;
        private int _gamblesUsed;
        private bool _busy;

        public event Action<int> CreditsChanged;
        public event Action<int> BetChanged;
        public event Action SpinStarted;
        public event Action<bool> BusyChanged;

        /// <summary>Total win and the individual winning paylines. Raised for every spin.</summary>
        public event Action<int, IReadOnlyList<PaylineWin>> SpinResolved;

        /// <summary>True when the gamble was won (with the new stake), false when it was lost.</summary>
        public event Action<bool, int> GambleResolved;

        public event Action<int> WinCollected;
        public event Action<string> StatusMessage;

        public int Credits => _wallet != null ? _wallet.Credits : 0;
        public int CurrentBet => config != null ? config.BetLevels[_betIndex] : 0;
        public IReadOnlyList<int> BetLevels => config != null ? config.BetLevels : Array.Empty<int>();
        public int PendingWin => _pendingWin;
        public bool IsBusy => _busy;

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
            if (_busy || config == null || config.BetLevels.Count == 0)
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
                return;
            }

            _wallet.TrySpend(CurrentBet);
            _busy = true;
            _stoppedReels = 0;
            BusyChanged?.Invoke(true);
            SpinStarted?.Invoke();

            // Outcome first: the reels are only ever told where to land.
            _lastGrid = _generator.Generate(config.Reels, config.Rows);

            for (int i = 0; i < reels.Length; i++)
            {
                float duration = config.SpinDuration + i * config.ReelStopStagger;
                float delay = i * config.ReelStartStagger;
                reels[i].Spin(_lastGrid[i, 0], _lastGrid[i, 1], _lastGrid[i, 2], duration, delay);
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
            float lineBet = CurrentBet / (float)Mathf.Max(1, config.Rows);
            List<PaylineWin> wins = PaylineEvaluator.Evaluate(_lastGrid, config.Reels, config.Rows, lineBet);

            int total = 0;
            foreach (PaylineWin win in wins)
            {
                total += win.Payout;
            }

            _busy = false;
            _gamblesUsed = 0;
            _pendingWin = total;
            BusyChanged?.Invoke(false);
            SpinResolved?.Invoke(total, wins);

            StatusMessage?.Invoke(total > 0 ? $"WIN {total} G" : "NO WIN");
        }

        private void HandleCreditsChanged(int credits)
        {
            CreditsChanged?.Invoke(credits);
        }
    }
}
