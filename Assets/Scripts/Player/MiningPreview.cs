// namespace: Game.Player
using UnityEngine;

namespace Game.Player
{
    /// <summary>
    /// Soft filled mining preview clipped to SOLID terrain + a thin forward-arc line
    /// aligned to the strike direction (toward the cursor).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Miner))]
    public sealed class MiningPreview : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private Miner miner;

        [Header("Fill (disc)")]
        [SerializeField] private bool showFill = true;
        [Tooltip("If empty, a default Sprites/Default (or URP Particles/Unlit) material is created at runtime.")]
        [SerializeField] private Material fillMaterial;
        [SerializeField, Min(12)] private int segments = 48;
        [Tooltip("Radial samples per angle (higher = tighter clip).")]
        [SerializeField, Range(3, 12)] private int radialSamples = 6;
        [SerializeField] private Color fillColor = new Color(1f, 0.97f, 0.75f, 0.33f);
        [SerializeField] private float zOffset = -0.01f;

        [Header("Rim line (forward arc only)")]
        [SerializeField] private bool showRimLine = true;
        [Tooltip("0 = half rim, 1 = only straight-ahead arc.")]
        [SerializeField, Range(0f, 1f)] private float rimArcWidth = 0.4f; // dot threshold
        [SerializeField, Min(0.001f)] private float rimLineWidth = 0.025f;
        [SerializeField] private Color rimLineColor = new Color(1f, 0.96f, 0.70f, 0.55f);

        [Header("Collision")]
        [Tooltip("Terrain layers to test for 'solid'. If 0, auto-uses Ground.")]
        [SerializeField] private LayerMask terrainMask;

        // ---- Fill mesh objects ----
        GameObject _fillGO;
        MeshFilter _mf;
        MeshRenderer _mr;
        Mesh _mesh;

        // ---- Rim line objects ----
        GameObject _rimGO;
        LineRenderer _lr;

        // Cached unit circle directions
        Vector2[] _dirs;

        // Reusable buffers (no GC)
        Vector3[] _verts;   // fill verts (center + ring)
        Color[]   _colors;  // fill colors
        int[]     _tris;    // fill triangles (static)
        readonly Collider2D[] _probe = new Collider2D[1];
        ContactFilter2D _probeFilter;

        // Rim positions buffer
        Vector3[] _rimPos;
        int _rimCount;

        // Shared runtime default material to avoid duplication
        static Material s_DefaultFillMat;

        void Reset()
        {
            miner = GetComponent<Miner>();
            if (terrainMask == 0)
            {
                int g = LayerMask.NameToLayer("Ground");
                if (g >= 0) terrainMask = (LayerMask)(1 << g);
            }
        }

        void Awake()
        {
            if (!miner) miner = GetComponent<Miner>();

            // ---------- Fill (disc) ----------
            _fillGO = new GameObject("MiningPreviewFill");
            _fillGO.transform.SetParent(transform, false);
            _fillGO.layer = gameObject.layer;

            _mf = _fillGO.AddComponent<MeshFilter>();
            _mr = _fillGO.AddComponent<MeshRenderer>();
            _mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _mr.receiveShadows = false;
            _mr.sortingOrder = 1000;

            if (!fillMaterial) fillMaterial = GetOrCreateDefaultFillMaterial();
            _mr.sharedMaterial = fillMaterial;

            BuildStaticTopology();

            // Probe filter for "solid"
            _probeFilter.SetLayerMask(terrainMask);
            _probeFilter.useTriggers = false;

            _fillGO.SetActive(false);

            // ---------- Rim line ----------
            _rimGO = new GameObject("MiningPreviewRim");
            _rimGO.transform.SetParent(transform, false);
            _rimGO.layer = gameObject.layer;

            _lr = _rimGO.AddComponent<LineRenderer>();
            _lr.useWorldSpace = true;
            _lr.loop = false;
            _lr.textureMode = LineTextureMode.Stretch;
            _lr.alignment = LineAlignment.View; // billboarded for 2D
            _lr.numCornerVertices = 1;
            _lr.numCapVertices = 1;
            _lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _lr.receiveShadows = false;
            _lr.sortingOrder = 1001;
            _lr.widthMultiplier = rimLineWidth;
            _lr.startColor = _lr.endColor = rimLineColor;
            // Reuse fill material if available (any transparent sprite/unlit is fine)
            _lr.sharedMaterial = _mr.sharedMaterial;

            _rimGO.SetActive(false);

            // Rim positions buffer
            _rimPos = new Vector3[Mathf.Max(segments, 12)];
        }

        void OnValidate()
        {
            if (segments < 12) segments = 12;
            if (radialSamples < 3) radialSamples = 3;
            if (_mesh != null) BuildStaticTopology();

            // Keep LR width synced if edited in inspector at runtime
            if (_lr != null) _lr.widthMultiplier = rimLineWidth;
        }

