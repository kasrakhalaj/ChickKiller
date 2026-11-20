using ChickenShooter.Player;
using UnityEngine;

namespace ChickenShooter.Combat
{
    /// <summary>Slow slime orb that steers toward the player for a limited time, then fizzles out.</summary>
    public class HomingProjectile : EnemyProjectile
    {
        [SerializeField] private float turnRate = 110f;       // degrees per second
        [SerializeField] private float homingDuration = 2.2f;
        [SerializeField] private float lifetime = 6f;
        [SerializeField] private float pulseSpeed = 9f;
        [SerializeField] private float pulseAmount = 0.12f;

        private Vector3 baseScale;

        protected override void Awake()
        {
            base.Awake();
            baseScale = transform.localScale;
        }

        protected override void Tick(float dt)
        {
            PlayerController player = PlayerController.Current;
            if (player != null && age < homingDuration)
            {
                Vector2 v = body.linearVelocity;
                Vector2 desired = (player.Position - body.position).normalized * v.magnitude;
                body.linearVelocity = Vector3.RotateTowards(v, desired, turnRate * Mathf.Deg2Rad * dt, 0f);
            }

            transform.localScale = baseScale * (1f + Mathf.Sin(age * pulseSpeed) * pulseAmount);
            if (age >= lifetime) Break();
        }

        public override void OnDespawned()
        {
            base.OnDespawned();
            transform.localScale = baseScale;
        }
    }
}
