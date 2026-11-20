using ChickenShooter.Combat;
using ChickenShooter.Core;
using ChickenShooter.Pooling;
using UnityEngine;

namespace ChickenShooter.Player
{
    /// <summary>Secondary weapon: launches the screen-clearing missile on the dedicated key (X).</summary>
    [RequireComponent(typeof(PlayerInputReader))]
    public class MissileLauncher : MonoBehaviour
    {
        [SerializeField] private Missile missilePrefab;
        [Tooltip("Fraction of a boss's max health removed by one missile (spec: 15-20%).")]
        [Range(0.15f, 0.2f)] [SerializeField] private float bossDamageFraction = 0.18f;
        [SerializeField] private float cooldown = 0.8f;

        private PlayerInputReader input;
        private PlayerController controller;
        private float readyTime;

        private void Awake()
        {
            input = GetComponent<PlayerInputReader>();
            controller = GetComponent<PlayerController>();
        }

        private void OnEnable() => input.MissilePressed += TryLaunch;
        private void OnDisable() => input.MissilePressed -= TryLaunch;

        private void TryLaunch()
        {
            if (Time.time < readyTime || !controller.ControlsEnabled) return;
            GameManager gm = GameManager.Instance;
            if (gm == null || !gm.TryUseMissile()) return;

            readyTime = Time.time + cooldown;
            Missile m = PoolManager.Spawn(missilePrefab, transform.position + Vector3.up * 0.6f, Quaternion.identity);
            m.Launch(bossDamageFraction);
        }
    }
}
