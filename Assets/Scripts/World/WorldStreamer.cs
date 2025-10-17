// Namespace: Game.World
using System.Collections.Generic;
using UnityEngine;
using Game.Services;

namespace Game.World
{
    /// <summary>
    /// Loads/unloads a square window of chunks around the camera center.
    /// Dynamic-by-camera option grows with orthographicSize.
    /// Includes: in-memory cache, disk saves (optional), and a load budget per frame.
    /// </summary>
    public sealed class WorldStreamer : MonoBehaviour
    {
        public enum WindowSizing { Fixed, DynamicByCamera }

        [Header("Refs")]
        public TerrainWorld world;
        public Camera targetCamera;

        [Header("Window")]
        public WindowSizing sizing = WindowSizing.DynamicByCamera;
        public int fixedRadius = 1;
        public int paddingChunks = 1;
        public int minRadius = 1;
        public int maxRadius = 3;

        [Header("Persistence")]
        public bool enableDiskSaves = true;
        public bool keepMemoryCache = true;

        [Header("Performance")]
        [Tooltip("How many new chunks can be created per frame.")]
        public int loadsPerFrame = 2;

        // RAM cache for deltas this session
        readonly Dictionary<Vector2Int, ChunkSaveData> _cache = new();

        // Loaded chunks
        readonly Dictionary<Vector2Int, TerrainChunk> _loaded = new();

        // Internal working sets
        readonly List<Vector2Int> _toRemove = new();
        readonly Queue<Vector2Int> _pendingLoads = new();      // enforce order & budget
        readonly HashSet<Vector2Int> _pendingSet = new();      // prevent duplicates
        HashSet<Vector2Int> _desiredNow = new();

        Vector2Int _center;
        int _activeRadius = -999;

        void Start()
        {
            if (!targetCamera) targetCamera = Camera.main;
            if (enableDiskSaves && world != null)
                SaveManager.Init(world.seed);

            UpdateCenter(force: true);
        }

        void Update()
        {
            UpdateCenter(force: false);
            ProcessPendingLoads();
        }

        public void ClearCachesAndReload()
        {
            _cache.Clear(); // RAM delta cache
            // Also wipe currently loaded chunks to get a clean slate window
            foreach (var kv in _loaded)
                if (kv.Value) Destroy(kv.Value.gameObject);
            _loaded.Clear();

            // Clear pending and force a window rebuild
            _pendingLoads.Clear();
            _pendingSet.Clear();
            _activeRadius = -999; // force refresh
            UpdateCenter(force: true);
        }

        void UpdateCenter(bool force)
        {
            if (!world) return;
            if (!targetCamera) targetCamera = Camera.main;
            if (!targetCamera) return;

            Vector3 pos = targetCamera.transform.position;
            float s = world.ChunkWorldSize;
            var center = new Vector2Int(
                Mathf.FloorToInt(pos.x / s),
                Mathf.FloorToInt(pos.y / s)
            );

            int desiredRadius = fixedRadius;
            if (sizing == WindowSizing.DynamicByCamera)
            {
                float halfH = targetCamera.orthographicSize;
                float halfW = halfH * targetCamera.aspect;
                int rx = Mathf.CeilToInt(halfW / s) + paddingChunks;
                int ry = Mathf.CeilToInt(halfH / s) + paddingChunks;
                desiredRadius = Mathf.Clamp(Mathf.Max(rx, ry), minRadius, maxRadius);
            }

            if (!force && center == _center && desiredRadius == _activeRadius) return;

            _center = center;
            _activeRadius = desiredRadius;
            RebuildWindow();
        }

        void RebuildWindow()
        {
            // Desired set of keys for the new window
            _desiredNow = new HashSet<Vector2Int>();
            for (int dy = -_activeRadius; dy <= _activeRadius; dy++)
                for (int dx = -_activeRadius; dx <= _activeRadius; dx++)
                    _desiredNow.Add(new Vector2Int(_center.x + dx, _center.y + dy));

            // Unload anything not desired (save first!)
            _toRemove.Clear();
            foreach (var kv in _loaded)
                if (!_desiredNow.Contains(kv.Key))
                    _toRemove.Add(kv.Key);

            for (int i = 0; i < _toRemove.Count; i++)
            {
                var key = _toRemove[i];
                var ch = _loaded[key];
                if (ch)
                {
                    var sd = ch.BuildSaveData(key);
                    if (keepMemoryCache) _cache[key] = sd;
                    if (enableDiskSaves) SaveManager.SaveChunk(world.seed, key, sd);
                    Destroy(ch.gameObject);
                }
                _loaded.Remove(key);
            }

            // Queue loads for anything missing
            foreach (var key in _desiredNow)
            {
                if (_loaded.ContainsKey(key)) continue;
                if (_pendingSet.Contains(key)) continue;

                _pendingLoads.Enqueue(key);
                _pendingSet.Add(key);
            }
        }

        void ProcessPendingLoads()
        {
            int budget = Mathf.Max(1, loadsPerFrame);
            while (budget-- > 0 && _pendingLoads.Count > 0)
            {
                var key = _pendingLoads.Dequeue();
                _pendingSet.Remove(key);

                // A fast camera move may have changed the desired set
                if (_desiredNow != null && !_desiredNow.Contains(key))
                    continue; // skip stale load

                // Create chunk
                var chunk = world.CreateChunk(key, transform);
                _loaded[key] = chunk;

                // Apply disk or RAM deltas (disk preferred)
                if (enableDiskSaves && SaveManager.TryLoadChunk(world.seed, key, out var disk))
                {
                    chunk.ApplySaveData(in disk);
                }
                else if (keepMemoryCache && _cache.TryGetValue(key, out var ram))
                {
                    chunk.ApplySaveData(in ram);
                }
            }
        }
    }
}
