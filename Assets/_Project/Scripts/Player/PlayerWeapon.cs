using ChickenShooter.Combat;
using ChickenShooter.Core;
using ChickenShooter.Data;
using ChickenShooter.Pooling;
using UnityEngine;

namespace ChickenShooter.Player
{
    /// <summary>
    /// Main weapon. Fires while the fire action is held, using the volley pattern of the current
    /// power level (1 = single, 2 = twin parallel, 3 = triple spread) defined in WeaponDataSO.
    /// </summary>
    [RequireComponent(typeof(PlayerInputReader))]
    public class PlayerWeapon : MonoBehaviour
    {
        [SerializeField] private WeaponDataSO weapon;
        [SerializeField] private Transform muzzle;

        private PlayerInputReader input;
        private float nextFireTime;

        public WeaponDataSO Weapon => weapon;

        private void Awake() => input = GetComponent<PlayerInputReader>();

        private void Update()
        {
            GameManager gm = GameManager.Instance;
            if (gm == null || !gm.IsGameplayActive || !input.FireHeld) return;
            if (Time.time < nextFireTime) return;

            int level = gm.State.powerLevel;
            WeaponPowerLevel stats = weapon.GetLevel(level);
            FireVolley(stats);
            nextFireTime = Time.time + 1f / Mathf.Max(0.01f, stats.fireRate);
            GameEvents.RaiseWeaponFired(level);
        }

        private void FireVolley(WeaponPowerLevel stats)
        {
            Vector3 origin = muzzle != null ? muzzle.position : transform.position;
            foreach (ShotPattern shot in stats.shots)
            {
                Quaternion rot = Quaternion.Euler(0f, 0f, shot.angle);
                Vector3 pos = origin + (Vector3)shot.offset;
                Projectile p = PoolManager.Spawn(weapon.projectilePrefab, pos, rot);
                p.Launch(rot * Vector2.up, stats.projectileSpeed, stats.damage, stats.projectileSprite, stats.tint, stats.scale);
            }
        }
    }
}
