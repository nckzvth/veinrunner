// File: Assets/Scripts/World/TerrainChunk.cs
using UnityEngine;

namespace Game.World
{
    /// <summary>Holds pixel/voxel data, texture refs, collider rebuild hooks.</summary>
    public sealed class TerrainChunk : MonoBehaviour
    {
        public Vector2Int Coord { get; private set; }

        public void Init(Vector2Int coord) => Coord = coord;

        // TODO: pixel data, texture refs, collider rebuilds.
    }
}
