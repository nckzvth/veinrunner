// File: Assets/Scripts/Services/SaveManager.cs
// Namespace: Game.Services
using System;
using System.IO;
using UnityEngine;
using Game.World; // for ChunkSaveData

namespace Game.Services
{
    /// <summary>
    /// Minimal, synchronous, per-chunk binary saves keyed by seed/cx/cy.
    /// Writes/reads ChunkSaveData v2 (minedBits, filledBits, oreConsumedBits).
    /// </summary>
    public static class SaveManager
    {
        static string _root;     // persistent root
        static int _seed = int.MinValue;

        /// <summary>Initialize root folder for the given seed.</summary>
        public static void Init(int seed)
        {
            if (_root == null)
            {
                _root = Path.Combine(Application.persistentDataPath, "saves");
                Directory.CreateDirectory(_root);
            }
            _seed = seed;
            Directory.CreateDirectory(GetSeedDir(seed));
        }

        static string GetSeedDir(int seed) => Path.Combine(_root, seed.ToString());
        static string GetChunkPath(int seed, Vector2Int coord)
        {
            // Example: /saves/12345/-2_7.bin
            return Path.Combine(GetSeedDir(seed), $"{coord.x}_{coord.y}.bin");
        }

        public static void SaveChunk(int seed, Vector2Int coord, in ChunkSaveData data)
        {
            if (data.version < 1) return; // ignore invalid
            try
            {
                string path = GetChunkPath(seed, coord);
                using var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
                using var bw = new BinaryWriter(fs);

                bw.Write(data.version);   // int32
                bw.Write(data.size);      // int32 (chunk side in pixels)
                bw.Write(coord.x);        // int32
                bw.Write(coord.y);        // int32

                WriteBytes(bw, data.minedBits);
                WriteBytes(bw, data.filledBits);
                WriteBytes(bw, data.oreConsumedBits); // may be null; we write length=0
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveManager] SaveChunk failed {coord}: {e.Message}");
            }
        }

        public static bool TryLoadChunk(int seed, Vector2Int coord, out ChunkSaveData data)
        {
            data = default;
            string path = GetChunkPath(seed, coord);
            if (!File.Exists(path)) return false;

            try
            {
                using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                using var br = new BinaryReader(fs);

                int version   = br.ReadInt32();
                int chunkSize = br.ReadInt32();
                int cx        = br.ReadInt32();
                int cy        = br.ReadInt32();

                byte[] mined  = ReadBytes(br);
                byte[] filled = ReadBytes(br);
                byte[] ore    = ReadBytes(br); // may be empty

                data = new ChunkSaveData(
                    v: Math.Max(2, version),   // normalize up to 2
                    x: cx,
                    y: cy,
                    s: chunkSize,
                    mined: mined,
                    filled: filled,
                    oreConsumed: (ore != null && ore.Length > 0) ? ore : null
                );
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveManager] TryLoadChunk failed {coord}: {e.Message}");
                return false;
            }
        }

        /// <summary>Delete all saved chunks for the given seed.</summary>
        public static bool DeleteWorld(int seed)
        {
            try
            {
                string dir = Path.Combine(Application.persistentDataPath, "saves", seed.ToString());
                if (Directory.Exists(dir))
                {
                    Directory.Delete(dir, recursive: true);
                    Debug.Log($"[SaveManager] Deleted world folder: {dir}");
                    // Recreate to keep future saves simple
                    Directory.CreateDirectory(dir);
                    return true;
                }
                return false;
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveManager] DeleteWorld({seed}) failed: {e.Message}");
                return false;
            }
        }

        static void WriteBytes(BinaryWriter bw, byte[] arr)
        {
            if (arr == null) { bw.Write(0); return; }
            bw.Write(arr.Length);
            bw.Write(arr);
        }

        static byte[] ReadBytes(BinaryReader br)
        {
            int len = br.ReadInt32();
            if (len <= 0) return Array.Empty<byte>();
            return br.ReadBytes(len);
        }
    }
}
