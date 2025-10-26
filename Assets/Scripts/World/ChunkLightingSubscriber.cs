using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// Thin bridge: subscribe to your WorldStreamer event and forward to Runtime. 
    /// If you don't have an event, you can call ChunkLightingRuntime.Equip from your 
    /// world code directly and DELETE this class.
    /// </summary>
    public sealed class ChunkLightingSubscriber : MonoBehaviour
    {
        [Header("Stream source (optional)")]
        public WorldStreamer streamer;

        [Header("Config passed to runtime")]
        public string groundLayerName = "Ground";
        public string giEmittersLayerName = "GIEmitters";
        public string underlaySortingLayerName = "GIBlockers";
        public int underlayOrderOffset = -1000;
        public Material blockerMaterial;
        public bool mirrorParentMaterial = false;
        public bool cullHiddenUnderlays = true;
        public bool skipOversizeSprites = true;
        public float maxSpriteWorldSize = 8f;

        void OnEnable()
        {
            if (streamer != null)
                streamer.OnChunkFinalized += HandleChunk;
        }

        void OnDisable()
        {
            if (streamer != null)
                streamer.OnChunkFinalized -= HandleChunk;
        }

        void HandleChunk(GameObject chunkRoot)
        {
            ChunkLightingRuntime.Equip(
                chunkRoot,
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