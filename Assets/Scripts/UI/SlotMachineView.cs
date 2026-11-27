using System;
using System.Collections;
using SlotGame.Machine;
using UnityEngine;
using UnityEngine.UI;

namespace SlotGame.UI
{
    /// <summary>Every HUD widget the view drives, grouped so the builder can hand them over in one go.</summary>
    [Serializable]
    public sealed class HudReferences
    {
        public Text creditsText;
        public Text betText;
        public Text winText;
        public Text statusText;
        public Button spinButton;
        public Button[] betButtons = Array.Empty<Button>();
        public Button[] autoButtons = Array.Empty<Button>();
        public GameObject[] winHighlights = Array.Empty<GameObject>();
    }

    /// <summary>Widgets that make up the gamble offer.</summary>
    [Serializable]
    public sealed class GambleReferences
    {
        public GameObject panel;
        public Text title;
        public Text amount;
        public Button yes;
        public Button no;
    }

    /// <summary>
    /// Presentation layer. Subscribes to <see cref="SlotMachine"/> events and updates
    /// texts, buttons, payline highlights and the gamble popup. It never changes game
    /// state except by asking the machine to (spin, gamble, collect).
    /// </summary>
    public sealed class SlotMachineView : MonoBehaviour
    {
        private const float WinPopupDelay = 0.9f;
        private const float MegaPopupDelay = 1.5f;

        private static readonly Color SelectedBetColor = Color.white;
        private static readonly Color UnselectedBetColor = new Color(0.72f, 0.75f, 0.85f, 1f);

        [SerializeField] private SlotMachine machine;
        [SerializeField] private HudReferences hud = new HudReferences();
        [SerializeField] private GambleReferences gamble = new GambleReferences();

        private Text[] _autoLabels = Array.Empty<Text>();
        private Coroutine _popupRoutine;
        private bool _pendingMega;

        public void Configure(SlotMachine slotMachine, HudReferences hudReferences, GambleReferences gambleReferences)
        {
            machine = slotMachine;
            hud = hudReferences;
            gamble = gambleReferences;
        }

        private void OnEnable()
        {
            if (machine == null)
            {
                Debug.LogError("[SlotMachineView] No SlotMachine assigned.", this);
                return;
            }

            machine.CreditsChanged += HandleCreditsChanged;
            machine.BetChanged += HandleBetChanged;
            machine.SpinStarted += HandleSpinStarted;
            machine.BusyChanged += HandleBusyChanged;
            machine.SpinResolved += HandleSpinResolved;
            machine.GambleResolved += HandleGambleResolved;
            machine.WinCollected += HandleWinCollected;
            machine.StatusMessage += HandleStatusMessage;
            machine.AutoPlayChanged += HandleAutoPlayChanged;

            _autoLabels = new Text[hud.autoButtons.Length];
            for (int i = 0; i < hud.autoButtons.Length; i++)
            {
                int index = i;
                Button button = hud.autoButtons[i];
                if (button == null)
                {
                    continue;
                }

                _autoLabels[i] = button.GetComponentInChildren<Text>();
                button.onClick.AddListener(() => HandleAutoClicked(index));
            }

            if (hud.spinButton != null)
            {
                hud.spinButton.onClick.AddListener(HandleSpinClicked);
            }

            for (int i = 0; i < hud.betButtons.Length; i++)
            {
                int index = i;
                if (hud.betButtons[i] != null)
                {
                    hud.betButtons[i].onClick.AddListener(() => machine.SetBetIndex(index));
                }
            }

            if (gamble.yes != null)
            {
                gamble.yes.onClick.AddListener(HandleGambleClicked);
            }

            if (gamble.no != null)
            {
                gamble.no.onClick.AddListener(HandleCollectClicked);
            }

            if (gamble.panel != null)
            {
                gamble.panel.SetActive(false);
            }

            ClearHighlights();
            HandleCreditsChanged(machine.Credits);
            HandleBetChanged(machine.CurrentBet);
            HandleAutoPlayChanged(0);
            RefreshInteractables();

            if (hud.winText != null)
            {
                hud.winText.text = "WIN 0 G";
            }
        }

        private void OnDisable()
        {
            if (machine == null)
            {
                return;
            }

            machine.CreditsChanged -= HandleCreditsChanged;
            machine.BetChanged -= HandleBetChanged;
            machine.SpinStarted -= HandleSpinStarted;
            machine.BusyChanged -= HandleBusyChanged;
            machine.SpinResolved -= HandleSpinResolved;
            machine.GambleResolved -= HandleGambleResolved;
            machine.WinCollected -= HandleWinCollected;
            machine.StatusMessage -= HandleStatusMessage;
            machine.AutoPlayChanged -= HandleAutoPlayChanged;

            if (hud.spinButton != null)
            {
                hud.spinButton.onClick.RemoveListener(HandleSpinClicked);
            }

            if (gamble.yes != null)
            {
                gamble.yes.onClick.RemoveListener(HandleGambleClicked);
            }

            if (gamble.no != null)
            {
                gamble.no.onClick.RemoveListener(HandleCollectClicked);
            }
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                if (machine.IsAutoPlaying)
                {
                    machine.StopAutoPlay();
                    return;
                }

                machine.Spin();
            }
        }

        private void HandleSpinClicked()
        {
            machine.Spin();
        }

        private void HandleAutoClicked(int index)
        {
            if (machine.IsAutoPlaying)
            {
                machine.StopAutoPlay();
                return;
            }

            if (index >= 0 && index < machine.AutoSpinOptions.Count)
            {
                machine.StartAutoPlay(machine.AutoSpinOptions[index]);
            }
        }

