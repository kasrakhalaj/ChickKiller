using System;
using ChickenShooter.Data;
using UnityEngine;

namespace ChickenShooter.Core
{
    /// <summary>Snapshot of the round that just started (sent to UI / audio / save systems).</summary>
    public readonly struct RoundInfo
    {
        public readonly int LevelNumber;
        public readonly int RoundNumber;   // 1-based
        public readonly int TotalRounds;
        public readonly WaveType WaveType;
        public readonly string Label;

        public RoundInfo(int levelNumber, int roundNumber, int totalRounds, WaveType waveType, string label)
        {
            LevelNumber = levelNumber;
            RoundNumber = roundNumber;
            TotalRounds = totalRounds;
            WaveType = waveType;
            Label = label;
        }
    }

    /// <summary>
    /// Central event hub (C# events). Gameplay systems raise events, UI / audio / VFX listen.
    /// No system holds a direct reference to the UI, which keeps them decoupled.
    /// </summary>
    public static class GameEvents
    {
        // ---- Player stats ----
        public static event Action<int> ScoreChanged;
        public static event Action<int> LivesChanged;
        public static event Action<int> MissilesChanged;
        public static event Action<int, int> PowerLevelChanged;      // (level, maxLevel)
        public static event Action<float> FoodProgressChanged;       // normalized 0..1
        public static event Action<SpecialWeaponSO, float> SpecialWeaponChanged; // (selected or null, ammo)
        public static event Action<SpecialWeaponSO> SpecialWeaponAcquired;      // crate picked up

        // ---- Player state ----
        public static event Action PlayerDamaged;
        public static event Action PlayerDied;
        public static event Action GameOver;
        public static event Action<bool> PauseChanged;

        // ---- Flow ----
        public static event Action<LevelDataSO> LevelStarted;
        public static event Action<RoundInfo> RoundStarted;
        public static event Action<int> RoundCleared;                // 1-based round number
        public static event Action<LevelDataSO> LevelCompleted;
        public static event Action GameCompleted;

        // ---- Combat ----
        public static event Action<Vector3, int> EnemyKilled;        // (position, score)
        public static event Action<int> WeaponFired;                 // power level used
        public static event Action<Vector3> MissileDetonated;
        public static event Action<float, float> ScreenShakeRequested; // (intensity, duration)

        // ---- Boss ----
        public static event Action<string> BossSpawned;
        public static event Action<float, float> BossHealthChanged;  // (current, max)
        public static event Action BossDefeated;
        public static event Action<int, string> BossPhaseChanged;     // (phase index, label)

        public static void RaiseScoreChanged(int v) => ScoreChanged?.Invoke(v);
        public static void RaiseLivesChanged(int v) => LivesChanged?.Invoke(v);
        public static void RaiseMissilesChanged(int v) => MissilesChanged?.Invoke(v);
        public static void RaisePowerLevelChanged(int level, int max) => PowerLevelChanged?.Invoke(level, max);
        public static void RaiseFoodProgressChanged(float v) => FoodProgressChanged?.Invoke(v);
        public static void RaiseSpecialWeaponChanged(SpecialWeaponSO w, float ammo) => SpecialWeaponChanged?.Invoke(w, ammo);
        public static void RaiseSpecialWeaponAcquired(SpecialWeaponSO w) => SpecialWeaponAcquired?.Invoke(w);

        public static void RaisePlayerDamaged() => PlayerDamaged?.Invoke();
        public static void RaisePlayerDied() => PlayerDied?.Invoke();
        public static void RaiseGameOver() => GameOver?.Invoke();
        public static void RaisePauseChanged(bool paused) => PauseChanged?.Invoke(paused);

        public static void RaiseLevelStarted(LevelDataSO level) => LevelStarted?.Invoke(level);
        public static void RaiseRoundStarted(RoundInfo info) => RoundStarted?.Invoke(info);
        public static void RaiseRoundCleared(int round) => RoundCleared?.Invoke(round);
        public static void RaiseLevelCompleted(LevelDataSO level) => LevelCompleted?.Invoke(level);
        public static void RaiseGameCompleted() => GameCompleted?.Invoke();

        public static void RaiseEnemyKilled(Vector3 pos, int score) => EnemyKilled?.Invoke(pos, score);
        public static void RaiseWeaponFired(int level) => WeaponFired?.Invoke(level);
        public static void RaiseMissileDetonated(Vector3 pos) => MissileDetonated?.Invoke(pos);
        public static void RaiseScreenShake(float intensity, float duration) => ScreenShakeRequested?.Invoke(intensity, duration);

        public static void RaiseBossSpawned(string name) => BossSpawned?.Invoke(name);
        public static void RaiseBossHealthChanged(float cur, float max) => BossHealthChanged?.Invoke(cur, max);
        public static void RaiseBossDefeated() => BossDefeated?.Invoke();
        public static void RaiseBossPhaseChanged(int phase, string label) => BossPhaseChanged?.Invoke(phase, label);

        /// <summary>Clears all subscribers when entering play mode (safe with domain reload disabled).</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetAll()
        {
            ScoreChanged = null; LivesChanged = null; MissilesChanged = null;
            PowerLevelChanged = null; FoodProgressChanged = null; SpecialWeaponChanged = null; SpecialWeaponAcquired = null;
            PlayerDamaged = null; PlayerDied = null; GameOver = null; PauseChanged = null;
            LevelStarted = null; RoundStarted = null; RoundCleared = null;
            LevelCompleted = null; GameCompleted = null;
            EnemyKilled = null; WeaponFired = null; MissileDetonated = null; ScreenShakeRequested = null;
            BossSpawned = null; BossHealthChanged = null; BossDefeated = null; BossPhaseChanged = null;
        }
    }
}
