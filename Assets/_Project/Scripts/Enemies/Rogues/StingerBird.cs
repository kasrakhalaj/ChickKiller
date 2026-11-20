using System.Collections;
using ChickenShooter.Core;
using ChickenShooter.Player;
using UnityEngine;

namespace ChickenShooter.Enemies
{
    /// <summary>
    /// Stinger (angry bee-bird). States:
    /// Enter -> Strafe (zig-zags across the upper screen firing aimed needle bursts) -> Telegraph (shivers)
    /// -> Dash (charges at the player's position) -> Recover (re-enters from the top) -> Strafe ...
    /// </summary>
    public class StingerBird : RogueEnemy
    {
        [SerializeField] private float strafeDuration = 3.5f;
        [SerializeField] private float burstInterval = 1.4f;
        [SerializeField] private int needlesPerBurst = 3;
        [SerializeField] private float needleGap = 0.12f;
        [SerializeField] private float telegraphTime = 0.55f;
        [SerializeField] private float dashSpeed = 13f;

        protected override Vector2 SpawnPosition() => Viewport(Random.Range(0.15f, 0.85f), 1.12f);

        protected override IEnumerator Behaviour()
        {
            yield return FlyTo(Viewport(Random.Range(0.2f, 0.8f), Random.Range(0.62f, 0.8f)), Speed * 1.5f);

            while (alive)
            {
                // --- Strafe
                float t = 0f, nextBurst = burstInterval * 0.5f;
                float dir = Random.value < 0.5f ? -1f : 1f;
                float baseY = body.position.y;
                Rect cam = CameraBounds.WorldRect;
                while (alive && t < strafeDuration)
                {
                    t += Time.fixedDeltaTime;
                    float x = body.position.x + dir * Speed * Time.fixedDeltaTime;
                    if (x < cam.xMin + 0.8f || x > cam.xMax - 0.8f) dir = -dir;
                    Vector2 next = new Vector2(x, baseY + Mathf.Sin(t * 5f) * 0.45f);
                    Face(next - body.position);
                    body.MovePosition(next);

                    if (t >= nextBurst)
                    {
                        nextBurst += burstInterval / Mathf.Max(0.1f, mods.FireRate);
                        StartCoroutine(NeedleBurst());
                    }
                    yield return new WaitForFixedUpdate();
                }

                // --- Telegraph + Dash
                yield return Telegraph(telegraphTime);
                PlayerController player = PlayerController.Current;
                Vector2 target = player != null ? player.Position : body.position + Vector2.down * 5f;
                Vector2 dashDir = (target - body.position).normalized;
                body.MoveRotation(Mathf.Atan2(dashDir.y, dashDir.x) * Mathf.Rad2Deg + 90f);
                while (alive && !CameraBounds.IsOutside(body.position, 1f))
                {
                    body.MovePosition(body.position + dashDir * (dashSpeed * mods.Speed * Time.fixedDeltaTime));
                    yield return new WaitForFixedUpdate();
                }

                // --- Recover: come back from the top
                body.rotation = 0f;
                body.position = Viewport(Random.Range(0.15f, 0.85f), 1.12f);
                yield return FlyTo(Viewport(Random.Range(0.2f, 0.8f), Random.Range(0.62f, 0.8f)), Speed * 1.5f);
            }
        }

        private IEnumerator NeedleBurst()
        {
            for (int i = 0; i < needlesPerBurst && alive; i++)
            {
                data.attack.Fire(body.position + Vector2.down * 0.3f);
                yield return new WaitForSeconds(needleGap);
            }
        }
    }
}
