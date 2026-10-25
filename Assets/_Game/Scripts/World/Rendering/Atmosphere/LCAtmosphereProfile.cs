using UnityEngine;

namespace LittleCastle.Rendering.Atmosphere
{
    /// <summary>
    /// Presentation-only authoring values for the optional Built-in atmosphere pass.
    /// The existing WorldTimeSystem and DayNightLightingController remain authoritative
    /// for time and sky. These settings never enter saves or multiplayer state.
    /// </summary>
    [CreateAssetMenu(fileName = "LC Atmosphere Profile",
        menuName = "Little Castle/Rendering/Atmosphere Profile")]
    public sealed class LCAtmosphereProfile : ScriptableObject
    {
        [Header("Distance haze (world units)")]
        [Min(0f)] public float startDistance = 18f;
        [Min(0f)] public float distanceDensity = 0.0022f;
        [Min(1f)] public float maxRayDistance = 580f;

        [Header("Ground / valley layer")]
        public float heightBase = 12f;
        [Min(0f)] public float heightDensity = 0.011f;
        [Min(0.1f)] public float heightFalloff = 23f;

        [Header("Wind-blown breakup (world-space)")]
        [Min(0.0001f)] public float noiseScale = 0.018f;
        [Range(0f, 1f)] public float noiseStrength = 0.22f;
        [Min(0f)] public float noiseSpeed = 0.5f;

        [Header("Light-scattering approximation")]
        [Range(0f, 3f)] public float sunScatter = 0.6f;
        [Range(1f, 64f)] public float forwardExponent = 16f;

        [Header("Biomes tint the EXISTING day/night fog palette")]
        public Color dayTint = Color.white;
        public Color nightTint = Color.white;

        [Header("Lightweight finishing (Gamma project; subtle values)")]
        [Range(0.5f, 1.5f)] public float saturation = 1.02f;
        [Range(0.5f, 1.5f)] public float contrast = 1.015f;

        [Header("Quality / accessibility")]
        [Range(4, 24)] public int sampleCount = 12;
        [Range(0f, 1f)] public float fogOpacity = 1f;

        private void OnValidate()
        {
            startDistance = Mathf.Max(0f, startDistance);
            distanceDensity = Mathf.Max(0f, distanceDensity);
            maxRayDistance = Mathf.Max(startDistance + 1f, maxRayDistance);
            heightDensity = Mathf.Max(0f, heightDensity);
            heightFalloff = Mathf.Max(0.1f, heightFalloff);
            noiseScale = Mathf.Max(0.0001f, noiseScale);
            noiseSpeed = Mathf.Max(0f, noiseSpeed);
            sampleCount = Mathf.Clamp(sampleCount, 4, 24);
        }
    }
}
