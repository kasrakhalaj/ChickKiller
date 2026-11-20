using ChickenShooter.Combat;
using ChickenShooter.Pooling;
using UnityEngine;

namespace ChickenShooter.Data
{
    /// <summary>Saw launcher: piercing discs that bounce around the upper screen.</summary>
    [CreateAssetMenu(fileName = "Special_SawLauncher", menuName = "Chick Killer/Special Weapons/Saw Launcher")]
    public class SawWeaponSO : SpecialWeaponSO
    {
        public SawBlade sawPrefab;
        public float damage = 1.5f;
        public float speed = 9f;
        public float maxAngle = 30f;
        public float cooldown = 0.6f;

        public override float Tick(SpecialWeaponContext ctx, bool held, float dt)
        {
            if (!held || !ctx.IsReady(this)) return 0f;
            ctx.StartCooldown(this, cooldown);
            Vector2 dir = Quaternion.Euler(0f, 0f, Random.Range(-maxAngle, maxAngle)) * Vector2.up;
            SawBlade saw = PoolManager.Spawn(sawPrefab, ctx.Muzzle.position, Quaternion.identity);
            saw.Launch(dir * speed, damage);
            return 1f;
        }
    }
}
