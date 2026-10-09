using System;
using UnityEngine;

namespace LittleCastle.World
{
    [Serializable]
    public sealed class RiverPlannerSettings
    {
        public bool enabled = true;

        [Header("Sources")]
        public float sourceSpacing = 1400f;

        [Range(0f, 1f)]
        public float sourceChance = 0.45f;

        public TerrainClassMask sourceTerrain =
            TerrainClassMask.Highlands |
            TerrainClassMask.RollingHills;

        public float minSourceHeight = 16f;

        [Range(0f, 90f)]
        public float maxSourceSlope = 32f;

        [Header("Zero-river natural source fallback")]
        [Tooltip(
            "When enabled, a bounded deterministic source pass runs only " +
            "when the ordinary source pass leaves the plan with no rivers. " +
            "Disabled preserves the legacy source order and output.")]
        public bool useNaturalSourceFallback = false;

        [Min(100f)]
        public float fallbackSourceSpacing = 320f;

        [Range(1, 32)]
        public int fallbackMaximumAttempts = 24;

        public TerrainClassMask fallbackSourceTerrain =
            TerrainClassMask.Plains |
            TerrainClassMask.RollingHills;

        public float fallbackMinSourceHeight = 1f;

        [Min(0.0001f)]
        [Tooltip(
            "Minimum source-to-terminal height loss required before a " +
            "fallback trace is accepted as a natural downhill river.")]
        public float fallbackMinimumNetDrop = 0.5f;

        [Header("Tracing")]
        public float traceStep = 28f;

        [Range(6, 32)]
        public int directionSamples = 12;

        public int maxSteps = 220;

        public float minimumPreferredDrop = 0.04f;
        public float maxAllowedRise = 0.35f;
        public float turnPenalty = 0.3f;
        public float stopHeight = -2f;

        [Header("Confluences")]
        [Tooltip(
            "A tracing river joins an already generated river when it comes " +
            "within this distance of that river's centerline.")]
        [Min(0f)]
        public float mergeDistance = 20f;

        [Tooltip(
            "Prevents a source from immediately attaching to a nearby river " +
            "before it has formed a visible tributary.")]
        [Min(0)]
        public int minimumStepsBeforeMerge = 3;

        [Header("Result")]
        public int minimumPoints = 6;

        [Tooltip(
            "Reference width used by the profile builder. Actual local width " +
            "can be smaller upstream and larger downstream.")]
        public float nominalWidth = 8f;

        [Tooltip(
            "Reference depth used by the profile builder.")]
        public float nominalDepth = 1.5f;

        [Header("Downstream width profile")]
        [Min(0.05f)]
        public float headwaterWidthMultiplier = 0.45f;

        [Min(0.05f)]
        public float downstreamWidthMultiplier = 1.15f;

        [Range(0f, 1f)]
        public float flowWidthExponent = 0.35f;

        [Header("Downstream depth profile")]
        [Min(0.05f)]
        public float headwaterDepthMultiplier = 0.65f;

        [Min(0.05f)]
        public float downstreamDepthMultiplier = 1.15f;

        [Range(0f, 1f)]
        public float flowDepthExponent = 0.2f;

        [Header("Relative flow")]
        [Min(0.01f)]
        public float baseSourceFlow = 1f;

        [Tooltip(
            "Natural flow gain from source to mouth before tributaries are " +
            "added. This is a generation weight, not a physical flow unit.")]
        [Min(0f)]
        public float downstreamBaseFlowGain = 0.75f;
    }
}
