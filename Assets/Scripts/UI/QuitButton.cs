using UnityEngine;
using UnityEngine.UI;

namespace SlotGame.UI
{
    /// <summary>Closes the game (or leaves Play mode in the editor).</summary>
    public sealed class QuitButton : MonoBehaviour
    {
        [SerializeField] private Button button;

        public void Configure(Button target)
        {
            button = target;
        }

        private void OnEnable()
        {
            if (button != null)
            {
                button.onClick.AddListener(Quit);
            }
        }

        private void OnDisable()
        {
            if (button != null)
            {
                button.onClick.RemoveListener(Quit);
            }
        }

        private void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
