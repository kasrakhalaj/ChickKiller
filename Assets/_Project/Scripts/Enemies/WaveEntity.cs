using System;
using System.Collections.Generic;
using ChickenShooter.Pooling;
using UnityEngine;

namespace ChickenShooter.Enemies
{
    /// <summary>
    /// Base for anything a wave runner must wait for (chickens, asteroids, free agents, bosses).
    /// Raises <see cref="Removed"/> exactly once when it dies or leaves the play field.
    /// Also keeps a registry of active entities (used by homing weapons to pick targets).
    /// </summary>
    public abstract class WaveEntity : MonoBehaviour, IPoolable
    {
        private static readonly List<WaveEntity> active = new();

        /// <summary>Entities currently on the battlefield.</summary>
        public static IReadOnlyList<WaveEntity> Active => active;

        public event Action<WaveEntity> Removed;

        private bool removed;

        /// <summary>Raises Removed (once) and returns the object to its pool.</summary>
        protected void Remove()
        {
            if (removed) return;
            removed = true;
            active.Remove(this);
            Action<WaveEntity> handler = Removed;
            Removed = null;
            handler?.Invoke(this);
            PoolManager.Despawn(gameObject);
        }

        public virtual void OnSpawned()
        {
            removed = false;
            if (!active.Contains(this)) active.Add(this);
        }

        public virtual void OnDespawned()
        {
            Removed = null;
            active.Remove(this);
        }

        private void OnDestroy() => active.Remove(this);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRegistry() => active.Clear();
    }
}