        private void HandleGambleClicked()
        {
            machine.Gamble();
        }

        private void HandleCollectClicked()
        {
            machine.CollectWin();
        }

        private void HandleCreditsChanged(int credits)
        {
            if (hud.creditsText != null)
            {
                hud.creditsText.text = $"CREDIT\n{credits} G";
            }
        }

        private void HandleBetChanged(int bet)
        {
            if (hud.betText != null)
            {
                hud.betText.text = $"BET {bet} G";
            }

            RefreshBetButtons(bet);
        }

        private void HandleSpinStarted()
        {
            if (hud.winText != null)
            {
                hud.winText.text = "WIN 0 G";
            }

            ClearHighlights();
        }

        private void HandleBusyChanged(bool busy)
        {
            RefreshInteractables();
        }

        private void HandleSpinResolved(SpinOutcome outcome)
        {
            if (hud.winText != null)
            {
                hud.winText.text = $"WIN {outcome.TotalWin} G";
            }

            ShowWinningRows(outcome.Lines);

            if (outcome.TotalWin <= 0 || gamble.panel == null)
            {
                return;
            }

            // Let the winning payline glow for a moment before asking about the gamble.
            if (_popupRoutine != null)
            {
                StopCoroutine(_popupRoutine);
            }

            _pendingMega = outcome.IsMegaWin;
            float delay = outcome.IsMegaWin ? MegaPopupDelay : WinPopupDelay;
            _popupRoutine = StartCoroutine(ShowGamblePopupAfterDelay(outcome.TotalWin, delay));
        }

        private IEnumerator ShowGamblePopupAfterDelay(int amount, float delay)
        {
            yield return new WaitForSeconds(delay);

            if (machine.PendingWin <= 0 || gamble.panel == null)
            {
                yield break;
            }

            gamble.panel.SetActive(true);

            if (gamble.title != null)
            {
                gamble.title.text = _pendingMega ? "MEGA WIN!" : "YOU WIN";
            }

            if (gamble.amount != null)
            {
                gamble.amount.text = $"{amount} G";
            }

            if (gamble.yes != null)
            {
                gamble.yes.interactable = true;
            }

            _popupRoutine = null;
        }

        private void HandleGambleResolved(bool won, int newAmount)
        {
            if (won)
            {
                if (gamble.title != null)
                {
                    gamble.title.text = "DOUBLE!";
                }

                if (gamble.amount != null)
                {
                    gamble.amount.text = $"{newAmount} G";
                }

                if (hud.winText != null)
                {
                    hud.winText.text = $"WIN {newAmount} G";
                }

                return;
            }

            if (gamble.amount != null)
            {
                gamble.amount.text = "0 G";
            }

            if (gamble.title != null)
            {
                gamble.title.text = "GAMBLE LOST";
            }

            ClearHighlights();
            StartCoroutine(ClosePopupAfterDelay(1.1f));
        }

        private void HandleWinCollected(int amount)
        {
            if (_popupRoutine != null)
            {
                StopCoroutine(_popupRoutine);
                _popupRoutine = null;
            }

            ClearHighlights();
            StartCoroutine(ClosePopupAfterDelay(0.35f));
        }

        private void HandleStatusMessage(string message)
        {
            if (hud.statusText != null)
            {
                hud.statusText.text = message;
            }
        }

        private void HandleAutoPlayChanged(int remaining)
        {
            for (int i = 0; i < _autoLabels.Length; i++)
            {
                if (_autoLabels[i] == null)
                {
                    continue;
                }

                if (remaining > 0)
                {
                    _autoLabels[i].text = $"STOP {remaining}";
                }
                else
                {
                    int option = i < machine.AutoSpinOptions.Count ? machine.AutoSpinOptions[i] : 0;
                    _autoLabels[i].text = $"AUTO {option}";
                }
            }

            RefreshInteractables();
        }

        private IEnumerator ClosePopupAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            if (gamble.panel != null)
            {
                gamble.panel.SetActive(false);
            }
        }

        private void ShowWinningRows(System.Collections.Generic.IReadOnlyList<PaylineWin> wins)
        {
            ClearHighlights();
            if (wins == null)
            {
                return;
            }

            foreach (PaylineWin win in wins)
            {
                if (win.Row >= 0 && win.Row < hud.winHighlights.Length && hud.winHighlights[win.Row] != null)
                {
                    hud.winHighlights[win.Row].SetActive(true);
                }
            }
        }

        private void ClearHighlights()
        {
            foreach (GameObject highlight in hud.winHighlights)
            {
                if (highlight != null)
                {
                    highlight.SetActive(false);
                }
            }
        }

        private void RefreshBetButtons(int selectedBet)
        {
            for (int i = 0; i < hud.betButtons.Length; i++)
            {
                if (hud.betButtons[i] == null || hud.betButtons[i].targetGraphic == null)
                {
                    continue;
                }

                bool isSelected = i < machine.BetLevels.Count && machine.BetLevels[i] == selectedBet;
                hud.betButtons[i].targetGraphic.color = isSelected ? SelectedBetColor : UnselectedBetColor;
            }
        }

        private void RefreshInteractables()
        {
            bool idle = !machine.IsBusy && !machine.IsAutoPlaying;

            if (hud.spinButton != null)
            {
                hud.spinButton.interactable = idle;
            }

            foreach (Button button in hud.betButtons)
            {
                if (button != null)
                {
                    button.interactable = idle;
                }
            }

            foreach (Button button in hud.autoButtons)
            {
                if (button != null)
                {
                    button.interactable = idle || !machine.IsBusy || machine.IsAutoPlaying;
                }
            }
        }
    }
}
