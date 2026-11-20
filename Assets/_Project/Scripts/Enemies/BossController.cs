using System.Collections.Generic;
using ChickenShooter.Combat;
using ChickenShooter.Core;
using ChickenShooter.Data;
using ChickenShooter.Enemies.Bosses;
using ChickenShooter.Player;
using ChickenShooter.Pooling;
using UnityEngine;

namespace ChickenShooter.Enemies
{
    /// <summary>
    /// End-of-level boss driven by a Finite State Machine (State pattern, see <see cref="BossStateMachine"/>).
    /// This class is the FSM *context*: it owns health, phases, minions and shared helpers (move, fire
    /// patterns). States decide the behaviour; <see cref="BossProfileSO"/> decides which states each boss
    /// uses in each health phase:
    ///   Intro -> [Reposition -> Attack]* ; on crossing a health threshold -> PhaseShift (roar, summons)
    ///   Last phase is "Enraged" (below 30%): faster, denser patterns and minion summons.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(SpriteRenderer))]
    public class BossController : WaveEntity, IDamageable, IPlayerHazard, IMissileTarget
    {
        [SerializeField] protected SpriteAnimator animator;
        [SerializeField] protected int defeatScore = 5000;

        private readonly List<RogueEnemy> minions = new();
        private BossStateMachine fsm;
        private SpriteFlasher flasher;
        private float maxHealth;
        private float health;
        private bool alive;
        private bool lastWasAttack;
        private BossAttack lastAttack = (BossAttack)(-1);
        private float shake;

        public EnemyDataSO Data { get; private set; }
        public BossProfileSO Profile { get; private set; }
        public WaveModifiers Mods { get; private set; }
        public Rigidbody2D Body { get; private set; }
        public SpriteRenderer Renderer { get; private set; }
        public int PhaseIndex { get; private set; } = -1;
        public BossPhase Phase => Profile.phases[Mathf.Clamp(PhaseIndex, 0, Profile.phases.Length - 1)];
        public bool Invulnerable { get; set; }
        public bool IsAlive => alive;
        public bool IsHarmful => alive && Renderer.color.a > 0.5f;
        public float HealthFraction => maxHealth > 0f ? health / maxHealth : 0f;
        public string CurrentStateName => fsm?.Current?.Name ?? "-";

        public Rect View => CameraBounds.WorldRect;
        public Vector2 Position => Body.position;
        public Vector2 Muzzle => Body.position + Vector2.down * (Renderer.bounds.extents.y * 0.55f);
        public float Speed => Profile.moveSpeed * Phase.speedMultiplier * Mods.Speed;
        public float DashSpeed => Profile.dashSpeed * Phase.speedMultiplier * Mods.Speed;
        public float ShotSpeed => Profile.projectileSpeed * Mods.Speed * (Phase.enraged ? 1.15f : 1f);

        public Vector2 PlayerPosition
        {
            get
            {
                PlayerController p = PlayerController.Current;
                return p != null ? p.Position : new Vector2(View.center.x, View.yMin + 1.5f);
            }
        }

        protected virtual void Awake()
        {
            Body = GetComponent<Rigidbody2D>();
            Renderer = GetComponent<SpriteRenderer>();
            flasher = GetComponent<SpriteFlasher>();
            if (animator == null) animator = GetComponent<SpriteAnimator>();
        }

        private void OnEnable() => MissileTargetRegistry.Register(this);
        private void OnDisable() => MissileTargetRegistry.Unregister(this);

        // ------------------------------------------------------------------ setup

        public void Init(EnemyDataSO bossData, WaveModifiers modifiers, BossProfileSO profile)
        {
            Data = bossData;
            Mods = modifiers;
            Profile = profile;
            maxHealth = health = bossData.maxHealth * modifiers.Health;
            alive = true;
            PhaseIndex = 0;
            lastWasAttack = true; // first action after the intro is an attack
            transform.localScale = Vector3.one * bossData.scale;
            Renderer.color = bossData.tint;
            if (animator != null) animator.Play(bossData.flyFrames, bossData.frameRate);

            Body.position = new Vector2(View.center.x, View.yMax + 2.5f);
            transform.position = Body.position;

            GameEvents.RaiseBossSpawned(bossData.displayName);
            GameEvents.RaiseBossHealthChanged(health, maxHealth);

            fsm = new BossStateMachine(this);
            fsm.ChangeState(new BossIntroState());
        }

        // ------------------------------------------------------------------ FSM plumbing

        private void FixedUpdate()
        {
            if (!alive) return;
            fsm.Tick(Time.fixedDeltaTime);
        }

        private void Update()
        {
            if (!alive) return;
            // Enraged: pulse toward the rage colour.
            if (Phase.enraged)
            {
                float k = 0.5f + 0.5f * Mathf.Sin(Time.time * 9f);
                Color c = Color.Lerp(Data.tint, Profile.enragedTint, k);
                c.a = Renderer.color.a;
                Renderer.color = c;
            }
            // Telegraph: quick "breathing" scale pulse (visual only, does not move the physics body).
            if (shake > 0f)
            {
                shake = Mathf.Max(0f, shake - Time.deltaTime);
                float pulse = shake > 0f ? 1f + Mathf.Sin(Time.time * 45f) * 0.05f : 1f;
                transform.localScale = Vector3.one * (Data.scale * pulse);
            }
        }

        public void ChangeState(BossState state) => fsm.ChangeState(state);

        /// <summary>Called by states when they finish: alternates Reposition and an attack of the current phase.</summary>
        public void NextAction()
        {
            if (!alive) return;
            if (!lastWasAttack)
            {
                lastWasAttack = true;
                BossAttack attack = PickAttack();
                lastAttack = attack;
                fsm.ChangeState(BossAttackFactory.Create(attack));
            }
            else
            {
                lastWasAttack = false;
                fsm.ChangeState(new BossRepositionState());
            }
        }

