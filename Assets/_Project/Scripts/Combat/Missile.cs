using ChickenShooter.Core;
using ChickenShooter.Pooling;
using UnityEngine;

namespace ChickenShooter.Combat
{
    /// <summary>
    /// Screen-clearing super missile. Flies upward for a short time, then detonates:
    /// every registered IMissileTarget is struck (normal enemies, eggs and boss minions die instantly,
    /// bosses lose a fixed fraction of their max health).
    /// </summary>
    public class Missile : MonoBehaviour, IPoolable
    {
        [SerializeField] private float flightTime = 0.55f;
        [SerializeField] private float startSpeed = 4f;
        [SerializeField] private float acceleration = 22f;
        [SerializeField] private GameObject shockwaveVfx;
        [SerializeField] private GameObject explosionVfx;

        private float bossDamageFraction;
        private float timer;
        private float speed;
        private bool detonated;

        public bool IsInFlight => !detonated && gameObject.activeInHierarchy;

        public void Launch(float bossFraction)
        {
            bossDamageFraction = bossFraction;
        }

        private void Update()
        {
            if (detonated) return;
            timer += Time.deltaTime;
            speed += acceleration * Time.deltaTime;
            transform.position += Vector3.up * (speed * Time.deltaTime);

            Rect cam = CameraBounds.WorldRect;
            if (timer >= flightTime || transform.position.y > cam.center.y + cam.height * 0.25f) Detonate();
        }

        private void Detonate()
        {
            detonated = true;
            Vector3 pos = transform.position;
            Vfx.Spawn(shockwaveVfx, pos);
            Vfx.Spawn(explosionVfx, pos, 2f);
            MissileTargetRegistry.StrikeAll(bossDamageFraction);
            GameEvents.RaiseMissileDetonated(pos);
            GameEvents.RaiseScreenShake(0.45f, 0.5f);
            PoolManager.Despawn(gameObject);
        }

        public void OnSpawned()
        {
            timer = 0f;
            speed = startSpeed;
            detonated = false;
        }

        public void OnDespawned() { }
    }
}
