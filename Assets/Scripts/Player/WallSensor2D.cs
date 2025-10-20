// namespace: Game.Player
using UnityEngine;

namespace Game.Player
{
    /// <summary>
    /// Wall/ceiling probes for future wall-tech & dash.
    /// Adds auto-fit from the player's Collider2D so probes track collider size.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WallSensors2D : MonoBehaviour
    {
        [Header("Layer Mask")]
        [SerializeField] private LayerMask groundMask;

        [Header("Auto-fit")]
        [Tooltip("When enabled, offsets are derived from the first Collider2D on this object.")]
        [SerializeField] private bool autoFitFromCollider = true;
        [Tooltip("Extra clearance applied when auto-fitting (world units).")]
        [SerializeField, Min(0f)] private float fitMargin = 0.01f;

        [Header("Geometry (world units)")]
        [Tooltip("Vertical half-extent from center to shoulder/hip probe.")]
        [SerializeField, Min(0.05f)] private float halfHeight = 0.42f;
        [Tooltip("Horizontal offset from center to side probes (roughly half-body width).")]
        [SerializeField, Min(0.05f)] private float sideOffset = 0.20f;
        [Tooltip("Probe circle radius for Overlap tests.")]
        [SerializeField, Min(0.01f)] private float probeRadius = 0.06f;
        [Tooltip("Ceiling probe height from center.")]
        [SerializeField, Min(0.05f)] private float ceilingOffset = 0.46f;

        [Header("Update")]
        [Tooltip("If true, sensors update in FixedUpdate (physics step). Otherwise Update().")]
        [SerializeField] private bool runInFixedUpdate = true;

        // Public readouts
        public bool IsTouchingWallLeft  { get; private set; }
        public bool IsTouchingWallRight { get; private set; }
        public bool IsCeilingBlocked    { get; private set; }

        // internals
        readonly Collider2D[] _hits = new Collider2D[1];
        ContactFilter2D _filter;

        void Reset()
        {
            // default Ground
            int g = LayerMask.NameToLayer("Ground");
            if (g >= 0) groundMask = (LayerMask)(1 << g);
            FitFromColliderIfNeeded();
        }

        void Awake()
        {
            _filter.ClearLayerMask();
            _filter.SetLayerMask(groundMask);
            _filter.useTriggers = false;
            FitFromColliderIfNeeded();
        }

        void OnValidate()
        {
            _filter.ClearLayerMask();
            _filter.SetLayerMask(groundMask);
            _filter.useTriggers = false;

            halfHeight    = Mathf.Max(0.05f, halfHeight);
            sideOffset    = Mathf.Max(0.05f, sideOffset);
            probeRadius   = Mathf.Max(0.01f, probeRadius);
            ceilingOffset = Mathf.Max(0.05f, ceilingOffset);

            FitFromColliderIfNeeded();
        }

        void Update()      { if (!runInFixedUpdate) Sample(); }
        void FixedUpdate() { if ( runInFixedUpdate) Sample(); }

        void Sample()
        {
            Vector2 c = transform.position;

            // Left side: shoulder & hip
            Vector2 lShoulder = c + new Vector2(-sideOffset,  halfHeight);
            Vector2 lHip      = c + new Vector2(-sideOffset, -halfHeight);

            // Right side: shoulder & hip
            Vector2 rShoulder = c + new Vector2( sideOffset,  halfHeight);
            Vector2 rHip      = c + new Vector2( sideOffset, -halfHeight);

            // Ceiling straight up from center
            Vector2 head = c + new Vector2(0f, ceilingOffset);

            IsTouchingWallLeft  = (Physics2D.OverlapCircle(lShoulder, probeRadius, _filter, _hits) > 0)
                               || (Physics2D.OverlapCircle(lHip,      probeRadius, _filter, _hits) > 0);

            IsTouchingWallRight = (Physics2D.OverlapCircle(rShoulder, probeRadius, _filter, _hits) > 0)
                               || (Physics2D.OverlapCircle(rHip,      probeRadius, _filter, _hits) > 0);

            IsCeilingBlocked    =  Physics2D.OverlapCircle(head,      probeRadius, _filter, _hits) > 0;
        }

        void FitFromColliderIfNeeded()
        {
            if (!autoFitFromCollider) return;
            var col = GetComponent<Collider2D>();
            if (!col) return;

            // World-space bounds of collider (supports Box, Capsule, Circle…)
            var b = col.bounds;
            float halfW = b.extents.x;
            float halfH = b.extents.y;

            // Probe radius: ~2px at 32 PPU by default if unset; otherwise keep user value
            probeRadius = Mathf.Max(probeRadius, 0.04f);

            // Side probes sit just outside collider edge, minus probe radius, minus margin
            sideOffset    = Mathf.Max(0.05f, halfW + fitMargin);
            // Shoulder/hip slightly inside the top/bottom to avoid flooring/ceiling noise
            halfHeight    = Mathf.Max(0.05f, halfH - fitMargin);
            // Ceiling probe a hair above the collider
            ceilingOffset = Mathf.Max(0.05f, halfH + fitMargin * 2f);
        }

#if UNITY_EDITOR
        [ContextMenu("Fit From Collider Now")]
        void ContextFit() { autoFitFromCollider = true; FitFromColliderIfNeeded(); }

        void OnDrawGizmosSelected()
        {
            Vector2 c = transform.position;

            Vector2 lShoulder = c + new Vector2(-sideOffset,  halfHeight);
            Vector2 lHip      = c + new Vector2(-sideOffset, -halfHeight);
            Vector2 rShoulder = c + new Vector2( sideOffset,  halfHeight);
            Vector2 rHip      = c + new Vector2( sideOffset, -halfHeight);
            Vector2 head      = c + new Vector2(0f, ceilingOffset);

            DrawProbe(lShoulder, IsTouchingWallLeft);
            DrawProbe(lHip,      IsTouchingWallLeft);
            DrawProbe(rShoulder, IsTouchingWallRight);
            DrawProbe(rHip,      IsTouchingWallRight);
            DrawProbe(head,      IsCeilingBlocked);

            void DrawProbe(Vector2 p, bool hit)
            {
                Color on  = new Color(0.2f, 1f, 0.4f, 0.85f);
                Color off = new Color(1f, 0.9f, 0.2f, 0.35f);
                Gizmos.color = hit ? on : off;
                Gizmos.DrawWireSphere(p, probeRadius);
            }

            // Draw collider bounds outline for quick visual calibration
            var col = GetComponent<Collider2D>();
            if (col)
            {
                Gizmos.color = new Color(0.3f, 0.7f, 1f, 0.25f);
                var b = col.bounds;
                Gizmos.DrawWireCube(b.center, b.size);
            }
        }
#endif
    }
}