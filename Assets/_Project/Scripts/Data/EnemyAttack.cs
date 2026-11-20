using System;
using ChickenShooter.Combat;
using ChickenShooter.Player;
using ChickenShooter.Pooling;
using UnityEngine;

namespace ChickenShooter.Data
{
    /// <summary>
    /// Data-driven enemy attack: which projectile, how many per volley, spread, aimed or straight down,
    /// speed and the random interval between volleys. Leave <see cref="projectile"/> empty for enemies
    /// that never shoot (e.g. medics).
    /// </summary>
    [Serializable]
    public class EnemyAttack
    {
        public EnemyProjectile projectile;
        public Color tint = Color.white;
        [Min(1)] public int count = 1;
        [Tooltip("Total fan angle in degrees between the first and last projectile.")]
        public float spread;
        [Tooltip("Aim the volley at the player instead of straight down.")]
        public bool aimAtPlayer;
        public float speed = 3.5f;
        [Tooltip("Random seconds between volleys (min, max).")]
        public Vector2 interval = new Vector2(4f, 9f);

        public bool HasProjectile => projectile != null;

        public float NextInterval(float fireRateMultiplier) =>
            UnityEngine.Random.Range(interval.x, interval.y) / Mathf.Max(0.1f, fireRateMultiplier);

        /// <summary>Spawns one volley from <paramref name="origin"/>.</summary>
        public void Fire(Vector2 origin, float speedMultiplier = 1f)
        {
            if (projectile == null) return;

            Vector2 baseDir = Vector2.down;
            PlayerController player = PlayerController.Current;
            if (aimAtPlayer && player != null)
            {
                Vector2 toPlayer = player.Position - origin;
                if (toPlayer.sqrMagnitude > 0.01f) baseDir = toPlayer.normalized;
            }

            for (int i = 0; i < count; i++)
            {
                float t = count == 1 ? 0.5f : i / (float)(count - 1);
                float angle = Mathf.Lerp(-spread * 0.5f, spread * 0.5f, t);
                Vector2 dir = Quaternion.Euler(0f, 0f, angle) * baseDir;
                EnemyProjectile p = PoolManager.Spawn(projectile, origin, Quaternion.identity);
                p.Launch(dir * (speed * speedMultiplier), tint);
            }
        }
    }
}
