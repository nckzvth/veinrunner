// Namespace aligned to your map: /FX
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace FX
{
    /// <summary>
    /// Configures a Light2D to be "shadows-only" so RC2DGI handles bounce while
    /// this light contributes silhouettes/occlusion without adding direct light.
    /// Unity 6.2 / URP 17 compatible.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class TorchShadowLight : MonoBehaviour
    {
        [Header("Hookup")]
        public Light2D light2D;

        [Header("Renderer 2D Blend Style")]
        [Tooltip("Index of the Multiply (or similar) style in your Renderer 2D Data (0..3).")]
        public int blendStyleIndex = 0;

        [Header("Shadow Settings")]
        public bool castShadows = true;
        [Range(0f, 1f)] public float shadowIntensity = 1.0f;
        [Range(0f, 2f)] public float shadowSoftness  = 1.0f;
        [Range(0f, 1f)] public float shadowVolumeIntensity = 0.0f;

        [Header("No Direct Light")]
        [Range(0f, 2f)] public float intensity = 0.0f; // keep 0 for shadows-only

        [Header("Falloff (Point Light)")]
        [Range(0f, 1f)] public float innerRadius = 0.2f;
        [Range(0f, 5f)] public float outerRadius = 1.0f;

        void Reset()
        {
            light2D = GetComponentInChildren<Light2D>();
            Apply();
        }

        void OnEnable()   => Apply();
        void OnValidate() => Apply();

        void Apply()
        {
            if (!light2D) light2D = GetComponentInChildren<Light2D>();
            if (!light2D) return;

            light2D.lightType = Light2D.LightType.Point;
            light2D.blendStyleIndex = Mathf.Clamp(blendStyleIndex, 0, 3);

            // "Shadows-only": zero direct intensity; shadows enabled.
            light2D.intensity = intensity;
            light2D.shadowsEnabled = castShadows;
            light2D.shadowIntensity = shadowIntensity;
            light2D.shadowSoftness  = shadowSoftness;
            light2D.shadowVolumeIntensity = shadowVolumeIntensity;

            // Point light falloff
            light2D.pointLightInnerRadius = innerRadius;
            light2D.pointLightOuterRadius = Mathf.Max(outerRadius, innerRadius + 0.001f);
        }
    }
}