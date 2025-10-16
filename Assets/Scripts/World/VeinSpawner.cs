// File: Assets/Scripts/World/VeinSpawner.cs
using System;
using UnityEngine;
using Game.Data;

namespace Game.World
{
    /// <summary>
    /// Deterministic (seed + chunk coord) rare vein spawner.
    /// Paints material ids into material[,] without changing solidity.
    /// </summary>
    public static class VeinSpawner
    {
        public static void ApplyVeins(
            int worldSeed, Vector2Int chunkCoord, int chunkPixels,
            BandResolver.Band band, VeinTableSO table,
            bool[,] solid, byte[,] material)
        {
            if (table == null) return;

            var rule = band switch
            {
                BandResolver.Band.Yard      => table.yard,
                BandResolver.Band.Galleries => table.galleries,
                _                           => table.ancient
            };
            if (rule.material == VeinMaterial.None || rule.spawnChance <= 0f) return;

            // Deterministic RNG per chunk
            var rng = new System.Random(Hash(worldSeed, chunkCoord.x, chunkCoord.y, 137));

            // Roll presence
            double roll = rng.NextDouble();
            if (roll > rule.spawnChance) return;

            // Pick a center within the chunk; prefer solid spots (try K attempts)
            const int K = 8;
            int cx = rng.Next(4, chunkPixels - 4);
            int cy = rng.Next(4, chunkPixels - 4);
            for (int a = 0; a < K; a++)
            {
                int tx = rng.Next(4, chunkPixels - 4);
                int ty = rng.Next(4, chunkPixels - 4);
                if (solid[tx, ty]) { cx = tx; cy = ty; break; }
            }

            int r = Mathf.Clamp(rule.clusterRadiusPx, 2, Mathf.Min(12, chunkPixels / 2));
            float r2 = r * r;

            // Jittered fill inside radius based on density and RNG
            for (int y = cy - r; y <= cy + r; y++)
            {
                if (y < 0 || y >= chunkPixels) continue;
                int dy = y - cy;
                int dxMax = Mathf.RoundToInt(Mathf.Sqrt(Mathf.Max(0, r2 - dy * dy)));
                int xlo = Mathf.Max(0, cx - dxMax);
                int xhi = Mathf.Min(chunkPixels - 1, cx + dxMax);

                for (int x = xlo; x <= xhi; x++)
                {
                    if (!solid[x, y]) continue; // keep ore embedded in rock
                    if (rng.NextDouble() <= rule.density)
                        material[x, y] = (byte)rule.material;
                }
            }
        }

        static int Hash(int s, int x, int y, int z)
        {
            unchecked
            {
                int h = s;
                h = (h * 73856093) ^ x;
                h = (h * 19349663) ^ y;
                h = (h * 83492791) ^ z;
                return h;
            }
        }
    }
}

