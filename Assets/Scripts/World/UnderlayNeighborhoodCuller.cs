using System.Collections.Generic;
using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// Optional grid helper to skip underlays for fully-buried tiles.
    /// </summary>
    public static class UnderlayNeighborhoodCuller
    {
        public sealed class Grid
        {
            public readonly float cell;
            public readonly Vector2 origin;
            public readonly int width, height;
            public readonly bool[,] solid;

            public Grid(int w, int h, float cell, Vector2 origin)
            {
                width = w; height = h; this.cell = cell; this.origin = origin;
                solid = new bool[w, h];
            }
        }

        /// <summary>Build occupancy grid from ground-layer SpriteRenderers.</summary>
        public static Grid BuildGridFromSprites(List<SpriteRenderer> srs, int groundLayer)
        {
            SpriteRenderer first = null;
            foreach (var sr in srs)
                if (sr && sr.gameObject.layer == groundLayer) { first = sr; break; }
            if (!first) return null;

            float cell = Mathf.Max(0.01f, first.bounds.size.x);
            Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            Vector2 max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);

            foreach (var sr in srs)
            {
                if (!sr || sr.gameObject.layer != groundLayer) continue;
                var b = sr.bounds;
                if (b.min.x < min.x) min.x = b.min.x;
                if (b.min.y < min.y) min.y = b.min.y;
                if (b.max.x > max.x) max.x = b.max.x;
                if (b.max.y > max.y) max.y = b.max.y;
            }

            int w = Mathf.Clamp(Mathf.CeilToInt((max.x - min.x) / cell) + 2, 1, 8192);
            int h = Mathf.Clamp(Mathf.CeilToInt((max.y - min.y) / cell) + 2, 1, 8192);
            var grid = new Grid(w, h, cell, min);

            foreach (var sr in srs)
            {
                if (!sr || sr.gameObject.layer != groundLayer) continue;
                Vector2 p = sr.transform.position;
                int ix = Mathf.Clamp(Mathf.FloorToInt((p.x - min.x) / cell), 0, w - 1);
                int iy = Mathf.Clamp(Mathf.FloorToInt((p.y - min.y) / cell), 0, h - 1);
                grid.solid[ix, iy] = true;
            }
            return grid;
        }

        /// <summary>True if the sprite has 4 solid neighbors (up/down/left/right).</summary>
        public static bool IsFullyBuried(SpriteRenderer sr, Grid grid)
        {
            if (!sr || grid == null) return false;
            Vector2 p = sr.transform.position;
            int ix = Mathf.Clamp(Mathf.FloorToInt((p.x - grid.origin.x) / grid.cell), 0, grid.width - 1);
            int iy = Mathf.Clamp(Mathf.FloorToInt((p.y - grid.origin.y) / grid.cell), 0, grid.height - 1);

            bool up    = iy + 1 < grid.height && grid.solid[ix, iy + 1];
            bool down  = iy - 1 >= 0          && grid.solid[ix, iy - 1];
            bool left  = ix - 1 >= 0          && grid.solid[ix - 1, iy];
            bool right = ix + 1 < grid.width  && grid.solid[ix + 1, iy];

            return up && down && left && right;
        }
    }
}