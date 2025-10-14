// File: Assets/Scripts/World/TerrainWorld.cs
using UnityEngine;

namespace Game.World
{
    /// <summary>Entry for world settings and references (seed, bands, chunk grid).</summary>
    public sealed class TerrainWorld : MonoBehaviour
    {
        [Header("World Seed & Grid")]
        [SerializeField] private int seed = 12345;
        [SerializeField] private Vector2Int chunkSize = new Vector2Int(64, 64);

        public int Seed => seed;
        public Vector2Int ChunkSize => chunkSize;
    }
}
