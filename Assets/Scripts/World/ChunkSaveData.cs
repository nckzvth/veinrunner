// File: Assets/Scripts/World/ChunkSaveData.cs
using System;

namespace Game.World
{
    /// <summary>
    /// Versioned, compact per-chunk delta: what changed vs generated base.
    /// v1 stores mined (was solid -> now air) and filled (was air -> now solid).
    /// </summary>
    [Serializable]
    public struct ChunkSaveData
    {
        public int version;               // = 1
        public int chunkX;
        public int chunkY;
        public int size;                  // pixels per side (e.g. 64)
        public byte[] minedBits;          // bit-packed N*N, 1 = mined
        public byte[] filledBits;         // bit-packed N*N, 1 = filled

        public ChunkSaveData(int v, int x, int y, int s, byte[] mined, byte[] filled)
        {
            version = v; chunkX = x; chunkY = y; size = s; minedBits = mined; filledBits = filled;
        }

        public bool IsValid(int expectedSize) => version == 1 && size == expectedSize
            && minedBits != null && filledBits != null;
    }
}

