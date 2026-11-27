using System.Collections.Generic;
using SlotGame.Machine;
using UnityEngine;
using UnityEngine.UI;

namespace SlotGame.UI
{
    /// <summary>
    /// Presentation layer. Subscribes to <see cref="SlotMachine"/> events and updates
    /// texts, buttons, payline highlights and the gamble popup. It never changes game
    /// state except by asking the machine to (spin, gamble, collect).
    /// </summary>
    public sealed class SlotMachineView : MonoBehaviour
    {
        [SerializeField] private SlotMachine machine;
        [SerializeField] private Text creditsText;
        [SerializeField] private Text betText;
        [SerializeField] private Text winText;
        [SerializeField] private Text statusText;
        [SerializeField] private Button spinButton;
        [SerializeField] private Button[] betButtons = new Button[0];
        [SerializeField] private GameObject[] winHighlights = new GameObject[0];
        [SerializeField] private GameObject gamblePanel;
        [SerializeField] private Text gambleTitle;
        [SerializeField] private Text gambleAmount;
        [SerializeField] private Button gambleYes;
        [SerializeField] private Button gambleNo;

        private static readonly Color SelectedBetColor = Color.white;
        private static readonly Color UnselectedBetColor = new Color(0.72f, 0.75f, 0.85f, 1f);

        private Coroutine _popupRoutine;

        public void Configure(
            SlotMachine slotMachine,
            Text credits,
            Text bet,
            Text win,
            Text status,
            Button spin,
            Button[] bets,
            GameObject[] highlights,
            GameObject panel,
            Text title,
            Text amount,
            Button yes,
            Button no)
        {
            machine = slotMachine;
            creditsText = credits;
            betText = bet;
            winText = win;
            statusText = status;
            spinButton = spin;
            betButtons = bets;
            winHighlights = highlights;
            gamblePanel = panel;
            gambleTitle = title;
            gambleAmount = amount;
            gambleYes = yes;
            gambleNo = no;
        }

        private void OnEnable()
        {
            if (machine == null)
            {
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

            if (spinButton != null)
            {
                spinButton.onClick.AddListener(HandleSpinClicked);
            }

            for (int i = 0; i < betButtons.Length; i++)
            {
                int index = i;
                if (betButtons[i] != null)
                {
                    betButtons[i].onClick.AddListener(() => machine.SetBetIndex(index));
                }
            }

            if (gambleYes != null)
            {
                gambleYes.onClick.AddListener(HandleGambleClicked);
            }

            if (gambleNo != null)
            {
                gambleNo.onClick.AddListener(HandleCollectClicked);
            }

            if (gamblePanel != null)
            {
                gamblePanel.SetActive(false);
            }

            ClearHighlights();
            RefreshBetButtons(0);

            HandleCreditsChanged(machine.Credits);
            HandleBetChanged(machine.CurrentBet);
            if (winText != null)
            {
                winText.text = "WIN 0 G";
            }
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                machine.Spin();
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

            if (spinButton != null)
            {
                spinButton.onClick.RemoveListener(HandleSpinClicked);
            }

            if (gambleYes != null)
            {
                gambleYes.onClick.RemoveListener(HandleGambleClicked);
            }

            if (gambleNo != null)
            {
                gambleNo.onClick.RemoveListener(HandleCollectClicked);
            }
        }

        private void HandleSpinClicked()
        {
            machine.Spin();
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
            if (creditsText != null)
            {
                creditsText.text = $"CREDIT\n{credits} G";
            }
        }

        private void HandleBetChanged(int bet)
        {
            if (betText != null)
            {
                betText.text = $"BET {bet} G";
            }

            RefreshBetButtons(bet);
        }

        private void HandleSpinStarted()
        {
            if (winText != null)
            {
                winText.text = "WIN 0 G";
            }

            ClearHighlights();
        }

        private void HandleBusyChanged(bool busy)
        {
            if (spinButton != null)
            {
                spinButton.interactable = !busy;
            }

            foreach (Button button in betButtons)
            {
                if (button != null)
                {
                    button.interactable = !busy;
                }
            }
        }

        private void HandleSpinResolved(int totalWin, IReadOnlyList<PaylineWin> wins)
        {
            if (winText != null)
            {
                winText.text = $"WIN {totalWin} G";
            }

            ShowWinningRows(wins);

            if (totalWin <= 0 || gamblePanel == null)
            {
                return;
            }

            // Let the winning payline glow for a moment before asking about the gamble.
            if (_popupRoutine != null)
            {
                StopCoroutine(_popupRoutine);
            }

            _popupRoutine = StartCoroutine(ShowGamblePopupAfterDelay(totalWin, 0.9f));
        }

        private System.Collections.IEnumerator ShowGamblePopupAfterDelay(int amount, float delay)
        {
            yield return new WaitForSeconds(delay);

            if (machine.PendingWin <= 0 || gamblePanel == null)
            {
                yield break;
            }

            gamblePanel.SetActive(true);
            if (gambleTitle != null)
            {
                gambleTitle.text = "YOU WIN";
            }

            if (gambleAmount != null)
            {
                gambleAmount.text = $"{amount} G";
            }

            if (gambleYes != null)
            {
                gambleYes.interactable = true;
            }
        }

        private void HandleGambleResolved(bool won, int newAmount)
        {
            if (won)
            {
                if (gambleTitle != null)
                {
                    gambleTitle.text = "DOUBLE!";
                }

                if (gambleAmount != null)
                {
                    gambleAmount.text = $"{newAmount} G";
                }

                if (winText != null)
                {
                    winText.text = $"WIN {newAmount} G";
                }

                return;
            }

            if (gambleAmount != null)
            {
                gambleAmount.text = "0 G";
            }

            if (gambleTitle != null)
            {
                gambleTitle.text = "GAMBLE LOST";
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
            if (statusText != null)
            {
                statusText.text = message;
            }
        }

        private System.Collections.IEnumerator ClosePopupAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            if (gamblePanel != null)
            {
                gamblePanel.SetActive(false);
            }
        }

        private void ShowWinningRows(IReadOnlyList<PaylineWin> wins)
        {
            ClearHighlights();
            if (wins == null)
            {
                return;
            }

            foreach (PaylineWin win in wins)
            {
                if (win.Row >= 0 && win.Row < winHighlights.Length && winHighlights[win.Row] != null)
                {
                    winHighlights[win.Row].SetActive(true);
                }
            }
        }

        private void ClearHighlights()
        {
            foreach (GameObject highlight in winHighlights)
            {
                if (highlight != null)
                {
                    highlight.SetActive(false);
                }
            }
        }

        private void RefreshBetButtons(int selectedBet)
        {
            for (int i = 0; i < betButtons.Length; i++)
            {
                if (betButtons[i] == null || betButtons[i].targetGraphic == null)
                {
                    continue;
                }

                bool isSelected = machine != null && i < machine.BetLevels.Count && machine.BetLevels[i] == selectedBet;
                betButtons[i].targetGraphic.color = isSelected ? SelectedBetColor : UnselectedBetColor;
            }
        }
    }
}
