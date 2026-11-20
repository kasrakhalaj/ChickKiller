using ChickenShooter.Core;
using UnityEngine;

namespace ChickenShooter.Combat
{
    /// <summary>Roasted drumstick / burger: score + fills the food progress meter.</summary>
    public class FoodPickup : Pickup
    {
        [SerializeField] private int scoreValue = 50;
        [SerializeField] private float foodValue = 8f;

        protected override void ApplyReward(GameManager gm)
        {
            if (gm == null) return;
            gm.AddScore(scoreValue);
            gm.AddFood(foodValue);
        }
    }
}
