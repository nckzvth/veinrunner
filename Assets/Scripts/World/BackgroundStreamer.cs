// Assets/Scripts/World/BackgroundStreamer.cs
using System.Collections.Generic;
using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// Streams independent background chunks and equips them with GI blocker underlays
    /// via ChunkLightingRuntime. No coupling to your Terrain streaming.
    /// </summary>
    /// 
    public sealed class BackgroundStreamer : MonoBehaviour
    {
        [Header("Auto-wire")]
        public BackgroundWorld world;
        public Camera targetCamera;

        [Header("Window (same UX as WorldStreamer)")]
        public int radius = 3;
        public int loadsPerFrame = 2;
        public int unloadsPerFrame = 8;

        [Header("GI wiring (autofill; override if you want)")]
        public string bgGroundLayerName = "Background";   // physics layer for parent SRs
        public string giEmittersLayerName = "GIEmitters";
        public string underlaySortingLayerName = "GIBlockers";
        public int    underlayOrderOffset = -1200;
        public Material blockerMaterial; // auto-find "GI_BlockerUnderlay"

        readonly Dictionary<Vector2Int, GameObject> _loaded = new();
        readonly Queue<Vector2Int> _pending = new();
        HashSet<Vector2Int> _desired = new();

        Vector2Int _center = new(999999,999999);

        void Awake()
        {
            if (!world) world = FindFirstObjectByType<BackgroundWorld>() ?? gameObject.AddComponent<BackgroundWorld>();
            if (!targetCamera) targetCamera = Camera.main;

            // Auto-find GI_BlockerUnderlay if not wired
            if (!blockerMaterial)
            {
                var mats = Resources.FindObjectsOfTypeAll<Material>();
                foreach (var m in mats)
                    if (m && m.name.Contains("GI_BlockerUnderlay")) { blockerMaterial = m; break; }
                if (!blockerMaterial)
                {
                    blockerMaterial = new Material(Shader.Find("Sprites/Default"));
                    blockerMaterial.color = Color.black;
                }
            }

            // Ensure the bg physics layer exists; if not, fall back to this GO’s layer
            int layerIdx = LayerMask.NameToLayer(bgGroundLayerName);
            if (layerIdx < 0) bgGroundLayerName = LayerMask.LayerToName(gameObject.layer);
        }

        void Start(){ RebuildWindow(force:true); }
        void Update(){ RebuildWindow(force:false); ProcessLoads(); }

        void RebuildWindow(bool force)
        {
            if (!world || !targetCamera) return;

            var pos = targetCamera.transform.position;
            float s = world.ChunkWorldSize;
            var c = new Vector2Int(Mathf.FloorToInt(pos.x / s), Mathf.FloorToInt(pos.y / s));
            if (!force && c == _center) return;
            _center = c;

            // desired
            _desired = new HashSet<Vector2Int>();
            for (int dy=-radius; dy<=radius; dy++)
                for (int dx=-radius; dx<=radius; dx++)
                    _desired.Add(new Vector2Int(_center.x+dx, _center.y+dy));

            // enqueue loads
            foreach (var key in _desired)
                if (!_loaded.ContainsKey(key)) _pending.Enqueue(key);

            // unload
            int toUnload = unloadsPerFrame;
            var drop = new List<Vector2Int>();
            foreach (var kv in _loaded)
                if (!_desired.Contains(kv.Key)) { drop.Add(kv.Key); if (--toUnload<=0) break; }
            for (int i=0;i<drop.Count;i++)
            {
                Destroy(_loaded[drop[i]]);
                _loaded.Remove(drop[i]);
            }
        }

        void ProcessLoads()
        {
            int budget = Mathf.Max(1, loadsPerFrame);
            while (budget-- > 0 && _pending.Count > 0)
            {
                var key = _pending.Dequeue();
                if (_loaded.ContainsKey(key) || (_desired.Count>0 && !_desired.Contains(key))) continue;

                // create bg chunk
                var chunk = world.CreateChunk(key, transform);
                _loaded[key] = chunk;

                // put parent SRs on the bg physics layer (so runtime can filter)
                int bgLayer = LayerMask.NameToLayer(bgGroundLayerName);
                chunk.layer = bgLayer;
                foreach (var sr in chunk.GetComponentsInChildren<SpriteRenderer>(true))
                    sr.gameObject.layer = bgLayer;

                // Equip GI blocker underlays (mirror=false, cullHidden=false to keep blockers)
                ChunkLightingRuntime.Equip(
                    chunk,
                    bgGroundLayerName,
                    giEmittersLayerName,
                    underlaySortingLayerName,
                    underlayOrderOffset,
                    blockerMaterial,
                    mirrorParentMaterial: false,
                    cullHiddenUnderlays: false,
                    skipOversizeSprites: false,
                    maxSpriteWorldSize: 999f
                );
            }
        }
    }
}