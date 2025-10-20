// namespace: Game.Player
using System;
using UnityEngine;

namespace Game.Player
{
    /// <summary>
    /// Holds paydirt with a hard capacity. Notifies listeners on change.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Bag : MonoBehaviour
    {
        [Header("Bag")]
        [SerializeField, Min(1)] private int capacity = 60;
        [SerializeField, Min(0)] private int current;

        public int Capacity => capacity;
        public int Current  => current;
        public bool IsOverCap => current > capacity;
        public float OverCapRatio => capacity > 0 ? Mathf.Clamp01((current - capacity) / (float)capacity) : 0f;

        /// <summary>Fired on any state change (add/remove/capacity set).</summary>
        public event Action<int,int> OnChanged;

        public bool TryAdd(int amount)
        {
            if (amount <= 0) return false;
            int before = current;
            current += amount;
            if (current != before) { OnChanged?.Invoke(current, capacity); }
            return current != before;
        }

        public bool TryRemove(int amount)
        {
            if (amount <= 0) return false;
            int before = current;
            current = Mathf.Max(0, current - amount);
            if (current != before) { OnChanged?.Invoke(current, capacity); }
            return current != before;
        }

        /// <summary>Sets absolute value (used by save/load).</summary>
        public void SetCurrent(int value)
        {
            value = Mathf.Max(0, value);
            if (current != value)
            {
                current = value;
                OnChanged?.Invoke(current, capacity);
            }
        }

        /// <summary>Set capacity; will not clamp current (over-cap allowed by design).</summary>
        public void SetCapacity(int cap)
        {
            cap = Mathf.Max(1, cap);
            if (capacity != cap)
            {
                capacity = cap;
                OnChanged?.Invoke(current, capacity);
            }
        }
    }
}