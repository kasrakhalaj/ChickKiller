using UnityEngine;

namespace ChickenShooter.Pooling
{
    /// <summary>
    /// Pooled particle effect: restarts on spawn and returns itself to the pool when the
    /// ParticleSystem stops (main.stopAction = Callback).
    /// </summary>
    [RequireComponent(typeof(ParticleSystem))]
    public class PooledParticle : MonoBehaviour, IPoolable
    {
        private ParticleSystem system;

        private void Awake()
        {
            system = GetComponent<ParticleSystem>();
            ParticleSystem.MainModule main = system.main;
            main.stopAction = ParticleSystemStopAction.Callback;
        }

        public void OnSpawned()
        {
            system.Clear(true);
            system.Play(true);
        }

        public void OnDespawned() { }

        private void OnParticleSystemStopped() => PoolManager.Despawn(gameObject);
    }
}
