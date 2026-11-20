using ChickenShooter.Core;
using ChickenShooter.Data;
using UnityEngine;

namespace ChickenShooter.Combat
{
    /// <summary>
    /// Crate that equips one special weapon (replacing the current one, or refilling it if it is the same).
    /// Icon and glow colour are set when it drops; it pulses so it stands out from food and gift boxes.
    /// </summary>
    public class SpecialWeaponPickup : Pickup
    {
        [SerializeField] private SpriteRenderer iconRenderer;
        [SerializeField] private SpriteRenderer glowRenderer;
        [SerializeField] private float pulseSpeed = 6f;

        private int weaponIndex;
        private Color glowColor = Color.white;

        public void Setup(int index, SpecialWeaponSO weapon)
        {
            weaponIndex = index;
            if (iconRenderer != null) iconRenderer.sprite = weapon.icon;
            glowColor = weapon.color;
        }

        private void LateUpdate()
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * pulseSpeed);
            if (iconRenderer != null)
            {
                float k = 0.85f + 0.15f * pulse;
                iconRenderer.color = new Color(k, k, k, 1f);
            }
            if (glowRenderer != null)
            {
                glowRenderer.color = new Color(glowColor.r, glowColor.g, glowColor.b, 0.45f + 0.35f * pulse);
                glowRenderer.transform.localScale = Vector3.one * (0.85f + 0.15f * pulse);
            }
        }

        protected override void ApplyReward(GameManager gm)
        {
            if (gm == null) return;
            SpecialWeaponSO w = gm.Config.specialWeapons[weaponIndex];
            gm.AddSpecialAmmo(weaponIndex, w.ammoPerPickup);
        }
    }
}
