// File: Assets/Scripts/UI/PauseMenu.cs
// Namespace: Game.UI
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using Game.Services;
using Game.World;

namespace Game.UI
{
    /// <summary>
    /// ESC toggles a simple pause menu with Resume / Reset World / Quit.
    /// Reset World deletes disk saves for the current seed and reloads chunks.
    /// </summary>
    public sealed class PauseMenu : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] TerrainWorld world;
        [SerializeField] WorldStreamer streamer;

        [Header("UI")]
        [SerializeField] GameObject pausePanel;
        [SerializeField] Button resumeButton;
        [SerializeField] Button resetWorldButton;
        [SerializeField] Button quitButton; // optional

        bool _paused;

        void Awake()
        {
            if (!world) world = FindFirstObjectByType<TerrainWorld>();
            if (!streamer) streamer = FindFirstObjectByType<WorldStreamer>();
            SetPaused(false);

            // Optional: wire buttons in code if not wired in Inspector
            if (resumeButton)     resumeButton.onClick.AddListener(OnResume);
            if (resetWorldButton) resetWorldButton.onClick.AddListener(OnResetWorld);
            if (quitButton)       quitButton.onClick.AddListener(OnQuit);
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame)
            {
                TogglePause();
            }
        }

        public void TogglePause()
        {
            SetPaused(!_paused);
        }

        public void OnResume()
        {
            SetPaused(false);
        }

        public void OnResetWorld()
        {
            if (!world || !streamer)
            {
                Debug.LogWarning("[PauseMenu] Missing World or WorldStreamer reference.");
                return;
            }

            // Delete disk saves for this seed
            SaveManager.DeleteWorld(world.seed);

            // Clear RAM cache and rebuild window from base gen
            streamer.ClearCachesAndReload();

            // Keep paused or resume — choose resume for quick feedback
            SetPaused(false);
            Debug.Log($"[PauseMenu] World reset for seed {world.seed}.");
        }

        public void OnQuit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        void SetPaused(bool paused)
        {
            _paused = paused;
            if (pausePanel) pausePanel.SetActive(paused);
            Time.timeScale = paused ? 0f : 1f;
        }
    }
}
