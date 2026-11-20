using ChickenShooter.Combat;
using UnityEngine;

namespace ChickenShooter.Enemies
{
    /// <summary>
    /// Flock ability (nurse hens): periodically emits a healing pulse that restores health to nearby
    /// flock members. Never shoots or dives, which makes it a priority target.
    /// </summary>
    [RequireComponent(typeof(Chicken))]
    public class HealerAura : MonoBehaviour
    {
        [SerializeField] private float radius = 2.8f;
        [SerializeField] private float interval = 4f;
        [SerializeField] private int healAmount = 2;
        [SerializeField] private GameObject pulseVfx;

        private Chicken owner;
        private float timer;

        private void Awake() => owner = GetComponent<Chicken>();

        private void OnEnable() => timer = interval * Random.Range(0.6f, 1f);

        private void Update()
        {
            if (!owner.IsInFormation || owner.Formation == null) return;
            timer -= Time.deltaTime;
            if (timer > 0f) return;
            timer = interval;
            Pulse();
        }

        private void Pulse()
        {
            Vfx.Spawn(pulseVfx, transform.position, radius / 9f);
            float r2 = radius * radius;
            foreach (Chicken c in owner.Formation.Members)
            {
                if (c == null || c == owner || !c.IsAlive) continue;
                if (((Vector2)(c.transform.position - transform.position)).sqrMagnitude <= r2) c.Heal(healAmount);
            }
        }
    }
}
