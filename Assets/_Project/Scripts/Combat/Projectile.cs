using ChickenShooter.Core;
using ChickenShooter.Pooling;
using UnityEngine;

namespace ChickenShooter.Combat
{
    /// <summary>
    /// Pooled player projectile. Damages the first IDamageable it touches, then returns to the pool.
    /// One prefab serves several weapons: sprite, tint and scale are set per launch.
    /// Subclasses (homing rocket) override <see cref="Steer"/>.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(SpriteRenderer))]
    public class Projectile : MonoBehaviour, IPoolable
    {
        [SerializeField] protected GameObject hitVfx;
        [SerializeField] protected float hitVfxScale = 0.6f;

        protected Rigidbody2D body;
        protected SpriteRenderer spriteRenderer;
        protected TrailRenderer trail;
        protected float damage;
        protected float age;
        private Sprite defaultSprite;
        private Vector3 defaultScale;
        private float trailWidth = 1f;
        private bool consumed;

        protected virtual void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            spriteRenderer = GetComponent<SpriteRenderer>();
            trail = GetComponentInChildren<TrailRenderer>();
            if (trail != null) trailWidth = trail.widthMultiplier;
            defaultSprite = spriteRenderer.sprite;
            defaultScale = transform.localScale;
        }

        public void Launch(Vector2 direction, float speed, float dmg, Sprite sprite = null, Color? tint = null, float scale = 1f)
        {
            damage = dmg;
            spriteRenderer.sprite = sprite != null ? sprite : defaultSprite;
            spriteRenderer.color = tint ?? Color.white;
            if (trail != null)
            {
                Color c = tint ?? Color.white;
                trail.startColor = new Color(c.r, c.g, c.b, 0.8f);
                trail.endColor = new Color(c.r, c.g, c.b, 0f);
                trail.widthMultiplier = trailWidth * scale;
                trail.Clear();
            }
            transform.localScale = defaultScale * scale;
            body.linearVelocity = direction.normalized * speed;
            body.rotation = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
        }

        private void FixedUpdate()
        {
            age += Time.fixedDeltaTime;
            Steer(Time.fixedDeltaTime);
        }

        protected virtual void Steer(float dt) { }

        private void Update()
        {
            if (CameraBounds.IsOutside(transform.position, 1f)) PoolManager.Despawn(gameObject);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (consumed) return;
            IDamageable target = other.GetComponentInParent<IDamageable>();
            if (target == null || !target.IsAlive) return;

            consumed = true;
            target.TakeDamage(damage);
            Vfx.Spawn(hitVfx, transform.position, hitVfxScale);
            PoolManager.Despawn(gameObject);
        }

        public virtual void OnSpawned()
        {
            consumed = false;
            age = 0f;
        }

        public virtual void OnDespawned()
        {
            body.linearVelocity = Vector2.zero;
            if (trail != null) trail.Clear();
        }
    }
}
