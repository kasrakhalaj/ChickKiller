using UnityEngine;

namespace ChickenShooter.Core
{
    /// <summary>World-space rectangle visible by the main orthographic camera (cached per frame).</summary>
    public static class CameraBounds
    {
        private static Camera cachedCamera;
        private static Rect cachedRect;
        private static int cachedFrame = -1;

        public static Camera Camera
        {
            get
            {
                if (cachedCamera == null) cachedCamera = Camera.main;
                return cachedCamera;
            }
        }

        public static Rect WorldRect
        {
            get
            {
                if (cachedFrame == Time.frameCount) return cachedRect;
                Camera cam = Camera;
                if (cam == null) return new Rect(-8, -5, 16, 10);
                float h = cam.orthographicSize * 2f;
                float w = h * cam.aspect;
                Vector3 c = cam.transform.position;
                cachedRect = new Rect(c.x - w * 0.5f, c.y - h * 0.5f, w, h);
                cachedFrame = Time.frameCount;
                return cachedRect;
            }
        }

        public static Vector2 ViewportToWorld(Vector2 viewport)
        {
            Rect r = WorldRect;
            return new Vector2(r.xMin + viewport.x * r.width, r.yMin + viewport.y * r.height);
        }

        public static bool IsOutside(Vector2 pos, float margin)
        {
            Rect r = WorldRect;
            return pos.x < r.xMin - margin || pos.x > r.xMax + margin ||
                   pos.y < r.yMin - margin || pos.y > r.yMax + margin;
        }

        public static bool IsBelow(Vector2 pos, float margin) => pos.y < WorldRect.yMin - margin;
    }
}
