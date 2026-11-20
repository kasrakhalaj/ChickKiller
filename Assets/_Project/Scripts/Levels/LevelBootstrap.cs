using ChickenShooter.Core;
using ChickenShooter.Data;
using ChickenShooter.Player;
using ChickenShooter.Waves;
using UnityEngine;

namespace ChickenShooter.Levels
{
    /// <summary>
    /// Entry point of a gameplay scene. Tells the persistent GameManager which level was loaded,
    /// applies the level theme (background + music) and starts the RoundManager at the stored round.
    /// </summary>
    public class LevelBootstrap : MonoBehaviour
    {
        [SerializeField] private LevelDataSO levelData;
        [SerializeField] private RoundManager roundManager;
        [SerializeField] private ParallaxBackground background;
        [SerializeField] private AudioSource musicSource;
        [SerializeField] private PlayerInputReader playerInput;

        public LevelDataSO LevelData => levelData;

        private void Start()
        {
            GameManager gm = GameManager.Instance;
            gm.EnterLevel(levelData);

            if (background != null) background.ApplyTheme(levelData.theme);
            if (musicSource != null && levelData.music != null)
            {
                musicSource.clip = levelData.music;
                musicSource.loop = true;
                musicSource.Play();
            }

            roundManager.Begin(levelData, gm.State.roundIndex);
        }

        private void OnEnable()
        {
            if (playerInput != null) playerInput.PausePressed += OnPausePressed;
        }

        private void OnDisable()
        {
            if (playerInput != null) playerInput.PausePressed -= OnPausePressed;
        }

        private static void OnPausePressed() => GameManager.Instance?.TogglePause();
    }
}
