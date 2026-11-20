using System;
using ChickenShooter.Combat;
using UnityEngine;

namespace ChickenShooter.Data
{
    /// <summary>Attack states a boss can use. Each maps to one state class of the boss FSM.</summary>
    public enum BossAttack
    {
        EggRain,        // sweeps across the top showering egg fans
        ChargeDash,     // telegraphs, rams the player's position, bursts into a feather ring
        AimedBurst,     // rapid aimed feather volleys
        FeatherSpiral,  // rotating bullet-hell spiral
        RadialBurst,    // expanding rings of eggs
        LaneBarrage,    // warning columns, then dense streams down those lanes
        HomingSalvo,    // fan of homing orbs
        Teleport,       // vanishes, reappears elsewhere with a burst
        SummonMinions   // calls small diving chicks (missile kills them instantly)
    }

    /// <summary>One health phase of a boss: entered when health drops below <see cref="enterBelowHealth"/>.</summary>
    [Serializable]
    public class BossPhase
    {
        public string label = "PHASE 1";
        [Range(0f, 1f)] public float enterBelowHealth = 1f;
        public BossAttack[] attacks = { BossAttack.AimedBurst };
        [Tooltip("Multiplies movement / dash speed in this phase.")]
        public float speedMultiplier = 1f;
        [Tooltip("Multiplies wait times between shots (lower = more aggressive).")]
        public float cooldownMultiplier = 1f;
        [Tooltip("Projectiles per pattern multiplier (spiral arms, ring size, volley size).")]
        public float densityMultiplier = 1f;
        public bool enraged;
        [Tooltip("Minions summoned when this phase begins (and by the SummonMinions attack).")]
        [Min(0)] public int minionsPerSummon;
    }

    /// <summary>
    /// Data-driven boss behaviour: ordered health phases, each with its own attack set and aggression,
    /// plus the projectiles and minions the boss uses. The FSM in BossController reads this asset,
    /// so each of the three bosses fights differently without boss-specific code.
    /// </summary>
    [CreateAssetMenu(fileName = "BossProfile", menuName = "Chick Killer/Boss Profile")]
    public class BossProfileSO : ScriptableObject
    {
        [Header("Phases (first = full health, last = enraged)")]
        public BossPhase[] phases;

        [Header("Movement")]
        public float moveSpeed = 4f;
        public float dashSpeed = 13f;
        [Tooltip("Pause after repositioning before the next attack (seconds).")]
        public float restTime = 0.5f;

        [Header("Projectiles")]
        public EnemyProjectile egg;
        public EnemyProjectile feather;
        public EnemyProjectile orb;
        public Color eggTint = Color.white;
        public Color featherTint = Color.white;
        public Color orbTint = Color.white;
        public float projectileSpeed = 5f;

        [Header("Minions")]
        public EnemyDataSO minion;

        [Header("Look")]
        public Color enragedTint = new Color(1f, 0.45f, 0.4f);
        public Sprite laneWarningSprite;
        public Material laneWarningMaterial;
        public GameObject phaseShiftVfx;
        public GameObject teleportVfx;
    }
}
