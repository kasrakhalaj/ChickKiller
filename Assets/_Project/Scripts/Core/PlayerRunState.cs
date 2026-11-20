using System;
using ChickenShooter.Data;

namespace ChickenShooter.Core
{
    /// <summary>
    /// Plain serializable snapshot of the player's progress. Carried across scenes by the
    /// GameManager and designed to be written as-is to JSON (savegame.json) in Phase 2.
    /// </summary>
    [Serializable]
    public class PlayerRunState
    {
        public int levelIndex;      // 0-based index into GameConfigSO.levels
        public int roundIndex;      // 0-based index into LevelDataSO.rounds
        public int score;
        public int lives;
        public int missiles;
        public int powerLevel;
        public float foodProgress;  // 0..GameConfigSO.foodMeterMax
        public float[] specialAmmo; // parallel to GameConfigSO.specialWeapons
        public int selectedSpecial = -1;

        public static PlayerRunState CreateNew(GameConfigSO config)
        {
            return new PlayerRunState
            {
                specialAmmo = new float[config.specialWeapons?.Length ?? 0],
                selectedSpecial = -1,
                levelIndex = 0,
                roundIndex = 0,
                score = 0,
                lives = config.startingLives,
                missiles = config.startingMissiles,
                powerLevel = config.basePowerLevel,
                foodProgress = 0f
            };
        }

        public PlayerRunState Clone()
        {
            var copy = (PlayerRunState)MemberwiseClone();
            copy.specialAmmo = (float[])specialAmmo?.Clone();
            return copy;
        }
    }
}
