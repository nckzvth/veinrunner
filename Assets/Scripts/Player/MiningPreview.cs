// namespace: Game.Player
using UnityEngine;

namespace Game.Player
{
    /// <summary>
    /// Draws a circular preview at the miner's target center.
    /// Uses cached buffers + OverlapCircleNonAlloc to avoid GC.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Miner))]
    public sealed class MiningPreview : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private Miner miner;

        [Header("Visuals")]
        [SerializeField, Min(8)] private int segments = 48;
        [SerializeField] private float lineWidth = 0.02f;
        [SerializeField] private Color validColor = new Color(0f, 1f, 0.2f, 0.45f);
        [SerializeField] private Color invalidColor = new Color(1f, 0f, 0f, 0.35f);
        [SerializeField] private bool hideWhenInvalid = false;

        LineRenderer lr;
        Vector3[] unitCircle;   // cached unit circle points
        Vector3[] worldCircle;  // reused per-frame buffer
        Collider2D[] hits;      // reused for NonAlloc overlap

        void Reset()
        {
            miner = GetComponent<Miner>();
        }

        void Awake()
        {
            if (!miner) miner = GetComponent<Miner>();

            // Build renderer
            lr = gameObject.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.loop = true;
            lr.widthMultiplier = lineWidth;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.textureMode = LineTextureMode.Stretch;
            lr.numCornerVertices = 0;
            lr.numCapVertices = 0;
            lr.sortingOrder = 1000; // draw on top

            // Buffers
            int count = Mathf.Max(segments, 8);
            unitCircle = new Vector3[count];
            worldCircle = new Vector3[count];
            hits = new Collider2D[8]; // small buffer; we only need to detect >0

            // Precompute unit circle once
            float step = Mathf.PI * 2f / count;
            for (int i = 0; i < count; i++)
            {
                float a = i * step;
                unitCircle[i] = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
            }
            lr.positionCount = count;
        }

        void LateUpdate()
        {
            if (!miner) return;

            // Get miner targeting info
            miner.GetTarget(out Vector2 center, out float radius, out LayerMask terrainMask);

            // Quick validity check: any terrain overlaps this circle?
            // Build a contact filter from the layer mask (struct, no GC)
            var filter = new ContactFilter2D();
            filter.SetLayerMask(terrainMask);
            filter.useTriggers = false;
            int hitCount = Physics2D.OverlapCircle(center, radius * 1.05f, filter, hits);
            bool isValid = hitCount > 0;

            // Hide entirely if invalid and requested
            if (hideWhenInvalid && !isValid)
            {
                if (lr.enabled) lr.enabled = false;
                return;
            }
            if (!lr.enabled) lr.enabled = true;

            // Set color
            lr.startColor = lr.endColor = isValid ? validColor : invalidColor;

            // Transform cached unit circle to world circle
            for (int i = 0; i < unitCircle.Length; i++)
            {
                worldCircle[i] = center + (Vector2)(unitCircle[i] * radius);
            }
            lr.SetPositions(worldCircle);
        }
    }
}