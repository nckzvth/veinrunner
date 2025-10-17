// File: Assets/Scripts/World/TerrainChunk.cs
// Namespace: Game.World
using System.Collections.Generic;
using UnityEngine;

namespace Game.World
{
    [RequireComponent(typeof(SpriteRenderer))]
    public class TerrainChunk : MonoBehaviour
    {
        public enum BrushMode { Dig, Fill }

        // Shipping baseline: Greedy Boxes merged by Composite (stable & scalable)
        public enum ColliderMode { GreedyBoxes /* Polygon disabled for now */ }
        public ColliderMode colliderMode = ColliderMode.GreedyBoxes;

        int _px;                 // texture size (e.g., 64)
        float _ppu;              // pixels-per-unit
        Material _mat;

        // Dual-grid: occupancy + material id (0 = base rock)
        bool[,] _solid;
        byte[,] _material;

        // Base snapshots for delta save
        bool[,] _baseSolid;
        byte[,] _baseMaterial;

        // Runtime ore-consumed mask: once true, ore should never reappear here
        bool[,] _oreConsumed;

        Texture2D _tex;
        Color32[] _pixels;
        SpriteRenderer _sr;

        // pooled rectangular colliders (source for Composite)
        readonly List<BoxCollider2D> _colliders = new();

        // collider rebuild tracking
        RectInt _dirtyAABB = new RectInt(-1, -1, 0, 0);
        bool _rebuildQueued;

        CompositeCollider2D _composite; // <— NEW (merged final collider)

        // ---------------------------------------------------------------------
        // Init & setup
        // ---------------------------------------------------------------------
        public void Init(int pixels, float pixelsPerUnit, Material spriteMat)
        {
            _px = pixels;
            _ppu = pixelsPerUnit;
            _mat = spriteMat;

            _solid = new bool[_px, _px];
            _material = new byte[_px, _px];
            _pixels = new Color32[_px * _px];

            _tex = new Texture2D(_px, _px, TextureFormat.RGBA32, false, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };

            _sr = GetComponent<SpriteRenderer>();
            _sr.material = _mat;
            _sr.sprite = Sprite.Create(_tex, new Rect(0, 0, _px, _px), new Vector2(0.5f, 0.5f), _ppu);
            _sr.sortingLayerName = "Default";
            _sr.sortingOrder = 0;

            var rb = GetComponent<Rigidbody2D>();
            if (!rb) rb = gameObject.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Static;

            // Add/Configure CompositeCollider2D once on the chunk GO
            _composite = GetComponent<CompositeCollider2D>();
            if (!_composite) _composite = gameObject.AddComponent<CompositeCollider2D>();
            _composite.geometryType = CompositeCollider2D.GeometryType.Polygons;
            _composite.generationType = CompositeCollider2D.GenerationType.Synchronous;
            _composite.vertexDistance = 0.0001f; // tight joins on grid
            _composite.offsetDistance = 0f;

            // Preserve parent layer (expected "Ground")
            gameObject.layer = gameObject.layer;
        }

        /// <summary>Sets current maps; on first call also snapshots base map for delta saves.</summary>
        public void SetMaps(bool[,] solid, byte[,] material)
        {
            _solid = solid;
            _material = material;

            if (_baseSolid == null)
                _baseSolid = CloneBoolGrid(_solid);
            if (_baseMaterial == null)
                _baseMaterial = (byte[,])_material.Clone();
            if (_oreConsumed == null)
                _oreConsumed = new bool[_px, _px];

            UploadTexture();
            RebuildColliders();
        }

