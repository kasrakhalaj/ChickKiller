using System.Collections;
using ChickenShooter.Core;
using ChickenShooter.Data;
using UnityEngine;
using UnityEngine.UI;

namespace ChickenShooter.UI
{
    /// <summary>
    /// In-game HUD. Purely event-driven: it subscribes to GameEvents and never reads gameplay objects.
    /// Shows level/round, lives, score, missiles, weapon power, food meter, boss bar, round banners,
    /// and minimal pause / game-over panels (fully styled screens come in Phase 2).
    /// </summary>
    public class GameHUD : MonoBehaviour
    {
        [Header("Top bar")]
        [SerializeField] private Text levelRoundText;
        [SerializeField] private Text scoreText;
        [SerializeField] private Text livesText;
        [SerializeField] private Text missilesText;
        [SerializeField] private Text powerText;
        [SerializeField] private Image foodBarFill;

        [Header("Special weapon panel")]
        [SerializeField] private CanvasGroup specialPanel;
        [SerializeField] private Image specialIcon;
        [SerializeField] private Text specialText;
        [SerializeField] private Image specialAmmoFill;
        [SerializeField] private Text specialAmmoText;
        [Tooltip("Compact line under POWER (always visible even if the bottom of the screen is cut off).")]
        [SerializeField] private Text specialLineText;

        [Header("Boss")]
        [SerializeField] private GameObject bossBarRoot;
        [SerializeField] private Image bossBarFill;
        [SerializeField] private Text bossNameText;

        [Header("Banner")]
        [SerializeField] private CanvasGroup bannerGroup;
        [SerializeField] private Text bannerTitle;
        [SerializeField] private Text bannerSubtitle;

        [Header("Panels")]
        [SerializeField] private GameObject pausePanel;
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button pauseMenuButton;
        [SerializeField] private GameObject gameOverPanel;
        [SerializeField] private Button retryButton;
        [SerializeField] private Button gameOverMenuButton;
        [SerializeField] private Text gameOverScoreText;
        [SerializeField] private GameObject victoryPanel;
        [SerializeField] private Text victoryScoreText;
        [SerializeField] private Button victoryMenuButton;

        private int levelNumber = 1;
        private int lastScore;
        private Coroutine bannerRoutine;
        private Color bannerDefaultColor = new Color(1f, 0.85f, 0.3f);
        private bool epicBossTitle;

        private static readonly Color BossRed = new Color(1f, 0.2f, 0.18f);
        private const string EmperorGreen = "#5CFF4D";

        private void Awake()
        {
            if (bossBarRoot) bossBarRoot.SetActive(false);
            if (pausePanel) pausePanel.SetActive(false);
            if (gameOverPanel) gameOverPanel.SetActive(false);
            if (victoryPanel) victoryPanel.SetActive(false);
            victoryMenuButton?.onClick.AddListener(() => GameManager.Instance.ReturnToMainMenu());
            if (bannerGroup) bannerGroup.alpha = 0f;
            if (bannerTitle) bannerDefaultColor = bannerTitle.color;

            resumeButton?.onClick.AddListener(() => GameManager.Instance.TogglePause());
            pauseMenuButton?.onClick.AddListener(() => GameManager.Instance.ReturnToMainMenu());
            retryButton?.onClick.AddListener(() => GameManager.Instance.RetryLevel());
            gameOverMenuButton?.onClick.AddListener(() => GameManager.Instance.ReturnToMainMenu());
        }

        private void OnEnable()
        {
            GameEvents.ScoreChanged += OnScore;
            GameEvents.LivesChanged += OnLives;
            GameEvents.MissilesChanged += OnMissiles;
            GameEvents.PowerLevelChanged += OnPower;
            GameEvents.FoodProgressChanged += OnFood;
            GameEvents.SpecialWeaponChanged += OnSpecial;
            GameEvents.SpecialWeaponAcquired += OnSpecialAcquired;
            GameEvents.LevelStarted += OnLevelStarted;
            GameEvents.RoundStarted += OnRoundStarted;
            GameEvents.RoundCleared += OnRoundCleared;
            GameEvents.LevelCompleted += OnLevelCompleted;
            GameEvents.GameCompleted += OnGameCompleted;
            GameEvents.BossSpawned += OnBossSpawned;
            GameEvents.BossHealthChanged += OnBossHealth;
            GameEvents.BossDefeated += OnBossDefeated;
            GameEvents.BossPhaseChanged += OnBossPhase;
            GameEvents.PauseChanged += OnPause;
            GameEvents.GameOver += OnGameOver;
        }

