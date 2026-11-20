using ChickenShooter.Combat;
using ChickenShooter.Core;
using ChickenShooter.Data;
using UnityEngine;

namespace ChickenShooter.Player
{
    /// <summary>
    /// Fires the player's single limited-ammo special weapon (right mouse / C). While a special weapon is
    /// equipped, a pulsing aura in the weapon's colour surrounds the ship (and flares when one is picked up),
    /// so it is always clear which weapon is loaded.
    /// </summary>
    [RequireComponent(typeof(PlayerInputReader))]
    public class PlayerSpecialWeapons : MonoBehaviour
    {
        [SerializeField] private Transform muzzle;
        [SerializeField] private LaserBeam beam;
        [SerializeField] private SpriteRenderer aura;
        [SerializeField] private float auraAlpha = 0.45f;
        [SerializeField] private float auraPulseSpeed = 5f;
        [SerializeField] private GameObject acquireVfx;

        private PlayerInputReader input;
        private PlayerController controller;
        private SpecialWeaponContext context;
        private SpecialWeaponSO lastWeapon;
        private Vector3 auraBaseScale;
        private float flare;

        private void Awake()
        {
            input = GetComponent<PlayerInputReader>();
            controller = GetComponent<PlayerController>();
            context = new SpecialWeaponContext(muzzle != null ? muzzle : transform, beam);
            if (aura != null)
            {
                auraBaseScale = aura.transform.localScale;
                aura.enabled = false;
            }
        }

        private void OnEnable() => GameEvents.SpecialWeaponAcquired += OnAcquired;

        private void OnDisable()
        {
            GameEvents.SpecialWeaponAcquired -= OnAcquired;
            lastWeapon?.Release(context);
        }

        private void OnAcquired(SpecialWeaponSO weapon)
        {
            flare = 1f;
            Vfx.Spawn(acquireVfx, transform.position, 1.2f);
        }

        private void Update()
        {
            GameManager gm = GameManager.Instance;
            SpecialWeaponSO weapon = gm != null ? gm.SelectedSpecial : null;

            if (weapon != lastWeapon)
            {
                lastWeapon?.Release(context);
                lastWeapon = weapon;
            }
            UpdateAura(weapon);
            if (weapon == null) return;

            bool canFire = gm.IsGameplayActive && controller.ControlsEnabled;
            bool held = canFire && input.SpecialHeld;
            float used = weapon.Tick(context, held, Time.deltaTime);
            if (!held) weapon.Release(context);
            if (used > 0f) gm.ConsumeSpecialAmmo(used);
        }

        private void UpdateAura(SpecialWeaponSO weapon)
        {
            if (aura == null) return;
            bool visible = weapon != null && controller.ControlsEnabled;
            aura.enabled = visible;
            if (!visible) return;

            flare = Mathf.MoveTowards(flare, 0f, Time.deltaTime * 1.5f);
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * auraPulseSpeed);
            Color c = weapon.color;
            c.a = auraAlpha * (0.7f + 0.3f * pulse) + flare * 0.5f;
            aura.color = c;
            aura.transform.localScale = auraBaseScale * (1f + 0.08f * pulse + flare * 0.8f);
        }
    }
}
