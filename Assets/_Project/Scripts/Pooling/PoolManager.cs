using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace ChickenShooter.Pooling
{
    /// <summary>Optional callbacks for components on pooled objects.</summary>
    public interface IPoolable
    {
        void OnSpawned();
        void OnDespawned();
    }

    /// <summary>
    /// Scene-local object pool registry keyed by prefab. Bullets, eggs, food, gift boxes, asteroids,
    /// chickens and particle effects are all spawned / recycled through here instead of Instantiate/Destroy.
    /// Lives in the scene (not DontDestroyOnLoad) so pooled objects are cleaned up with the scene.
    /// </summary>
    public class PoolManager : MonoBehaviour
    {
        private static PoolManager instance;

        private readonly Dictionary<GameObject, ObjectPool<PooledObject>> pools = new();
        private readonly Dictionary<GameObject, Transform> containers = new();

        public static PoolManager Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new GameObject("[PoolManager]").AddComponent<PoolManager>();
                }
                return instance;
            }
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        // ------------------------------------------------------------------ public API

        public static T Spawn<T>(T prefab, Vector3 position, Quaternion rotation) where T : Component
        {
            GameObject go = Spawn(prefab.gameObject, position, rotation);
            return go.GetComponent<T>();
        }

        public static GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            return Instance.SpawnInternal(prefab, position, rotation);
        }

        /// <summary>Returns an object to its pool (or destroys it if it was never pooled).</summary>
        public static void Despawn(GameObject go)
        {
            if (go == null) return;
            if (go.TryGetComponent(out PooledObject pooled) && pooled.Owner != null)
            {
                pooled.Release();
            }
            else
            {
                Destroy(go);
            }
        }

        public static void Prewarm(GameObject prefab, int count)
        {
            ObjectPool<PooledObject> pool = Instance.GetPool(prefab);
            var temp = new List<PooledObject>(count);
            for (int i = 0; i < count; i++) temp.Add(pool.Get());
            foreach (PooledObject p in temp) p.Release();
        }

        // ------------------------------------------------------------------ internals

        private GameObject SpawnInternal(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            PooledObject pooled = GetPool(prefab).Get();
            Transform t = pooled.transform;
            t.SetPositionAndRotation(position, rotation);
            pooled.gameObject.SetActive(true);
            pooled.NotifySpawned();
            return pooled.gameObject;
        }

        private ObjectPool<PooledObject> GetPool(GameObject prefab)
        {
            if (pools.TryGetValue(prefab, out ObjectPool<PooledObject> existing)) return existing;

            var container = new GameObject($"Pool<{prefab.name}>").transform;
            container.SetParent(transform, false);
            containers[prefab] = container;

            ObjectPool<PooledObject> pool = null;
            pool = new ObjectPool<PooledObject>(
                createFunc: () =>
                {
                    GameObject go = Instantiate(prefab, container);
                    go.name = prefab.name;
                    go.SetActive(false);
                    if (!go.TryGetComponent(out PooledObject po)) po = go.AddComponent<PooledObject>();
                    po.Bind(pool);
                    return po;
                },
                actionOnGet: po => po.MarkTaken(),
                actionOnRelease: po =>
                {
                    po.NotifyDespawned();
                    po.gameObject.SetActive(false);
                },
                actionOnDestroy: po => { if (po != null) Destroy(po.gameObject); },
                collectionCheck: false,
                defaultCapacity: 16,
                maxSize: 512);

            pools[prefab] = pool;
            return pool;
        }
    }
}
