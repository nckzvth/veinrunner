// Assets/Scripts/World/ChunkLightingRuntime.cs
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal; // only for ShadowCaster2D (optional)

namespace Game.World
{
    /// <summary>
    /// Single entry point to equip a freshly created chunk with GI blocker underlays.
    /// Call once per new chunk root (right after TerrainWorld.CreateChunk).
    /// </summary>
    public static class ChunkLightingRuntime
    {
        /// <remarks>
        /// groundLayerName  = Physics Layer name your terrain chunks sit on (e.g. "Ground")
        /// giEmittersLayerName = Physics Layer name that RC2DGI reads (its lightElementLayerMask)
        /// underlaySortingLayerName = Sorting Layer (renderer) used for blockers (e.g. "GIBlockers")
        /// underlayOrderOffset = usually a large negative like -1000 to sit deep behind
        /// blockerMaterial = your unlit black material (GI_BlockerUnderlay)
        /// mirrorParentMaterial = use parent material instead of blockerMaterial (usually FALSE)
        /// cullHiddenUnderlays = skip sprites that are fully transparent (cheap alpha scan)
        /// skipOversizeSprites = skip very large sprites (safety)
        /// maxSpriteWorldSize = world-size cap used with skipOversizeSprites
        /// </remarks>
        public static void Equip(
            GameObject chunkRoot,
            string groundLayerName,
            string giEmittersLayerName,
            string underlaySortingLayerName,
            int underlayOrderOffset,
            Material blockerMaterial,
            bool mirrorParentMaterial,
            bool cullHiddenUnderlays,
            bool skipOversizeSprites,
            float maxSpriteWorldSize
        )
        {
            if (!chunkRoot) return;

            int groundLayer = LayerMask.NameToLayer(groundLayerName);
            int giLayer     = LayerMask.NameToLayer(giEmittersLayerName);
            if (groundLayer < 0 || giLayer < 0)
            {
                Debug.LogError($"[ChunkLighting] Invalid layer names. Ground='{groundLayerName}'({groundLayer}), GI='{giEmittersLayerName}'({giLayer}).", chunkRoot);
                return;
            }

            // Find all SpriteRenderers that belong to the ground layer (this chunk’s visuals)
            var srs = _scratchSRs; srs.Clear();
            chunkRoot.GetComponentsInChildren(includeInactive: true, srs);

            for (int i = 0; i < srs.Count; i++)
            {
                var parent = srs[i];
                if (!parent || !parent.gameObject) continue;
                if (parent.gameObject.layer != groundLayer) continue;
                if (!parent.sprite) continue;

                if (skipOversizeSprites && IsOversize(parent, maxSpriteWorldSize)) continue;

                if (cullHiddenUnderlays && IsFullyTransparent(parent)) continue;

                // Ensure child underlay exists & is synced
                var child = EnsureUnderlayChild(parent.transform, giLayer);
                var childSR = child.GetComponent<SpriteRenderer>();
                if (!childSR) childSR = child.gameObject.AddComponent<SpriteRenderer>();

                // Sorting (rendering order): force to a dedicated sorting layer and deep order
                childSR.sortingLayerName = underlaySortingLayerName;
                childSR.sortingOrder     = parent.sortingOrder + underlayOrderOffset;

                // Material choice
                childSR.sharedMaterial = mirrorParentMaterial && parent.sharedMaterial
                    ? parent.sharedMaterial
                    : blockerMaterial;

                // Full sprite mirror (kept in sync every LateUpdate by the follower)
                var follower = child.GetComponent<OccluderUnderlayFollower>();
                if (!follower) follower = child.gameObject.AddComponent<OccluderUnderlayFollower>();
                follower.parentSR = parent;
                follower.matchMaterial = mirrorParentMaterial;
                follower.forceMaterial = (!mirrorParentMaterial) ? blockerMaterial : null;
                follower.matchSortingLayer = false; // we control sorting above
                follower.fixedSortingLayerName = underlaySortingLayerName;
                follower.fixedOrderOffset = underlayOrderOffset;
            }
        }

        static readonly List<SpriteRenderer> _scratchSRs = new(128);

        static bool IsOversize(SpriteRenderer sr, float maxWorld)
        {
            var b = sr.bounds;
            float w = b.size.x, h = b.size.y;
            return (w > maxWorld || h > maxWorld);
        }

        // Cheap “fully transparent?” check: look at texture alpha’s four corners + center (works for our generated atlases)
        static bool IsFullyTransparent(SpriteRenderer sr)
        {
            var sp = sr.sprite; if (!sp) return true;
            var tex = sp.texture; if (!tex) return true;
            if (!tex.isReadable) return false; // can’t inspect → assume visible

            Rect r = sp.textureRect;
            Vector2Int[] samples =
            {
                new((int)r.xMin+1, (int)r.yMin+1),
                new((int)r.xMax-1, (int)r.yMin+1),
                new((int)r.xMin+1, (int)r.yMax-1),
                new((int)r.xMax-1, (int)r.yMax-1),
                new((int)(r.center.x), (int)(r.center.y))
            };
            for (int i = 0; i < samples.Length; i++)
            {
                var c = tex.GetPixel(samples[i].x, samples[i].y);
                if (c.a > 0.02f) return false;
            }
            return true;
        }

        static Transform EnsureUnderlayChild(Transform parent, int giLayer)
        {
            const string NAME = "_GI_BlockerUnderlay";
            var t = parent.Find(NAME);
            if (!t)
            {
                var go = new GameObject(NAME);
                t = go.transform;
                t.SetParent(parent, false);
            }

            // Make sure it renders on the GI layer the RC2DGI pass reads.
            t.gameObject.layer = giLayer;

            // Push back a hair to be “behind” the parent in depth without z-fighting
            // (sorting layer already handles order; this is just a safety belt).
            var lp = t.localPosition;
            lp.x = 0f; lp.y = 0f; lp.z = 0.001f;
            t.localPosition = lp;
            t.localRotation = Quaternion.identity;
            t.localScale = Vector3.one;
            return t;
        }
    }
}