// Namespace: Game.World
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Game.Data;

namespace Game.World
{
    /// <summary>
    /// World factory + debug mining. No FX or Player dependency; Camera is used for input target.
    /// </summary>
    public class TerrainWorld : MonoBehaviour
    {
        [Header("World Generation")]
        public int seed = 12345;

        [Tooltip("Base scale of noise. Smaller = larger features.")]
        public float noiseScale = 0.0105f;

        [Range(0f, 1f)]
        [Tooltip("Cave fill threshold after shaping. Lower = more open air.")]
        public float threshold = 0.52f;

        [Header("FBM (multi-octave)")]
        [Range(1, 8)] public int octaves = 4;
        [Tooltip("Frequency multiplier per octave (≈2).")]
        public float lacunarity = 2.0f;
        [Tooltip("Amplitude multiplier per octave (≈0.5).")]
        public float gain = 0.5f;

        [Header("Domain Warp (subtle)")]
        [Tooltip("How much to perturb the coordinates (0…0.25 recommended).")]
        public float warpStrength = 0.12f;

        [Header("Cave Smoothing")]
        [Tooltip("How many cellular passes to run (2–3 typical).")]
        [Range(0,6)] public int smoothIterations = 2;
        [Tooltip("Solid if >= this many solid neighbors (8-neighborhood).")]
        [Range(0,8)] public int birthLimit = 5;   // B5
        [Tooltip("Solid survives if >= this many solid neighbors.")]
        [Range(0, 8)] public int surviveMin = 4;  // S45 (lower bound)
        
        [Header("Passability Guard")]
        [Tooltip("Minimum open corridor width in tiles. 2 is a good default.")]
        [Range(1,4)] public int minCorridorWidth = 2;
        [Tooltip("Apply passability guard (seam-safe).")]
        public bool enablePassabilityGuard = true;

        [Header("Chunk")]
        public int chunkPixels = 64;
        public float pixelsPerUnit = 32f;
        public Material spriteMat;

        [Header("Brush (Debug Mining)")]
        public float brushWorldRadius = 0.35f;
        public float digRateHz = 8f;
        public float fillRateHz = 6f;

        [Header("Bands")]
        public BandConfigSO bandConfig;

        float _chunkWorldSize;
        Camera _cam;
        float _digAcc, _fillAcc;
        readonly List<TerrainChunk.RimSample> _rimScratch = new(256);

        [Header("Content")]
        public VeinTableSO veinTable;
        public bool debugTintMaterials = true;
        

        // NEW: one global offset so adjacent chunks share the same noise field
        Vector2 _noiseOffset;
        Vector2 _warpOffset;

        void Awake()
        {
            _cam = Camera.main;
            _chunkWorldSize = chunkPixels / pixelsPerUnit;
            BandResolver.SetConfig(bandConfig);

            // Deterministic offsets from seed (stable across all chunks)
            var rng = new System.Random(seed);
            _noiseOffset = new Vector2(rng.Next(0, 100000), rng.Next(0, 100000));
            _warpOffset  = new Vector2(rng.Next(0, 100000), rng.Next(0, 100000));
        }

        public TerrainChunk CreateChunk(Vector2Int cxy, Transform parent = null)
        {
            var go = new GameObject($"Chunk_{cxy.x}_{cxy.y}");
            if (parent != null) go.transform.SetParent(parent, false);
            else go.transform.SetParent(transform, false);

            // inherit layer from parent (expect Ground)
            go.layer = gameObject.layer;

            var c = go.AddComponent<TerrainChunk>();
            c.Init(chunkPixels, pixelsPerUnit, spriteMat);

            go.transform.position = new Vector2(
                (cxy.x + 0.5f) * _chunkWorldSize,
                (cxy.y + 0.5f) * _chunkWorldSize
            );

            (bool[,] solid, byte[,] material) = GenerateMapsForChunk(cxy);
            c.SetMaps(solid, material);
            return c;
        }

