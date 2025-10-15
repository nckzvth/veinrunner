// File: Assets/Scripts/Data/EnemySO.cs
using UnityEngine;

namespace Game.Data
{
    [CreateAssetMenu(menuName = "Veinrunner/Enemy")]
    public sealed class EnemySO : ScriptableObject
    {
        public string id;
        // TODO: stats, band tag, prefab ref.
    }
}
