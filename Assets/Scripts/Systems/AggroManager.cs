// namespace: Game.Systems
using System;
using UnityEngine;
using Game.Player;

namespace Game.Systems
{
    /// <summary>
    /// Tracks enemy "heat" from mining actions and time in mine.
    /// MVP: +1 per swing, +5 per dig application, -decay per second.
    /// Logs when crossing thresholds upward (15/35/60/90 by default).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AggroManager : MonoBehaviour
    {
        [Header("Gains")]
        [Tooltip("+ heat when a swing input that actually changed terrain occurs.")]
        [SerializeField] private float gainPerSwing = 1f;

        [Tooltip("+ heat when a dig application occurred (per click that changed pixels).")]
        [SerializeField] private float gainPerDig = 5f;

        [Header("Decay")]
        [Tooltip("Heat decays per real-time second while in the mine.")]
        [SerializeField] private float decayPerSecond = 2f;

        [Header("Thresholds")]
        [SerializeField] private int[] thresholds = new[] { 15, 35, 60, 90 };

        [Header("Debug")]
        [SerializeField] private bool logThresholds = true;
        [SerializeField] private bool logHeatTicks = false;

        public float Heat => _heat;

        // Events for future systems (HUD, WaveSpawner, etc.)
        public static event Action<float> OnHeatChanged;       // passes new heat
        public static event Action<int> OnThresholdCrossed;    // passes threshold value reached

        float _heat;
        int _highestIndexReached = -1; // -1 means none reached yet
        Miner _miner;                  // the player's miner

        void OnEnable()
        {
            // +1 per successful swing (Miner raises this only when anyChange==true)
            Miner.GlobalOnSwing += OnGlobalSwing;
        }

        void Start()
        {
            // Subscribe to player's Miner for +5 per dig (DigApplied)
            _miner = UnityEngine.Object.FindFirstObjectByType<Miner>();
            if (_miner != null)
            {
                _miner.DigApplied += OnDigApplied;
            }
            else
            {
                if (logThresholds) Debug.LogWarning("[Aggro] No Miner found at start; will still gain from GlobalOnSwing.");
            }
        }

        void OnDisable()
        {
            Miner.GlobalOnSwing -= OnGlobalSwing;
            if (_miner != null) _miner.DigApplied -= OnDigApplied;
        }

        void Update()
        {
            if (_heat <= 0f) return;

            float before = _heat;
            _heat = Mathf.Max(0f, _heat - decayPerSecond * Time.deltaTime);

            if (!Mathf.Approximately(before, _heat))
            {
                OnHeatChanged?.Invoke(_heat);
                if (logHeatTicks)
                    Debug.Log($"[Aggro] Heat decayed: {before:F1} → {_heat:F1}");
            }
        }

        void OnGlobalSwing(Miner _)
        {
            AddHeat(gainPerSwing);
        }

        void OnDigApplied(System.Collections.Generic.IReadOnlyList<World.TerrainChunk.RimSample> _)
        {
            AddHeat(gainPerDig);
        }

        void AddHeat(float amt)
        {
            if (amt <= 0f) return;

            float before = _heat;
            _heat += amt;

            OnHeatChanged?.Invoke(_heat);
            if (logHeatTicks)
                Debug.Log($"[Aggro] Heat +{amt:F1}: {before:F1} → {_heat:F1}");

            CheckThresholds(before, _heat);
        }

        void CheckThresholds(float before, float after)
        {
            // We only care about upward crossings for MVP
            for (int i = 0; i < thresholds.Length; i++)
            {
                int t = thresholds[i];
                bool crossedUp = before < t && after >= t;

                if (crossedUp)
                {
                    _highestIndexReached = Mathf.Max(_highestIndexReached, i);
                    OnThresholdCrossed?.Invoke(t);

                    if (logThresholds)
                        Debug.Log($"[Aggro] Threshold reached: {t} (heat={after:F1})");

                    // Future: trigger WaveSpawner here
                }
            }
        }

        // Optional API for wash/hub behavior later
        public void ResetHeat(float to = 0f)
        {
            float before = _heat;
            _heat = Mathf.Max(0f, to);
            OnHeatChanged?.Invoke(_heat);

            if (logHeatTicks)
                Debug.Log($"[Aggro] Heat reset: {before:F1} → {_heat:F1}");
        }

        // Expose for tuning in tests
        public void SetDecay(float perSecond) => decayPerSecond = Mathf.Max(0f, perSecond);
        public void SetGains(float perSwing, float perDig)
        {
            gainPerSwing = Mathf.Max(0f, perSwing);
            gainPerDig = Mathf.Max(0f, perDig);
        }
    }
}