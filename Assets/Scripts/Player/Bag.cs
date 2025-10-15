// File: Assets/Scripts/Player/Bag.cs
using UnityEngine;

namespace Game.Player
{
    /// <summary>Tracks paydirt and over-cap penalties.</summary>
    public sealed class Bag : MonoBehaviour
    {
        [SerializeField] private int bagCap = 60;
        public int Current { get; private set; }

        public bool TryAdd(int amount)
        {
            Current += amount;
            return true;
        }
    }
}
