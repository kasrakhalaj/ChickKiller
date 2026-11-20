using ChickenShooter.Core;
using ChickenShooter.Pooling;
using UnityEngine;

namespace ChickenShooter.Combat
{
    /// <summary>
    /// Bomb that blinks while its fuse burns, then bursts into a ring of shards.
    /// Shooting it before the fuse ends defuses it (no shards).
    /// </summary>
    public class ClusterProjectile : EnemyProjectile
    {
        [SerializeField] private EnemyProjectile shardPrefab;
        [SerializeField] private int shardCount = 8;
        [SerializeField] private float shardSpeed = 4f;
        [SerializeField] private Color shardTint = Color.white;
        [SerializeField] private Vector2 fuseRange = new Vector2(1.1f, 1.6f);
        [Tooltip("Explodes early when it falls below this viewport height.")]
        [SerializeField] private float minViewportY = 0.2f;
        [SerializeField] private Color blinkColor = new Color(1f, 0.25f, 0.2f);
        [SerializeField] private GameObject burstVfx;

        private float fuse;

        public override void Launch(Vector2 velocity, Color tint)
        {
            base.Launch(velocity, tint);
            fuse = Random.Range(fuseRange.x, fuseRange.y);
        }

        protected override void Tick(float dt)
        {
            // Blink faster as the fuse runs out.
            float remaining = Mathf.Max(0.05f, fuse - age);
            bool on = Mathf.Sin(age * (6f + 18f / remaining)) > 0f;
            spriteRenderer.color = on ? blinkColor : baseTint;

            Rect cam = CameraBounds.WorldRect;
            bool tooLow = body.position.y < cam.yMin + cam.height * minViewportY;
            if (age >= fuse || tooLow) Burst();
        }

        private void Burst()
        {
            Vector3 pos = transform.position;
            if (shardPrefab != null)
            {
                float offset = Random.Range(0f, 360f);
                for (int i = 0; i < shardCount; i++)
                {
                    float a = offset + i * 360f / shardCount;
                    Vector2 dir = Quaternion.Euler(0f, 0f, a) * Vector2.up;
                    EnemyProjectile shard = PoolManager.Spawn(shardPrefab, pos, Quaternion.identity);
                    shard.Launch(dir * shardSpeed, shardTint);
                }
            }
            Vfx.Spawn(burstVfx, pos, 0.8f);
            GameEvents.RaiseScreenShake(0.08f, 0.12f);
            Break();
        }
    }
}
