using System;
using UnityEngine;

namespace LittleCastle.World.Weather
{
    public enum RainMode { Off, Drizzle, Rain, Downpour, Storm }
    public enum RainQuality { Off, Low, Medium, High, Custom }

    [Serializable]
    public struct RainBudget
    {
        public int nearParticles, distantParticles, probeCount, puddleCount, splashCount;
        public static RainBudget For(RainQuality quality, RainBudget custom)
        {
            switch (quality)
            {
                case RainQuality.Low: return new RainBudget { nearParticles = 220, distantParticles = 220, probeCount = 3, puddleCount = 5, splashCount = 4 };
                case RainQuality.Medium: return new RainBudget { nearParticles = 650, distantParticles = 850, probeCount = 6, puddleCount = 12, splashCount = 8 };
                case RainQuality.High: return new RainBudget { nearParticles = 1100, distantParticles = 1700, probeCount = 10, puddleCount = 22, splashCount = 12 };
                case RainQuality.Custom: return new RainBudget
                {
                    nearParticles = Mathf.Clamp(custom.nearParticles, 0, 4000),
                    distantParticles = Mathf.Clamp(custom.distantParticles, 0, 6000),
                    probeCount = Mathf.Clamp(custom.probeCount, 0, 24),
                    puddleCount = Mathf.Clamp(custom.puddleCount, 0, 64),
                    splashCount = Mathf.Clamp(custom.splashCount, 0, 40)
                };
                default: return default;
            }
        }
    }

    [CreateAssetMenu(menuName = "Little Castle/Weather/Cozy Rain and Storm", fileName = "CozyRainProfile")]
    public sealed class RainStormProfile : ScriptableObject
    {
        [Header("Transitions (real seconds)")]
        [Min(0.1f)] public float rainBlendSeconds = 8f;
        [Min(0.1f)] public float wettingSeconds = 40f;
        [Min(0.1f)] public float dryingSeconds = 160f;

        [Header("Rain")]
        [Range(8f, 60f)] public float nearRadius = 22f;
        [Range(20f, 180f)] public float distantRadius = 65f;
        [Range(12f, 80f)] public float spawnHeight = 30f;
        [Range(4f, 35f)] public float fallSpeed = 22f;
        [Range(0.01f, 0.15f)] public float streakWidth = 0.035f;
        [Range(0.1f, 2f)] public float streakLength = 0.65f;
        [Range(0f, 3f)] public float windInfluence = 0.55f;
        public Color rainTint = new Color(0.68f, 0.78f, 0.88f, 0.55f);
        [Min(1)] public int seed = 90317;

        [Header("Surface sampling: manually choose solid ground and optionally water layers")]
        public LayerMask groundLayers = ~0;
        public LayerMask waterLayers = 0;
        [Tooltip("Assign only roof / bridge / shelter physics layers. Zero disables roof tests; no per-drop raycasts.")]
        public LayerMask roofLayers = 0;
        public bool useMapHeightFallback = true;
        [Range(0.1f, 0.85f)] public float maximumGroundSlope = 0.27f;
        [Range(0.3f, 4f)] public float puddleRadius = 1.25f;
        [Range(0.05f, 2f)] public float rippleSpeed = 0.65f;
        [Range(0f, 0.8f)] public float wetReflection = 0.36f;
        [Range(0f, 1f)] public float splashIntensity = 0.65f;

        [Header("Atmosphere (uses existing DayNightLightingController)")]
        [Range(0f, 1f)] public float cloudiness = 0.95f;
        [Range(0f, 1f)] public float precipitationForAtmosphere = 0.9f;
        [Tooltip("For a complete current AtmosphereProfile this changes clouds and fog; without it the existing controller remains authoritative.")]
        public bool driveExistingAtmosphere = true;

        [Header("Thunder; optional licensed audio assigned in Inspector")]
        public bool enableLightning = true;
        [Min(4f)] public float minLightningInterval = 16f;
        [Min(5f)] public float maxLightningInterval = 42f;
        [Range(0f, 1f)] public float lightningFlash = 0.5f;
        public bool reducedFlashes;
        public bool enableThunder = true;
        [Range(0f, 1f)] public float thunderVolume = 0.55f;
        public AudioClip distantThunder;
        public AudioClip closeThunder;
        [Header("Optional ambient rain audio (supply a licensed loop)")]
        public AudioClip rainAmbience;
        [Range(0f, 1f)] public float rainVolume = 0.35f;

        [Header("Optional lens / later integration")]
        public bool allowLensRain;
        [Tooltip("Lens effect requires a camera-space adapter; disabled in this initial working module.")]
        [Range(0f, 1f)] public float lensStrength = 0.15f;

        [Header("Quality custom budgets (clamped at runtime)")]
        public RainBudget customBudget = new RainBudget { nearParticles = 1100, distantParticles = 1700, probeCount = 10, puddleCount = 22, splashCount = 12 };

        private void OnValidate()
        {
            maxLightningInterval = Mathf.Max(minLightningInterval + 1f, maxLightningInterval);
        }
    }
}