        private void OnDisable()
        {
            GameEvents.ScoreChanged -= OnScore;
            GameEvents.LivesChanged -= OnLives;
            GameEvents.MissilesChanged -= OnMissiles;
            GameEvents.PowerLevelChanged -= OnPower;
            GameEvents.FoodProgressChanged -= OnFood;
            GameEvents.SpecialWeaponChanged -= OnSpecial;
            GameEvents.SpecialWeaponAcquired -= OnSpecialAcquired;
            GameEvents.LevelStarted -= OnLevelStarted;
            GameEvents.RoundStarted -= OnRoundStarted;
            GameEvents.RoundCleared -= OnRoundCleared;
            GameEvents.LevelCompleted -= OnLevelCompleted;
            GameEvents.GameCompleted -= OnGameCompleted;
            GameEvents.BossSpawned -= OnBossSpawned;
            GameEvents.BossHealthChanged -= OnBossHealth;
            GameEvents.BossDefeated -= OnBossDefeated;
            GameEvents.BossPhaseChanged -= OnBossPhase;
            GameEvents.PauseChanged -= OnPause;
            GameEvents.GameOver -= OnGameOver;
        }

        // ------------------------------------------------------------------ stat handlers

        private void OnScore(int v)
        {
            lastScore = v;
            SetText(scoreText, $"SCORE  {v:N0}");
        }
        private void OnLives(int v) => SetText(livesText, $"X {v}");
        private void OnMissiles(int v) => SetText(missilesText, $"MISSILES  {v}");
        private void OnPower(int level, int max) => SetText(powerText, $"POWER  {level}/{max}");

        private void OnFood(float normalized)
        {
            if (foodBarFill) foodBarFill.fillAmount = normalized;
        }

        private void OnSpecial(SpecialWeaponSO weapon, float ammo)
        {
            if (weapon == null)
            {
                if (specialPanel) specialPanel.alpha = 0.35f;
                SetText(specialText, "NO WEAPON");
                if (specialText) specialText.color = new Color(1f, 1f, 1f, 0.7f);
                SetText(specialAmmoText, "");
                SetText(specialLineText, "SPECIAL  -");
                if (specialLineText) specialLineText.color = new Color(1f, 1f, 1f, 0.6f);
                if (specialIcon) specialIcon.enabled = false;
                if (specialAmmoFill) specialAmmoFill.fillAmount = 0f;
                return;
            }

            if (specialPanel) specialPanel.alpha = 1f;
            SetText(specialText, weapon.displayName.ToUpperInvariant());
            if (specialText) specialText.color = Color.Lerp(weapon.color, Color.white, 0.25f);
            SetText(specialAmmoText, weapon.FormatAmmo(ammo));
            SetText(specialLineText, $"SPECIAL  {weapon.displayName.ToUpperInvariant()}  {weapon.FormatAmmo(ammo)}");
            if (specialLineText) specialLineText.color = Color.Lerp(weapon.color, Color.white, 0.3f);
            if (specialIcon)
            {
                specialIcon.enabled = true;
                specialIcon.sprite = weapon.icon;
            }
            if (specialAmmoFill)
            {
                specialAmmoFill.color = weapon.color;
                specialAmmoFill.fillAmount = Mathf.Clamp01(ammo / Mathf.Max(0.01f, weapon.ammoPerPickup));
            }
        }

        private void OnSpecialAcquired(SpecialWeaponSO weapon) =>
            ShowBanner(weapon.displayName.ToUpperInvariant() + "!", "SPECIAL WEAPON  -  RIGHT CLICK / C", 1.1f);

        // ------------------------------------------------------------------ flow handlers

        private void OnLevelStarted(LevelDataSO level)
        {
            levelNumber = level.levelNumber;
            ShowBanner($"LEVEL {level.levelNumber}", level.displayName.ToUpperInvariant(), 2f);
        }

        private void OnRoundStarted(RoundInfo info)
        {
            SetText(levelRoundText, $"LEVEL {info.LevelNumber} - ROUND {info.RoundNumber}/{info.TotalRounds}");
            string subtitle = info.WaveType switch
            {
                WaveType.Asteroids => "ASTEROID FIELD",
                WaveType.Boss => "BOSS INCOMING!",
                _ => info.Label.ToUpperInvariant()
            };
            ShowBanner($"ROUND {info.RoundNumber}", subtitle, 1.8f);
        }

        private void OnRoundCleared(int round) { }

        private void OnLevelCompleted(LevelDataSO level) =>
            ShowBanner($"LEVEL {level.levelNumber} COMPLETE!", "GET READY FOR THE NEXT STAGE", 3f);

        private void OnGameCompleted()
        {
            if (bossBarRoot) bossBarRoot.SetActive(false);
            SetText(victoryScoreText, $"FINAL SCORE  {lastScore:N0}");
            if (victoryPanel) victoryPanel.SetActive(true);
        }

