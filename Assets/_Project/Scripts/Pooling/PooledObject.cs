using UnityEngine;
using UnityEngine.Pool;

namespace ChickenShooter.Pooling
{
    /// <summary>
    /// Added automatically to every pooled instance. Remembers its owning pool, guards against
    /// double-release (e.g. a bullet touching two eggs in the same physics step) and forwards
    /// spawn/despawn callbacks to IPoolable components.
    /// </summary>
    [DisallowMultipleComponent]
    public class PooledObject : MonoBehaviour
    {
        private IPoolable[] poolables;
        private bool inPool = true;

        public IObjectPool<PooledObject> Owner { get; private set; }
        public bool IsInPool => inPool;

        public void Bind(IObjectPool<PooledObject> owner)
        {
            Owner = owner;
            poolables = GetComponentsInChildren<IPoolable>(true);
        }

        public void MarkTaken() => inPool = false;

        public void Release()
        {
            if (inPool || Owner == null) return;
            inPool = true;
            Owner.Release(this);
        }

        public void NotifySpawned()
        {
            if (poolables == null) return;
            foreach (IPoolable p in poolables) p.OnSpawned();
        }

        public void NotifyDespawned()
        {
            if (poolables == null) return;
            foreach (IPoolable p in poolables) p.OnDespawned();
        }
    }
}
