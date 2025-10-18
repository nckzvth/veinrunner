// File: Assets/Scripts/World/TerrainWorld.cs
// Namespace: Game.World
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
        [Range(0, 6)] public int smoothIterations = 2;
        [Tooltip("Solid if >= this many solid neighbors (8-neighborhood).")]
        [Range(0, 8)] public int birthLimit = 5;   // B5
        [Tooltip("Solid survives if >= this many solid neighbors.")]
        [Range(0, 8)] public int surviveMin = 4;  // S45 (lower bound)

        [Header("Passability Guard")]
        [Tooltip("Minimum open corridor width in tiles. 2 is a good default.")]
        [Range(1, 4)] public int minCorridorWidth = 2;
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

        [Header("Bands & Content")]
        public BandConfigSO bandConfig;
        public VeinTableSO veinTable;

        // --- runtime ---
        float _chunkWorldSize;
        Camera _cam;
        float _digAcc, _fillAcc;
        readonly List<TerrainChunk.RimSample> _rimScratch = new(256);

        // one global offset so adjacent chunks share the same noise field
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
            c.colliderMode = TerrainChunk.ColliderMode.GreedyBoxes; // shipping baseline
            c.Init(chunkPixels, pixelsPerUnit, spriteMat);

            go.transform.position = new Vector2(
                (cxy.x + 0.5f) * _chunkWorldSize,
                (cxy.y + 0.5f) * _chunkWorldSize
            );

            (bool[,] solid, byte[,] material) = GenerateMapsForChunk(cxy);
            c.SetMaps(solid, material);
            return c;
        }

        // File: Assets/Scripts/World/TerrainWorld.cs
// inside class TerrainWorld

