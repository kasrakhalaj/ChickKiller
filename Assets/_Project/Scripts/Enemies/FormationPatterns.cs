using System.Collections.Generic;
using ChickenShooter.Data;
using UnityEngine;

namespace ChickenShooter.Enemies
{
    /// <summary>Generates local slot offsets (centered on 0,0) for each formation pattern.</summary>
    public static class FormationPatterns
    {
        public static List<Vector2> Generate(FormationPattern pattern, int rows, int columns, Vector2 spacing)
        {
            List<Vector2> slots = pattern switch
            {
                FormationPattern.Grid => Grid(rows, columns, spacing),
                FormationPattern.VShape => VShape(rows, columns, spacing),
                FormationPattern.Arc => Arc(rows, columns, spacing),
                FormationPattern.Diamond => Diamond(rows, columns, spacing),
                FormationPattern.Columns => Columns(rows, columns, spacing),
                _ => Grid(rows, columns, spacing)
            };
            Recenter(slots);
            return slots;
        }

        private static List<Vector2> Grid(int rows, int cols, Vector2 s)
        {
            var list = new List<Vector2>(rows * cols);
            for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
                list.Add(new Vector2(c * s.x, -r * s.y));
            return list;
        }

        /// <summary>Chevrons pointing down; each row is another, wider V stacked above.</summary>
        private static List<Vector2> VShape(int rows, int cols, Vector2 s)
        {
            var list = new List<Vector2>();
            int armLength = Mathf.Max(1, cols / 2);
            for (int r = 0; r < rows; r++)
            {
                float yBase = r * s.y;
                list.Add(new Vector2(0f, yBase));
                for (int k = 1; k <= armLength; k++)
                {
                    float y = yBase + k * s.y * 0.55f;
                    list.Add(new Vector2(-k * s.x, y));
                    list.Add(new Vector2(k * s.x, y));
                }
            }
            return list;
        }

        /// <summary>Concentric arcs opening downward.</summary>
        private static List<Vector2> Arc(int rows, int cols, Vector2 s)
        {
            var list = new List<Vector2>();
            for (int r = 0; r < rows; r++)
            {
                float radius = cols * s.x * 0.32f + r * s.y;
                int count = cols + r;
                for (int i = 0; i < count; i++)
                {
                    float t = count == 1 ? 0.5f : i / (float)(count - 1);
                    float angle = Mathf.Lerp(200f, 340f, t) * Mathf.Deg2Rad;
                    list.Add(new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius * 0.6f + r * 0.1f));
                }
            }
            return list;
        }

        /// <summary>Tight diamond: rows widen to the middle then narrow again.</summary>
        private static List<Vector2> Diamond(int rows, int cols, Vector2 s)
        {
            var list = new List<Vector2>();
            int totalRows = rows * 2 + 1;
            for (int r = 0; r < totalRows; r++)
            {
                int width = Mathf.Max(1, Mathf.RoundToInt(Mathf.Lerp(1, cols, 1f - Mathf.Abs(r - rows) / (float)rows)));
                for (int c = 0; c < width; c++)
                {
                    float x = (c - (width - 1) * 0.5f) * s.x;
                    list.Add(new Vector2(x, -r * s.y * 0.75f));
                }
            }
            return list;
        }

        /// <summary>Dense staggered columns (checkerboard offset).</summary>
        private static List<Vector2> Columns(int rows, int cols, Vector2 s)
        {
            var list = new List<Vector2>(rows * cols);
            for (int c = 0; c < cols; c++)
            for (int r = 0; r < rows; r++)
                list.Add(new Vector2(c * s.x, -r * s.y - (c % 2) * s.y * 0.5f));
            return list;
        }

        private static void Recenter(List<Vector2> slots)
        {
            if (slots.Count == 0) return;
            Vector2 min = slots[0], max = slots[0];
            foreach (Vector2 p in slots)
            {
                min = Vector2.Min(min, p);
                max = Vector2.Max(max, p);
            }
            Vector2 center = (min + max) * 0.5f;
            for (int i = 0; i < slots.Count; i++) slots[i] -= center;
        }
    }
}
