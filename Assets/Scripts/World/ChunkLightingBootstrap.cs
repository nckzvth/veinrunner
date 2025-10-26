using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// Thin, per-chunk configurator that forwards to ChunkLightingRuntime.Equip(...).
    /// Keep this on a chunk prefab or add at runtime, then call EquipForLighting().
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ChunkLightingBootstrap : MonoBehaviour
    {
        [Header("Match your scene layers")]                // Physics layers
        public string groundLayerName = "Ground";
        public string giEmittersLayerName = "GIEmitters";

        [Header("Underlay render placement")]              // Sorting layers
        public string underlaySortingLayerName = "GIBlockers";
        public int underlayOrderOffset = -1000;

        [Header("Underlay appearance")]
        public bool    mirrorParentMaterial = false;
        public Material blockerMaterial = null;            // Assign GI_BlockerUnderlay (unlit black)

        [Header("Behavior")]
        public bool  cullHiddenUnderlays = true;
        public bool  skipOversizeSprites = true;
        public float maxSpriteWorldSize  = 8f;

        /// <summary>Optional one-shot config if you add this component from code.</summary>
        public void Configure(
            string groundLayerName,
            string giEmittersLayerName,
            string underlaySortingLayerName,
            int    underlayOrderOffset,
            Material blockerMaterial,
            bool   mirrorParentMaterial,
            bool   cullHiddenUnderlays,
            bool   skipOversizeSprites,
            float  maxSpriteWorldSize)
        {
            this.groundLayerName          = groundLayerName;
            this.giEmittersLayerName      = giEmittersLayerName;
            this.underlaySortingLayerName = underlaySortingLayerName;
            this.underlayOrderOffset      = underlayOrderOffset;
            this.blockerMaterial          = blockerMaterial;
            this.mirrorParentMaterial     = mirrorParentMaterial;
            this.cullHiddenUnderlays      = cullHiddenUnderlays;
            this.skipOversizeSprites      = skipOversizeSprites;
            this.maxSpriteWorldSize       = Mathf.Max(0.01f, maxSpriteWorldSize);
        }

        /// <summary>Call once after the chunk’s sprites exist.</summary>
        public void EquipForLighting()
        {
            ChunkLightingRuntime.Equip(
                gameObject,
                groundLayerName,
                giEmittersLayerName,
                underlaySortingLayerName,
                underlayOrderOffset,
                blockerMaterial,
                mirrorParentMaterial,
                cullHiddenUnderlays,
                skipOversizeSprites,
                maxSpriteWorldSize
            );
        }
    }
}