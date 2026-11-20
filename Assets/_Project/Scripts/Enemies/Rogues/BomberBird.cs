using System.Collections;
using ChickenShooter.Core;
using UnityEngine;

namespace ChickenShooter.Enemies
{
    /// <summary>
    /// Bomber (grumpy bird). Makes several horizontal passes across the top of the screen, dropping
    /// cluster bombs that burst into shards. Leaves after its last pass (cleared without score).
    /// </summary>
    public class BomberBird : RogueEnemy
    {
        [SerializeField] private int passes = 3;
        [SerializeField] private Vector2 bandViewportY = new Vector2(0.78f, 0.9f);
        [SerializeField] private float bobAmplitude = 0.3f;

        private float side;

        protected override Vector2 SpawnPosition()
        {
            side = Random.value < 0.5f ? -1f : 1f;
            return Viewport(side < 0 ? -0.08f : 1.08f, Random.Range(bandViewportY.x, bandViewportY.y));
        }

        protected override IEnumerator Behaviour()
        {
            for (int pass = 0; pass < passes && alive; pass++)
            {
                Rect cam = CameraBounds.WorldRect;
                float y = cam.yMin + cam.height * Random.Range(bandViewportY.x, bandViewportY.y);
                float startX = side < 0 ? cam.xMin - 1.2f : cam.xMax + 1.2f;
                float endX = side < 0 ? cam.xMax + 1.2f : cam.xMin - 1.2f;
                float dir = Mathf.Sign(endX - startX);
                body.position = new Vector2(startX, y);

                float t = 0f;
                float bombTimer = data.attack.NextInterval(mods.FireRate) * 0.5f;
                while (alive && (endX - body.position.x) * dir > 0f)
                {
                    t += Time.fixedDeltaTime;
                    Vector2 next = new Vector2(body.position.x + dir * Speed * Time.fixedDeltaTime,
                                               y + Mathf.Sin(t * 3f) * bobAmplitude);
                    Face(next - body.position);
                    body.MovePosition(next);

                    bombTimer -= Time.fixedDeltaTime;
                    bool onScreen = !CameraBounds.IsOutside(body.position, -0.5f);
                    if (bombTimer <= 0f && onScreen)
                    {
                        data.attack.Fire(body.position + Vector2.down * 0.4f);
                        bombTimer = data.attack.NextInterval(mods.FireRate);
                    }
                    yield return new WaitForFixedUpdate();
                }
                side = -side;
                yield return new WaitForSeconds(0.8f);
            }
            Leave();
        }
    }
}
