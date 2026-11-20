using ChickenShooter.Combat;
using UnityEngine;

namespace ChickenShooter.Enemies
{
    /// <summary>
    /// Flock ability: a helmet that soaks the first N hits (sparks, no health loss). While armored the hen
    /// is tinted steel; when the armor breaks it flashes and becomes vulnerable.
    /// </summary>
    public class ArmorPlating : MonoBehaviour
    {
        [SerializeField] private int armorHits = 3;
        [SerializeField] private Color armoredTint = new Color(0.72f, 0.78f, 0.9f);
        [SerializeField] private GameObject pingVfx;
        [SerializeField] private GameObject breakVfx;

        private int remaining;

        public bool IsArmored => remaining > 0;
        public Color ArmoredTint => armoredTint;

        /// <summary>Called by the owner on spawn. Round difficulty scales the armor.</summary>
        public void ResetArmor(float multiplier) => remaining = Mathf.Max(1, Mathf.RoundToInt(armorHits * multiplier));

        /// <summary>Consumes one armor hit. Returns true if the armor just broke.</summary>
        public bool AbsorbHit(Vector3 position)
        {
            if (remaining <= 0) return false;
            remaining--;
            if (remaining > 0)
            {
                Vfx.Spawn(pingVfx, position, 0.5f);
                return false;
            }
            Vfx.Spawn(breakVfx, position, 0.9f);
            return true;
        }
    }
}
