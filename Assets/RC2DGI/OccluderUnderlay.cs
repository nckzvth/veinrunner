using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Game.World
{
    /// <summary>
    /// Creates a black, unlit underlay SpriteRenderer child to block RC2DGI emission.
    /// This version avoids creating/parenting in OnValidate to prevent editor warnings.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class OccluderUnderlay : MonoBehaviour
    {
        [Header("Setup")]
        [Tooltip("Layer used by RC2DGI as Light Element Layer Mask (e.g., 'GIEmitters').")]
        public string giEmittersLayerName = "GIEmitters";

        [Tooltip("Optional: explicit black unlit material (Sprites/Default). If null, default sprite mat is used.")]
        public Material blockerMaterial; // Assign GI_BlockerUnderlay.mat if you want determinism

        [Header("Child Naming")]
        public string childName = "_GI_BlockerUnderlay";

        [Header("Sorting")]
        [Tooltip("Place underlay just beneath the parent sprite to avoid leaks.")]
        public int sortingOrderOffset = -1;

        SpriteRenderer _parentSR;
        SpriteRenderer _childSR;

        // Instance flag so delayed calls don't run after object was destroyed
        bool _pendingEditorBuild = false;

        void Awake()
        {
            // Cache parent SR early (safe)
            if (!_parentSR) _parentSR = GetComponent<SpriteRenderer>();
        }

        void OnEnable()
        {
            EnsureUnderlaySafe();   // create/parent only from here (or delayed editor callback)
            SyncNow();
        }

        void OnDisable()
        {
            _pendingEditorBuild = false;
        }

        void Reset()
        {
            // Editor Reset calls are safe to schedule work, not to create immediately
            EditorDeferEnsure();
        }

        void OnValidate()
        {
            // DO NOT create/parent here; defer to the next editor tick.
            EditorDeferEnsure();
        }

        void LateUpdate()
        {
            // Keep synced if sprite / sorting changes
            if (!_parentSR) _parentSR = GetComponent<SpriteRenderer>();
            if (_parentSR && _childSR)
            {
                if (_childSR.sprite != _parentSR.sprite ||
                    _childSR.flipX  != _parentSR.flipX  ||
                    _childSR.flipY  != _parentSR.flipY  ||
                    _childSR.sortingLayerID != _parentSR.sortingLayerID)
                {
                    SyncNow();
                }
            }
        }

        // ---------------- helpers ----------------

        void EnsureUnderlaySafe()
        {
            if (!_parentSR) _parentSR = GetComponent<SpriteRenderer>();
            if (!_parentSR) return;

            // Find or create child safely (not in OnValidate)
            var t = transform.Find(childName);
            if (!t)
            {
                var go = new GameObject(childName);
#if UNITY_EDITOR
                // Keep it editable but avoid persistent warnings while creating in editor
                if (!Application.isPlaying)
                    go.hideFlags = HideFlags.DontSaveInEditor;
#endif
                go.transform.SetParent(transform, false);
                go.transform.localPosition = Vector3.zero;
                go.transform.localRotation = Quaternion.identity;
                go.transform.localScale = Vector3.one;

                _childSR = go.AddComponent<SpriteRenderer>();
            }
            else
            {
                _childSR = t.GetComponent<SpriteRenderer>() ?? t.gameObject.AddComponent<SpriteRenderer>();
            }

            // Assign layer
            int giLayer = LayerMask.NameToLayer(giEmittersLayerName);
            if (giLayer < 0)
            {
#if UNITY_EDITOR
                Debug.LogWarning($"[OccluderUnderlay] Layer '{giEmittersLayerName}' not found. " +
                                 $"Create it and set it as RC2DGI Light Element Layer Mask.", this);
#endif
            }
            else
            {
                _childSR.gameObject.layer = giLayer;
            }

            SyncNow();
        }

        void SyncNow()
        {
            if (!_parentSR || !_childSR) return;

            _childSR.sprite = _parentSR.sprite;
            _childSR.flipX  = _parentSR.flipX;
            _childSR.flipY  = _parentSR.flipY;

            // Render as pure black; explicit unlit mat if provided
            _childSR.color = Color.black;
            if (blockerMaterial) _childSR.sharedMaterial = blockerMaterial;

            // Sort just beneath parent
            _childSR.sortingLayerID = _parentSR.sortingLayerID;
            _childSR.sortingOrder   = _parentSR.sortingOrder + sortingOrderOffset;
        }

#if UNITY_EDITOR
        void EditorDeferEnsure()
        {
            if (Application.isPlaying) return;         // runtime path uses OnEnable
            if (_pendingEditorBuild) return;           // already scheduled
            _pendingEditorBuild = true;

            EditorApplication.delayCall += () =>
            {
                // Delay to get out of OnValidate/Awake/CheckConsistency stage
                _pendingEditorBuild = false;
                if (!this) return;                     // object destroyed
                if (!enabled || !gameObject.activeInHierarchy) return;

                EnsureUnderlaySafe();
                SyncNow();
            };
        }
#endif
    }
}