       public (bool[,] solid, byte[,] material) GenerateMapsForChunk(Vector2Int cxy)
        {
            int N = chunkPixels;
            bool[,] solid = new bool[N, N];
            byte[,] material = new byte[N, N];

            // 1) Base fill from the same global noise field (already seamless).
            for (int py = 0; py < N; py++)
            {
                for (int px = 0; px < N; px++)
                {
                    int wx = cxy.x * N + px;
                    int wy = cxy.y * N + py;
                    bool isSolid = BaseSolidAtWorld(wx, wy);
                    solid[px, py] = isSolid;
                    material[px, py] = 0;
                }
            }

            // 2) Seam-safe cellular smoothing (B5/S45) using world sampling along edges.
            if (smoothIterations > 0)
                SmoothChunkSeamSafe(cxy, ref solid, smoothIterations, birthLimit, surviveMin);

            // 3) Passability guard (seam-safe)
            if (enablePassabilityGuard && minCorridorWidth > 1)
                EnsurePassabilitySeamSafe(cxy, ref solid, minCorridorWidth);

            // Determine band from tile Y (top row world Y)
            int topTileY = cxy.y * chunkPixels; // tile coords since 1px == 1 tile
            var band = BandResolver.BandFromY(topTileY);

            // Paint veins deterministically (does not change solid[,]—only material[,])
            VeinSpawner.ApplyVeins(seed, cxy, chunkPixels, band, veinTable, solid, material);

            return (solid, material);
        }

        // --- Debug mining (LMB dig, RMB fill) ---
        void Update()
        {
            if (_cam == null) _cam = Camera.main;
            var mouse = Mouse.current; if (mouse == null || _cam == null) return;

            bool digging = mouse.leftButton.isPressed;
            bool filling = mouse.rightButton.isPressed;

            _digAcc += digging ? Time.deltaTime : 0f;
            _fillAcc += filling ? Time.deltaTime : 0f;

            if (digging)
            {
                float step = 1f / Mathf.Max(1e-3f, digRateHz);
                while (_digAcc >= step) { _digAcc -= step; Stroke(true); }
            }
            else _digAcc = 0f;

            if (filling)
            {
                float step = 1f / Mathf.Max(1e-3f, fillRateHz);
                while (_fillAcc >= step) { _fillAcc -= step; Stroke(false); }
            }
            else _fillAcc = 0f;
        }

        float Fbm(float x, float y)
        {
            float amp = 1f;
            float freq = 1f;
            float sum = 0f;
            float norm = 0f;
            for (int i = 0; i < octaves; i++)
            {
                sum  += amp * Mathf.PerlinNoise(x * freq, y * freq);
                norm += amp;
                amp  *= gain;
                freq *= lacunarity;
            }
            return (norm > 0f) ? (sum / norm) : 0.5f;
        }

        Vector2 Warp(float x, float y)
        {
            if (warpStrength <= 0f) return Vector2.zero;

            // two small fbm fields to perturb x & y
            float wx = Fbm(x + _warpOffset.x, y + _warpOffset.y);
            float wy = Fbm(x + _warpOffset.y, y + _warpOffset.x);
            // remap to [-1,1]
            wx = wx * 2f - 1f;
            wy = wy * 2f - 1f;
            return new Vector2(wx, wy) * warpStrength;
        }

        static float Smooth01(float v)
        {
            // cubic smoothstep (0..1)
            v = Mathf.Clamp01(v);
            return v * v * (3f - 2f * v);
        }

        void Stroke(bool isDig)
        {
            Vector2 screen = Mouse.current.position.ReadValue();
            var ray = _cam.ScreenPointToRay(screen);
            var plane = new Plane(Vector3.forward, Vector3.zero);
            if (!plane.Raycast(ray, out float t)) return;

            Vector2 world = ray.GetPoint(t);
            float radius = brushWorldRadius;

            var hits = Physics2D.OverlapCircleAll(world, radius * 1.1f);
            bool any = false;
            _rimScratch.Clear();

            for (int i = 0; i < hits.Length; i++)
            {
                var chunk = hits[i].GetComponent<TerrainChunk>();
                if (!chunk) continue;
                any |= chunk.ApplyCircle(
                    world, radius,
                    isDig ? TerrainChunk.BrushMode.Dig : TerrainChunk.BrushMode.Fill,
                    _rimScratch, 192);
            }

            // No FX yet. We'll wire particles later.
        }


        // === NEW: world-space base solidity (deterministic, matches generation) ===
        bool BaseSolidAtWorld(int wx, int wy)
        {
            float x = (wx + _noiseOffset.x) * noiseScale;
            float y = (wy + _noiseOffset.y) * noiseScale;
            Vector2 w = Warp(x, y);
            float n = Fbm(x + w.x, y + w.y);
            n = Smooth01(n);
            return n > threshold;
        }

