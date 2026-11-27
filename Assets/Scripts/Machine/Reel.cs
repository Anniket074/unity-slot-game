using System;
using System.Collections;
using System.Collections.Generic;
using SlotGame.Core;
using SlotGame.Data;
using UnityEngine;

namespace SlotGame.Machine
{
    /// <summary>
    /// One vertical reel of a three-row slot machine.
    ///
    /// The reel does not decide anything. <see cref="SlotMachine"/> hands it the three
    /// symbols that must be visible when it stops; the reel builds a strip of random
    /// filler with that outcome at the bottom and animates the strip upwards until the
    /// outcome lines up with the window.
    /// </summary>
    public sealed class Reel : MonoBehaviour
    {
        /// <summary>Rows visible in the reel window. Fixed by the machine artwork.</summary>
        public const int VisibleRows = 3;

        [SerializeField] private RectTransform content;
        [SerializeField] private SymbolTileView tilePrefab;
        [SerializeField, Min(1f)] private float rowHeight = 68f;
        [SerializeField, Min(0)] private int fillerSymbols = 12;

        private readonly List<SymbolTileView> _tiles = new List<SymbolTileView>();

        private SpinOutcomeGenerator _fillerSource;
        private bool _isSpinning;
        private float _elapsed;
        private float _startDelay;
        private float _duration;
        private Vector2 _targetPosition;
        private Coroutine _settleRoutine;

        /// <summary>Raised once the reel has finished moving and settled.</summary>
        public event Action<Reel> Stopped;

        public bool IsSpinning => _isSpinning;

        /// <summary>Authoring helper: wires the reel to its strip container and tile prefab.</summary>
        public void Configure(RectTransform stripContent, SymbolTileView prefab, float stripRowHeight, int fillerCount)
        {
            content = stripContent;
            tilePrefab = prefab;
            rowHeight = Mathf.Max(1f, stripRowHeight);
            fillerSymbols = Mathf.Max(1, fillerCount);
        }

        /// <summary>Runtime helper: the generator used for the random filler symbols.</summary>
        public void Initialize(SpinOutcomeGenerator fillerSource)
        {
            _fillerSource = fillerSource;
        }

        /// <summary>Shows a result immediately, without animating (used for the idle machine).</summary>
        public void ShowInstant(SymbolDefinition top, SymbolDefinition centre, SymbolDefinition bottom)
        {
            if (_settleRoutine != null)
            {
                StopCoroutine(_settleRoutine);
                _settleRoutine = null;
            }

            BuildStrip(top, centre, bottom);
            _targetPosition = new Vector2(0f, (_tiles.Count - 3) * rowHeight);
            content.anchoredPosition = _targetPosition;
            _isSpinning = false;
        }

        /// <summary>
        /// Spins and lands with <paramref name="top"/>, <paramref name="centre"/> and
        /// <paramref name="bottom"/> visible in the window.
        /// </summary>
        public void Spin(
            SymbolDefinition top,
            SymbolDefinition centre,
            SymbolDefinition bottom,
            float duration,
            float startDelay)
        {
            if (_settleRoutine != null)
            {
                StopCoroutine(_settleRoutine);
                _settleRoutine = null;
            }

            BuildStrip(top, centre, bottom);

            // The outcome column occupies the last three strip entries; the one before
            // the final entry is the middle of the window once the strip has been pulled up.
            int landingIndex = _tiles.Count - 2;
            _targetPosition = new Vector2(0f, (landingIndex - 1) * rowHeight);

            content.anchoredPosition = Vector2.zero;
            _duration = Mathf.Max(0.1f, duration);
            _startDelay = Mathf.Max(0f, startDelay);
            _elapsed = 0f;
            _isSpinning = true;
        }

        private void Update()
        {
            if (!_isSpinning)
            {
                return;
            }

            _elapsed += Time.deltaTime;
            float normalized = (_elapsed - _startDelay) / _duration;
            if (normalized <= 0f)
            {
                return;
            }

            content.anchoredPosition = Vector2.LerpUnclamped(
                Vector2.zero,
                _targetPosition,
                SpinCurve.Evaluate(normalized));

            if (normalized >= 1f)
            {
                CompleteSpin();
            }
        }

        private void CompleteSpin()
        {
            _isSpinning = false;
            content.anchoredPosition = _targetPosition;
            _settleRoutine = StartCoroutine(Settle());
            Stopped?.Invoke(this);
        }

        /// <summary>
        /// Tiny overshoot-and-return after the strip stops. Without it the reel looks
        /// like it teleported into place rather than being caught by the machine.
        /// </summary>
        private IEnumerator Settle()
        {
            const float settleDuration = 0.13f;
            const float overshoot = 7f;
            float elapsed = 0f;

            while (elapsed < settleDuration)
            {
                elapsed += Time.deltaTime;
                float remaining = 1f - Mathf.Clamp01(elapsed / settleDuration);
                content.anchoredPosition = _targetPosition + Vector2.up * (overshoot * remaining * remaining);
                yield return null;
            }

            content.anchoredPosition = _targetPosition;
            _settleRoutine = null;
        }

        private void BuildStrip(SymbolDefinition top, SymbolDefinition centre, SymbolDefinition bottom)
        {
            int total = fillerSymbols + VisibleRows;
            EnsureTileCount(total);

            for (int i = 0; i < total - VisibleRows; i++)
            {
                _tiles[i].Show(_fillerSource != null ? _fillerSource.NextSymbol() : top);
            }

            _tiles[total - 3].Show(top);
            _tiles[total - 2].Show(centre);
            _tiles[total - 1].Show(bottom);

            for (int i = 0; i < total; i++)
            {
                var rect = (RectTransform)_tiles[i].transform;
                rect.anchoredPosition = new Vector2(0f, -(i * rowHeight + rowHeight * 0.5f));
            }
        }

        private void EnsureTileCount(int total)
        {
            while (_tiles.Count < total)
            {
                SymbolTileView tile = Instantiate(tilePrefab, content);
                tile.name = $"Tile_{_tiles.Count}";
                var rect = (RectTransform)tile.transform;
                rect.anchorMin = new Vector2(0.5f, 1f);
                rect.anchorMax = new Vector2(0.5f, 1f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(rowHeight, rowHeight);
                _tiles.Add(tile);
            }

            while (_tiles.Count > total)
            {
                int last = _tiles.Count - 1;
                Destroy(_tiles[last].gameObject);
                _tiles.RemoveAt(last);
            }
        }
    }
}