        // ---------------------------------------------------------------------
        // Delta Save/Load
        // ---------------------------------------------------------------------
        public ChunkSaveData BuildSaveData(Vector2Int coord)
        {
            var mined = new byte[BitBytesCount(_px)];
            var filled = new byte[BitBytesCount(_px)];
            var ore = new byte[BitBytesCount(_px)];

            int bitIndex = 0;
            for (int y = 0; y < _px; y++)
                for (int x = 0; x < _px; x++, bitIndex++)
                {
                    bool wasSolid = _baseSolid[x, y];
                    bool nowSolid = _solid[x, y];

                    if (wasSolid && !nowSolid) SetBit(mined, bitIndex, true);
                    else if (!wasSolid && nowSolid) SetBit(filled, bitIndex, true);

                    if (_oreConsumed != null && _oreConsumed[x, y])
                        SetBit(ore, bitIndex, true);
                }

            return new ChunkSaveData(
                v: 2,
                x: coord.x,
                y: coord.y,
                s: _px,
                mined: mined,
                filled: filled,
                oreConsumed: ore
            );
        }

        public void ApplySaveData(in ChunkSaveData data)
        {
            if (!data.IsValid(_px) || _baseSolid == null) return;

            int bitIndex = 0;
            for (int y = 0; y < _px; y++)
                for (int x = 0; x < _px; x++, bitIndex++)
                {
                    bool wasSolid = _baseSolid[x, y];
                    bool nowSolid = wasSolid;

                    if (GetBit(data.minedBits, bitIndex)) nowSolid = false;
                    if (GetBit(data.filledBits, bitIndex)) nowSolid = true;
                    _solid[x, y] = nowSolid;

                    bool consumed = (data.version >= 2) && data.oreConsumedBits != null && GetBit(data.oreConsumedBits, bitIndex);
                    if (_oreConsumed == null) _oreConsumed = new bool[_px, _px];
                    _oreConsumed[x, y] = consumed || _oreConsumed[x, y];

                    if (_oreConsumed[x, y]) _material[x, y] = 0; // ore never reappears
                }

            UploadTexture();
            RebuildColliders();
        }

