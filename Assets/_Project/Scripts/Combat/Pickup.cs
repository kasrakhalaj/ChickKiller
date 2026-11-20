using ChickenShooter.Core;
using ChickenShooter.Pooling;
using UnityEngine;

namespace ChickenShooter.Combat
{
    /// <summary>
    /// Base class for falling collectibles (food, gift boxes). Pooled. Subclasses define the reward.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public abstract class Pickup : MonoBehaviour, IPoolable
    {
        [SerializeField] protected float fallSpeed = 2.2f;
        [SerializeField] protected float swayAmplitude = 0.6f;
        [SerializeField] protected float swayFrequency = 2f;
        [SerializeField] protected float spinSpeed = 120f;

        private Rigidbody2D body;
        private float phase;
        private bool collected;

        protected virtual void Awake() => body = GetComponent<Rigidbody2D>();

        private void FixedUpdate()
        {
            float vx = Mathf.Cos((Time.time + phase) * swayFrequency) * swayAmplitude;
            body.linearVelocity = new Vector2(vx, -fallSpeed);
            body.MoveRotation(body.rotation + spinSpeed * Time.fixedDeltaTime);
        }

        private void Update()
        {
            if (CameraBounds.IsBelow(transform.position, 1f)) PoolManager.Despawn(gameObject);
        }

        /// <summary>Called by the player when touching the pickup.</summary>
        public void Collect()
        {
            if (collected) return;
            collected = true;
            ApplyReward(GameManager.Instance);
            PoolManager.Despawn(gameObject);
        }

        protected abstract void ApplyReward(GameManager gm);

        public virtual void OnSpawned()
        {
            collected = false;
            phase = Random.value * 10f;
            body.rotation = Random.Range(0f, 360f);
        }

        public virtual void OnDespawned() => body.linearVelocity = Vector2.zero;
    }
}
