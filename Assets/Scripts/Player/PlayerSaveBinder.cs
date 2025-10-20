// namespace: Game.Player
using UnityEngine;
using Game.Services;

namespace Game.Player
{
    /// <summary>
    /// Loads/saves Bag.Current using PlayerSaveService. Attach to Player.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerSaveBinder : MonoBehaviour
    {
        [SerializeField] private Bag bag;
        [Tooltip("If true, we ignore saved bag and start at 0 next session (for testing).")]
        [SerializeField] private bool startFresh = false;

        void Reset() { bag = GetComponent<Bag>() ?? GetComponentInChildren<Bag>(); }

        void Awake()
        {
            if (!bag) bag = GetComponent<Bag>() ?? GetComponentInChildren<Bag>();
            if (!bag) { enabled = false; return; }

            if (startFresh) PlayerSaveService.Clear();

            if (PlayerSaveService.TryLoad(out int savedCurrent))
            {
                bag.SetCurrent(savedCurrent);
            }

            bag.OnChanged += OnBagChanged;
        }

        void OnDestroy()
        {
            if (bag) bag.OnChanged -= OnBagChanged;
        }

        void OnBagChanged(int current, int capacity)
        {
            PlayerSaveService.Save(current);
        }
    }
}