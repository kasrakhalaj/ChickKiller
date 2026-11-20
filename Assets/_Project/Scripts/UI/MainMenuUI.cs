using ChickenShooter.Core;
using UnityEngine;
using UnityEngine.UI;

namespace ChickenShooter.UI
{
    /// <summary>Main menu buttons. Continue / volume sliders are wired to the save & audio systems in Phase 2.</summary>
    public class MainMenuUI : MonoBehaviour
    {
        [SerializeField] private Button newGameButton;
        [SerializeField] private Button continueButton;
        [SerializeField] private Button quitButton;

        private void Awake()
        {
            newGameButton.onClick.AddListener(() => GameManager.Instance.StartNewGame());
            quitButton.onClick.AddListener(Quit);

            // Phase 2: continueButton.interactable = SaveSystem.HasSave;
            if (continueButton != null) continueButton.interactable = false;
        }

        private static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
