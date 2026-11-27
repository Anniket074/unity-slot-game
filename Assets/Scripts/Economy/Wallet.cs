using System;
using UnityEngine;

namespace SlotGame.Economy
{
    /// <summary>
    /// Owns the player's credit balance. Every mutation goes through this class so
    /// there is exactly one place that can change the balance, and one event that
    /// the UI listens to.
    /// </summary>
    public sealed class Wallet
    {
        public Wallet(int startingCredits)
        {
            Credits = Mathf.Max(0, startingCredits);
        }

        /// <summary>Raised with the new balance after every successful change.</summary>
        public event Action<int> CreditsChanged;

        public int Credits { get; private set; }

        public bool CanAfford(int amount)
        {
            return amount > 0 && Credits >= amount;
        }

        /// <summary>Attempts to take a stake. Returns false (and changes nothing) when the balance is short.</summary>
        public bool TrySpend(int amount)
        {
            if (!CanAfford(amount))
            {
                return false;
            }

            Credits -= amount;
            CreditsChanged?.Invoke(Credits);
            return true;
        }

        public void Add(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            Credits += amount;
            CreditsChanged?.Invoke(Credits);
        }
    }
}
