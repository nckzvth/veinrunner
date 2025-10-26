using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// Mirrors a parent SpriteRenderer so the black blocker underlay always matches
    /// the terrain (including runtime mining). Assigned by ChunkLightingRuntime.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class OccluderUnderlayFollower : MonoBehaviour
    {
        [Header("Assigned by runtime")]
        public SpriteRenderer parentSR;
        public bool           matchMaterial = false;
        public Material       forceMaterial = null;

        [Header("Sorting control")]
        public bool   matchSortingLayer = false;
        public string fixedSortingLayerName = "GIBlockers";
        public int    fixedOrderOffset = -1000;

        SpriteRenderer _sr;

        void Awake()
        {
            _sr = GetComponent<SpriteRenderer>();
            if (!_sr) _sr = gameObject.AddComponent<SpriteRenderer>();
        }

        void LateUpdate()
        {
            if (!parentSR) { if (_sr) _sr.enabled = false; return; }
            if (!_sr) _sr = GetComponent<SpriteRenderer>();

            _sr.enabled  = parentSR.enabled;
            _sr.sprite   = parentSR.sprite;
            _sr.flipX    = parentSR.flipX;
            _sr.flipY    = parentSR.flipY;
            _sr.drawMode = parentSR.drawMode;

            if (_sr.drawMode != SpriteDrawMode.Simple)
            {
                _sr.size     = parentSR.size;
                _sr.tileMode = parentSR.tileMode;
            }

            // Material policy
            if (matchMaterial && parentSR.sharedMaterial != null)
                _sr.sharedMaterial = parentSR.sharedMaterial;
            else if (forceMaterial != null)
                _sr.sharedMaterial = forceMaterial;

            // Sorting policy
            if (matchSortingLayer)
            {
                _sr.sortingLayerID = parentSR.sortingLayerID;
                _sr.sortingOrder   = parentSR.sortingOrder + fixedOrderOffset;
            }
            else
            {
                _sr.sortingLayerName = fixedSortingLayerName;
                _sr.sortingOrder     = parentSR.sortingOrder + fixedOrderOffset;
            }

            transform.localScale = Vector3.one; // keep scale sane
        }
    }
}