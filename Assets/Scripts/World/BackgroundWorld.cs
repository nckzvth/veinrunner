// Assets/Scripts/World/BackgroundWorld.cs
using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// Independent background "terrain": generates a dense solid/air bitmap per chunk.
    /// No mining. Used only to drive GI blocker underlays.
    /// </summary>
    public sealed class BackgroundWorld : MonoBehaviour
    {
        [Header("Seed (defaults to TerrainWorld.seed ^ 0x4B1D)")]
        public int seed = 0;

        [Header("Geometry")]
        public int chunkPixels = 64;
        public float pixelsPerUnit = 32f;

        [Header("Noise (denser than terrain by default)")]
        [Range(0.002f, 0.08f)] public float noiseScale = 0.015f;
        [Range(1, 8)] public int octaves = 4;
        public float lacunarity = 2f;
        public float gain = 0.5f;
        [Range(0f, 0.35f)] public float warpStrength = 0.07f;
        [Range(0f, 1f)] public float threshold = 0.48f; // higher => fewer holes

        [Header("Post-shape")]
        [Range(0, 4)] public int growPixels = 2;
        public bool blurOnePixel = true;

        [Header("Materials (optional, used for the invisible parent SR)")]
        public Material spriteMat; // fallback Sprites/Default

        // runtime
        float _chunkWorldSize;
        Vector2 _off;

        void Awake()
        {
            // Auto-derive defaults from TerrainWorld if present
            var tw = FindFirstObjectByType<TerrainWorld>();
            if (tw)
            {
                if (seed == 0) seed = tw.seed ^ 0x4B1D;
                chunkPixels = tw.chunkPixels;
                pixelsPerUnit = tw.pixelsPerUnit;
                if (!spriteMat) spriteMat = tw.spriteMat;
            }
            if (seed == 0) seed = 1337;

            _chunkWorldSize = chunkPixels / pixelsPerUnit;

            var rng = new System.Random(seed);
            _off = new Vector2(rng.Next(0, 100000), rng.Next(0, 100000));
            if (!spriteMat)
                spriteMat = new Material(Shader.Find("Sprites/Default"));
        }

        public float ChunkWorldSize => _chunkWorldSize;

        /// <summary>Create one background chunk root with a SpriteRenderer (invisible) containing the dense bitmap.</summary>
        public GameObject CreateChunk(Vector2Int cxy, Transform parent)
        {
            var go = new GameObject($"BgChunk_{cxy.x}_{cxy.y}");
            if (parent) go.transform.SetParent(parent, false);

            // Place to world like TerrainWorld
            go.transform.position = new Vector2(
                (cxy.x + 0.5f) * _chunkWorldSize,
                (cxy.y + 0.5f) * _chunkWorldSize
            );

            // Parent SR that holds the shape (kept invisible). Underlay child will do the real draw.
            var sr = go.AddComponent<SpriteRenderer>();
            sr.material = spriteMat;
            sr.sortingLayerName = "WorldBG";
            sr.sortingOrder = 0;
            sr.color = new Color(1,1,1,0f); // invisible parent

            // Build bitmap (seamless across chunks)
            var tex = new Texture2D(chunkPixels, chunkPixels, TextureFormat.RGBA32, false, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };

            var solid = GenerateMapsForChunk(cxy);
            var px = new Color32[chunkPixels*chunkPixels];
            int i = 0;
            for (int y=0; y<chunkPixels; y++)
                for (int x=0; x<chunkPixels; x++, i++)
                    px[i] = solid[x,y] ? new Color32(255,255,255,255) : new Color32(0,0,0,0);

            tex.SetPixels32(px);
            tex.Apply(false,false);

            sr.sprite = Sprite.Create(tex, new Rect(0, 0, chunkPixels, chunkPixels),
                                      new Vector2(0.5f, 0.5f), pixelsPerUnit);

            BgChunkMask.Attach(go.transform, solid, chunkPixels, chunkPixels, pixelsPerUnit);
            return go;
        }

        // ===== background field (true = SOLID = blocks skylight) =====
        public bool[,] GenerateMapsForChunk(Vector2Int cxy)
        {
            int N = chunkPixels;
            var solid = new bool[N,N];

            for (int py=0; py<N; py++)
            for (int px=0; px<N; px++)
            {
                int wx = cxy.x * N + px;
                int wy = cxy.y * N + py;

                float ax = (wx + _off.x) * noiseScale;
                float ay = (wy + _off.y) * noiseScale;

                if (warpStrength > 0f)
                {
                    var w = Fbm2(ax+13.7f, ay+91.3f, 2, 2f, 0.5f);
                    ax += (w.x*2f-1f) * warpStrength;
                    ay += (w.y*2f-1f) * warpStrength;
                }

                float n = Fbm1(ax, ay, octaves, lacunarity, gain);
                solid[px,py] = (n > threshold);
            }

            if (growPixels > 0) Dilate(ref solid, growPixels);
            if (blurOnePixel)    Blur1(ref solid);
            return solid;
        }

        static float Fbm1(float x, float y, int oct, float lac, float gain)
        {
            float a=1f,f=1f,s=0f,n=0f;
            for (int i=0;i<oct;i++){ s+=a; n+=a*Mathf.PerlinNoise(x*f,y*f); a*=gain; f*=lac; }
            return s>0? n/s : 0.5f;
        }
        static Vector2 Fbm2(float x, float y, int oct, float lac, float gain)
        {
            float a=1f,f=1f,s=0f,sx=0f,sy=0f;
            for (int i=0;i<oct;i++){ s+=a; sx+=a*Mathf.PerlinNoise(x*f,y*f); sy+=a*Mathf.PerlinNoise(y*f,x*f); a*=gain; f*=lac; }
            return s>0? new Vector2(sx/s, sy/s) : new Vector2(0.5f,0.5f);
        }

        static void Dilate(ref bool[,] a, int r)
        {
            int W=a.GetLength(0), H=a.GetLength(1);
            var src=a; var dst=(bool[,])a.Clone();
            for (int pass=0; pass<r; pass++)
            {
                for (int y=0;y<H;y++)
                for (int x=0;x<W;x++)
                {
                    if (src[x,y]) { dst[x,y]=true; continue; }
                    bool m = (x>0   && src[x-1,y]) ||
                             (x<W-1&& src[x+1,y]) ||
                             (y>0   && src[x,y-1]) ||
                             (y<H-1&& src[x,y+1]);
                    dst[x,y]=m;
                }
                var tmp=src; src=dst; dst=tmp;
            }
            a = src;
        }
        static void Blur1(ref bool[,] a)
        {
            int W=a.GetLength(0), H=a.GetLength(1);
            var dst=(bool[,])a.Clone();
            for (int y=0;y<H;y++)
            for (int x=0;x<W;x++)
            {
                int hits=0, cnt=0;
                for (int oy=-1; oy<=1; oy++)
                for (int ox=-1; ox<=1; ox++)
                {
                    int nx=x+ox, ny=y+oy;
                    if (nx<0||ny<0||nx>=W||ny>=H) continue;
                    if (a[nx,ny]) hits++;
                    cnt++;
                }
                dst[x,y] = hits > (cnt>>1);
            }
            a = dst;
        }
    }
}