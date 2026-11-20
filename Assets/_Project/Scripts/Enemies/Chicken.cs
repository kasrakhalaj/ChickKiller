using System.Collections;
using System.Collections.Generic;
using ChickenShooter.Combat;
using ChickenShooter.Core;
using ChickenShooter.Data;
using ChickenShooter.Player;
using ChickenShooter.Pooling;
using UnityEngine;

namespace ChickenShooter.Enemies
{
    /// <summary>Runtime stats of one wave after applying the round's difficulty multipliers.</summary>
    public readonly struct WaveModifiers
    {
        public readonly float Health;
        public readonly float Speed;
        public readonly float FireRate;
        public readonly float DiveFrequency;

        public WaveModifiers(RoundDefinition round)
        {
            Health = round?.healthMultiplier ?? 1f;
            Speed = round?.speedMultiplier ?? 1f;
            FireRate = round?.fireRateMultiplier ?? 1f;
            DiveFrequency = round?.diveFrequencyMultiplier ?? 1f;
        }

        public static WaveModifiers Default => new WaveModifiers(null);
    }

    /// <summary>
    /// Flock member with a small state machine:
    /// Entering (curved waypoint path) -> InFormation (follows slot, attacks) -> Diving (kamikaze, no
    /// attacks) -> Returning (re-enters from the top back to its slot). Zombie hens add a Downed state.
    /// Optional ability components on the prefab change its mechanics:
    /// <see cref="ArmorPlating"/>, <see cref="ReviveOnDeath"/>, <see cref="HealerAura"/>.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer), typeof(Rigidbody2D))]
    public class Chicken : WaveEntity, IDamageable, IPlayerHazard, IMissileTarget
    {
        private enum State { Entering, InFormation, Diving, Returning, Downed, Dead }

        [SerializeField] private SpriteAnimator animator;
        [SerializeField] private float hitFlashTime = 0.08f;
        [SerializeField] private Color hitFlashColor = Color.white;
        [SerializeField] private Color armorPingColor = new Color(1f, 1f, 0.6f);
        [SerializeField] private Color healFlashColor = new Color(0.5f, 1f, 0.55f);
        [SerializeField] private float homingTime = 0.7f;

        private SpriteRenderer spriteRenderer;
        private Rigidbody2D body;
        private ArmorPlating armor;
        private ReviveOnDeath revive;
        private SpriteFlasher flasher;

        private EnemyDataSO data;
        private WaveModifiers mods;
        private int slotIndex;
        private List<Vector3> path;
        private int pathIndex;
        private float startDelay;
        private float entrySpeed;

        private State state;
        private float health;
        private float maxHealth;
        private float attackTimer;
        private float downedTimer;
        private Vector2 diveDirection;
        private float diveTimer;
        private Coroutine flashRoutine;

        public FormationController Formation { get; private set; }
        public bool IsAlive => state != State.Dead && gameObject.activeInHierarchy;
        public bool IsHarmful => state != State.Dead && state != State.Downed;
        public bool IsInFormation => state == State.InFormation;
        public bool IsDiving => state == State.Diving || state == State.Returning;
        public bool CanDive => data != null && data.canDive && state == State.InFormation;
        public int SlotIndex => slotIndex;

        private Color CurrentTint => armor != null && armor.IsArmored ? armor.ArmoredTint : data.tint;

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            body = GetComponent<Rigidbody2D>();
            armor = GetComponent<ArmorPlating>();
            revive = GetComponent<ReviveOnDeath>();
            flasher = GetComponent<SpriteFlasher>();
            if (animator == null) animator = GetComponent<SpriteAnimator>();
        }

        private void OnEnable() => MissileTargetRegistry.Register(this);
        private void OnDisable() => MissileTargetRegistry.Unregister(this);

        // ------------------------------------------------------------------ setup

        public void Init(EnemyDataSO enemy, WaveModifiers modifiers, FormationController owner, int slot,
                         List<Vector3> entryPath, float delay, float speed)
        {
            data = enemy;
            mods = modifiers;
            Formation = owner;
            slotIndex = slot;
            path = entryPath;
            pathIndex = 0;
            startDelay = delay;
            entrySpeed = speed * mods.Speed;

            maxHealth = health = Mathf.Max(1, Mathf.RoundToInt(enemy.maxHealth * mods.Health));
            armor?.ResetArmor(mods.Health);
            revive?.ResetRevives();

            transform.localScale = Vector3.one * enemy.scale;
            spriteRenderer.color = CurrentTint;
            if (animator != null) animator.Play(enemy.flyFrames, enemy.frameRate);
            attackTimer = enemy.attack.NextInterval(mods.FireRate);

            state = State.Entering;
            if (path != null && path.Count > 0) body.position = path[0];
            transform.rotation = Quaternion.identity;
        }

