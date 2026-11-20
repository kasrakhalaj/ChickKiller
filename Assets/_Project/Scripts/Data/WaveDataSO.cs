using ChickenShooter.Enemies;
using UnityEngine;

namespace ChickenShooter.Data
{
    public enum WaveType
    {
        Formation,
        Asteroids,
        Boss
    }

    public enum FormationPattern
    {
        Grid,       // Level 1: classic rows & columns
        VShape,     // Level 2: chevrons
        Arc,        // Level 2: curved arc
        Diamond,    // Level 3: tight diamond cluster
        Columns     // Level 3: staggered dense columns
    }

    /// <summary>Special birds that replace some slots of the flock (armored, zombie, nurse hens ...).</summary>
    [System.Serializable]
    public class FlockSpecial
    {
        public EnemyDataSO enemy;
        [Min(1)] public int count = 2;
    }

    /// <summary>Free agents (stinger, bomber, sniper) that join the round independently of the flock.</summary>
    [System.Serializable]
    public class RogueSpawn
    {
        public EnemyDataSO enemy;
        [Min(1)] public int count = 1;
        [Tooltip("Seconds after the round starts before the first one appears.")]
        public float startDelay = 4f;
        [Tooltip("Seconds between consecutive spawns of this entry.")]
        public float interval = 5f;
    }

    /// <summary>
    /// Configuration of one wave template: enemy type, formation layout, entry path, movement,
    /// dive behaviour and (for special rounds) asteroid / boss settings.
    /// </summary>
    [CreateAssetMenu(fileName = "Wave", menuName = "Chick Killer/Wave Data")]
    public class WaveDataSO : ScriptableObject
    {
        public string label = "Wave";
        public WaveType waveType = WaveType.Formation;

        [Header("Enemy")]
        [Tooltip("Enemy archetype (sprite, health, egg rate, prefab ...). For boss waves: the boss data.")]
        public EnemyDataSO enemy;

        [Header("Formation")]
        public FormationPattern formation = FormationPattern.Grid;
        [Min(1)] public int rows = 3;
        [Min(1)] public int columns = 8;
        public Vector2 spacing = new Vector2(1.15f, 0.95f);
        [Tooltip("Viewport-space center of the formation.")]
        public Vector2 formationCenterViewport = new Vector2(0.5f, 0.7f);
        public float swayAmplitude = 1.2f;
        public float swaySpeed = 0.5f;
        public float bobAmplitude = 0.15f;

        [Tooltip("Slots of the flock taken by special birds instead of the main enemy.")]
        public FlockSpecial[] flockSpecials;

        [Header("Free agents (any wave type)")]
        public RogueSpawn[] rogues;

        [Header("Entry")]
        public EntryPathSO entryPath;
        [Tooltip("Odd-indexed chickens use the mirrored path (enter from both sides).")]
        public bool mirrorAlternate = true;
        public float entrySpeed = 7f;
        [Tooltip("Delay between consecutive chickens starting their entry path.")]
        public float entryStagger = 0.12f;

        [Header("Dive bombing")]
        public Vector2 diveInterval = new Vector2(3f, 6f);
        [Min(0)] public int maxSimultaneousDivers = 1;

        [Header("Asteroid round")]
        public Asteroid asteroidPrefab;
        public Sprite[] asteroidSprites;
        [Min(1)] public int asteroidCount = 20;
        [Min(1)] public int asteroidHealth = 3;
        public Vector2 asteroidSpawnInterval = new Vector2(0.35f, 0.8f);
        public Vector2 asteroidSpeed = new Vector2(2f, 4f);
        public Vector2 asteroidScale = new Vector2(0.8f, 1.4f);
        [Range(0f, 1f)] public float asteroidFoodDropChance = 0.25f;

        [Header("Boss round")]
        public BossController bossPrefab;
        [Tooltip("Phases and attack patterns of the boss FSM.")]
        public BossProfileSO bossProfile;
    }
}
