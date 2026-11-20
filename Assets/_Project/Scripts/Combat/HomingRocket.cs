using ChickenShooter.Enemies;
using UnityEngine;

namespace ChickenShooter.Combat
{
    /// <summary>Player rocket that locks on to the nearest enemy and steers toward it.</summary>
    public class HomingRocket : Projectile
    {
        [SerializeField] private float turnRate = 300f;      // degrees per second
        [SerializeField] private float armDelay = 0.12f;     // fly straight briefly before homing
        [SerializeField] private float acceleration = 10f;
        [SerializeField] private float maxSpeed = 16f;
        [SerializeField] private float retargetInterval = 0.25f;

        private Transform target;
        private float retargetTimer;

        protected override void Steer(float dt)
        {
            Vector2 v = body.linearVelocity;
            float speed = Mathf.Min(maxSpeed, v.magnitude + acceleration * dt);

            if (age > armDelay)
            {
                retargetTimer -= dt;
                if (retargetTimer <= 0f || target == null || !target.gameObject.activeInHierarchy)
                {
                    target = FindTarget();
                    retargetTimer = retargetInterval;
                }

                if (target != null)
                {
                    Vector2 desired = ((Vector2)target.position - body.position).normalized * speed;
                    v = Vector3.RotateTowards(v, desired, turnRate * Mathf.Deg2Rad * dt, 0f);
                }
            }

            body.linearVelocity = v.normalized * speed;
            body.rotation = Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg - 90f;
        }

        private Transform FindTarget()
        {
            Transform best = null;
            float bestScore = float.MaxValue;
            foreach (WaveEntity e in WaveEntity.Active)
            {
                if (e == null || !(e is IDamageable d) || !d.IsAlive) continue;
                Vector2 to = (Vector2)e.transform.position - body.position;
                if (to.y < -1f) continue; // ignore enemies well behind the rocket
                float score = to.sqrMagnitude;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = e.transform;
                }
            }
            return best;
        }

        public override void OnSpawned()
        {
            base.OnSpawned();
            target = null;
            retargetTimer = 0f;
        }
    }
}
