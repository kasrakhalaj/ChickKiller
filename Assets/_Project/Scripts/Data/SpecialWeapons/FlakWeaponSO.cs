using ChickenShooter.Combat;
using ChickenShooter.Pooling;
using UnityEngine;

namespace ChickenShooter.Data
{
    /// <summary>Flak shotgun: a wide fan of pellets per shot.</summary>
    [CreateAssetMenu(fileName = "Special_FlakCannon", menuName = "Chick Killer/Special Weapons/Flak Cannon")]
    public class FlakWeaponSO : SpecialWeaponSO
    {
        public Projectile pelletPrefab;
        public Sprite pelletSprite;
        public float pelletScale = 0.5f;
        [Min(1)] public int pellets = 9;
        public float spread = 70f;
        public float damage = 0.9f;
        public float speed = 15f;
        public float cooldown = 0.5f;

        public override float Tick(SpecialWeaponContext ctx, bool held, float dt)
        {
            if (!held || !ctx.IsReady(this)) return 0f;
            ctx.StartCooldown(this, cooldown);
            for (int i = 0; i < pellets; i++)
            {
                float t = pellets == 1 ? 0.5f : i / (float)(pellets - 1);
                float angle = Mathf.Lerp(-spread * 0.5f, spread * 0.5f, t) + Random.Range(-3f, 3f);
                Vector2 dir = Quaternion.Euler(0f, 0f, angle) * Vector2.up;
                Projectile p = PoolManager.Spawn(pelletPrefab, ctx.Muzzle.position, Quaternion.identity);
                p.Launch(dir, speed * Random.Range(0.9f, 1.1f), damage, pelletSprite, color, pelletScale);
            }
            return 1f;
        }
    }
}
