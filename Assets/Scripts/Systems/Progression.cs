// File: Assets/Scripts/Systems/Progression.cs
using UnityEngine;

namespace Game.Systems
{
    public sealed class Progression : MonoBehaviour
    {
        public int BandReached { get; private set; }
        public int ClassifierTier { get; private set; } = 0;
        // TODO: pick sigils owned; persistence.
    }
}
