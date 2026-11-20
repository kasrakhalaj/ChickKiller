using System.Collections.Generic;
using ChickenShooter.Combat;
using UnityEngine;

namespace ChickenShooter.Data
{
    /// <summary>
    /// Runtime context handed to special weapons (they are shared assets, so per-player state such as
    /// cooldown timers lives here, not in the ScriptableObject).
    /// </summary>
    public class SpecialWeaponContext
    {
        private readonly Dictionary<SpecialWeaponSO, float> readyTimes = new();
        private readonly Dictionary<SpecialWeaponSO, float> accumulators = new();

        public Transform Muzzle { get; }
        public LaserBeam Beam { get; }

        public SpecialWeaponContext(Transform muzzle, LaserBeam beam)
        {
            Muzzle = muzzle;
            Beam = beam;
        }

        public bool IsReady(SpecialWeaponSO w) => !readyTimes.TryGetValue(w, out float t) || Time.time >= t;
        public void StartCooldown(SpecialWeaponSO w, float seconds) => readyTimes[w] = Time.time + seconds;

        public float GetAccumulator(SpecialWeaponSO w) => accumulators.TryGetValue(w, out float v) ? v : 0f;
        public void SetAccumulator(SpecialWeaponSO w, float v) => accumulators[w] = v;
    }

    /// <summary>
    /// Limited-ammo special weapon (Strategy pattern). Each subclass implements its own firing behaviour;
    /// the player component only calls <see cref="Tick"/> for the selected weapon and subtracts the ammo it
    /// reports. Adding a new special weapon = one new subclass + one asset, no changes elsewhere.
    /// </summary>
    public abstract class SpecialWeaponSO : ScriptableObject
    {
        public string displayName = "Special";
        public Sprite icon;
        public Color color = Color.white;
        [Tooltip("Ammo granted by one pickup (shots, or seconds for continuous weapons).")]
        public float ammoPerPickup = 10f;
        public float maxAmmo = 30f;
        [Tooltip("Ammo is measured in seconds of firing instead of shots.")]
        public bool continuous;

        /// <summary>Called every frame while this weapon is selected. Returns the ammo consumed this frame.</summary>
        public abstract float Tick(SpecialWeaponContext ctx, bool triggerHeld, float dt);

        /// <summary>Called when the trigger is released or the weapon is deselected / empty.</summary>
        public virtual void Release(SpecialWeaponContext ctx) { }

        public string FormatAmmo(float ammo) => continuous ? $"{ammo:0.0}s" : Mathf.CeilToInt(ammo).ToString();
    }
}
