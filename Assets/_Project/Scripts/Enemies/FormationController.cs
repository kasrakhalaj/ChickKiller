using System.Collections.Generic;
using ChickenShooter.Core;
using ChickenShooter.Data;
using UnityEngine;

namespace ChickenShooter.Enemies
{
    /// <summary>
    /// Owns the slots of one formation wave: moves the whole block (sway + bob), keeps it inside the
    /// screen, and schedules kamikaze dives among chickens currently sitting in formation.
    /// </summary>
    public class FormationController : MonoBehaviour
    {
        private readonly List<Chicken> members = new();
        private List<Vector2> slots;
        private WaveDataSO wave;
        private WaveModifiers mods;
        private Vector2 anchor;
        private float clampedSway;
        private float time;
        private float diveTimer;
        private int activeDivers;
        private int arrivedCount;

        /// <summary>Eggs only start once most of the formation has arrived (fair entry).</summary>
        public bool AllowEggs => arrivedCount >= Mathf.CeilToInt(slots.Count * 0.5f);

        public int SlotCount => slots.Count;
        public IReadOnlyList<Chicken> Members => members;

        public void Setup(WaveDataSO waveData, WaveModifiers modifiers)
        {
            wave = waveData;
            mods = modifiers;
            slots = FormationPatterns.Generate(wave.formation, wave.rows, wave.columns, wave.spacing);
            anchor = CameraBounds.ViewportToWorld(wave.formationCenterViewport);

            // Limit sway so the widest slot never leaves the screen.
            float halfWidth = 0f;
            foreach (Vector2 s in slots) halfWidth = Mathf.Max(halfWidth, Mathf.Abs(s.x));
            float room = CameraBounds.WorldRect.width * 0.5f - halfWidth - 0.6f;
            clampedSway = Mathf.Clamp(wave.swayAmplitude, 0f, Mathf.Max(0f, room));

            ResetDiveTimer();
        }

        public void AddMember(Chicken c) => members.Add(c);

        public Vector2 GetSlotPosition(int index)
        {
            float sway = Mathf.Sin(time * wave.swaySpeed * mods.Speed * Mathf.PI * 2f) * clampedSway;
            float bob = Mathf.Sin(time * 1.7f + index * 0.35f) * wave.bobAmplitude;
            return anchor + slots[index] + new Vector2(sway, bob);
        }

        private void Update()
        {
            time += Time.deltaTime;

            if (wave.maxSimultaneousDivers <= 0 || !AllowEggs) return;
            diveTimer -= Time.deltaTime;
            if (diveTimer > 0f) return;

            ResetDiveTimer();
            if (activeDivers >= wave.maxSimultaneousDivers) return;

            Chicken diver = PickDiver();
            if (diver == null) return;
            activeDivers++;
            diver.StartDive();
        }

        private Chicken PickDiver()
        {
            int start = Random.Range(0, Mathf.Max(1, members.Count));
            for (int i = 0; i < members.Count; i++)
            {
                Chicken c = members[(start + i) % members.Count];
                if (c != null && c.CanDive) return c;
            }
            return null;
        }

        private void ResetDiveTimer()
        {
            float interval = Random.Range(wave.diveInterval.x, wave.diveInterval.y);
            diveTimer = interval / Mathf.Max(0.1f, mods.DiveFrequency);
        }

        // ------------------------------------------------------------------ chicken callbacks

        public void NotifyArrived(Chicken c) => arrivedCount++;

        public void NotifyDiveFinished(Chicken c) => activeDivers = Mathf.Max(0, activeDivers - 1);

        public void NotifyRemoved(Chicken c, bool wasDiving)
        {
            if (wasDiving) activeDivers = Mathf.Max(0, activeDivers - 1);
            members.Remove(c);
        }
    }
}