        /// <summary>
        /// Boss arrival: red title in the boss bar plus a big red banner. Bosses whose name is a list of
        /// epithets ("Invincible, Fearless, ..., Green Emperor Chick") get an epithet roll: every title slams
        /// onto the screen in rainbow colours, then the true name lands huge and green.
        /// </summary>
        private void OnBossSpawned(string bossName)
        {
            if (bossBarRoot) bossBarRoot.SetActive(true);
            string upper = bossName.ToUpperInvariant();
            int lastComma = upper.LastIndexOf(',');

            if (bossNameText) bossNameText.color = BossRed;
            if (lastComma > 0)
            {
                string epithets = upper.Substring(0, lastComma + 1).Trim();
                string core = upper.Substring(lastComma + 1).Trim();
                SetText(bossNameText, $"<size=15>{epithets}</size>\n<size=40><color={EmperorGreen}>{core}</color></size>");
                epicBossTitle = true;
                if (bannerRoutine != null) StopCoroutine(bannerRoutine);
                bannerRoutine = StartCoroutine(EpithetRoll(epithets.Split(','), core));
            }
            else
            {
                SetText(bossNameText, $"<size=38>{upper}</size>");
                epicBossTitle = false;
                ShowBanner(upper, "", 1.5f, BossRed);
            }
        }

        private IEnumerator EpithetRoll(string[] epithets, string core)
        {
            if (bannerGroup == null) yield break;
            SetText(bannerSubtitle, "");
            bannerGroup.alpha = 1f;
            RectTransform rt = bannerTitle.rectTransform;
            int n = epithets.Length;
            for (int i = 0; i < n; i++)
            {
                string word = epithets[i].Trim();
                if (word.Length == 0) continue;
                SetText(bannerTitle, word + "!");
                bannerTitle.color = Color.HSVToRGB(i / (float)n, 0.75f, 1f);
                yield return Punch(rt, 1.7f, 1f, 0.2f);
                yield return new WaitForSecondsRealtime(0.05f);
            }

            SetText(bannerTitle, $"<color={EmperorGreen}>{core}</color>");
            SetText(bannerSubtitle, "BOW BEFORE THE EMPEROR");
            GameEvents.RaiseScreenShake(0.35f, 0.5f);
            yield return Punch(rt, 2.4f, 1.25f, 0.35f);
            yield return new WaitForSecondsRealtime(1.6f);
            for (float t = 0; t < 0.3f; t += Time.unscaledDeltaTime)
            {
                bannerGroup.alpha = 1f - t / 0.3f;
                yield return null;
            }
            bannerGroup.alpha = 0f;
            rt.localScale = Vector3.one;
            bannerTitle.color = bannerDefaultColor;
            bannerRoutine = null;
        }

        private static IEnumerator Punch(RectTransform rt, float from, float to, float duration)
        {
            for (float t = 0; t < duration; t += Time.unscaledDeltaTime)
            {
                float k = 1f - Mathf.Pow(1f - t / duration, 3f); // ease-out cubic
                rt.localScale = Vector3.one * Mathf.Lerp(from, to, k);
                yield return null;
            }
            rt.localScale = Vector3.one * to;
        }

        private void Update()
        {
            // The emperor's title in the boss bar breathes.
            if (!epicBossTitle || bossNameText == null || bossBarRoot == null || !bossBarRoot.activeSelf) return;
            float pulse = 1f + Mathf.Sin(Time.unscaledTime * 5f) * 0.04f;
            bossNameText.rectTransform.localScale = Vector3.one * pulse;
        }

        private void OnBossHealth(float current, float max)
        {
            if (bossBarFill) bossBarFill.fillAmount = max > 0 ? current / max : 0f;
        }

        private void OnBossDefeated()
        {
            if (bossBarRoot) bossBarRoot.SetActive(false);
            epicBossTitle = false;
        }

        private void OnBossPhase(int phase, string label) => ShowBanner(label, "", 1.2f);

        private void OnPause(bool paused)
        {
            if (pausePanel) pausePanel.SetActive(paused);
        }

        private void OnGameOver()
        {
            if (bossBarRoot) bossBarRoot.SetActive(false);
            SetText(gameOverScoreText, $"SCORE  {lastScore:N0}");
            if (gameOverPanel) gameOverPanel.SetActive(true);
        }

        // ------------------------------------------------------------------ banner

        private void ShowBanner(string title, string subtitle, float hold, Color? titleColor = null)
        {
            if (bannerGroup == null) return;
            SetText(bannerTitle, title);
            bannerTitle.color = titleColor ?? bannerDefaultColor;
            bannerTitle.rectTransform.localScale = Vector3.one;
            SetText(bannerSubtitle, subtitle);
            if (bannerRoutine != null) StopCoroutine(bannerRoutine);
            bannerRoutine = StartCoroutine(BannerRoutine(hold));
        }

        private IEnumerator BannerRoutine(float hold)
        {
            const float fade = 0.25f;
            for (float t = 0; t < fade; t += Time.unscaledDeltaTime)
            {
                bannerGroup.alpha = t / fade;
                yield return null;
            }
            bannerGroup.alpha = 1f;
            yield return new WaitForSecondsRealtime(hold);
            for (float t = 0; t < fade; t += Time.unscaledDeltaTime)
            {
                bannerGroup.alpha = 1f - t / fade;
                yield return null;
            }
            bannerGroup.alpha = 0f;
            bannerRoutine = null;
        }

        private static void SetText(Text t, string value)
        {
            if (t != null) t.text = value;
        }
    }
}
