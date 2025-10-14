// File: Assets/Scripts/Data/AbilitySO.cs
using UnityEngine;

namespace Game.Data
{
    [CreateAssetMenu(menuName = "Veinrunner/Ability")]
    public sealed class AbilitySO : ScriptableObject
    {
        public string id;
        // TODO: reference to prefab/behavior, level curves.
    }
}
