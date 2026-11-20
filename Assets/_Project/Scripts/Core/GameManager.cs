using System.Collections;
using ChickenShooter.Data;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ChickenShooter.Core
{
    /// <summary>
    /// Persistent (DontDestroyOnLoad) owner of the run state: lives, score, missiles, weapon power,
    /// food meter and level/round progress. Every mutation goes through this class and raises a
    /// GameEvents event, so UI never polls gameplay code. Also drives scene transitions.
    /// </summary>
    /// <summary>What a hit did to the player.</summary>
    public enum PlayerHitResult
    {
        PowerLost,  // shielded by weapon power: power -1, ship survives
        LifeLost,   // ship destroyed, respawns
        GameOver    // last life lost
    }

    public class GameManager : MonoBehaviour
    {
        private const string ConfigResourcePath = "GameConfig";

        public static GameManager Instance { get; private set; }

        [SerializeField] private GameConfigSO config;

        public GameConfigSO Config => config;
        public PlayerRunState State { get; private set; }
        public bool IsGameOver { get; private set; }
        public bool IsPaused { get; private set; }
        public bool IsTransitioning { get; private set; }
        public bool IsGameCompleted { get; private set; }
        public bool IsGameplayActive => !IsGameOver && !IsPaused && !IsTransitioning;

        public LevelDataSO CurrentLevel =>
            config != null && config.levels.Length > 0
                ? config.levels[Mathf.Clamp(State.levelIndex, 0, config.levels.Length - 1)]
                : null;

        // ------------------------------------------------------------------ bootstrap

        /// <summary>Guarantees a GameManager exists before any scene loads (also when a Level scene is played directly).</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            var cfg = Resources.Load<GameConfigSO>(ConfigResourcePath);
            if (cfg == null)
            {
                Debug.LogError($"[GameManager] Missing Resources/{ConfigResourcePath}.asset");
                return;
            }

            var go = new GameObject("[GameManager]");
            var gm = go.AddComponent<GameManager>();
            gm.config = cfg;
            gm.State = PlayerRunState.CreateNew(cfg);
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDestroy()
        {
            if (Instance != this) return;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Instance = null;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Time.timeScale = 1f;
            IsPaused = false;
            IsTransitioning = false;
        }

        // ------------------------------------------------------------------ run flow

        /// <summary>Main menu "New Game": fresh state, load Level 1.</summary>
        public void StartNewGame()
        {
            State = PlayerRunState.CreateNew(config);
            IsGameOver = false;
            IsGameCompleted = false;
            LoadLevelScene(0);
        }

        /// <summary>Starts the run from an existing state (Phase 2 "Continue" uses this with the loaded save).</summary>
        public void StartFrom(PlayerRunState state)
        {
            State = state;
            IsGameOver = false;
            LoadLevelScene(Mathf.Clamp(state.levelIndex, 0, config.levels.Length - 1));
        }

        /// <summary>Called by a level scene's bootstrap once it is loaded.</summary>
        public void EnterLevel(LevelDataSO level)
        {
            int index = System.Array.IndexOf(config.levels, level);
            if (index < 0)
            {
                Debug.LogWarning($"[GameManager] Level '{level.name}' is not in GameConfig.levels.");
            }
            else if (index != State.levelIndex)
            {
                // Scene was opened directly in the editor: jump the run to this level.
                State.levelIndex = index;
                State.roundIndex = 0;
            }

            IsGameOver = false;
            State.roundIndex = Mathf.Clamp(State.roundIndex, 0, Mathf.Max(0, level.RoundCount - 1));
            GameEvents.RaiseLevelStarted(level);
            BroadcastAllStats();
        }

        /// <summary>Round checkpoint: called when a round begins. Phase 2 hooks the auto-save here.</summary>
        public void BeginRound(int roundIndex)
        {
            State.roundIndex = roundIndex;
            Checkpoint();
        }

        /// <summary>Boss of the current level defeated -> show message, then load the next scene by name.</summary>
        public void CompleteLevel()
        {
            if (IsTransitioning) return;
            StartCoroutine(CompleteLevelRoutine());
        }

        private IEnumerator CompleteLevelRoutine()
        {
            IsTransitioning = true;
            LevelDataSO finished = CurrentLevel;
            bool isLastLevel = State.levelIndex >= config.levels.Length - 1;

            if (isLastLevel)
            {
                // Final boss defeated: the HUD shows the victory screen and waits for the player.
                // Phase 2: the save file is reset here so the next run starts fresh.
                IsGameCompleted = true;
                GameEvents.RaiseGameCompleted();
                yield break;
            }
            GameEvents.RaiseLevelCompleted(finished);

            yield return new WaitForSecondsRealtime(config.levelCompleteDelay);

            State.levelIndex++;
            State.roundIndex = 0;
            Checkpoint();
            LoadLevelScene(State.levelIndex);
        }

        private void LoadLevelScene(int levelIndex)
        {
            State.levelIndex = levelIndex;
            LevelDataSO level = config.levels[levelIndex];
            // Scene is loaded by the name string stored in LevelDataSO.
            SceneManager.LoadScene(level.sceneName);
        }

        public void RetryLevel()
        {
            State.lives = config.startingLives;
            State.powerLevel = config.powerActsAsShield ? config.respawnPowerLevel : config.basePowerLevel;
            State.roundIndex = 0;
            IsGameOver = false;
            LoadLevelScene(State.levelIndex);
        }

        public void ReturnToMainMenu()
        {
            Time.timeScale = 1f;
            if (IsGameCompleted)
            {
                State = PlayerRunState.CreateNew(config);
                IsGameCompleted = false;
            }
            SceneManager.LoadScene(config.mainMenuScene);
        }

        public void TogglePause()
        {
            if (IsGameOver || IsTransitioning) return;
            IsPaused = !IsPaused;
            Time.timeScale = IsPaused ? 0f : 1f;
            GameEvents.RaisePauseChanged(IsPaused);
        }

        /// <summary>Single place where progress becomes "committed". Phase 2: SaveSystem.Save(State).</summary>
        private void Checkpoint()
        {
            // Intentionally empty in Phase 1 (JSON save system arrives in Phase 2).
        }

        // ------------------------------------------------------------------ stat mutations

        public void AddScore(int amount)
        {
            if (amount == 0) return;
            State.score += amount;
            GameEvents.RaiseScoreChanged(State.score);
        }

        /// <summary>
        /// Resolves one hit on the player.
        /// Power-shield rule (default): while weapon power is above the base level, a hit only drops power by
        /// one. At base power the hit destroys the ship: one life is lost and the ship respawns at
        /// <see cref="GameConfigSO.respawnPowerLevel"/>.
        /// Classic rule (powerActsAsShield = false): every hit costs a life and one power level.
        /// </summary>
        public PlayerHitResult PlayerHit()
        {
            if (IsGameOver) return PlayerHitResult.GameOver;

            if (config.powerActsAsShield && State.powerLevel > config.basePowerLevel)
            {
                ChangePowerLevel(-1);
                return PlayerHitResult.PowerLost;
            }

            State.lives = Mathf.Max(0, State.lives - 1);
            GameEvents.RaiseLivesChanged(State.lives);

            if (State.lives > 0)
            {
                if (config.powerActsAsShield) SetPowerLevel(config.respawnPowerLevel);
                else ChangePowerLevel(-1);
                return PlayerHitResult.LifeLost;
            }

            HandleGameOver();
            return PlayerHitResult.GameOver;
        }

        private void SetPowerLevel(int level)
        {
            int clamped = Mathf.Clamp(level, config.basePowerLevel, config.maxPowerLevel);
            if (clamped == State.powerLevel) return;
            State.powerLevel = clamped;
            GameEvents.RaisePowerLevelChanged(State.powerLevel, config.maxPowerLevel);
        }

        private void HandleGameOver()
        {
            IsGameOver = true;
            // Game over policy: progress is rewritten to round 1 of the current level.
            State.roundIndex = 0;
            Checkpoint();
            GameEvents.RaisePlayerDied();
            GameEvents.RaiseGameOver();
        }

        public void ChangePowerLevel(int delta)
        {
            int newLevel = Mathf.Clamp(State.powerLevel + delta, config.basePowerLevel, config.maxPowerLevel);
            if (newLevel == State.powerLevel) return;
            State.powerLevel = newLevel;
            GameEvents.RaisePowerLevelChanged(State.powerLevel, config.maxPowerLevel);
        }

        public bool IsPowerMaxed => State.powerLevel >= config.maxPowerLevel;

        public bool TryUseMissile()
        {
            if (State.missiles <= 0 || !IsGameplayActive) return false;
            State.missiles--;
            GameEvents.RaiseMissilesChanged(State.missiles);
            return true;
        }

        public void AddMissile(int amount = 1)
        {
            State.missiles += amount;
            GameEvents.RaiseMissilesChanged(State.missiles);
        }

        /// <summary>Adds food to the progress meter. A full meter grants one missile and resets.</summary>
        public void AddFood(float amount)
        {
            State.foodProgress += amount;
            while (State.foodProgress >= config.foodMeterMax)
            {
                State.foodProgress -= config.foodMeterMax;
                AddMissile();
            }
            GameEvents.RaiseFoodProgressChanged(State.foodProgress / config.foodMeterMax);
        }

        // ------------------------------------------------------------------ special weapons

        public SpecialWeaponSO SelectedSpecial =>
            State.selectedSpecial >= 0 && State.selectedSpecial < config.specialWeapons.Length
                ? config.specialWeapons[State.selectedSpecial]
                : null;

        public float SelectedSpecialAmmo => SelectedSpecial != null ? State.specialAmmo[State.selectedSpecial] : 0f;

        /// <summary>
        /// Weapon crate collected. The player carries ONE special weapon: a different crate replaces the
        /// current weapon (old ammo is lost); the same crate refills it (capped at maxAmmo).
        /// </summary>
        public void AddSpecialAmmo(int index, float amount)
        {
            EnsureSpecialArray();
            if (index < 0 || index >= config.specialWeapons.Length) return;
            SpecialWeaponSO w = config.specialWeapons[index];

            bool sameWeapon = State.selectedSpecial == index;
            if (!sameWeapon) System.Array.Clear(State.specialAmmo, 0, State.specialAmmo.Length);
            State.specialAmmo[index] = Mathf.Min(w.maxAmmo, (sameWeapon ? State.specialAmmo[index] : 0f) + amount);
            State.selectedSpecial = index;

            GameEvents.RaiseSpecialWeaponAcquired(w);
            RaiseSpecialChanged();
        }

        public void ConsumeSpecialAmmo(float amount)
        {
            int i = State.selectedSpecial;
            if (i < 0 || amount <= 0f) return;
            State.specialAmmo[i] = Mathf.Max(0f, State.specialAmmo[i] - amount);
            if (State.specialAmmo[i] <= 0f) State.selectedSpecial = -1; // weapon used up
            RaiseSpecialChanged();
        }

        private void EnsureSpecialArray()
        {
            int n = config.specialWeapons?.Length ?? 0;
            if (State.specialAmmo == null || State.specialAmmo.Length != n)
            {
                var resized = new float[n];
                if (State.specialAmmo != null) System.Array.Copy(State.specialAmmo, resized, Mathf.Min(n, State.specialAmmo.Length));
                State.specialAmmo = resized;
            }
        }

        private void RaiseSpecialChanged() => GameEvents.RaiseSpecialWeaponChanged(SelectedSpecial, SelectedSpecialAmmo);

        private void BroadcastAllStats()
        {
            EnsureSpecialArray();
            RaiseSpecialChanged();
            GameEvents.RaiseScoreChanged(State.score);
            GameEvents.RaiseLivesChanged(State.lives);
            GameEvents.RaiseMissilesChanged(State.missiles);
            GameEvents.RaisePowerLevelChanged(State.powerLevel, config.maxPowerLevel);
            GameEvents.RaiseFoodProgressChanged(State.foodProgress / config.foodMeterMax);
        }
    }
}
