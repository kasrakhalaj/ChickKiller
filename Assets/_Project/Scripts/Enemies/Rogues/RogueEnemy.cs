using System.Collections;
using ChickenShooter.Combat;
using ChickenShooter.Core;
using ChickenShooter.Data;
using UnityEngine;

namespace ChickenShooter.Enemies
{
    /// <summary>
    /// Free-agent enemy that does NOT join the flock. Shares health, hit-flash, death, loot and missile
    /// handling; each subclass implements its own behaviour state machine in <see cref="Behaviour"/>
    /// (written as a coroutine: one block per state, transitions are sequential/looped).
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(SpriteRenderer))]
    public abstract class RogueEnemy : WaveEntity, IDamageable, IPlayerHazard, IMissileTarget
    {
        [SerializeField] protected SpriteAnimator animator;
        [SerializeField] protected Color hitFlashColor = Color.white;
        [SerializeField] protected Color telegraphColor = new Color(1f, 1f, 0.45f);

        protected EnemyDataSO data;
        protected WaveModifiers mods;
        protected SpriteRenderer spriteRenderer;
        protected Rigidbody2D body;
        protected SpriteFlasher flasher;
        protected float health;
        protected bool alive;
        private Coroutine behaviour;
        private Vector2? forcedSpawn;
        private Coroutine flash;

        public bool IsAlive => alive;
        public bool IsHarmful => alive;
        protected float Speed => data.moveSpeed * mods.Speed;

        protected virtual void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            body = GetComponent<Rigidbody2D>();
            flasher = GetComponent<SpriteFlasher>();
            if (animator == null) animator = GetComponent<SpriteAnimator>();
        }

        private void OnEnable() => MissileTargetRegistry.Register(this);
        private void OnDisable() => MissileTargetRegistry.Unregister(this);

        public void Init(EnemyDataSO enemy, WaveModifiers modifiers)
        {
            data = enemy;
            mods = modifiers;
            health = Mathf.Max(1, Mathf.RoundToInt(enemy.maxHealth * modifiers.Health));
            alive = true;
            transform.localScale = Vector3.one * enemy.scale;
            transform.rotation = Quaternion.identity;
            spriteRenderer.color = enemy.tint;
            if (animator != null) animator.Play(enemy.flyFrames, enemy.frameRate);

            body.position = forcedSpawn ?? SpawnPosition();
            forcedSpawn = null;
            transform.position = body.position;
            behaviour = StartCoroutine(Behaviour());
        }

        /// <summary>Spawns at a specific position (e.g. minions summoned next to a boss).</summary>
        public void InitAt(EnemyDataSO enemy, WaveModifiers modifiers, Vector2 position)
        {
            forcedSpawn = position;
            Init(enemy, modifiers);
        }

        /// <summary>Removes the enemy without score or loot (e.g. minions when their boss dies).</summary>
        public void Kill() => Die(awardScore: false, rareLoot: false);

        /// <summary>Where the enemy appears (normally just off-screen).</summary>
        protected abstract Vector2 SpawnPosition();

        /// <summary>The enemy's state machine.</summary>
        protected abstract IEnumerator Behaviour();

        // ------------------------------------------------------------------ movement helpers

        protected IEnumerator FlyTo(Vector2 target, float speed)
        {
            while (alive && (body.position - target).sqrMagnitude > 0.004f)
            {
                Vector2 next = Vector2.MoveTowards(body.position, target, speed * Time.fixedDeltaTime);
                Face(next - body.position);
                body.MovePosition(next);
                yield return new WaitForFixedUpdate();
            }
        }

        protected void Face(Vector2 delta)
        {
            if (Mathf.Abs(delta.x) > 0.001f) spriteRenderer.flipX = delta.x < 0f;
        }

        protected static Vector2 Viewport(float x, float y) => CameraBounds.ViewportToWorld(new Vector2(x, y));

        /// <summary>Flashes the telegraph colour and shivers in place before an attack.</summary>
        protected IEnumerator Telegraph(float duration)
        {
            Vector2 origin = body.position;
            float t = 0f;
            while (alive && t < duration)
            {
                t += Time.fixedDeltaTime;
                bool on = Mathf.Sin(t * 40f) > 0f;
                spriteRenderer.color = on ? telegraphColor : data.tint;
                body.MovePosition(origin + Random.insideUnitCircle * 0.06f);
                yield return new WaitForFixedUpdate();
            }
            spriteRenderer.color = data.tint;
            body.MovePosition(origin);
        }

        /// <summary>Leaves the battlefield without awarding score (still counts as cleared).</summary>
        protected void Leave()
        {
            alive = false;
            Remove();
        }

        // ------------------------------------------------------------------ damage

        public void TakeDamage(float amount)
        {
            if (!alive) return;
            health -= amount;
            if (health <= 0)
            {
                Die(awardScore: true);
                return;
            }
            Vfx.Spawn(data.hitVfx, transform.position, 0.5f);
            if (flasher != null) flasher.Flash(hitFlashColor);
            else
            {
                if (flash != null) StopCoroutine(flash);
                flash = StartCoroutine(Flash());
            }
        }

        public void OnHitPlayer() => Die(awardScore: false);

        public void OnMissileStrike(float bossDamageFraction) => Die(awardScore: true, rareLoot: false);

        protected void Die(bool awardScore, bool rareLoot = true)
        {
            if (!alive) return;
            alive = false;
            StopAllCoroutines();
            Vector3 pos = transform.position;
            if (awardScore)
            {
                GameManager.Instance?.AddScore(data.scoreValue);
                GameEvents.RaiseEnemyKilled(pos, data.scoreValue);
                Loot.Drop(data, pos, rareLoot);
            }
            Vfx.Spawn(data.deathVfx, pos, 1.3f * data.scale);
            Remove();
        }

        private IEnumerator Flash()
        {
            spriteRenderer.color = hitFlashColor;
            yield return new WaitForSeconds(0.08f);
            if (alive) spriteRenderer.color = data.tint;
            flash = null;
        }

        public override void OnDespawned()
        {
            base.OnDespawned();
            StopAllCoroutines();
            behaviour = null;
            flash = null;
            spriteRenderer.flipX = false;
            body.rotation = 0f;
            body.linearVelocity = Vector2.zero;
        }
    }
}
