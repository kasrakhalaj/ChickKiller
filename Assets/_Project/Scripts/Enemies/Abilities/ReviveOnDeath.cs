using UnityEngine;

namespace ChickenShooter.Enemies
{
    /// <summary>
    /// Flock ability (zombie hens): when "killed" the hen falls limp and greyed out for a few seconds and
    /// then gets back up with half health. Shooting the limp body finishes it for good. The missile
    /// kills it permanently.
    /// </summary>
    public class ReviveOnDeath : MonoBehaviour
    {
        [SerializeField] private int revives = 1;
        [SerializeField] private float downedDuration = 2.6f;
        [SerializeField] private Color downedTint = new Color(0.45f, 0.5f, 0.45f, 0.65f);
        [SerializeField] [Range(0.1f, 1f)] private float reviveHealthFraction = 0.5f;
        [SerializeField] private GameObject downVfx;
        [SerializeField] private GameObject reviveVfx;

        private int remaining;

        public float DownedDuration => downedDuration;
        public Color DownedTint => downedTint;
        public float ReviveHealthFraction => reviveHealthFraction;
        public GameObject DownVfx => downVfx;
        public GameObject ReviveVfx => reviveVfx;

        public void ResetRevives() => remaining = revives;

        /// <summary>Uses one revive if available.</summary>
        public bool TryConsume()
        {
            if (remaining <= 0) return false;
            remaining--;
            return true;
        }
    }
}
