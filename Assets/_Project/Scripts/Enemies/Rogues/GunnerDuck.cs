using System.Collections;
using UnityEngine;

namespace ChickenShooter.Enemies
{
    /// <summary>
    /// Sniper duck. States: Enter (from a side) -> Perch -> Telegraph (charges up) -> Fire (aimed shots)
    /// -> Reposition to a new perch ... Leaves after a number of cycles if still alive.
    /// </summary>
    public class GunnerDuck : RogueEnemy
    {
        [SerializeField] private int cycles = 6;
        [SerializeField] private float telegraphTime = 0.75f;
        [SerializeField] private int shotsPerVolley = 2;
        [SerializeField] private float shotGap = 0.18f;
        [SerializeField] private float restTime = 0.9f;

        protected override Vector2 SpawnPosition() =>
            Viewport(Random.value < 0.5f ? -0.08f : 1.08f, Random.Range(0.55f, 0.85f));

        protected override IEnumerator Behaviour()
        {
            for (int i = 0; i < cycles && alive; i++)
            {
                Vector2 perch = Viewport(Random.Range(0.12f, 0.88f), Random.Range(0.55f, 0.82f));
                yield return FlyTo(perch, Speed);
                yield return Telegraph(telegraphTime / Mathf.Max(0.5f, mods.FireRate));
                for (int s = 0; s < shotsPerVolley && alive; s++)
                {
                    data.attack.Fire(body.position + Vector2.down * 0.35f, mods.Speed);
                    yield return new WaitForSeconds(shotGap);
                }
                yield return new WaitForSeconds(restTime);
            }

            if (!alive) yield break;
            Vector2 exit = Viewport(body.position.x < 0 ? -0.15f : 1.15f, 1.15f);
            yield return FlyTo(exit, Speed * 1.3f);
            Leave();
        }
    }
}
