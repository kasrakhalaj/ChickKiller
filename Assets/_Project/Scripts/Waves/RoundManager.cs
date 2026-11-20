using System.Collections;
using ChickenShooter.Core;
using ChickenShooter.Data;
using ChickenShooter.Enemies;
using UnityEngine;

namespace ChickenShooter.Waves
{
    /// <summary>
    /// Runs the rounds of the current level in order: announce -> spawn via the matching IWaveRunner ->
    /// wait until cleared -> next round. After the final (boss) round the GameManager loads the next level.
    /// Stops immediately on game over.
    /// </summary>
    public class RoundManager : MonoBehaviour
    {
        [SerializeField] private float roundIntroDelay = 2.2f;
        [SerializeField] private float roundOutroDelay = 1.2f;

        private Coroutine routine;
        private LevelDataSO level;

        public int CurrentRoundIndex { get; private set; }

        private void OnEnable() => GameEvents.GameOver += StopRounds;
        private void OnDisable() => GameEvents.GameOver -= StopRounds;

        public void Begin(LevelDataSO levelData, int startRoundIndex)
        {
            level = levelData;
            if (routine != null) StopCoroutine(routine);
            routine = StartCoroutine(RunLevel(startRoundIndex));
        }

        private IEnumerator RunLevel(int startRound)
        {
            for (int i = startRound; i < level.RoundCount; i++)
            {
                CurrentRoundIndex = i;
                RoundDefinition round = level.rounds[i];
                if (round.wave == null)
                {
                    Debug.LogWarning($"[RoundManager] {level.name} round {i + 1} has no wave assigned.");
                    continue;
                }

                GameManager.Instance.BeginRound(i); // checkpoint (auto-save in Phase 2)
                GameEvents.RaiseRoundStarted(new RoundInfo(level.levelNumber, i + 1, level.RoundCount,
                                                           round.wave.waveType, round.wave.label));

                yield return new WaitForSeconds(roundIntroDelay);

                IWaveRunner runner = WaveRunnerFactory.Create(round.wave.waveType, this);
                yield return runner.Run(round.wave, new WaveModifiers(round));

                GameEvents.RaiseRoundCleared(i + 1);
                yield return new WaitForSeconds(roundOutroDelay);
            }

            routine = null;
            GameManager.Instance.CompleteLevel();
        }

        private void StopRounds()
        {
            // Also stops pending free-agent spawn timers started by the wave runners.
            StopAllCoroutines();
            routine = null;
        }
    }
}