        void LateUpdate()
        {
            if (!miner)
            {
                _fillGO.SetActive(false);
                _rimGO.SetActive(false);
                return;
            }

            // Need strikeDir to orient the forward arc
            if (!miner.TryGetDigTargetDetailed(out Vector2 center, out float radius, out Vector2 strikeDir))
            {
                if (_fillGO.activeSelf) _fillGO.SetActive(false);
                if (_rimGO.activeSelf) _rimGO.SetActive(false);
                return;
            }

            // Position parents
            _fillGO.transform.position = new Vector3(center.x, center.y, zOffset);
            _rimGO.transform.position  = new Vector3(0f, 0f, 0f); // LR uses world positions

            // ----- Build clipped fill -----
            if (showFill)
            {
                float step = radius / Mathf.Max(1, radialSamples);
                _verts[0]  = Vector3.zero; // center
                _colors[0] = fillColor;

                for (int i = 0; i < segments; i++)
                {
                    Vector2 dir = _dirs[i];
                    float r = 0f;
                    bool hitAir = false;

                    for (int s = 1; s <= radialSamples; s++)
                    {
                        float testR = s * step;
                        Vector2 testPos = center + dir * testR;
                        int count = Physics2D.OverlapPoint(testPos, _probeFilter, _probe);
                        if (count == 0)
                        {
                            hitAir = true;
                            r = Mathf.Max(0f, testR - step);
                            break;
                        }
                    }
                    if (!hitAir) r = radius;

                    _verts[1 + i] = new Vector3(dir.x * r, dir.y * r, 0f);

                    // Edge fades to 0 alpha
                    Color edge = fillColor; edge.a = 0f;
                    _colors[1 + i] = edge;
                }

                _mesh.SetVertices(_verts);
                _mesh.SetColors(_colors);

                if (!_fillGO.activeSelf) _fillGO.SetActive(true);
            }
            else if (_fillGO.activeSelf)
            {
                _fillGO.SetActive(false);
            }

            // ----- Build rim forward arc line -----
            if (showRimLine)
            {
                _rimCount = 0;
                Vector2 strikeN = strikeDir.normalized;
                for (int i = 0; i < segments; i++)
                {
                    Vector2 dir = _dirs[i];
                    float dot = Vector2.Dot(dir, strikeN);
                    if (dot < rimArcWidth) continue; // outside forward arc

                    // Use the same clipped radius we computed for fill.
                    // If fill is disabled, recompute quickly here.
                    float r;
                    if (showFill)
                    {
                        Vector3 v = _verts[1 + i];
                        r = new Vector2(v.x, v.y).magnitude; // local-space radius already clipped
                    }
                    else
                    {
                        // quick clip
                        float step = radius / Mathf.Max(1, radialSamples);
                        r = 0f; bool hitAir = false;
                        for (int s = 1; s <= radialSamples; s++)
                        {
                            float testR = s * step;
                            Vector2 testPos = center + dir * testR;
                            int count = Physics2D.OverlapPoint(testPos, _probeFilter, _probe);
                            if (count == 0) { hitAir = true; r = Mathf.Max(0f, testR - step); break; }
                        }
                        if (!hitAir) r = radius;
                    }

                    if (r <= 0.0001f) continue;
                    Vector2 worldPos = center + dir * r;
                    if (_rimCount < _rimPos.Length) _rimPos[_rimCount++] = new Vector3(worldPos.x, worldPos.y, zOffset);
                }

                if (_rimCount >= 2)
                {
                    _lr.positionCount = _rimCount;
                    _lr.SetPositions(_rimPos);
                    _lr.startColor = _lr.endColor = rimLineColor;
                    _lr.widthMultiplier = rimLineWidth;

                    if (!_rimGO.activeSelf) _rimGO.SetActive(true);
                }
                else
                {
                    if (_rimGO.activeSelf) _rimGO.SetActive(false);
                }
            }
            else if (_rimGO.activeSelf)
            {
                _rimGO.SetActive(false);
            }
        }

        // ---------- helpers ----------

        static Material GetOrCreateDefaultFillMaterial()
        {
            if (s_DefaultFillMat != null) return s_DefaultFillMat;

            Shader sh = Shader.Find("Sprites/Default");
            if (!sh) sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (!sh)
            {
                Debug.LogWarning("[MiningPreview] No suitable shader found. Assign a material in the Inspector.");
                return null;
            }

            s_DefaultFillMat = new Material(sh) { name = "MiningPreview_AutoMat" };
            s_DefaultFillMat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            return s_DefaultFillMat;
        }

        void BuildStaticTopology()
        {
            // Center + ring
            int vCount = 1 + segments;
            _verts  = new Vector3[vCount];
            _colors = new Color[vCount];

            // Triangles as a static fan
            _tris = new int[segments * 3];
            for (int i = 0; i < segments; i++)
            {
                int t = i * 3;
                _tris[t + 0] = 0;
                _tris[t + 1] = 1 + i;
                _tris[t + 2] = 1 + ((i + 1) % segments);
            }

            // Precompute unit ring directions
            _dirs = new Vector2[segments];
            float angStep = Mathf.PI * 2f / segments;
            for (int i = 0; i < segments; i++)
            {
                float a = i * angStep;
                _dirs[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            }

            if (_mesh == null) _mesh = new Mesh { name = "MiningPreview_Mesh" };
            _mesh.Clear(false);
            _mesh.SetVertices(_verts);
            _mesh.SetColors(_colors);
            _mesh.SetTriangles(_tris, 0, true);
            _mesh.RecalculateBounds();

            if (_mf == null && _fillGO != null) _mf = _fillGO.GetComponent<MeshFilter>();
            if (_mf != null) _mf.sharedMesh = _mesh;

            // Resize rim buffer to segments
            _rimPos = new Vector3[Mathf.Max(segments, 12)];
        }
    }
}