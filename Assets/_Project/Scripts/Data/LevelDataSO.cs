using System;
using UnityEngine;

namespace ChickenShooter.Data
{
    /// <summary>One parallax layer of a level's background theme.</summary>
    [Serializable]
    public class ParallaxLayerData
    {
        public Sprite sprite;
        [Tooltip("World units per second (downwards). Farther layers should be slower.")]
        public float scrollSpeed = 0.5f;
        public Color tint = Color.white;
    }

    /// <summary>A large decoration (planet, moon ...) drifting slowly behind the action.</summary>
    [Serializable]
    public class BackgroundPropData
    {
        public Sprite sprite;
        [Tooltip("Horizontal position in viewport space (0 = left, 1 = right; may be outside).")]
        public float viewportX = 0.8f;
        [Tooltip("Initial vertical position in viewport space.")]
        public float startViewportY = 0.6f;
        [Tooltip("Width in world units.")]
        public float worldWidth = 6f;
        public float scrollSpeed = 0.15f;
        public float rotationSpeed;
        public Color tint = Color.white;
        [Tooltip("Seconds to wait (off-screen) before the prop re-enters from the top after leaving.")]
        public float respawnDelay = 20f;
    }

    /// <summary>Visual theme of a level (Earth orbit, asteroid belt, deep galaxy ...).</summary>
    [Serializable]
    public class LevelTheme
    {
        public Color backgroundColor = Color.black;
        public ParallaxLayerData[] layers;
        [Tooltip("Planets / moons drawn between the far and near parallax layers.")]
        public BackgroundPropData[] props;
        [Tooltip("Index of the first layer drawn in front of the props.")]
        public int propsInsertIndex = 1;
    }

    /// <summary>
    /// A round = a reusable wave template + per-round difficulty multipliers. This lets several rounds
    /// share one WaveDataSO while still ramping up difficulty.
    /// </summary>
    [Serializable]
    public class RoundDefinition
    {
        public WaveDataSO wave;
        [Min(0.1f)] public float healthMultiplier = 1f;
        [Min(0.1f)] public float speedMultiplier = 1f;
        [Min(0.1f)] public float fireRateMultiplier = 1f;
        [Min(0.1f)] public float diveFrequencyMultiplier = 1f;
    }

    /// <summary>
    /// Data for one stage: number, scene name (used by SceneManager.LoadScene), music, theme and
    /// the ordered array of rounds. Editing/adding rounds needs no code changes.
    /// </summary>
    [CreateAssetMenu(fileName = "Level", menuName = "Chick Killer/Level Data")]
    public class LevelDataSO : ScriptableObject
    {
        [Header("Identity")]
        [Min(1)] public int levelNumber = 1;
        public string displayName = "Earth Orbit";
        [Tooltip("Exact scene name registered in Build Settings.")]
        public string sceneName = "Level1";

        [Header("Presentation")]
        public AudioClip music;
        public LevelTheme theme = new LevelTheme();

        [Header("Rounds (last one is normally the boss)")]
        public RoundDefinition[] rounds;

        public int RoundCount => rounds?.Length ?? 0;
    }
}
