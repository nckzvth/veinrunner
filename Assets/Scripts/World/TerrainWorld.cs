// Namespace: Game.World
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.World
{
    /// <summary>
    /// World factory + debug mining. No FX or Player dependency; Camera is used for input target.
    /// </summary>
    public class TerrainWorld : MonoBehaviour
    {
        [Header("World Generation")]
        public int seed = 12345;
        public float noiseScale = 0.0125f;
        [Range(0f, 1f)] public float threshold = 0.55f; // > thresh is SOLID

        [Header("Chunk")]
        public int chunkPixels = 64;
        public float pixelsPerUnit = 32f;
        public Material spriteMat;

        [Header("Brush (Debug Mining)")]
        public float brushWorldRadius = 0.35f;
        public float digRateHz = 8f;
        public float fillRateHz = 6f;

        float _chunkWorldSize;
        Camera _cam;
        float _digAcc, _fillAcc;
        readonly List<TerrainChunk.RimSample> _rimScratch = new(256);

        void Awake()
        {
            _cam = Camera.main;
            _chunkWorldSize = chunkPixels / pixelsPerUnit;
        }

        // --- Chunk factory ---

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
            bool[,] solid = new bool[chunkPixels, chunkPixels];
            byte[,] material = new byte[chunkPixels, chunkPixels];

            var rng = new System.Random(HashSeed(seed, cxy.x, cxy.y));
            float ox = rng.Next(0, 100000);
            float oy = rng.Next(0, 100000);

            for (int py = 0; py < chunkPixels; py++)
            {
                for (int px = 0; px < chunkPixels; px++)
                {
                    int wx = cxy.x * chunkPixels + px;
                    int wy = cxy.y * chunkPixels + py;
                    float n = Mathf.PerlinNoise(ox + wx * noiseScale, oy + wy * noiseScale);
                    solid[px, py] = n > threshold;
                    material[px, py] = 0;
                }
            }

            return (solid, material);
        }

        static int HashSeed(int baseSeed, int x, int y)
        {
            unchecked
            {
                int h = baseSeed;
                h = h * 73856093 ^ x;
                h = h * 19349663 ^ y;
                return h;
            }
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

        public float ChunkWorldSize => _chunkWorldSize;
    }
}
