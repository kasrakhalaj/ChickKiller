using System.Collections;
using ChickenShooter.Combat;
using ChickenShooter.Core;
using ChickenShooter.Pooling;
using UnityEngine;

namespace ChickenShooter.Enemies
{
    /// <summary>Destructible asteroid used in asteroid-belt rounds. Pooled, has health, hurts the player.</summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(SpriteRenderer), typeof(CircleCollider2D))]
    public class Asteroid : WaveEntity, IDamageable, IPlayerHazard, IMissileTarget
    {
        [SerializeField] private int scorePerHealth = 25;
        [SerializeField] private GameObject destroyVfx;
        [SerializeField] private GameObject hitVfx;
        [SerializeField] private Color hitFlashColor = new Color(1f, 0.6f, 0.6f);

        private Rigidbody2D body;
        private SpriteRenderer spriteRenderer;
        private CircleCollider2D circle;
        private SpriteFlasher flasher;
        private float health;
        private float maxHealth;
        private float foodChance;
        private Pickup foodPrefab;
        private bool alive;
        private bool enteredScreen;

        public bool IsAlive => alive;
        public bool IsHarmful => alive;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            spriteRenderer = GetComponent<SpriteRenderer>();
            circle = GetComponent<CircleCollider2D>();
            flasher = GetComponent<SpriteFlasher>();
        }

        private void OnEnable() => MissileTargetRegistry.Register(this);
        private void OnDisable() => MissileTargetRegistry.Unregister(this);

        public void Init(Sprite sprite, float scale, int hp, Vector2 velocity, float spin, Pickup food, float foodDropChance)
        {
            if (sprite != null) spriteRenderer.sprite = sprite;
            spriteRenderer.color = Color.white;
            transform.localScale = Vector3.one * scale;
            circle.radius = spriteRenderer.sprite.bounds.extents.x * 0.85f;

            // Bigger rocks are tougher.
            maxHealth = health = Mathf.Max(1, Mathf.RoundToInt(hp * scale));
            body.linearVelocity = velocity;
            body.angularVelocity = spin;
            foodPrefab = food;
            foodChance = foodDropChance;
            alive = true;
            enteredScreen = false;
        }

        private void Update()
        {
            if (!alive) return;
            if (!enteredScreen && !CameraBounds.IsOutside(transform.position, 0f)) enteredScreen = true;
            if (enteredScreen && CameraBounds.IsOutside(transform.position, 2f)) Remove(); // flew past
        }

        public void TakeDamage(float amount)
        {
            if (!alive) return;
            health -= amount;
            if (health <= 0)
            {
                Shatter(awardScore: true);
                return;
            }
            Vfx.Spawn(hitVfx, transform.position, 0.5f);
            if (flasher != null) flasher.Flash(Color.white);
            else
            {
                StopAllCoroutines();
                StartCoroutine(Flash());
            }
        }

        public void OnHitPlayer() => Shatter(awardScore: false);

        public void OnMissileStrike(float bossDamageFraction) => Shatter(awardScore: true);

        private void Shatter(bool awardScore)
        {
            if (!alive) return;
            alive = false;
            Vector3 pos = transform.position;
            if (awardScore)
            {
                int score = Mathf.RoundToInt(maxHealth * scorePerHealth);
                GameManager.Instance?.AddScore(score);
                GameEvents.RaiseEnemyKilled(pos, score);
                if (foodPrefab != null && Random.value < foodChance)
                    PoolManager.Spawn(foodPrefab, pos, Quaternion.identity);
            }
            Vfx.Spawn(destroyVfx, pos, transform.localScale.x);
            GameEvents.RaiseScreenShake(0.1f, 0.15f);
            Remove();
        }

        private IEnumerator Flash()
        {
            spriteRenderer.color = hitFlashColor;
            yield return new WaitForSeconds(0.07f);
            spriteRenderer.color = Color.white;
        }

        public override void OnDespawned()
        {
            base.OnDespawned();
            alive = false;
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
        }
    }
}
