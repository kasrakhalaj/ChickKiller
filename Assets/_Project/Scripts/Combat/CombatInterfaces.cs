using System.Collections.Generic;
using ChickenShooter.Pooling;
using UnityEngine;

namespace ChickenShooter.Combat
{
    /// <summary>Anything the player's projectiles can damage (chickens, eggs, asteroids, bosses).</summary>
    public interface IDamageable
    {
        bool IsAlive { get; }
        void TakeDamage(float amount);
    }

    /// <summary>Anything that costs the player a life on contact. Called after the player has taken the hit.</summary>
    public interface IPlayerHazard
    {
        /// <summary>False while the hazard is temporarily harmless (e.g. a downed zombie hen).</summary>
        bool IsHarmful { get; }
        void OnHitPlayer();
    }

    /// <summary>Anything affected by the screen-clearing missile.</summary>
    public interface IMissileTarget
    {
        /// <param name="bossDamageFraction">Fraction of max health applied to bosses (normal targets are insta-killed).</param>
        void OnMissileStrike(float bossDamageFraction);
    }

    /// <summary>
    /// Registry of active missile targets. Targets register in OnEnable / unregister in OnDisable
    /// (works with pooling), so the missile never needs FindObjectsOfType.
    /// </summary>
    public static class MissileTargetRegistry
    {
        private static readonly List<IMissileTarget> targets = new();

        public static void Register(IMissileTarget t)
        {
            if (!targets.Contains(t)) targets.Add(t);
        }

        public static void Unregister(IMissileTarget t) => targets.Remove(t);

        /// <summary>Strikes every target registered at the moment of detonation.</summary>
        public static int StrikeAll(float bossDamageFraction)
        {
            IMissileTarget[] snapshot = targets.ToArray();
            foreach (IMissileTarget t in snapshot) t.OnMissileStrike(bossDamageFraction);
            return snapshot.Length;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() => targets.Clear();
    }

    /// <summary>Spawns a pooled VFX prefab if one is assigned.</summary>
    public static class Vfx
    {
        public static void Spawn(GameObject prefab, Vector3 position, float scale = 1f)
        {
            if (prefab == null) return;
            GameObject go = PoolManager.Spawn(prefab, position, Quaternion.identity);
            go.transform.localScale = Vector3.one * scale;
        }
    }
}
