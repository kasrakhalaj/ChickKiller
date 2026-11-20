using ChickenShooter.Core;
using UnityEngine;

namespace ChickenShooter.Combat
{
    /// <summary>Weapon gift box: raises weapon power by one level (max 3). Bonus score when already maxed.</summary>
    public class GiftBoxPickup : Pickup
    {
        [SerializeField] private int bonusScoreWhenMaxed = 500;

        protected override void ApplyReward(GameManager gm)
        {
            if (gm == null) return;
            if (gm.IsPowerMaxed) gm.AddScore(bonusScoreWhenMaxed);
            else gm.ChangePowerLevel(+1);
        }
    }
}
