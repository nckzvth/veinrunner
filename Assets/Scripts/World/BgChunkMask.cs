// Assets/Scripts/World/BgChunkMask.cs
using UnityEngine;

namespace Game.World
{
    [DisallowMultipleComponent]
    public sealed class BgChunkMask : MonoBehaviour
    {
        // Flattened row-major: idx = y*width + x, true = SOLID
        [System.NonSerialized] public bool[] solid;
        [System.NonSerialized] public int width;
        [System.NonSerialized] public int height;
        [System.NonSerialized] public float pixelsPerUnit;

        public static BgChunkMask Attach(Transform chunk, bool[] solidFlat, int w, int h, float ppu)
        {
            var m = chunk.GetComponent<BgChunkMask>();
            if (!m) m = chunk.gameObject.AddComponent<BgChunkMask>();
            m.solid = solidFlat;
            m.width = w;
            m.height = h;
            m.pixelsPerUnit = ppu;
            return m;
        }

        // Convenience overload for your bool[,] map
        public static BgChunkMask Attach(Transform chunk, bool[,] solid2D, int w, int h, float ppu)
        {
            var flat = new bool[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    flat[y * w + x] = solid2D[x, y]; // same orientation you used when baking the texture

            return Attach(chunk, flat, w, h, ppu);
        }
    }
}