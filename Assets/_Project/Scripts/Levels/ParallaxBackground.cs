using System.Collections.Generic;
using ChickenShooter.Core;
using ChickenShooter.Data;
using UnityEngine;

namespace ChickenShooter.Levels
{
    /// <summary>
    /// Multi-layer vertically scrolling starfield built from the level theme. Each layer is two stacked
    /// copies of a sprite scaled to cover the screen width; they wrap seamlessly while scrolling down.
    /// Large props (planets, moons) drift between the layers and re-enter from the top after a delay.
    /// </summary>
    public class ParallaxBackground : MonoBehaviour
    {
        [SerializeField] private int baseSortingOrder = -100;
        [Tooltip("Optional: theme applied on Start (for scenes without a LevelBootstrap, e.g. the main menu).")]
        [SerializeField] private LevelDataSO themeOnStart;

        private class Layer
        {
            public Transform a;
            public Transform b;
            public float height;
            public float speed;
        }

        private class Prop
        {
            public Transform transform;
            public BackgroundPropData data;
            public float halfHeight;
            public float waitTimer;
        }

        private readonly List<Layer> layers = new();
        private readonly List<Prop> props = new();

        private void Start()
        {
            if (themeOnStart != null) ApplyTheme(themeOnStart.theme);
        }

        public void ApplyTheme(LevelTheme theme)
        {
            foreach (Transform child in transform) Destroy(child.gameObject);
            layers.Clear();
            props.Clear();

            Camera cam = CameraBounds.Camera;
            if (cam != null) cam.backgroundColor = theme.backgroundColor;
            if (theme.layers == null) return;

            Rect view = CameraBounds.WorldRect;
            for (int i = 0; i < theme.layers.Length; i++)
            {
                ParallaxLayerData data = theme.layers[i];
                if (data.sprite == null) continue;

                Vector2 size = data.sprite.bounds.size;
                // Cover the full screen width (and at least the full height per tile).
                float scale = Mathf.Max(view.width / size.x, view.height / size.y) * 1.02f;
                float height = size.y * scale;

                var layer = new Layer
                {
                    a = CreateTile(data, i, scale, "A"),
                    b = CreateTile(data, i, scale, "B"),
                    height = height,
                    speed = data.scrollSpeed
                };
                layer.a.position = new Vector3(view.center.x, view.center.y, 0f);
                layer.b.position = new Vector3(view.center.x, view.center.y + height, 0f);
                layers.Add(layer);
            }

            if (theme.props == null) return;
            int propOrder = baseSortingOrder + theme.propsInsertIndex * 2 - 1;
            foreach (BackgroundPropData data in theme.props)
            {
                if (data.sprite == null) continue;
                var go = new GameObject($"Prop_{data.sprite.name}");
                go.transform.SetParent(transform, false);
                float scale = data.worldWidth / data.sprite.bounds.size.x;
                go.transform.localScale = Vector3.one * scale;
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = data.sprite;
                sr.color = data.tint;
                sr.sortingOrder = propOrder;
                go.transform.position = CameraBounds.ViewportToWorld(new Vector2(data.viewportX, data.startViewportY));
                props.Add(new Prop { transform = go.transform, data = data, halfHeight = data.sprite.bounds.extents.y * scale });
            }
        }

        private Transform CreateTile(ParallaxLayerData data, int index, float scale, string suffix)
        {
            var go = new GameObject($"Layer{index}_{suffix}");
            go.transform.SetParent(transform, false);
            go.transform.localScale = Vector3.one * scale;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = data.sprite;
            sr.color = data.tint;
            sr.sortingOrder = baseSortingOrder + index * 2;
            return go.transform;
        }

        private void Update()
        {
            float bottom = CameraBounds.WorldRect.center.y;
            foreach (Layer l in layers)
            {
                Vector3 delta = Vector3.down * (l.speed * Time.deltaTime);
                l.a.position += delta;
                l.b.position += delta;
                Wrap(l.a, l.b, l.height, bottom);
                Wrap(l.b, l.a, l.height, bottom);
            }

            Rect view = CameraBounds.WorldRect;
            foreach (Prop p in props)
            {
                if (p.waitTimer > 0f)
                {
                    p.waitTimer -= Time.deltaTime;
                    if (p.waitTimer <= 0f)
                    {
                        float x = view.xMin + view.width * Mathf.Clamp(p.data.viewportX + Random.Range(-0.15f, 0.15f), -0.1f, 1.1f);
                        p.transform.position = new Vector3(x, view.yMax + p.halfHeight, 0f);
                    }
                    continue;
                }
                p.transform.position += Vector3.down * (p.data.scrollSpeed * Time.deltaTime);
                p.transform.Rotate(0f, 0f, p.data.rotationSpeed * Time.deltaTime);
                if (p.transform.position.y < view.yMin - p.halfHeight) p.waitTimer = p.data.respawnDelay;
            }
        }

        private static void Wrap(Transform tile, Transform other, float height, float centerY)
        {
            if (tile.position.y <= centerY - height)
            {
                tile.position = new Vector3(tile.position.x, other.position.y + height, tile.position.z);
            }
        }
    }
}
