// namespace: Game.Services
using System;
using System.IO;
using UnityEngine;

namespace Game.Services
{
    /// <summary>
    /// Minimal, versioned player-side save separate from chunk saves.
    /// Stores Bag.Current now; will extend for movement upgrades later.
    /// </summary>
    public static class PlayerSaveService
    {
        [Serializable]
        private struct PlayerSaveV1
        {
            public int version;
            public int bagCurrent;
            // future: extraJumps, moveSpeedMul, jumpForceMul...
        }

        private static readonly string FilePath =
            Path.Combine(Application.persistentDataPath, "player_v1.json");

        public static bool TryLoad(out int bagCurrent)
        {
            bagCurrent = 0;
            try
            {
                if (!File.Exists(FilePath)) return false;
                var json = File.ReadAllText(FilePath);
                var data = JsonUtility.FromJson<PlayerSaveV1>(json);
                if (data.version != 1) return false;
                bagCurrent = Mathf.Max(0, data.bagCurrent);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[PlayerSave] Load failed: {e.Message}");
                return false;
            }
        }

        public static void Save(int bagCurrent)
        {
            try
            {
                var data = new PlayerSaveV1 { version = 1, bagCurrent = Mathf.Max(0, bagCurrent) };
                var json = JsonUtility.ToJson(data);
                File.WriteAllText(FilePath, json);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[PlayerSave] Save failed: {e.Message}");
            }
        }

        public static void Clear()
        {
            try { if (File.Exists(FilePath)) File.Delete(FilePath); }
            catch (Exception e) { Debug.LogWarning($"[PlayerSave] Clear failed: {e.Message}"); }
        }
    }
}