using UnityEngine;
using UnityEngine.UI;

namespace SlotGame.Machine
{
    /// <summary>A single symbol image inside a reel strip.</summary>
    public sealed class SymbolTileView : MonoBehaviour
    {
        [SerializeField] private Image icon;

        public void Configure(Image image)
        {
            icon = image;
        }

        public void Show(Data.SymbolDefinition symbol)
        {
            if (icon != null)
            {
                icon.sprite = symbol != null ? symbol.Icon : null;
            }
        }
    }
}