        // === NEW: seam-safe cellular smooth ===
        void SmoothChunkSeamSafe(Vector2Int cxy, ref bool[,] solid, int iters, int birth, int survive)
        {
            int N = chunkPixels;
            bool[,] buffer = new bool[N, N];

            for (int iter = 0; iter < iters; iter++)
            {
                for (int py = 0; py < N; py++)
                {
                    for (int px = 0; px < N; px++)
                    {
                        int wx = cxy.x * N + px;
                        int wy = cxy.y * N + py;

                        int solidNeighbors = 0;
                        for (int oy = -1; oy <= 1; oy++)
                            for (int ox = -1; ox <= 1; ox++)
                            {
                                if (ox == 0 && oy == 0) continue;
                                int nx = px + ox, ny = py + oy;
                                bool neighborSolid;

                                if (nx >= 0 && ny >= 0 && nx < N && ny < N)
                                {
                                    neighborSolid = solid[nx, ny];
                                }
                                else
                                {
                                    // Outside this chunk? Sample the same global field
                                    // so smoothing is continuous across borders.
                                    neighborSolid = BaseSolidAtWorld(wx + ox, wy + oy);
                                }
                                if (neighborSolid) solidNeighbors++;
                            }

                        bool current = solid[px, py];
                        bool next = current
                            ? (solidNeighbors >= survive)
                            : (solidNeighbors >= birth);
                        buffer[px, py] = next;
                    }
                }

                // swap buffers
                var tmp = solid;
                solid = buffer;
                buffer = tmp;
            }
        }
        // Enforce ≥ minWidth corridors and remove 1px diagonals, seam-safe across chunk edges.
        void EnsurePassabilitySeamSafe(Vector2Int cxy, ref bool[,] solid, int minWidth)
        {
            int N = chunkPixels;
            // 1) Remove single-pixel diagonal connections (4-neighborhood continuity)
            bool changed;
            do
            {
                changed = false;
                for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    if (solid[x, y]) continue; // only consider open tiles
                    // Diagonal bridges: open with both orthogonal neighbors blocked
                    // Check 4 diagonal patterns
                    if (IsSolidWorld(cxy, x-1, y, solid) && IsSolidWorld(cxy, x, y-1, solid) && !IsSolidWorld(cxy, x-1, y-1, solid))
                    { solid[x, y] = true; changed = true; }
                    else if (IsSolidWorld(cxy, x+1, y, solid) && IsSolidWorld(cxy, x, y-1, solid) && !IsSolidWorld(cxy, x+1, y-1, solid))
                    { solid[x, y] = true; changed = true; }
                    else if (IsSolidWorld(cxy, x-1, y, solid) && IsSolidWorld(cxy, x, y+1, solid) && !IsSolidWorld(cxy, x-1, y+1, solid))
                    { solid[x, y] = true; changed = true; }
                    else if (IsSolidWorld(cxy, x+1, y, solid) && IsSolidWorld(cxy, x, y+1, solid) && !IsSolidWorld(cxy, x+1, y+1, solid))
                    { solid[x, y] = true; changed = true; }
                }
            } while (changed);

            // 2) Enforce minimum corridor width (Manhattan).
            // If an open tile is pinched by solids such that either horizontal or vertical open run is < minWidth, fill it.
            // (Fast approximation that yields smoother walkable space.)
            var buf = (bool[,])solid.Clone();
            for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                if (solid[x, y]) continue; // only open tiles
                int openH = 1 + CountOpenDir(cxy, x, y, -1, 0, solid) + CountOpenDir(cxy, x, y, 1, 0, solid);
                int openV = 1 + CountOpenDir(cxy, x, y, 0, -1, solid) + CountOpenDir(cxy, x, y, 0, 1, solid);
                if (openH < minWidth || openV < minWidth)
                    buf[x, y] = true; // fill to widen corridors
            }
            solid = buf;
        }

        // Seam-safe “is solid” that samples base field outside this chunk
        bool IsSolidWorld(Vector2Int cxy, int px, int py, bool[,] solid)
        {
            int N = chunkPixels;
            if (px >= 0 && py >= 0 && px < N && py < N) return solid[px, py];

            int wx = cxy.x * N + px;
            int wy = cxy.y * N + py;
            return BaseSolidAtWorld(wx, wy);
        }

        int CountOpenDir(Vector2Int cxy, int px, int py, int dx, int dy, bool[,] solid)
        {
            int N = chunkPixels;
            int count = 0;
            for (int step = 1; step < 8; step++) // small horizon is fine for 64px chunks
            {
                int nx = px + dx * step;
                int ny = py + dy * step;
                bool isSolid = IsSolidWorld(cxy, nx, ny, solid);
                if (isSolid) break;
                count++;
            }
            return count;
        }


        public float ChunkWorldSize => _chunkWorldSize;
    }
}
