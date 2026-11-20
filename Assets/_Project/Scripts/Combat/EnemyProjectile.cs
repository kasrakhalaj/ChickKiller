using ChickenShooter.Core;
using ChickenShooter.Pooling;
using UnityEngine;

namespace ChickenShooter.Combat
{
    /// <summary>
    /// Base enemy projectile (eggs, needles, sniper bolts, shards). Pooled; hurts the player on contact;
    /// can be shot down in the air; wiped by the missile. Subclasses add steering (homing) or
    /// fuse behaviour (cluster bombs) by overriding <see cref="Tick"/>.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(SpriteRenderer))]
    public class EnemyProjectile : MonoBehaviour, IDamageable, IPlayerHazard, IMissileTarget, IPoolable
    {
        [SerializeField] protected GameObject breakVfx;
        [SerializeField] protected int scoreWhenShot = 10;
        [SerializeField] protected bool shootable = true;
        [Tooltip("Rotate the sprite to face its velocity (needles, bolts). Otherwise it spins.")]
        [SerializeField] protected bool alignToVelocity;
        [SerializeField] protected Vector2 spinRange = new Vector2(-90f, 90f);

        protected Rigidbody2D body;
        protected SpriteRenderer spriteRenderer;
        protected Color baseTint = Color.white;
        protected float age;
        private bool alive;

        public bool IsAlive => alive && shootable;
        public bool IsHarmful => alive;

        protected virtual void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        private void OnEnable() => MissileTargetRegistry.Register(this);
        private void OnDisable() => MissileTargetRegistry.Unregister(this);

        public virtual void Launch(Vector2 velocity, Color tint)
        {
            baseTint = tint;
            spriteRenderer.color = tint;
            body.linearVelocity = velocity;
            if (alignToVelocity) AlignTo(velocity);
            else body.angularVelocity = Random.Range(spinRange.x, spinRange.y);
        }

        private void FixedUpdate()
        {
            if (!alive) return;
            age += Time.fixedDeltaTime;
            Tick(Time.fixedDeltaTime);
            if (alignToVelocity) AlignTo(body.linearVelocity);
        }

        /// <summary>Per-physics-step behaviour hook for subclasses.</summary>
        protected virtual void Tick(float dt) { }

        private void Update()
        {
            if (alive && CameraBounds.IsOutside(transform.position, 1f)) PoolManager.Despawn(gameObject);
        }

        protected void AlignTo(Vector2 v)
        {
            if (v.sqrMagnitude < 0.0001f) return;
            body.rotation = Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg + 90f;
        }

        public void TakeDamage(float amount)
        {
            if (!IsAlive) return;
            GameManager.Instance?.AddScore(scoreWhenShot);
            OnShotDown();
        }

        /// <summary>Called when the player shoots this projectile out of the air.</summary>
        protected virtual void OnShotDown() => Break();

        public void OnHitPlayer() => Break();

        public void OnMissileStrike(float bossDamageFraction) => Break();

        protected void Break()
        {
            if (!alive) return;
            alive = false;
            Vfx.Spawn(breakVfx, transform.position, 0.6f);
            PoolManager.Despawn(gameObject);
        }

        public virtual void OnSpawned()
        {
            alive = true;
            age = 0f;
        }

        public virtual void OnDespawned()
        {
            alive = false;
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            body.rotation = 0f;
        }
    }
}
