using System.Collections;
using ChickenShooter.Core;
using ChickenShooter.Player;
using UnityEngine;

namespace ChickenShooter.Enemies
{
    /// <summary>
    /// Small chick summoned by bosses. States: Scatter (bursts away from the boss) -> Aim -> Dive (homes on
    /// the player briefly, then commits) -> leaves the screen. Weak, but fast and numerous.
    /// </summary>
    public class MinionDiver : RogueEnemy
    {
        [SerializeField] private float scatterTime = 0.45f;
        [SerializeField] private float homingTime = 0.5f;
        [SerializeField] private float turnRate = 160f;

        protected override Vector2 SpawnPosition() => Viewport(Random.Range(0.2f, 0.8f), 1.1f);

        protected override IEnumerator Behaviour()
        {
            // Scatter
            Vector2 scatter = body.position + Random.insideUnitCircle.normalized * Random.Range(1.2f, 2.2f);
            float t = 0f;
            while (alive && t < scatterTime)
            {
                t += Time.fixedDeltaTime;
                Vector2 next = Vector2.Lerp(body.position, scatter, 0.15f);
                Face(next - body.position);
                body.MovePosition(next);
                yield return new WaitForFixedUpdate();
            }

            // Dive at the player
            PlayerController player = PlayerController.Current;
            Vector2 dir = player != null ? (player.Position - body.position).normalized : Vector2.down;
            float speed = data.diveSpeed * mods.Speed;
            t = 0f;
            while (alive && !CameraBounds.IsOutside(body.position, 1f))
            {
                t += Time.fixedDeltaTime;
                if (player != null && t < homingTime)
                {
                    Vector2 to = (player.Position - body.position).normalized;
                    dir = Vector3.RotateTowards(dir, to, turnRate * Mathf.Deg2Rad * Time.fixedDeltaTime, 0f);
                }
                Vector2 next = body.position + dir * (speed * Time.fixedDeltaTime);
                Face(next - body.position);
                body.MovePosition(next);
                yield return new WaitForFixedUpdate();
            }
            Leave();
        }
    }
}
