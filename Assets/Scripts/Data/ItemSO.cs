// File: Assets/Scripts/Data/ItemSO.cs
using UnityEngine;

namespace Game.Data
{
    [CreateAssetMenu(menuName = "Veinrunner/Item")]
    public sealed class ItemSO : ScriptableObject
    {
        public string id;
        public string displayName;
        // TODO: weight, rarity, value, sprite.
    }
}
