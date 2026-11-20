using System;
using ChickenShooter.Combat;
using UnityEngine;

namespace ChickenShooter.Data
{
    /// <summary>One projectile of a volley: spawn offset from the muzzle and firing angle (0 = straight up).</summary>
    [Serializable]
    public struct ShotPattern
    {
        public Vector2 offset;
        public float angle;

        public ShotPattern(float x, float y, float angle)
        {
            offset = new Vector2(x, y);
            this.angle = angle;
        }
    }

    /// <summary>Stats of the weapon at one power level.</summary>
    [Serializable]
    public class WeaponPowerLevel
    {
        public string label = "Single Laser";
        public Sprite projectileSprite;
        public Color tint = Color.white;
        public float scale = 1f;
        [Min(0.1f)] public float damage = 1f;
        public float projectileSpeed = 16f;
        [Tooltip("Volleys per second while the fire button is held.")]
        public float fireRate = 7f;
        public ShotPattern[] shots = { new ShotPattern(0f, 0.5f, 0f) };
    }

    /// <summary>
    /// Weapon definition: name, icon, projectile prefab and one entry per power level
    /// (damage, speed, fire rate and firing pattern).
    /// </summary>
    [CreateAssetMenu(fileName = "Weapon", menuName = "Chick Killer/Weapon Data")]
    public class WeaponDataSO : ScriptableObject
    {
        public string weaponName = "Ion Blaster";
        public Sprite icon;
        public Projectile projectilePrefab;
        public WeaponPowerLevel[] powerLevels;

        public int MaxLevel => powerLevels?.Length ?? 0;

        /// <param name="level">1-based power level.</param>
        public WeaponPowerLevel GetLevel(int level)
        {
            return powerLevels[Mathf.Clamp(level - 1, 0, powerLevels.Length - 1)];
        }
    }
}
