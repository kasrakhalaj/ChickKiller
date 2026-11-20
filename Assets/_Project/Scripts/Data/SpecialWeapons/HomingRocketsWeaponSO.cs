using ChickenShooter.Combat;
using ChickenShooter.Pooling;
using UnityEngine;

namespace ChickenShooter.Data
{
    /// <summary>Twin homing rockets: each shot launches a pair that lock on to the nearest enemies.</summary>
    [CreateAssetMenu(fileName = "Special_TwinRockets", menuName = "Chick Killer/Special Weapons/Homing Rockets")]
    public class HomingRocketsWeaponSO : SpecialWeaponSO
    {
        public HomingRocket rocketPrefab;
        [Min(1)] public int rocketsPerShot = 2;
        public float damage = 3f;
        public float launchSpeed = 6f;
        public float splayAngle = 25f;
        public float cooldown = 0.35f;

        public override float Tick(SpecialWeaponContext ctx, bool held, float dt)
        {
            if (!held || !ctx.IsReady(this)) return 0f;
            ctx.StartCooldown(this, cooldown);
            for (int i = 0; i < rocketsPerShot; i++)
            {
                float t = rocketsPerShot == 1 ? 0.5f : i / (float)(rocketsPerShot - 1);
                float angle = Mathf.Lerp(splayAngle, -splayAngle, t);
                Vector2 dir = Quaternion.Euler(0f, 0f, angle) * Vector2.up;
                Vector3 pos = ctx.Muzzle.position + new Vector3(Mathf.Lerp(-0.35f, 0.35f, t), 0f);
                HomingRocket r = PoolManager.Spawn(rocketPrefab, pos, Quaternion.identity);
                r.Launch(dir, launchSpeed, damage, null, Color.Lerp(color, Color.white, 0.4f));
            }
            return 1f;
        }
    }
}
