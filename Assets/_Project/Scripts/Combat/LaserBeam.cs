using System.Collections.Generic;
using ChickenShooter.Core;
using UnityEngine;

namespace ChickenShooter.Combat
{
    /// <summary>
    /// Continuous piercing beam attached to the ship. While on, it damages everything inside a vertical box
    /// above the muzzle (damage per second) and stretches its sprite up to the top of the screen.
    /// </summary>
    public class LaserBeam : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer beamRenderer;
        [Tooltip("Wide, soft underlay that makes the beam glow.")]
        [SerializeField] private SpriteRenderer glowRenderer;
        [SerializeField] private SpriteRenderer muzzleFlare;
        [SerializeField] private float width = 0.45f;
        [SerializeField] private LayerMask hitMask;
        [SerializeField] private GameObject hitVfx;
        [SerializeField] private float vfxInterval = 0.08f;

        private readonly RaycastHit2D[] hits = new RaycastHit2D[48];
        private readonly HashSet<IDamageable> damagedThisTick = new();
        private float vfxTimer;
        private float spriteHeight = 1f;

        public bool IsOn { get; private set; }

        private void Awake()
        {
            if (beamRenderer != null && beamRenderer.sprite != null) spriteHeight = beamRenderer.sprite.bounds.size.y;
            SetOn(false);
        }

        public void SetOn(bool on)
        {
            IsOn = on;
            if (beamRenderer != null) beamRenderer.enabled = on;
            if (glowRenderer != null) glowRenderer.enabled = on;
            if (muzzleFlare != null) muzzleFlare.enabled = on;
        }

        /// <summary>Applies one frame of beam damage.</summary>
        public void Tick(float damagePerSecond, Color color, float dt)
        {
            if (!IsOn) SetOn(true);
            Vector2 origin = transform.position;
            float length = CameraBounds.WorldRect.yMax - origin.y + 0.5f;

            // Stretch the visual from the muzzle to the top of the screen, with a little flicker.
            if (beamRenderer != null)
            {
                Transform t = beamRenderer.transform;
                t.position = origin + Vector2.up * (length * 0.5f);
                float flicker = 1f + Mathf.Sin(Time.time * 60f) * 0.12f;
                t.localScale = new Vector3(flicker * width * 2.2f, length / spriteHeight, 1f);
                beamRenderer.color = Color.Lerp(color, Color.white, 0.55f);

                if (glowRenderer != null)
                {
                    Transform g = glowRenderer.transform;
                    g.position = t.position;
                    float gh = glowRenderer.sprite.bounds.size.y;
                    float gw = glowRenderer.sprite.bounds.size.x;
                    g.localScale = new Vector3(width * 4f * flicker / gw, (length + 1f) / gh, 1f);
                    glowRenderer.color = new Color(color.r, color.g, color.b, 0.55f);
                }
                if (muzzleFlare != null)
                {
                    muzzleFlare.transform.localScale = Vector3.one * (1.2f + Mathf.Sin(Time.time * 45f) * 0.25f);
                    muzzleFlare.color = new Color(color.r, color.g, color.b, 0.9f);
                }
            }

            int count = Physics2D.BoxCastNonAlloc(origin + Vector2.up * (length * 0.5f), new Vector2(width, length), 0f,
                                                  Vector2.zero, hits, 0f, hitMask);
            damagedThisTick.Clear();
            vfxTimer -= dt;
            for (int i = 0; i < count; i++)
            {
                IDamageable d = hits[i].collider.GetComponentInParent<IDamageable>();
                if (d == null || !d.IsAlive || !damagedThisTick.Add(d)) continue;
                d.TakeDamage(damagePerSecond * dt);
                if (vfxTimer <= 0f) Vfx.Spawn(hitVfx, hits[i].collider.transform.position, 0.4f);
            }
            if (vfxTimer <= 0f) vfxTimer = vfxInterval;
        }
    }
}