public (bool[,] solid, byte[,] material) GenerateMapsForChunk(Vector2Int cxy)
{
    int N = chunkPixels;
    bool[,] solid = new bool[N, N];
    byte[,] material = new byte[N, N];

    // 1) Base field (deterministic, seam-safe)
    for (int py = 0; py < N; py++)
    {
        for (int px = 0; px < N; px++)
        {
            int wx = cxy.x * N + px;
            int wy = cxy.y * N + py;
            solid[px, py] = BaseSolidAtWorld(wx, wy);
            material[px, py] = 0; // base rock
        }
    }

    // 2) Seam-safe smoothing (B/S) – the values you already tuned
    if (smoothIterations > 0)
        SmoothChunkSeamSafe(cxy, ref solid, smoothIterations, birthLimit, surviveMin);

    // 3) Passability guard (keeps corridors ≥ minCorridorWidth)
    if (enablePassabilityGuard && minCorridorWidth > 1)
        EnsurePassabilitySeamSafe(cxy, ref solid, minCorridorWidth);

    // 4) Edge rounding (one cheap pass to kill stair-steps)
    RoundEdgesSeamSafe(cxy, ref solid, blurRadiusTiles: 1, bias: 0.5f);

    // 5) Veins (material-only paint; does NOT change solidity)
    int topTileY = cxy.y * chunkPixels;
    var band = BandResolver.BandFromY(topTileY);
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

        // ======================================================
        // Generation helpers
        // ======================================================
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

// Two-scale FBM with subtle anisotropy (vertical corridors) and light detail.
// Keeps it seam-safe, deterministic, and cheap.
bool BaseSolidAtWorld(int wx, int wy)
{
    // base coords in your world-tile space
    float xs = (wx + _noiseOffset.x) * noiseScale;
    float ys = (wy + _noiseOffset.y) * noiseScale;

    // Encourage vertical continuity: stretch Y a bit so features elongate up/down.
    const float verticalStretch = 0.75f;   // 1 = isotropic, <1 = longer vertical forms
    float ax = xs;
    float ay = ys * verticalStretch;

    // small domain warp (you already have Warp/FBM; this stays seam-safe)
    Vector2 w = Warp(ax, ay);
    ax += w.x;
    ay += w.y;

    // Macro shapes (your FBM: 3–4 octaves)
    float macro = Fbm(ax, ay);

    // Light edge detail: a single higher-frequency octave (cheap, no FBM stack)
    // 3.0 is a good ‘detail wavelength’ over the macro. Keep weight small.
    float detail = Mathf.PerlinNoise(ax * 3.0f, ay * 3.0f);

    // Blend: mostly macro, a touch of detail for interesting edges
    float n = Mathf.Lerp(macro, detail, 0.18f);

    // Smooth to kill harsh plateaus (same function you already use)
    n = Smooth01(n);

    // A tiny bias keeps openness from collapsing when we add detail
    const float opennessBias = -0.02f; // negative → slightly more air overall
    return n + opennessBias > threshold;
}

        // Seam-safe cellular smooth
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

        // Enforce ≥ minWidth corridors (Manhattan). Seam-safe across edges.
        void EnsurePassabilitySeamSafe(Vector2Int cxy, ref bool[,] solid, int minWidth)
        {
            int N = chunkPixels;

            // 2) Enforce minimum corridor width (fast approximation)
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

        // Smooth edges with a tiny circular kernel (seam-safe).
        // r=1 is enough to remove stair-steps without changing layouts.
        // softens jagged edges without changing big shapes; seam-safe via BaseSolidAtWorld
void RoundEdgesSeamSafe(Vector2Int cxy, ref bool[,] solid, int blurRadiusTiles, float bias)
{
    int N = chunkPixels;
    int r = Mathf.Clamp(blurRadiusTiles, 1, 3);
    float threshold = Mathf.Clamp01(bias); // 0..1

    bool[,] dst = new bool[N, N];
    int kernelDiam = r * 2 + 1;
    int kernelArea = 0;
    int[] xoff = new int[kernelDiam * kernelDiam];
    int[] yoff = new int[kernelDiam * kernelDiam];

    {   // precompute offsets in a disc
        int idx = 0;
        int r2 = r * r;
        for (int dy = -r; dy <= r; dy++)
        {
            for (int dx = -r; dx <= r; dx++)
            {
                if (dx * dx + dy * dy <= r2)
                {
                    xoff[idx] = dx;
                    yoff[idx] = dy;
                    idx++;
                }
            }
        }
        kernelArea = idx;
    }

    for (int py = 0; py < N; py++)
    {
        int wy = cxy.y * N + py;
        for (int px = 0; px < N; px++)
        {
            int wx = cxy.x * N + px;
            int solidCount = 0;

            for (int i = 0; i < kernelArea; i++)
            {
                int sx = px + xoff[i], sy = py + yoff[i];
                bool s;
                if (sx >= 0 && sy >= 0 && sx < N && sy < N) s = solid[sx, sy];
                else s = BaseSolidAtWorld(wx + xoff[i], wy + yoff[i]);
                if (s) solidCount++;
            }

            float frac = solidCount / (float)kernelArea;
            // if local neighborhood is mostly air, carve it; mostly rock, fill it
            bool outSolid = frac > threshold;
            dst[px, py] = outSolid;
        }
    }

    solid = dst;
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

        // ============================================
        // Debug mining
        // ============================================
        void Stroke(bool isDig)
        {
            Vector2 screen = Mouse.current.position.ReadValue();
            var ray = _cam.ScreenPointToRay(screen);
            var plane = new Plane(Vector3.forward, Vector3.zero);
            if (!plane.Raycast(ray, out float t)) return;

            Vector2 world = ray.GetPoint(t);
            float radius = brushWorldRadius;

            var hits = Physics2D.OverlapCircleAll(world, radius * 1.1f);
            _rimScratch.Clear();

            for (int i = 0; i < hits.Length; i++)
            {
                var chunk = hits[i].GetComponent<TerrainChunk>();
                if (!chunk) continue;
                chunk.ApplyCircle(
                    world, radius,
                    isDig ? TerrainChunk.BrushMode.Dig : TerrainChunk.BrushMode.Fill,
                    _rimScratch, 192);
            }
        }
        // --- Classic connectivity (tutorial-accurate), no inspector controls ---
const int CC_WallThreshold  = 50;
const int CC_RoomThreshold  = 50;
const int CC_TunnelRadius   = 5;   // circular corridor radius in tiles
const int CC_MarginChunks   = 1;   // 3×3 slab centered on this chunk

struct CC_Coord { public int x,y; public CC_Coord(int x,int y){ this.x=x; this.y=y; } }

sealed class CC_Room : System.IComparable<CC_Room> {
    public List<CC_Coord> tiles = new(256);
    public List<CC_Coord> edge  = new(128);
    public HashSet<CC_Room> linked = new();
    public int size; public bool fromMain, isMain;

    public CC_Room(List<CC_Coord> roomTiles, int[,] map){
        tiles = roomTiles; size = tiles.Count;
        // mark edge tiles (4-neigh)
        for (int i=0;i<tiles.Count;i++){
            int tx = tiles[i].x, ty = tiles[i].y;
            if (IsWall(map,tx+1,ty) || IsWall(map,tx-1,ty) || IsWall(map,tx,ty+1) || IsWall(map,tx,ty-1))
                edge.Add(tiles[i]);
        }
    }
    static bool IsWall(int[,] map,int x,int y){
        if (x<0||y<0||x>=map.GetLength(0)||y>=map.GetLength(1)) return true; return map[x,y]==1;
    }
    public static void Link(CC_Room a, CC_Room b){
        a.linked.Add(b); b.linked.Add(a);
        if (a.fromMain) b.MarkFromMain(); else if (b.fromMain) a.MarkFromMain();
    }
    void MarkFromMain(){ if (fromMain) return; fromMain=true; foreach (var r in linked) r.MarkFromMain(); }
    public bool IsLinked(CC_Room o) => linked.Contains(o);
    public int CompareTo(CC_Room o) => o.size.CompareTo(size);
}

void ApplyClassicConnectivity3x3(Vector2Int cxy, ref bool[,] solidCenter)
{
    int N = chunkPixels, M = CC_MarginChunks;
    int slabW = (2*M+1)*N, slabH = (2*M+1)*N;
    int wx0 = (cxy.x - M) * N, wy0 = (cxy.y - M) * N;

    // Build slab map from our current deterministic base field (air=0, wall=1)
    // We could pull neighbors, but sampling the same world field keeps it seam-safe.
    int[,] map = new int[slabW, slabH];
    for (int y = 0; y < slabH; y++){
        int wy = wy0 + y;
        for (int x = 0; x < slabW; x++){
            int wx = wx0 + x;
            map[x,y] = BaseSolidAtWorld(wx,wy) ? 1 : 0;
        }
    }

    // Run the same processing the lecture shows:
    // 1) prune tiny walls → carve to rooms
    PruneRegions(map, tileType:1, threshold:CC_WallThreshold, setTo:0);
    // 2) gather rooms, prune tiny → fill to wall; keep survivors for linking
    var rooms = GetRegions(map, 0);
    var survivors = new List<CC_Room>(rooms.Count);
    for (int i=0;i<rooms.Count;i++){
        if (rooms[i].Count < CC_RoomThreshold){
            foreach (var c in rooms[i]) map[c.x,c.y] = 1;
        } else {
            survivors.Add(new CC_Room(rooms[i], map));
        }
    }
    survivors.Sort();
    if (survivors.Count > 0){ survivors[0].isMain = true; survivors[0].fromMain = true; }

    // 3) connect closest rooms with circular tunnels (Bresenham line + filled discs)
    ConnectClosestRooms(map, survivors);

    // 4) Write back ONLY the center chunk area to our 'solid' (wall=1, air=0)
    int cx0 = M*N, cy0 = M*N;
    for (int y=0;y<N;y++)
        for (int x=0;x<N;x++)
            solidCenter[x,y] = (map[cx0+x, cy0+y] == 1);
}

void PruneRegions(int[,] map, int tileType, int threshold, int setTo)
{
    var regs = GetRegions(map, tileType);
    for (int i=0;i<regs.Count;i++){
        if (regs[i].Count < threshold){
            var ls = regs[i];
            for (int t=0;t<ls.Count;t++){ var c = ls[t]; map[c.x,c.y] = setTo; }
        }
    }
}

List<List<CC_Coord>> GetRegions(int[,] map, int tileType)
{
    int W = map.GetLength(0), H = map.GetLength(1);
    int[,] flags = new int[W,H];
    var regs = new List<List<CC_Coord>>(128);

    for (int x=0;x<W;x++)
        for (int y=0;y<H;y++)
            if (flags[x,y]==0 && map[x,y]==tileType)
                regs.Add(GetRegionTiles(map,x,y,tileType,flags));

    return regs;
}

List<CC_Coord> GetRegionTiles(int[,] map, int sx, int sy, int tileType, int[,] flags)
{
    int W = map.GetLength(0), H = map.GetLength(1);
    var tiles = new List<CC_Coord>(256);
    var q = new System.Collections.Generic.Queue<CC_Coord>();
    q.Enqueue(new CC_Coord(sx,sy)); flags[sx,sy]=1;
    while (q.Count>0){
        var c = q.Dequeue(); tiles.Add(c);
        // 4-neigh flood like the tutorial
        if (c.x>0   && flags[c.x-1,c.y]==0 && map[c.x-1,c.y]==tileType){ flags[c.x-1,c.y]=1; q.Enqueue(new CC_Coord(c.x-1,c.y)); }
        if (c.x<W-1 && flags[c.x+1,c.y]==0 && map[c.x+1,c.y]==tileType){ flags[c.x+1,c.y]=1; q.Enqueue(new CC_Coord(c.x+1,c.y)); }
        if (c.y>0   && flags[c.x,c.y-1]==0 && map[c.x,c.y-1]==tileType){ flags[c.x,c.y-1]=1; q.Enqueue(new CC_Coord(c.x,c.y-1)); }
        if (c.y<H-1 && flags[c.x,c.y+1]==0 && map[c.x,c.y+1]==tileType){ flags[c.x,c.y+1]=1; q.Enqueue(new CC_Coord(c.x,c.y+1)); }
    }
    return tiles;
}

void ConnectClosestRooms(int[,] map, List<CC_Room> rooms, bool forceFromMain=false)
{
    List<CC_Room> A = new(), B = new();
    if (forceFromMain){
        for (int i=0;i<rooms.Count;i++) (rooms[i].fromMain ? B : A).Add(rooms[i]);
    } else { A = rooms; B = rooms; }

    int bestD2=0; CC_Coord bestA=default, bestB=default; CC_Room RA=null,RB=null; bool found=false;

    for (int i=0;i<A.Count;i++){
        var rA = A[i]; if (!forceFromMain && rA.linked.Count>0) continue;
        for (int j=0;j<B.Count;j++){
            var rB = B[j]; if (rA==rB || rA.IsLinked(rB)) continue;
            for (int ea=0; ea<rA.edge.Count; ea++){
                var ta = rA.edge[ea];
                for (int eb=0; eb<rB.edge.Count; eb++){
                    var tb = rB.edge[eb];
                    int dx = ta.x - tb.x, dy = ta.y - tb.y; int d2 = dx*dx + dy*dy;
                    if (d2 < bestD2 || !found){ found=true; bestD2=d2; bestA=ta; bestB=tb; RA=rA; RB=rB; }
                }
            }
        }
        if (found && !forceFromMain) CreatePassageOwnedByChunk(map, RA, RB, bestA, bestB);
    }

    if (found && forceFromMain){
        CreatePassageOwnedByChunk(map, RA, RB, bestA, bestB);
        ConnectClosestRooms(map, rooms, true);
    }
    if (!forceFromMain) ConnectClosestRooms(map, rooms, true);
}

// Carve corridor only if this center chunk owns the midpoint (prevents double-carve at borders)
// Inside TerrainWorld
void CreatePassageOwnedByChunk(int[,] map, CC_Room A, CC_Room B, CC_Coord a, CC_Coord b)
{
    // Carve this corridor only if the midpoint lies inside the center-chunk area
    // (slab coords). Prevents double-carving on borders.
    int N = chunkPixels, M = CC_MarginChunks;

    // Midpoint in slab coords
    int midx = (a.x + b.x) >> 1;
    int midy = (a.y + b.y) >> 1;

    // Center chunk rectangle in slab coords:
    int cx0 = M * N, cy0 = M * N, cx1 = cx0 + N - 1, cy1 = cy0 + N - 1;
    if (midx < cx0 || midx > cx1 || midy < cy0 || midy > cy1) return;

    // Carve with circular brush along a Bresenham line
    CC_Room.Link(A, B);
    var line = GetLine(a, b);
    for (int i = 0; i < line.Count; i++) DrawCircle(map, line[i], CC_TunnelRadius);
}

List<CC_Coord> GetLine(CC_Coord from, CC_Coord to){
    var line = new List<CC_Coord>(256);
    int x=from.x, y=from.y; int dx=to.x-from.x, dy=to.y-from.y;
    bool inv = Mathf.Abs(dx) < Mathf.Abs(dy);
    int step = System.Math.Sign(inv?dy:dx), gradStep = System.Math.Sign(inv?dx:dy);
    int longest = Mathf.Abs(inv?dy:dx), shortest = Mathf.Abs(inv?dx:dy);
    int acc = longest/2;
    for (int i=0;i<longest;i++){
        line.Add(new CC_Coord(x,y));
        if (inv) y+=step; else x+=step;
        acc += shortest;
        if (acc >= longest){ if (inv) x+=gradStep; else y+=gradStep; acc -= longest; }
    }
    return line;
}

void DrawCircle(int[,] map, CC_Coord c, int r){
    int W = map.GetLength(0), H = map.GetLength(1), r2=r*r;
    for (int y=-r; y<=r; y++){
        int dxMax = (int)Mathf.Floor(Mathf.Sqrt(Mathf.Max(0, r2 - y*y)));
        for (int x=-dxMax; x<=dxMax; x++){
            int px = c.x + x, py = c.y + y;
            if (px>=0 && py>=0 && px<W && py<H) map[px,py] = 0; // carve to air
        }
    }
}

        public float ChunkWorldSize => _chunkWorldSize;
    }
}