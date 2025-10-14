// File: Assets/Scripts/Game/GameBootstrap.cs
using UnityEngine;
using Game.World;
using Game.Services;

namespace Game
{
    /// <summary>Single entry point; later wires databases/services and loads first chunks.</summary>
    public sealed class GameBootstrap : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private TerrainWorld world;
        [SerializeField] private PoolManager poolManager;
        [SerializeField] private SaveManager saveManager;

        private void Awake()
        {
            // TODO: init order and service registration.
            DontDestroyOnLoad(gameObject);
        }
    }
}
