using ChickenShooter.Combat;
using ChickenShooter.Enemies;
using UnityEngine;

namespace ChickenShooter.Data
{
    /// <summary>
    /// Enemy archetype. The prefab decides the behaviour family:
    /// a <see cref="Chicken"/> prefab flies in the flock (optionally with ability components such as
    /// ArmorPlating / ReviveOnDeath / HealerAura), a <see cref="RogueEnemy"/> prefab is a free agent with
    /// its own state machine (Stinger, Bomber, Gunner), a <see cref="BossController"/> prefab is a boss.
    /// Stats, look, attack and drops come from this asset, so stages can differ without code changes.
    /// </summary>
    [CreateAssetMenu(fileName = "Enemy", menuName = "Chick Killer/Enemy Data")]
    public class EnemyDataSO : ScriptableObject
    {
        public string displayName = "Space Hen";

        [Header("Behaviour")]
        public WaveEntity prefab;

        [Header("Visuals")]
        public Sprite[] flyFrames;
        public float frameRate = 10f;
        public Color tint = Color.white;
        public float scale = 1f;

        [Header("Stats")]
        [Min(1)] public int maxHealth = 2;
        [Tooltip("Base movement speed (formation approach / return, free-agent cruising).")]
        public float moveSpeed = 7f;
        public int scoreValue = 100;

        [Header("Attack")]
        public EnemyAttack attack = new EnemyAttack();

        [Header("Dive bomb (flock members)")]
        public bool canDive = true;
        public float diveSpeed = 8f;
        [Tooltip("Degrees per second the diver can steer toward the player during the homing phase.")]
        public float diveTurnRate = 90f;

        [Header("Drops")]
        public Pickup foodPrefab;
        [Range(0f, 1f)] public float foodDropChance = 0.5f;
        public Pickup giftPrefab;
        [Range(0f, 1f)] public float giftDropChance = 0.05f;
        [Tooltip("Independent chance to drop a special-weapon crate (rockets, minigun, laser ...).")]
        [Range(0f, 1f)] public float specialDropChance = 0.02f;

        [Header("Effects")]
        public GameObject deathVfx;
        public GameObject hitVfx;
    }
}
