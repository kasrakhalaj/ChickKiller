using System.Collections.Generic;
using ChickenShooter.Core;
using ChickenShooter.Pooling;
using UnityEngine;

namespace ChickenShooter.Combat
{
    /// <summary>
    /// Spinning saw disc: pierces through enemies (damaging each one at most every <see cref="hitCooldown"/>),
    /// ricochets off all four screen edges (the floor sits just above the ship zone) and expires after its lifetime.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class SawBlade : MonoBehaviour, IPoolable
    {
        [SerializeField] private float lifetime = 6f;
        [Tooltip("Viewport height of the invisible floor the saw bounces on.")]
        [SerializeField] private float floorViewportY = 0.28f;
        [SerializeField] private float spinSpeed = 900f;
        [SerializeField] private float hitCooldown = 0.15f;
        [SerializeField] private GameObject hitVfx;

        private readonly Dictionary<IDamageable, float> lastHit = new();
        private Rigidbody2D body;
        private TrailRenderer trail;
        private float damage;
        private float age;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            trail = GetComponentInChildren<TrailRenderer>();
        }

        public void Launch(Vector2 velocity, float dmg)
        {
            damage = dmg;
            body.linearVelocity = velocity;
            body.angularVelocity = spinSpeed;
        }

        private void FixedUpdate()
        {
            age += Time.fixedDeltaTime;
            if (age >= lifetime)
            {
                PoolManager.Despawn(gameObject);
                return;
            }

            // Bounce off left/right/top edges; fall away through the bottom.
            Rect r = CameraBounds.WorldRect;
            Vector2 p = body.position, v = body.linearVelocity;
            if ((p.x < r.xMin + 0.3f && v.x < 0f) || (p.x > r.xMax - 0.3f && v.x > 0f)) v.x = -v.x;
            if (p.y > r.yMax - 0.3f && v.y > 0f) v.y = -v.y;
            if (p.y < r.yMin + r.height * floorViewportY && v.y < 0f) v.y = -v.y;
            body.linearVelocity = v;
            if (CameraBounds.IsOutside(p, 2f)) PoolManager.Despawn(gameObject);
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            IDamageable target = other.GetComponentInParent<IDamageable>();
            if (target == null || !target.IsAlive) return;
            if (lastHit.TryGetValue(target, out float t) && Time.time - t < hitCooldown) return;
            lastHit[target] = Time.time;
            target.TakeDamage(damage);
            Vfx.Spawn(hitVfx, other.transform.position, 0.5f);
        }

        public void OnSpawned()
        {
            age = 0f;
            if (trail != null) trail.Clear();
            lastHit.Clear();
        }

        public void OnDespawned()
        {
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
        }
    }
}
