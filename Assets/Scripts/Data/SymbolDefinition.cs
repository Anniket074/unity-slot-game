using UnityEngine;

namespace SlotGame.Data
{
    /// <summary>
    /// One reel symbol: what it looks like, how often it appears, and what three
    /// of them on a payline pay.
    /// </summary>
    [CreateAssetMenu(fileName = "Symbol", menuName = "Slot Game/Symbol", order = 0)]
    public sealed class SymbolDefinition : ScriptableObject
    {
        [SerializeField] private string symbolId = "symbol";
        [SerializeField] private string displayName = "Symbol";
        [SerializeField] private Sprite icon;

        [Tooltip("Relative frequency. The odds of a symbol landing on a cell are weight / sum(all weights).")]
        [SerializeField, Min(1)] private int weight = 25;

        [Tooltip("Payout for three of this symbol on one payline, as a multiple of the line bet.")]
        [SerializeField, Min(0)] private int payoutMultiplier = 5;

        public string SymbolId => symbolId;
        public string DisplayName => displayName;
        public Sprite Icon => icon;
        public int Weight => weight;
        public int PayoutMultiplier => payoutMultiplier;

        /// <summary>Asset-creation helper used by the project builder and tests.</summary>
        public void Configure(string id, string label, Sprite sprite, int symbolWeight, int payout)
        {
            symbolId = id;
            displayName = label;
            icon = sprite;
            weight = Mathf.Max(1, symbolWeight);
            payoutMultiplier = Mathf.Max(0, payout);
        }
    }
}
