// File: Assets/Scripts/Data/VeinTableSO.cs
using UnityEngine;

namespace Game.Data
{
    public enum VeinMaterial : byte { None = 0, Copper = 1, Iron = 2, Gold = 3 }

    [System.Serializable]
    public struct BandVeinRule
    {
        [Range(0,1f)] public float spawnChance;   // chance per chunk (0..1)
        public VeinMaterial material;             // material Id to paint
        [Min(1)] public int clusterRadiusPx;      // approx pixel radius for cluster
        [Range(0f,1f)] public float density;      // fraction of filled pixels inside radius
    }

    [CreateAssetMenu(menuName = "World/Vein Table", fileName = "VeinTable")]
    public class VeinTableSO : ScriptableObject
    {
        [Header("Per-band vein rules (Yard/Galleries/Ancient)")]
        public BandVeinRule yard = new BandVeinRule { spawnChance = 0.10f, material = VeinMaterial.Copper, clusterRadiusPx = 6, density = 0.55f };
        public BandVeinRule galleries = new BandVeinRule { spawnChance = 0.08f, material = VeinMaterial.Iron, clusterRadiusPx = 7, density = 0.55f };
        public BandVeinRule ancient = new BandVeinRule { spawnChance = 0.06f, material = VeinMaterial.Gold, clusterRadiusPx = 8, density = 0.55f };
    }
}

