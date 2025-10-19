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
        [Header("Mining")]
        [SerializeField, Min(0.05f)] private float swingRate = 3.0f;
        [SerializeField, Min(0.05f)] private float brushWorldRadius = 0.2f;
        [SerializeField, Min(0.25f)] private float maxRange = 1.75f;
        [SerializeField] private LayerMask terrainMask;

        [Header("Paydirt")]
        [SerializeField] private int paydirtPerSwing = 1;
        [SerializeField] private Bag bag;

        [Header("Input (auto-bind)")]
        [SerializeField] private PlayerInput input;

        // Events
        public static event Action<Miner> GlobalOnSwing;
        public event Action<Miner> Swung;
        public event Action<IReadOnlyList<TerrainChunk.RimSample>> DigApplied;
        /// <summary>center, radius, strikeDir (player→center), rim positions</summary>
        public event Action<Vector2, float, Vector2, IReadOnlyList<Vector2>> DigAppliedDebug;

        float swingCooldown;
        readonly List<TerrainChunk.RimSample> rimScratch = new(192);
        readonly List<Vector2> rimPosScratch = new(192);

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

        public void TrySwing()
        {
            if (swingCooldown > 0f) return;

            // target center + strike direction
            Vector2 origin = (Vector2)transform.position;
            Vector2 mouseWorld = MouseToWorld();
            Vector2 dir = mouseWorld - origin;

            float dist = dir.magnitude;
            if (dist < 0.01f) return;
            if (dist > maxRange) dir = dir.normalized * maxRange;

            Vector2 center = origin + dir;
            Vector2 strikeDir = (center - origin).sqrMagnitude > 1e-6f ? (center - origin).normalized : Vector2.right;
            float r = brushWorldRadius;

            // apply dig
            var hits = Physics2D.OverlapCircleAll(center, r + 0.02f, terrainMask);
            bool anyChange = false;
            rimScratch.Clear();

            for (int i = 0; i < hits.Length; i++)
            {
                var chunk = hits[i] ? hits[i].GetComponentInParent<TerrainChunk>() : null;
                if (!chunk) continue;
                anyChange |= chunk.ApplyCircle(center, r, TerrainChunk.BrushMode.Dig, rimScratch, 192);
            }

            if (anyChange)
            {
                if (bag) bag.TryAdd(paydirtPerSwing);

                rimPosScratch.Clear();
                for (int i = 0; i < rimScratch.Count; i++)
                    rimPosScratch.Add(rimScratch[i].worldPos);

                Swung?.Invoke(this);
                GlobalOnSwing?.Invoke(this);
                DigApplied?.Invoke(rimScratch);
                DigAppliedDebug?.Invoke(center, r, strikeDir, rimPosScratch);
            }

            swingCooldown = 1f / swingRate;
        }

        static Vector2 MouseToWorld()
        {
            var cam = Camera.main;
            Vector3 p = Mouse.current != null ? Mouse.current.position.ReadValue() : Vector3.zero;
            return cam ? (Vector2)cam.ScreenToWorldPoint(p) : Vector2.zero;
        }

        public void GetTarget(out Vector2 center, out float radius, out LayerMask mask)
        {
            Vector2 origin = transform.position;
            Vector2 mouseWorld = MouseToWorld();
            Vector2 dir = mouseWorld - origin;
            if (dir.magnitude > maxRange) dir = dir.normalized * maxRange;

            center = origin + dir;
            radius = brushWorldRadius;
            mask = terrainMask;
        }
    }
}