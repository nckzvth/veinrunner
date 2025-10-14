// File: Assets/Scripts/Systems/ClassifierSystem.cs
using UnityEngine;

namespace Game.Systems
{
    /// <summary>Applies WashTable by EffectiveTier; rolls currencies & shard rewards.</summary>
    public sealed class ClassifierSystem : MonoBehaviour
    {
        [SerializeField] private ScriptableObject washTable; // placeholder until WashTableSO
    }
}
