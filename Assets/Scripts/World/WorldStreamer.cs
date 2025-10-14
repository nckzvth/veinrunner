// File: Assets/Scripts/World/WorldStreamer.cs
using UnityEngine;

namespace Game.World
{
    /// <summary>Loads/unloads a 3x3 (configurable) window of chunks around the player.</summary>
    public sealed class WorldStreamer : MonoBehaviour
    {
        [SerializeField] private TerrainWorld world;
        [SerializeField] private int windowRadius = 1; // 1 => 3x3

        // TODO: Hook player position; load/unload chunks; per-chunk save hooks.
    }
}
