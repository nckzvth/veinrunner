// File: Assets/Scripts/World/ChunkSaveData.cs
using System;

namespace Game.World
{
    /// <summary>Serializable state for a chunk (mined mask, veins, sockets, flags).</summary>
    [Serializable]
    public sealed class ChunkSaveData
    {
        public int x;
        public int y;
        // TODO: mined mask, veins, sockets, flags.
    }
}