        static bool[,] CloneBoolGrid(bool[,] src)
        {
            int w = src.GetLength(0), h = src.GetLength(1);
            var dst = new bool[w, h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    dst[x, y] = src[x, y];
            return dst;
        }

        static int BitBytesCount(int size) => ((size * size) + 7) >> 3;

        static void SetBit(byte[] arr, int idx, bool v)
        {
            int b = idx >> 3;
            int m = 1 << (idx & 7);
            if (v) arr[b] = (byte)(arr[b] | m);
            else arr[b] = (byte)(arr[b] & ~m);
        }

        static bool GetBit(byte[] arr, int idx)
        {
            int b = idx >> 3;
            int m = 1 << (idx & 7);
            return (arr[b] & m) != 0;
        }

        // ---------------------------------------------------------------------
        // Mining & rendering
        // ---------------------------------------------------------------------
        public struct RimSample
        {
            public Vector2 worldPos;
            public Vector2 outward;
            public RimSample(Vector2 wp, Vector2 n) { worldPos = wp; outward = n; }
        }

        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        bool SolidAt(int x, int y)
        {
            if (x < 0 || y < 0 || x >= _px || y >= _px) return false;
            return _solid[x, y];
        }

        float SobelAt(int cx, int cy, int ix, int iy, int[,] k)
        {
            return (SolidAt(cx + ix, cy + iy) ? 1f : 0f) * k[iy + 1, ix + 1];
        }

        public bool ApplyCircle(Vector2 worldPos, float worldRadius, BrushMode mode,
                                List<RimSample> rimOut, int rimCap = 128)
        {
            // world -> local px
            Vector2 local = transform.InverseTransformPoint(worldPos);
            float halfWU = _px / _ppu * 0.5f;
            float fx = (local.x + halfWU) * _ppu;
            float fy = (local.y + halfWU) * _ppu;

            int cx = Mathf.RoundToInt(fx);
            int cy = Mathf.RoundToInt(fy);
            int r = Mathf.RoundToInt(worldRadius * _ppu);

            bool changed = false;

            int xmin = Mathf.Max(cx - r, 0);
            int xmax = Mathf.Min(cx + r, _px - 1);
            int ymin = Mathf.Max(cy - r, 0);
            int ymax = Mathf.Min(cy + r, _px - 1);

            for (int y = ymin; y <= ymax; y++)
            {
                int dy = y - cy;
                int dxMax = Mathf.RoundToInt(Mathf.Sqrt(Mathf.Max(0, r * r - dy * dy)));
                int xlo = Mathf.Max(xmin, cx - dxMax);
                int xhi = Mathf.Min(xmax, cx + dxMax);

                for (int x = xlo; x <= xhi; x++)
                {
                    bool after = (mode == BrushMode.Fill);
                    if (_solid[x, y] == after) continue;

                    if (mode == BrushMode.Dig)
                    {
                        if (_material[x, y] != 0) _oreConsumed[x, y] = true;
                        _solid[x, y] = false;
                        _material[x, y] = 0;
                    }
                    else // Fill
                    {
                        _solid[x, y] = true;
                        _material[x, y] = 0;
                    }
                    changed = true;

                    if (rimOut != null && rimOut.Count < rimCap)
                    {
                        bool onRim =
                            SolidAt(x - 1, y) != after ||
                            SolidAt(x + 1, y) != after ||
                            SolidAt(x, y - 1) != after ||
                            SolidAt(x, y + 1) != after;

                        if (onRim)
                        {
                            int[,] sx = { { -1, 0, 1 }, { -2, 0, 2 }, { -1, 0, 1 } };
                            int[,] sy = { { -1, -2, -1 }, { 0, 0, 0 }, { 1, 2, 1 } };
                            float gx = 0f, gy = 0f;

                            for (int j = -1; j <= 1; j++)
                                for (int i = -1; i <= 1; i++)
                                {
                                    gx += SobelAt(x, y, i, j, sx);
                                    gy += SobelAt(x, y, i, j, sy);
                                }

                            Vector2 inward = new(gx, gy);
                            Vector2 outward = inward.sqrMagnitude > 1e-6f ? (-inward).normalized : Vector2.down;

                            Vector2 lp = new((x + 0.5f) / _ppu - halfWU, (y + 0.5f) / _ppu - halfWU);
                            Vector2 wp = transform.TransformPoint(lp);

                            rimOut.Add(new RimSample(wp, outward));
                        }
                    }
                }
            }

            if (changed)
            {
                UploadTexture(xmin, ymin, xmax, ymax);

                if (_dirtyAABB.width <= 0)
                    _dirtyAABB = new RectInt(xmin, ymin, xmax - xmin + 1, ymax - ymin + 1);
                else
                    _dirtyAABB = ExpandRect(_dirtyAABB, xmin, ymin, xmax, ymax);

                _rebuildQueued = true;
            }
            return changed;
        }

        void UploadTexture()
        {
            int i = 0;
            for (int y = 0; y < _px; y++)
                for (int x = 0; x < _px; x++, i++)
                    _pixels[i] = _solid[x, y] ? MatColor(_material[x, y]) : new Color32(0, 0, 0, 0);

            _tex.SetPixels32(_pixels);
            _tex.Apply(false, false);
        }

        void UploadTexture(int xmin, int ymin, int xmax, int ymax)
        {
            int w = xmax - xmin + 1, h = ymax - ymin + 1;
            if (w <= 0 || h <= 0) return;

            var slice = new Color32[w * h];
            int si = 0;
            for (int y = ymin; y <= ymax; y++)
                for (int x = xmin; x <= xmax; x++, si++)
                    slice[si] = _solid[x, y] ? MatColor(_material[x, y]) : new Color32(0, 0, 0, 0);

            _tex.SetPixels32(xmin, ymin, w, h, slice, 0);
            _tex.Apply(false, false);
        }

        // ---------------------------------------------------------------------
        // Greedy rectangles (source) → CompositeCollider2D (merged output)
        // ---------------------------------------------------------------------
        void RebuildCollidersGreedy()
        {
            // disable all pooled boxes
            for (int i = 0; i < _colliders.Count; i++) _colliders[i].enabled = false;

            bool[,] visited = new bool[_px, _px];

            for (int y = 0; y < _px; y++)
            {
                for (int x = 0; x < _px; x++)
                {
                    if (!_solid[x, y] || visited[x, y]) continue;

                    int maxX = x;
                    while (maxX + 1 < _px && _solid[maxX + 1, y] && !visited[maxX + 1, y]) maxX++;

                    int maxY = y;
                    bool canGrow = true;
                    while (canGrow && maxY + 1 < _px)
                    {
                        for (int xx = x; xx <= maxX; xx++)
                            if (!_solid[xx, maxY + 1] || visited[xx, maxY + 1]) { canGrow = false; break; }
                        if (canGrow) maxY++;
                    }

                    for (int yy = y; yy <= maxY; yy++)
                        for (int xx = x; xx <= maxX; xx++)
                            visited[xx, yy] = true;

                    AddOrReuseBox(x, y, maxX, maxY); // child sources for Composite
                }
            }

            // Force Composite to rebuild now (synchronous)
            if (_composite) _ = _composite.pathCount; // touching it forces internal rebuild this frame
        }

        void AddOrReuseBox(int x0, int y0, int x1, int y1)
        {
            BoxCollider2D bc = null;
            for (int i = 0; i < _colliders.Count; i++)
                if (!_colliders[i].enabled) { bc = _colliders[i]; break; }
            if (!bc)
            {
                bc = gameObject.AddComponent<BoxCollider2D>();
                // Feed into Composite instead of being a separate collider
                bc.compositeOperation = Collider2D.CompositeOperation.Merge;
                _colliders.Add(bc);
            }

            bc.enabled = true;

            float invPPU = 1f / _ppu;
            float w = (x1 - x0 + 1) * invPPU;
            float h = (y1 - y0 + 1) * invPPU;

            float half = _px * invPPU * 0.5f;
            float cx = (x0 + x1 + 1) * 0.5f * invPPU - half;
            float cy = (y0 + y1 + 1) * 0.5f * invPPU - half;

            bc.size = new Vector2(w, h);
            bc.offset = new Vector2(cx, cy);

            // IMPORTANT: make sure it’s still set to merge (in case of pooled reused component)
            bc.compositeOperation = Collider2D.CompositeOperation.Merge;
            bc.isTrigger = false;
        }

        public int Pixels => _px;
        public float PPU => _ppu;

        static Color32 MatColor(byte mat)
        {
            // 0=dirt/stone, 1=copper, 2=iron, 3=gold
            switch (mat)
            {
                case 1: return new Color32(196, 118, 56, 255);
                case 2: return new Color32(92, 124, 164, 255);
                case 3: return new Color32(216, 188, 72, 255);
                default: return new Color32(60, 64, 72, 255);
            }
        }

        // ---------------------------------------------------------------------
        // Collider dispatcher
        // ---------------------------------------------------------------------
        void RebuildColliders()
        {
            // Only Greedy (merged by Composite) is active for this slice
            RebuildCollidersGreedy();
        }

        // ---------------------------------------------------------------------
        // Utilities
        // ---------------------------------------------------------------------
        public void ForceRebuildCollidersNow(bool renameWithStats = false)
        {
            RebuildColliders();
            if (renameWithStats)
            {
                int boxEnabled = 0;
                for (int i = 0; i < _colliders.Count; i++)
                    if (_colliders[i].enabled) boxEnabled++;

                int paths = _composite ? _composite.pathCount : 0;
                gameObject.name = $"Chunk_{transform.position.x:0}_{transform.position.y:0} [CompositeGreedy] boxes:{boxEnabled} paths:{paths}";
            }
        }

        static RectInt ExpandRect(RectInt r, int xmin, int ymin, int xmax, int ymax)
        {
            int xMin = Mathf.Min(r.xMin, xmin);
            int yMin = Mathf.Min(r.yMin, ymin);
            int xMax = Mathf.Max(r.xMax, xmax);
            int yMax = Mathf.Max(r.yMax, ymax);
            return new RectInt(xMin, yMin, xMax - xMin + 1, yMax - yMin + 1);
        }

        void LateUpdate()
        {
            if (!_rebuildQueued) return;
            _rebuildQueued = false;

            // Greedy+Composite: rebuild here once per frame after edits
            _dirtyAABB = new RectInt(-1, -1, 0, 0);
            RebuildColliders();
        }
    }
}