        // ------------------------------------------------------------------ update / FSM

        private void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            switch (state)
            {
                case State.Entering: TickEntering(dt); break;
                case State.InFormation: TickInFormation(dt); break;
                case State.Diving: TickDiving(dt); break;
                case State.Returning: TickReturning(dt); break;
                case State.Downed: TickDowned(dt); break;
            }
        }

        private void TickEntering(float dt)
        {
            if (startDelay > 0f)
            {
                startDelay -= dt;
                return;
            }

            if (pathIndex < path.Count)
            {
                Vector2 target = path[pathIndex];
                Vector2 next = Vector2.MoveTowards(body.position, target, entrySpeed * dt);
                FaceMovement(next - body.position);
                body.MovePosition(next);
                if ((next - target).sqrMagnitude < 0.0025f) pathIndex++;
                return;
            }

            if (MoveToSlot(dt, entrySpeed))
            {
                state = State.InFormation;
                Formation.NotifyArrived(this);
            }
        }

        private void TickInFormation(float dt)
        {
            body.MovePosition(Vector2.Lerp(body.position, Formation.GetSlotPosition(slotIndex), 12f * dt));
            FaceMovement(Vector2.zero);

            if (!data.attack.HasProjectile) return;
            attackTimer -= dt;
            if (attackTimer <= 0f && Formation.AllowEggs)
            {
                data.attack.Fire(body.position + Vector2.down * 0.3f, mods.Speed);
                attackTimer = data.attack.NextInterval(mods.FireRate);
            }
        }

        private void TickDiving(float dt)
        {
            diveTimer += dt;
            float speed = data.diveSpeed * mods.Speed;

            PlayerController player = PlayerController.Current;
            if (player != null && diveTimer < homingTime)
            {
                Vector2 toPlayer = (player.Position - body.position).normalized;
                float maxRad = data.diveTurnRate * Mathf.Deg2Rad * dt;
                diveDirection = Vector3.RotateTowards(diveDirection, toPlayer, maxRad, 0f);
            }

            Vector2 next = body.position + diveDirection * (speed * dt);
            body.MovePosition(next);
            float angle = Mathf.Atan2(diveDirection.y, diveDirection.x) * Mathf.Rad2Deg + 90f;
            body.MoveRotation(Mathf.Clamp(Mathf.DeltaAngle(0f, angle), -45f, 45f));

            if (CameraBounds.IsOutside(next, 1.2f)) WrapToTop();
        }

        private void WrapToTop()
        {
            Rect cam = CameraBounds.WorldRect;
            Vector2 slot = Formation.GetSlotPosition(slotIndex);
            body.position = new Vector2(slot.x, cam.yMax + 1f);
            transform.position = body.position;
            body.rotation = 0f;
            state = State.Returning;
        }

        private void TickReturning(float dt)
        {
            if (MoveToSlot(dt, data.moveSpeed * mods.Speed))
            {
                state = State.InFormation;
                Formation.NotifyDiveFinished(this);
            }
        }

        private void TickDowned(float dt)
        {
            // Limp body slowly sinks and wobbles until it gets back up.
            downedTimer -= dt;
            body.MovePosition(body.position + new Vector2(Mathf.Sin(downedTimer * 6f) * 0.4f, -0.5f) * dt);
            body.MoveRotation(Mathf.Sin(downedTimer * 4f) * 25f + 180f);
            if (CameraBounds.IsBelow(body.position, 0.5f))
            {
                Die(awardScore: false);
                return;
            }
            if (downedTimer <= 0f) Revive();
        }

        private bool MoveToSlot(float dt, float speed)
        {
            Vector2 slot = Formation.GetSlotPosition(slotIndex);
            Vector2 next = Vector2.MoveTowards(body.position, slot, speed * dt);
            FaceMovement(next - body.position);
            body.MovePosition(next);
            return (next - slot).sqrMagnitude < 0.01f;
        }

        private void FaceMovement(Vector2 delta)
        {
            if (Mathf.Abs(delta.x) > 0.001f) spriteRenderer.flipX = delta.x < 0f;
            body.MoveRotation(Mathf.LerpAngle(body.rotation, 0f, 0.2f));
        }

        // ------------------------------------------------------------------ behaviours

        /// <summary>Called by the formation's dive scheduler.</summary>
        public void StartDive()
        {
            if (!CanDive) return;
            state = State.Diving;
            diveTimer = 0f;
            PlayerController player = PlayerController.Current;
            Vector2 target = player != null ? player.Position : body.position + Vector2.down;
            diveDirection = (target - body.position).normalized;
            if (diveDirection.y > -0.3f) diveDirection = new Vector2(diveDirection.x, -0.6f).normalized;
        }

        /// <summary>Healing from a nurse hen's pulse.</summary>
        public void Heal(int amount)
        {
            if (state == State.Dead || state == State.Downed || health >= maxHealth) return;
            health = Mathf.Min(maxHealth, health + amount);
            Flash(healFlashColor);
        }

        // ------------------------------------------------------------------ damage

        public void TakeDamage(float amount)
        {
            if (!IsAlive) return;

            if (state == State.Downed)
            {
                Die(awardScore: true); // finishing blow on a limp zombie
                return;
            }

            if (armor != null && armor.IsArmored)
            {
                bool broke = armor.AbsorbHit(transform.position);
                Flash(broke ? Color.white : armorPingColor);
                return;
            }

            health -= amount;
            if (health > 0)
            {
                Vfx.Spawn(data.hitVfx, transform.position, 0.5f);
                Flash(hitFlashColor);
                return;
            }

            if (revive != null && revive.TryConsume()) EnterDowned();
            else Die(awardScore: true);
        }

        public void OnHitPlayer() => Die(awardScore: false);

        public void OnMissileStrike(float bossDamageFraction) => Die(awardScore: true, rareLoot: false);

        private void EnterDowned()
        {
            if (IsDiving) Formation.NotifyDiveFinished(this);
            state = State.Downed;
            downedTimer = revive.DownedDuration;
            StopFlash();
            spriteRenderer.color = revive.DownedTint;
            Vfx.Spawn(revive.DownVfx, transform.position);
        }

        private void Revive()
        {
            health = Mathf.Max(1, Mathf.RoundToInt(maxHealth * revive.ReviveHealthFraction));
            spriteRenderer.color = CurrentTint;
            body.rotation = 0f;
            Vfx.Spawn(revive.ReviveVfx, transform.position);
            state = State.Returning;
        }

        private void Die(bool awardScore, bool rareLoot = true)
        {
            if (state == State.Dead) return;
            bool wasDiving = IsDiving;
            state = State.Dead;
            Vector3 pos = transform.position;

            if (awardScore)
            {
                GameManager.Instance?.AddScore(data.scoreValue);
                GameEvents.RaiseEnemyKilled(pos, data.scoreValue);
                Loot.Drop(data, pos, rareLoot);
            }
            Vfx.Spawn(data.deathVfx, pos, data.scale);
            if (Formation != null) Formation.NotifyRemoved(this, wasDiving);
            Remove();
        }

        private void Flash(Color color)
        {
            if (flasher != null)
            {
                flasher.Flash(color);
                return;
            }
            StopFlash();
            flashRoutine = StartCoroutine(FlashRoutine(color));
        }

        private void StopFlash()
        {
            if (flashRoutine != null) StopCoroutine(flashRoutine);
            flashRoutine = null;
        }

        private IEnumerator FlashRoutine(Color color)
        {
            spriteRenderer.color = color;
            yield return new WaitForSeconds(hitFlashTime);
            if (state != State.Downed) spriteRenderer.color = CurrentTint;
            flashRoutine = null;
        }

        // ------------------------------------------------------------------ pooling

        public override void OnSpawned()
        {
            base.OnSpawned();
            state = State.Entering;
        }

        public override void OnDespawned()
        {
            base.OnDespawned();
            flashRoutine = null;
            Formation = null;
            spriteRenderer.flipX = false;
            body.rotation = 0f;
        }
    }

    /// <summary>
    /// Shared loot roll: a gift box (weapon upgrade) or food, based on the enemy's drop chances,
    /// plus an independent roll for a special-weapon crate.
    /// </summary>
    public static class Loot
    {
        /// <param name="rareLoot">False for missile kills: only food can drop (no gift boxes / weapon crates).</param>
        public static void Drop(EnemyDataSO data, Vector3 pos, bool rareLoot = true)
        {
            if (rareLoot && data.giftPrefab != null && Random.value < data.giftDropChance)
                PoolManager.Spawn(data.giftPrefab, pos, Quaternion.identity);
            else if (data.foodPrefab != null && Random.value < data.foodDropChance)
                PoolManager.Spawn(data.foodPrefab, pos, Quaternion.identity);

            if (rareLoot && Random.value < data.specialDropChance) DropSpecial(pos);
        }

        /// <summary>Drops a crate for a random special weapon from the game config.</summary>
        public static void DropSpecial(Vector3 pos)
        {
            GameConfigSO cfg = GameManager.Instance != null ? GameManager.Instance.Config : null;
            if (cfg == null || cfg.specialPickupPrefab == null || cfg.specialWeapons == null || cfg.specialWeapons.Length == 0) return;
            int index = Random.Range(0, cfg.specialWeapons.Length);
            SpecialWeaponPickup crate = PoolManager.Spawn(cfg.specialPickupPrefab, pos, Quaternion.identity);
            crate.Setup(index, cfg.specialWeapons[index]);
        }
    }
}