        private BossAttack PickAttack()
        {
            BossAttack[] options = Phase.attacks;
            if (options == null || options.Length == 0) return BossAttack.AimedBurst;
            for (int i = 0; i < 6; i++)
            {
                BossAttack a = options[Random.Range(0, options.Length)];
                if (a != lastAttack || options.Length == 1) return a;
            }
            return options[0];
        }

        private void CheckPhase()
        {
            int target = PhaseIndex;
            float f = HealthFraction;
            for (int i = 0; i < Profile.phases.Length; i++)
                if (f <= Profile.phases[i].enterBelowHealth) target = Mathf.Max(target, i);
            if (target <= PhaseIndex) return;
            PhaseIndex = target;
            fsm.ChangeState(new BossPhaseShiftState());
        }

        // ------------------------------------------------------------------ helpers used by states

        public float Cooldown(float seconds) => seconds * Phase.cooldownMultiplier / Mathf.Max(0.1f, Mods.FireRate);

        public int Density(int count) => Mathf.Max(1, Mathf.RoundToInt(count * Phase.densityMultiplier));

        public bool MoveTowards(Vector2 target, float speed, float dt)
        {
            Vector2 next = Vector2.MoveTowards(Body.position, target, speed * dt);
            if (Mathf.Abs(next.x - Body.position.x) > 0.001f) Renderer.flipX = next.x < Body.position.x;
            Body.MovePosition(next);
            return (next - target).sqrMagnitude < 0.01f;
        }

        public Vector2 RandomHoverPoint() =>
            CameraBounds.ViewportToWorld(new Vector2(Random.Range(0.2f, 0.8f), Random.Range(0.6f, 0.76f)));

        public void Telegraph(float duration)
        {
            shake = duration;
            if (flasher != null) flasher.Flash(new Color(1f, 0.9f, 0.3f), duration);
        }

        public void Fire(EnemyProjectile prefab, Vector2 origin, Vector2 dir, float speed, Color tint)
        {
            if (prefab == null) return;
            EnemyProjectile p = PoolManager.Spawn(prefab, origin, Quaternion.identity);
            p.Launch(dir.normalized * speed, tint);
        }

        public void FireFan(EnemyProjectile prefab, Vector2 origin, Vector2 baseDir, int count, float spread, float speed, Color tint)
        {
            for (int i = 0; i < count; i++)
            {
                float t = count == 1 ? 0.5f : i / (float)(count - 1);
                float a = Mathf.Lerp(-spread * 0.5f, spread * 0.5f, t);
                Fire(prefab, origin, Quaternion.Euler(0f, 0f, a) * baseDir, speed, tint);
            }
        }

        public void FireRing(EnemyProjectile prefab, Vector2 origin, int count, float speed, Color tint, float offsetDeg = 0f)
        {
            for (int i = 0; i < count; i++)
            {
                float a = offsetDeg + i * 360f / count;
                Fire(prefab, origin, Quaternion.Euler(0f, 0f, a) * Vector2.up, speed, tint);
            }
        }

        public void SetAlpha(float a)
        {
            Color c = Renderer.color;
            c.a = a;
            Renderer.color = c;
        }

        public void SummonMinions(int count)
        {
            if (Profile.minion == null || !(Profile.minion.prefab is RogueEnemy prefab)) return;
            minions.RemoveAll(m => m == null || !m.IsAlive);
            for (int i = 0; i < count; i++)
            {
                Vector2 pos = Position + new Vector2(Random.Range(-1.5f, 1.5f), Random.Range(-0.5f, 0.5f));
                RogueEnemy m = PoolManager.Spawn(prefab, pos, Quaternion.identity);
                m.InitAt(Profile.minion, Mods, pos);
                minions.Add(m);
            }
        }

        // ------------------------------------------------------------------ damage

        public void TakeDamage(float amount) => ApplyDamage(amount);

        public void OnMissileStrike(float bossDamageFraction) => ApplyDamage(maxHealth * bossDamageFraction, ignoreInvulnerable: true);

        public void OnHitPlayer() { } // the boss body hurts the player but is not affected

        private void ApplyDamage(float amount, bool ignoreInvulnerable = false)
        {
            if (!alive || (Invulnerable && !ignoreInvulnerable)) return;
            health = Mathf.Max(0f, health - amount);
            GameEvents.RaiseBossHealthChanged(health, maxHealth);
            if (flasher != null) flasher.Flash(Color.white, 0.07f);
            if (health <= 0f)
            {
                Defeat();
                return;
            }
            CheckPhase();
        }

        private void Defeat()
        {
            alive = false;
            fsm.ChangeState(null);
            foreach (RogueEnemy m in minions)
                if (m != null && m.IsAlive) m.Kill();
            minions.Clear();

            Vector3 pos = transform.position;
            SetAlpha(1f);
            Renderer.color = Data.tint;
            GameManager.Instance?.AddScore(defeatScore);
            GameEvents.RaiseEnemyKilled(pos, defeatScore);
            Vfx.Spawn(Data.deathVfx, pos, 3f);
            GameEvents.RaiseScreenShake(0.7f, 0.8f);
            GameEvents.RaiseBossDefeated();
            for (int i = 0; i < 2; i++) Loot.DropSpecial(pos + new Vector3(i == 0 ? -0.8f : 0.8f, 0f));
            Remove();
        }

        public override void OnDespawned()
        {
            base.OnDespawned();
            Invulnerable = false;
            shake = 0f;
        }
    }
}
