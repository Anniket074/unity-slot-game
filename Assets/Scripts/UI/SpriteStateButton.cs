using UnityEngine;
using UnityEngine.UI;

namespace SlotGame.UI
{
    /// <summary>
    /// Button that uses the art pack's four-frame strips (normal / hover / pressed /
    /// disabled) instead of Unity's colour tinting, so the UI keeps the hand-drawn look.
    /// </summary>
    public sealed class SpriteStateButton : Button
    {
        [SerializeField] private Sprite normalSprite;
        [SerializeField] private Sprite hoverSprite;
        [SerializeField] private Sprite pressedSprite;
        [SerializeField] private Sprite disabledSprite;

        public void Configure(Sprite normal, Sprite hover, Sprite pressed, Sprite disabled)
        {
            normalSprite = normal;
            hoverSprite = hover;
            pressedSprite = pressed;
            disabledSprite = disabled;

            transition = Transition.None;
            if (targetGraphic is Image image && normalSprite != null)
            {
                image.sprite = normalSprite;
            }
        }

        protected override void DoStateTransition(SelectionState state, bool instant)
        {
            base.DoStateTransition(state, instant);

            Sprite sprite = state switch
            {
                SelectionState.Highlighted => hoverSprite,
                SelectionState.Pressed => pressedSprite,
                SelectionState.Disabled => disabledSprite,
                _ => normalSprite
            };

            if (sprite != null && targetGraphic is Image image)
            {
                image.sprite = sprite;
            }
        }
    }
}
