// namespace: Game.Player
using UnityEngine;

namespace Game.Player
{
    public class Bag : MonoBehaviour
    {
        [SerializeField, Min(1)] private int bagCap = 60;
        public int Capacity => bagCap;
        public int Current { get; private set; }
        public bool IsOverCap => Current > bagCap;

        // Returns actual amount added (allows over-cap; controller applies penalty)
        public int TryAdd(int amount)
        {
            if (amount <= 0) return 0;
            int before = Current;
            Current += amount;
            return Current - before;
        }

        public int TryRemove(int amount)
        {
            if (amount <= 0) return 0;
            int removed = Mathf.Min(amount, Current);
            Current -= removed;
            return removed;
        }

        public void Clear() => Current = 0;
    }
}
