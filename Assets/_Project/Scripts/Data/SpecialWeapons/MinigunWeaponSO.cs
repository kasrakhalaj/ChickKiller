using ChickenShooter.Combat;
using ChickenShooter.Pooling;
using UnityEngine;

namespace ChickenShooter.Data
{
    /// <summary>Bee minigun: very fast, slightly inaccurate stream of small bullets (1 ammo per bullet).</summary>
    [CreateAssetMenu(fileName = "Special_BeeMinigun", menuName = "Chick Killer/Special Weapons/Minigun")]
    public class MinigunWeaponSO : SpecialWeaponSO
    {
        public Projectile bulletPrefab;
        public Sprite bulletSprite;
        public float bulletScale = 0.7f;
        public float fireRate = 22f;
        public float damage = 0.6f;
        public float speed = 19f;
        public float jitterAngle = 6f;

        public override float Tick(SpecialWeaponContext ctx, bool held, float dt)
        {
            if (!held)
            {
                ctx.SetAccumulator(this, 0f);
                return 0f;
            }

            float acc = ctx.GetAccumulator(this) + dt * fireRate;
            int shots = Mathf.FloorToInt(acc);
            ctx.SetAccumulator(this, acc - shots);
            for (int i = 0; i < shots; i++)
            {
                Vector2 dir = Quaternion.Euler(0f, 0f, Random.Range(-jitterAngle, jitterAngle)) * Vector2.up;
                Vector3 pos = ctx.Muzzle.position + new Vector3(Random.Range(-0.12f, 0.12f), 0f);
                Projectile p = PoolManager.Spawn(bulletPrefab, pos, Quaternion.identity);
                p.Launch(dir, speed, damage, bulletSprite, color, bulletScale);
            }
            return shots;
        }
    }
}
