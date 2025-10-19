// namespace: Game.Player
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using Game.World;

namespace Game.Player
{
    [DisallowMultipleComponent]
    public sealed class Miner : MonoBehaviour
    {
        [Header("Mining (hold to mine)")]
        [FormerlySerializedAs("swingRate")]
        [SerializeField, Min(0.1f)] private float minesPerSecond = 1.0f;   // cadence while holding
        [SerializeField, Min(0.05f)] private float brushWorldRadius = 0.20f;
        [Tooltip("Max distance from player to the FIRST wall surface you’re allowed to hit.")]
        [SerializeField, Min(0.05f)] private float surfaceReach = 0.35f;   // short reach = you must be near the wall
        [Tooltip("How deep the dig center is pushed into the wall (0.5 = D-shape).")]
        [SerializeField, Range(0.4f, 0.6f)] private float depthFrac = 0.5f; // keep D-shape
        [Tooltip("Hard clamp for any targeting math (generally > surfaceReach).")]
        [SerializeField, Min(0.25f)] private float maxRange = 1.25f;

        [Header("Anti-lip path sweep")]
        [Tooltip("Distance between sweep samples as a fraction of radius (lower = more samples).")]
        [SerializeField, Range(0.25f, 1.0f)] private float bridgeStepFrac = 0.6f;
        [Tooltip("Radius used by sweep samples as a fraction of radius.")]
        [SerializeField, Range(0.6f, 1.2f)] private float bridgeRadiusFrac = 0.95f;

        [Header("Paydirt")]
        [SerializeField] private int paydirtPerSwing = 1;
        [SerializeField] private Bag bag;

        [Header("Input (auto-bind)")]
        [SerializeField] private PlayerInput input; // optional

        // Events (unchanged)
        public static event Action<Miner> GlobalOnSwing;
        public event Action<Miner> Swung;
        public event Action<IReadOnlyList<TerrainChunk.RimSample>> DigApplied;
        /// center, radius, strikeDir, rim world positions
        public event Action<Vector2, float, Vector2, IReadOnlyList<Vector2>> DigAppliedDebug;

        // State
        bool _isHeld;
        float _cooldown;
        Vector2 _lastDigCenter;
        bool _hasLastDig;

        // Scratch
        readonly List<TerrainChunk.RimSample> _rimScratch = new(192);
        readonly List<TerrainChunk.RimSample> _rimScratchSweep = new(32); // for sweep (ignored by FX)
        readonly List<Vector2> _rimPosScratch = new(192);

        [Header("Masks")]
        [SerializeField] private LayerMask terrainMask; // set to Ground

        void Awake()
        {
            if (!bag)  bag  = GetComponent<Bag>() ?? GetComponentInChildren<Bag>();
            if (!input) input = GetComponent<PlayerInput>();
        }

        void OnEnable()
        {
            if (!input) input = GetComponent<PlayerInput>();
            if (input && input.actions != null)
            {
                var map = input.actions.FindActionMap("Player", throwIfNotFound: false);
                var attack = map != null ? map.FindAction("Attack") : null;
                if (attack != null)
                {
                    attack.started  += OnAttackStarted;
                    attack.canceled += OnAttackCanceled;
                }
            }
        }

        void OnDisable()
        {
            if (input && input.actions != null)
            {
                var map = input.actions.FindActionMap("Player", throwIfNotFound: false);
                var attack = map != null ? map.FindAction("Attack") : null;
                if (attack != null)
                {
                    attack.started  -= OnAttackStarted;
                    attack.canceled -= OnAttackCanceled;
                }
            }
        }

        void Update()
        {
            if (_cooldown > 0f) _cooldown -= Time.unscaledDeltaTime;
            if (!_isHeld || _cooldown > 0f) return;

            if (TryComputeTarget(out Vector2 center, out Vector2 strikeDir, out float r))
            {
                bool changed = ApplyDig(center, r, strikeDir);
                if (changed)
                {
                    // anti-lip bridge
                    if (_hasLastDig) BridgePath(_lastDigCenter, center, r);
                    _lastDigCenter = center;
                    _hasLastDig = true;

                    _cooldown = 1f / Mathf.Max(0.1f, minesPerSecond);
                }
                else
                {
                    _cooldown = 0.05f; // back off a bit if nothing changed
                }
            }
            else
            {
                _cooldown = 0.05f;
            }
        }

