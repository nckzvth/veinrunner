// File: Assets/Scripts/Systems/ItemDatabase.cs
using UnityEngine;

namespace Game.Systems
{
    /// <summary>Lookup for ItemSO and related data.</summary>
    public sealed class ItemDatabase : MonoBehaviour
    {
        [SerializeField] private ScriptableObject[] items; // placeholder until ItemSO
    }
}
