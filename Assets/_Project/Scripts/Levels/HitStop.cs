using System.Collections;
using ChickenShooter.Core;
using UnityEngine;

namespace ChickenShooter.Levels
{
    /// <summary>
    /// "Hit-stop" juice: freezes time for a few frames on heavy impacts (player hit, elite kills, missile,
    /// boss defeat). Event-driven; never interferes with the pause menu.
    /// </summary>
    public class HitStop : MonoBehaviour
    {
        [SerializeField] private float playerHit = 0.12f;
        [SerializeField] private float eliteKill = 0.04f;
        [SerializeField] private int eliteScoreThreshold = 300;
        [SerializeField] private float missile = 0.08f;
        [SerializeField] private float bossDefeat = 0.3f;
        [SerializeField] private float slowScale = 0.05f;

        private Coroutine routine;
        private float until;

        private void OnEnable()
        {
            GameEvents.PlayerDamaged += OnPlayerDamaged;
            GameEvents.EnemyKilled += OnEnemyKilled;
            GameEvents.MissileDetonated += OnMissile;
            GameEvents.BossDefeated += OnBossDefeated;
        }

        private void OnDisable()
        {
            GameEvents.PlayerDamaged -= OnPlayerDamaged;
            GameEvents.EnemyKilled -= OnEnemyKilled;
            GameEvents.MissileDetonated -= OnMissile;
            GameEvents.BossDefeated -= OnBossDefeated;
            if (routine != null && !IsPaused) Time.timeScale = 1f;
        }

        private static bool IsPaused => GameManager.Instance != null && GameManager.Instance.IsPaused;

        private void OnPlayerDamaged() => Stop(playerHit);
        private void OnEnemyKilled(Vector3 _, int score) { if (score >= eliteScoreThreshold) Stop(eliteKill); }
        private void OnMissile(Vector3 _) => Stop(missile);
        private void OnBossDefeated() => Stop(bossDefeat);

        private void Stop(float duration)
        {
            if (IsPaused || duration <= 0f) return;
            until = Mathf.Max(until, Time.unscaledTime + duration);
            if (routine == null) routine = StartCoroutine(StopRoutine());
        }

        private IEnumerator StopRoutine()
        {
            Time.timeScale = slowScale;
            while (Time.unscaledTime < until && !IsPaused) yield return null;
            if (!IsPaused) Time.timeScale = 1f;
            routine = null;
        }
    }
}