        void OnAttackStarted(InputAction.CallbackContext _)
        {
            _isHeld = true;
            _hasLastDig = false; // start a fresh path
        }

        void OnAttackCanceled(InputAction.CallbackContext _)
        {
            _isHeld = false;
            _hasLastDig = false;
        }

        // ---------- Targeting (strict LOS) ----------
        bool TryComputeTarget(out Vector2 center, out Vector2 strikeDir, out float radius)
        {
            radius = brushWorldRadius;

            Vector2 origin = transform.position;
            Vector2 mouseWorld = MouseToWorld();
            Vector2 raw = mouseWorld - origin;
            float d = raw.magnitude;
            if (d < 0.01f) { center = default; strikeDir = default; return false; }

            Vector2 dirN = raw / d;

            // Only consider the NEAREST wall within both a short surface reach and maxRange.
            float rayMax = Mathf.Min(maxRange, surfaceReach + radius * 0.6f);
            var hit = Physics2D.Raycast(origin, dirN, rayMax, terrainMask);
            if (!hit.collider) { center = default; strikeDir = default; return false; }

            // Reject if the first surface is still too far (prevents “across the corridor” targeting)
            if (hit.distance > surfaceReach) { center = default; strikeDir = default; return false; }

            strikeDir = dirN;

            // D-shape: push center half a radius into the wall, never deeper.
            float depth = Mathf.Clamp01(depthFrac) * radius; // <= 0.5r
            center = hit.point + strikeDir * depth;

            return true;
        }

        // ---------- Apply dig ----------
        bool ApplyDig(in Vector2 center, in float r, in Vector2 strikeDir)
        {
            var hits = Physics2D.OverlapCircleAll(center, r + 0.02f, terrainMask);
            bool anyChange = false;
            _rimScratch.Clear();

            for (int i = 0; i < hits.Length; i++)
            {
                var ch = hits[i] ? hits[i].GetComponentInParent<TerrainChunk>() : null;
                if (!ch) continue;
                anyChange |= ch.ApplyCircle(center, r, TerrainChunk.BrushMode.Dig, _rimScratch, 192);
            }

            if (!anyChange) return false;

            if (bag) bag.TryAdd(paydirtPerSwing);

            _rimPosScratch.Clear();
            for (int i = 0; i < _rimScratch.Count; i++)
                _rimPosScratch.Add(_rimScratch[i].worldPos);

            Swung?.Invoke(this);
            GlobalOnSwing?.Invoke(this);
            DigApplied?.Invoke(_rimScratch);
            DigAppliedDebug?.Invoke(center, r, strikeDir, _rimPosScratch);
            return true;
        }

        // ---------- Anti-lip sweep between successive digs ----------
        void BridgePath(Vector2 from, Vector2 to, float r)
        {
            float dist = Vector2.Distance(from, to);
            if (dist <= 0.0001f) return;

            float step = Mathf.Max(0.05f, r * Mathf.Clamp01(bridgeStepFrac));
            int steps = Mathf.Clamp(Mathf.CeilToInt(dist / step), 1, 8);

            float sweepR = r * bridgeRadiusFrac;
            for (int s = 1; s < steps; s++)
            {
                Vector2 p = Vector2.Lerp(from, to, s / (float)steps);

                var hits = Physics2D.OverlapCircleAll(p, sweepR + 0.02f, terrainMask);
                _rimScratchSweep.Clear();
                for (int i = 0; i < hits.Length; i++)
                {
                    var ch = hits[i] ? hits[i].GetComponentInParent<TerrainChunk>() : null;
                    if (!ch) continue;
                    ch.ApplyCircle(p, sweepR, TerrainChunk.BrushMode.Dig, _rimScratchSweep, 0); // no FX spam
                }
            }
        }

        static Vector2 MouseToWorld()
        {
            var cam = Camera.main;
            Vector3 p = Mouse.current != null ? Mouse.current.position.ReadValue() : Vector3.zero;
            return cam ? (Vector2)cam.ScreenToWorldPoint(p) : Vector2.zero;
        }

        // For the preview
        public bool TryGetDigTarget(out Vector2 center, out float radius)
        {
            bool ok = TryComputeTarget(out center, out _, out radius);
            return ok;
        }

        // Add this public helper so preview can access strikeDir
public bool TryGetDigTargetDetailed(out Vector2 center, out float radius, out Vector2 strikeDir)
{
    return TryComputeTarget(out center, out strikeDir, out radius);
}
    }
}