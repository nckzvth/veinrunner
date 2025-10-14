// File: Assets/Scripts/Systems/AggroManager.cs
using UnityEngine;

namespace Game.Systems
{
    /// <summary>Tracks heat accumulation/decay and exposes thresholds.</summary>
    public sealed class AggroManager : MonoBehaviour
    {
        [SerializeField] private float idleDecayPerSec = 2f;
        // TODO: events: OnThresholdReached(int), OnReset().
    }
}
