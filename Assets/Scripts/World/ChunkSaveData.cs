// File: Assets/Scripts/World/ChunkSaveData.cs
using System;

namespace Game.World
{
    /// <summary>
    /// Versioned, compact per-chunk delta: what changed vs generated base.
    /// v1: minedBits, filledBits
    /// v2: adds oreConsumedBits (ore that should never reappear)
    /// </summary>
    [Serializable]
    public struct ChunkSaveData
    {
        public int version;               // 1 or 2
        public int chunkX;
        public int chunkY;
        public int size;                  // pixels per side (e.g. 64)
        public byte[] minedBits;          // base solid -> now air
        public byte[] filledBits;         // base air -> now solid
        public byte[] oreConsumedBits;    // v2: tiles where ore was dug at least once

        public ChunkSaveData(int v, int x, int y, int s, byte[] mined, byte[] filled, byte[] oreConsumed = null)
        {
            version = v; chunkX = x; chunkY = y; size = s;
            minedBits = mined; filledBits = filled; oreConsumedBits = oreConsumed;
        }

        public bool IsValid(int expectedSize)
            => (version == 1 || version == 2) && size == expectedSize
               && minedBits != null && filledBits != null; // oreConsumedBits may be null for v1
    }
}

