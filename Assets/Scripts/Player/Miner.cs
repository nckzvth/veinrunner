// File: Assets/Scripts/Player/Miner.cs
using UnityEngine;

namespace Game.Player
{
    /// <summary>Handles swing cadence, pixel damage, and paydirt drops.</summary>
    public sealed class Miner : MonoBehaviour
    {
        [SerializeField] private float swingRate = 1.2f;
        // TODO: apply pixel damage, raise AggroManager events.
    }
}
