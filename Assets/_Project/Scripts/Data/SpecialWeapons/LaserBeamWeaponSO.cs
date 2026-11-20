using ChickenShooter.Combat;
using ChickenShooter.Pooling;
using UnityEngine;

namespace ChickenShooter.Data
{
    /// <summary>Continuous piercing laser beam (ammo = seconds of firing).</summary>
    [CreateAssetMenu(fileName = "Special_LaserBeam", menuName = "Chick Killer/Special Weapons/Laser Beam")]
    public class LaserBeamWeaponSO : SpecialWeaponSO
    {
        public float damagePerSecond = 14f;

        public override float Tick(SpecialWeaponContext ctx, bool held, float dt)
        {
            if (!held || ctx.Beam == null)
            {
                Release(ctx);
                return 0f;
            }
            ctx.Beam.Tick(damagePerSecond, color, dt);
            return dt;
        }

        public override void Release(SpecialWeaponContext ctx)
        {
            if (ctx.Beam != null && ctx.Beam.IsOn) ctx.Beam.SetOn(false);
        }
    }
}
