using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Game.World
{
    /// <summary>
    /// Helper to configure ShadowCaster2D safely in URP 17+.
    /// </summary>
    public static class ShadowCasterAuto2D
    {
        /// <summary>Apply common settings for terrain sprites.</summary>
        public static void Configure(ShadowCaster2D caster, bool useRendererSilhouette, bool selfShadows)
        {
            if (!caster) return;

            // Public API in URP 17
            caster.selfShadows = selfShadows;

            // We cannot directly set the silhouette flag at runtime across all URP versions.
            // Toggling enabled forces a refresh from the SpriteRenderer geometry.
            if (useRendererSilhouette)
            {
                bool wasEnabled = caster.enabled;
                caster.enabled = false;
                caster.enabled = wasEnabled || true;
            }
        }
    }
}