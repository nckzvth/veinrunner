// namespace: Game.Player
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Game.World;

namespace Game.Player
{
    [DisallowMultipleComponent]
    public sealed class Miner : MonoBehaviour
    {
        [Header("Input (optional auto-bind)")]
        [SerializeField] private PlayerInput input;
        
        [Header("Mining")]
        [SerializeField, Min(0.05f)] private float swingRate = 3.0f; // swings per second
        [SerializeField, Min(0.05f)] private float brushWorldRadius = 0.2f; // tuned to new world
        [SerializeField, Min(0.25f)] private float maxRange = 1.75f;
        [SerializeField] private LayerMask terrainMask; // Ground layer

        [Header("Paydirt")]
        [SerializeField] private int paydirtPerSwing = 1; // simple proto
        [SerializeField] private Bag bag;

        // AggroManager can subscribe later; keep decoupled now
        public static event Action<Miner> GlobalOnSwing;
        public event System.Action<Miner> Swung;

        float swingCooldown;
        readonly List<TerrainChunk.RimSample> rimScratch = new(192);

        void Awake()
        {
            if (!bag) bag = GetComponent<Bag>() ?? GetComponentInChildren<Bag>();
            if (!input) input = GetComponent<PlayerInput>();
        }

                void OnEnable()
        {
            if (!input) input = GetComponent<PlayerInput>();
            if (input && input.actions != null)
            {
                var map = input.actions.FindActionMap("Player", throwIfNotFound: false);
                var attack = map != null ? map.FindAction("Attack") : null;
                if (attack != null) attack.performed += OnAttack;
            }
        }

        void OnDisable()
        {
            if (input && input.actions != null)
            {
                var map = input.actions.FindActionMap("Player", throwIfNotFound: false);
                var attack = map != null ? map.FindAction("Attack") : null;
                if (attack != null) attack.performed -= OnAttack;
            }
        }

        void Update()
        {
            if (swingCooldown > 0f) swingCooldown -= Time.unscaledDeltaTime;
        }

        public void OnAttack(InputAction.CallbackContext ctx)
        {
            if (!ctx.performed) return;
            TrySwing();
        }

        void TrySwing()
        {
            if (swingCooldown > 0f) return;

            Vector2 origin = transform.position;
            Vector2 mouseWorld = MouseToWorld();
            Vector2 dir = mouseWorld - origin;

            float dist = dir.magnitude;
            if (dist < 0.01f) return;
            if (dist > maxRange) dir = dir.normalized * maxRange;

            Vector2 center = origin + dir;
            float r = brushWorldRadius;

            // Overlap with CompositeCollider2D-based chunks (use parent lookup)
            var hits = Physics2D.OverlapCircleAll(center, r + 0.02f, terrainMask);
            bool anyChange = false;
            rimScratch.Clear();

            for (int i = 0; i < hits.Length; i++)
            {
                var h = hits[i];
                var chunk = h ? h.GetComponentInParent<TerrainChunk>() : null;
                if (!chunk) continue;

                bool changed = chunk.ApplyCircle(center, r, TerrainChunk.BrushMode.Dig, rimScratch, 192);
                anyChange |= changed;
            }

            if (anyChange)
            {
                if (bag) bag.TryAdd(paydirtPerSwing);
                Swung?.Invoke(this);              // NEW
                GlobalOnSwing?.Invoke(this);      // existing hook for Aggro later
                // TODO: FX via PoolManager using rimScratch
            }

            swingCooldown = 1f / swingRate;
        }

        static Vector2 MouseToWorld()
        {
            var cam = Camera.main;
            Vector3 p = Mouse.current != null ? Mouse.current.position.ReadValue() : Vector3.zero;
            return cam ? (Vector2)cam.ScreenToWorldPoint(p) : Vector2.zero;
        }
    }
}