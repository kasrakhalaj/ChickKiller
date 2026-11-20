using UnityEngine;

namespace ChickenShooter.Data
{
    /// <summary>
    /// Global game configuration (loaded from Resources/GameConfig). The ordered level list defines
    /// the campaign: adding a level = create a LevelDataSO + scene and append it here. No code changes.
    /// </summary>
    [CreateAssetMenu(fileName = "GameConfig", menuName = "Chick Killer/Game Config")]
    public class GameConfigSO : ScriptableObject
    {
        [Header("Campaign")]
        public LevelDataSO[] levels;
        public string mainMenuScene = "MainMenu";

        [Header("Player start values")]
        [Min(1)] public int startingLives = 3;
        [Min(0)] public int startingMissiles = 2;
        [Min(1)] public int basePowerLevel = 1;
        [Min(1)] public int maxPowerLevel = 3;

        [Header("Damage rule")]
        [Tooltip("ON: a hit only costs one power level while power is above base; at base power the ship is destroyed " +
                 "(one life) and respawns. OFF (assignment default): every hit costs a life AND one power level.")]
        public bool powerActsAsShield = true;
        [Tooltip("Weapon power after respawning from a destroyed ship / retrying (shield rule only).")]
        [Min(1)] public int respawnPowerLevel = 3;
        [Tooltip("Seconds the ship stays destroyed before respawning.")]
        public float respawnDelay = 1.4f;

        [Header("Special weapons (limited ammo, picked up from enemies)")]
        public SpecialWeaponSO[] specialWeapons;
        public Combat.SpecialWeaponPickup specialPickupPrefab;

        [Header("Food meter")]
        [Tooltip("Food points needed to earn one extra missile.")]
        [Min(1)] public float foodMeterMax = 100f;

        [Header("Flow timings (seconds)")]
        public float levelCompleteDelay = 3f;
    }
}
