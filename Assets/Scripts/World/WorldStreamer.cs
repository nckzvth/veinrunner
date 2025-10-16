// Namespace: Game.World
using System.Collections.Generic;
using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// Keeps a (2*windowRadius+1)^2 grid of chunks loaded around player.
    /// Adds in-memory save cache so edits persist while streaming (no disk yet).
    /// </summary>
    public class WorldStreamer : MonoBehaviour
    {
        [Header("Refs")]
        public TerrainWorld world;
        public Transform player; // optional; falls back to Camera.main

        [Header("Window")]
        [Tooltip("Number of chunks in radius to keep loaded (1 => 3x3).")]
        public int windowRadius = 1;

        readonly Dictionary<Vector2Int, TerrainChunk> _loaded = new();
        readonly Dictionary<Vector2Int, ChunkSaveData> _cache = new(); // in-memory deltas

        Vector2Int _center;

        void Start()
        {
            if (!world) world = Object.FindFirstObjectByType<TerrainWorld>();
            if (!player && Camera.main) player = Camera.main.transform;
            UpdateCenter(force: true);
        }

        void Update() => UpdateCenter(force: false);

        void UpdateCenter(bool force)
        {
            if (!world || !player) return;

            float cws = world.ChunkWorldSize;
            Vector2 p = player.position;
            Vector2Int at = new(Mathf.FloorToInt(p.x / cws), Mathf.FloorToInt(p.y / cws));

            if (!force && at == _center) return;
            _center = at;

            // target coords
            var target = new HashSet<Vector2Int>();
            for (int dy = -windowRadius; dy <= windowRadius; dy++)
                for (int dx = -windowRadius; dx <= windowRadius; dx++)
                    target.Add(new Vector2Int(_center.x + dx, _center.y + dy));

            // unload
            var toRemove = new List<Vector2Int>();
            foreach (var kv in _loaded)
                if (!target.Contains(kv.Key)) toRemove.Add(kv.Key);

            for (int i = 0; i < toRemove.Count; i++)
            {
                var key = toRemove[i];
                if (_loaded.TryGetValue(key, out var chunk) && chunk)
                {
                    // Save delta to in-memory cache
                    var data = chunk.BuildSaveData(key);
                    _cache[key] = data;

                    Destroy(chunk.gameObject); // Month-1: no pool, no disk save
                }
                _loaded.Remove(key);
            }

            // load
            foreach (var key in target)
            {
                if (_loaded.ContainsKey(key)) continue;

                var c = world.CreateChunk(key, parent: world.transform);

                // Re-apply deltas if we have any
                if (_cache.TryGetValue(key, out var data) && data.IsValid(c.Pixels))
                {
                    c.ApplySaveData(data);
                }

                _loaded.Add(key, c);
            }
        }
    }
}

