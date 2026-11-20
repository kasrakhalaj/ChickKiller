using System.Collections;
using ChickenShooter.Combat;
using ChickenShooter.Core;
using UnityEngine;

namespace ChickenShooter.Player
{
    /// <summary>
    /// Handles everything that touches the ship. Hazards (eggs, chickens, asteroids, boss bodies) are resolved
    /// by <see cref="GameManager.PlayerHit"/>:
    ///  - PowerLost: weapon power acted as a shield -> short blinking invincibility, ship keeps flying;
    ///  - LifeLost:  ship explodes, disappears, then respawns at the start position with invincibility;
    ///  - GameOver:  ship explodes for good.
    /// Pickups touching the ship are collected.
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public class PlayerHealth : MonoBehaviour
    {
        [SerializeField] private float shieldHitInvincibility = 1.2f;
        [SerializeField] private float respawnInvincibility = 2.5f;
        [SerializeField] private float blinkInterval = 0.08f;
        [SerializeField] private SpriteRenderer[] blinkRenderers;
        [SerializeField] private GameObject deathVfx;
        [SerializeField] private GameObject hitVfx;

        private PlayerController controller;
        private PlayerWeapon weapon;
        private Rigidbody2D body;
        private Collider2D[] colliders;
        private Vector2 spawnPosition;
        private bool invincible;
        private bool destroyed;
        private Coroutine blinkRoutine;

        public bool IsInvincible => invincible;

        private void Awake()
        {
            controller = GetComponent<PlayerController>();
            weapon = GetComponent<PlayerWeapon>();
            body = GetComponent<Rigidbody2D>();
            colliders = GetComponentsInChildren<Collider2D>();
            spawnPosition = transform.position;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.TryGetComponent(out Pickup pickup))
            {
                if (!destroyed) pickup.Collect();
                return;
            }
            TryHit(other);
        }

        // Stay: a hazard still overlapping when invincibility ends must hit again.
        private void OnTriggerStay2D(Collider2D other) => TryHit(other);

        private void TryHit(Collider2D other)
        {
            if (invincible || destroyed) return;
            GameManager gm = GameManager.Instance;
            if (gm == null || gm.IsGameOver || gm.IsTransitioning) return;

            IPlayerHazard hazard = other.GetComponentInParent<IPlayerHazard>();
            if (hazard == null || !hazard.IsHarmful) return;

            hazard.OnHitPlayer();
            TakeHit(gm);
        }

        private void TakeHit(GameManager gm)
        {
            GameEvents.RaisePlayerDamaged();
            switch (gm.PlayerHit())
            {
                case PlayerHitResult.PowerLost:
                    Vfx.Spawn(hitVfx, transform.position);
                    GameEvents.RaiseScreenShake(0.25f, 0.25f);
                    StartInvincibility(shieldHitInvincibility);
                    break;
                case PlayerHitResult.LifeLost when gm.Config.powerActsAsShield:
                    StartCoroutine(DestroyAndRespawn(gm.Config.respawnDelay));
                    break;
                case PlayerHitResult.LifeLost:
                    // Assignment rule: the ship survives the hit with flashing invincibility frames.
                    Vfx.Spawn(hitVfx, transform.position, 1.3f);
                    GameEvents.RaiseScreenShake(0.35f, 0.35f);
                    StartInvincibility(respawnInvincibility);
                    break;
                case PlayerHitResult.GameOver:
                    Explode();
                    break;
            }
        }

        private void Explode()
        {
            destroyed = true;
            controller.ControlsEnabled = false;
            if (weapon != null) weapon.enabled = false;
            Vfx.Spawn(deathVfx, transform.position, 1.8f);
            GameEvents.RaiseScreenShake(0.6f, 0.5f);
            SetVisible(false);
            foreach (Collider2D c in colliders) c.enabled = false;
        }

        private IEnumerator DestroyAndRespawn(float delay)
        {
            Explode();
            yield return new WaitForSeconds(delay);

            body.position = spawnPosition;
            transform.position = spawnPosition;
            foreach (Collider2D c in colliders) c.enabled = true;
            controller.ControlsEnabled = true;
            if (weapon != null) weapon.enabled = true;
            destroyed = false;
            SetVisible(true);
            StartInvincibility(respawnInvincibility);
        }

        private void StartInvincibility(float duration)
        {
            if (blinkRoutine != null) StopCoroutine(blinkRoutine);
            blinkRoutine = StartCoroutine(InvincibilityRoutine(duration));
        }

        private IEnumerator InvincibilityRoutine(float duration)
        {
            invincible = true;
            float end = Time.time + duration;
            bool visible = true;
            while (Time.time < end)
            {
                visible = !visible;
                SetVisible(visible);
                yield return new WaitForSeconds(blinkInterval);
            }
            SetVisible(true);
            invincible = false;
            blinkRoutine = null;
        }

        private void SetVisible(bool visible)
        {
            foreach (SpriteRenderer r in blinkRenderers)
            {
                if (r != null) r.enabled = visible;
            }
        }
    }
}
