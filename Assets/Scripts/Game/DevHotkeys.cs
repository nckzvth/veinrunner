// Namespace: Game.Game
using UnityEngine;
using UnityEngine.InputSystem;
using Game.Services;
using Game.World;

namespace Game.Game
{
    /// <summary>
    /// Minimal dev-only hotkeys. Attach to a GameObject in your Main scene.
    /// F2: Delete saves for current seed, clear streamer caches, rebuild window.
    /// </summary>
    public sealed class DevHotkeys : MonoBehaviour
    {
        [SerializeField] TerrainWorld world;
        [SerializeField] WorldStreamer streamer;

        void Awake()
        {
            if (!world) world = FindFirstObjectByType<TerrainWorld>();
            if (!streamer) streamer = FindFirstObjectByType<WorldStreamer>();
        }

        void Update()
        {
            if (Keyboard.current == null) return;

            if (Keyboard.current.f2Key.wasPressedThisFrame)
            {
                if (!world || !streamer) { Debug.LogWarning("[DevHotkeys] Missing refs."); return; }

                // Delete disk saves for this seed
                SaveManager.DeleteWorld(world.seed);

                // Clear RAM cache in streamer and force a clean rebuild
                streamer.ClearCachesAndReload();

                Debug.Log($"[DevHotkeys] Cleared saves & caches for seed {world.seed}.");
            }
        }
    }
}
