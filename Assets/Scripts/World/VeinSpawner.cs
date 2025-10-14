// File: Assets/Scripts/World/VeinSpawner.cs
using UnityEngine;

namespace Game.World
{
    /// <summary>Rolls rare veins on chunk create using band table.</summary>
    public sealed class VeinSpawner : MonoBehaviour
    {
        [SerializeField] private ScriptableObject veinTable; // placeholder until VeinTableSO exists
        // TODO: spawn logic using band + rng.
    }
}
