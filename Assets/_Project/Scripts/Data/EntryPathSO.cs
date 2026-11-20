using System.Collections.Generic;
using ChickenShooter.Core;
using UnityEngine;

namespace ChickenShooter.Data
{
    public enum PathSpace
    {
        /// <summary>Points are absolute viewport coordinates (0..1, can go outside the screen).</summary>
        Viewport,
        /// <summary>X is an offset added to the chicken's slot viewport X (vertical cascades).</summary>
        RelativeToSlotX
    }

    /// <summary>
    /// Designer-authored curved entry path. Control points are in viewport space so the path adapts
    /// to any resolution; they are smoothed with a Catmull-Rom spline and end at the chicken's slot.
    /// </summary>
    [CreateAssetMenu(fileName = "EntryPath", menuName = "Chick Killer/Entry Path")]
    public class EntryPathSO : ScriptableObject
    {
        public PathSpace space = PathSpace.Viewport;
        public Vector2[] controlPoints =
        {
            new Vector2(0.1f, 1.15f), new Vector2(0.2f, 0.5f), new Vector2(0.5f, 0.3f), new Vector2(0.8f, 0.55f)
        };
        [Range(2, 24)] public int samplesPerSegment = 10;

        /// <summary>Builds world-space waypoints ending at <paramref name="slotWorld"/>.</summary>
        public List<Vector3> BuildWaypoints(Vector2 slotWorld, bool mirrored)
        {
            Rect cam = CameraBounds.WorldRect;
            float slotViewportX = Mathf.InverseLerp(cam.xMin, cam.xMax, slotWorld.x);

            var control = new List<Vector3>(controlPoints.Length + 1);
            foreach (Vector2 vp in controlPoints)
            {
                Vector2 p = vp;
                if (space == PathSpace.RelativeToSlotX)
                {
                    p.x = slotViewportX + (mirrored ? -p.x : p.x);
                }
                else if (mirrored)
                {
                    p.x = 1f - p.x;
                }
                control.Add(CameraBounds.ViewportToWorld(p));
            }
            control.Add(slotWorld);

            return CatmullRom.Sample(control, samplesPerSegment);
        }
    }

    /// <summary>Centripetal-free uniform Catmull-Rom sampling helper.</summary>
    public static class CatmullRom
    {
        public static List<Vector3> Sample(IReadOnlyList<Vector3> points, int samplesPerSegment)
        {
            var result = new List<Vector3>();
            if (points.Count < 2)
            {
                result.AddRange(points);
                return result;
            }

            for (int i = 0; i < points.Count - 1; i++)
            {
                Vector3 p0 = points[Mathf.Max(i - 1, 0)];
                Vector3 p1 = points[i];
                Vector3 p2 = points[i + 1];
                Vector3 p3 = points[Mathf.Min(i + 2, points.Count - 1)];

                for (int s = 0; s < samplesPerSegment; s++)
                {
                    float t = s / (float)samplesPerSegment;
                    result.Add(Evaluate(p0, p1, p2, p3, t));
                }
            }
            result.Add(points[points.Count - 1]);
            return result;
        }

        private static Vector3 Evaluate(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float t2 = t * t;
            float t3 = t2 * t;
            return 0.5f * (2f * p1 +
                           (-p0 + p2) * t +
                           (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
                           (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }
    }
}
