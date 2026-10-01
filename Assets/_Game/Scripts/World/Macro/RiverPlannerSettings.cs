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

        [Header("Tracing")]
        public float traceStep = 28f;

        [Range(6, 32)]
        public int directionSamples = 12;

        public int maxSteps = 220;

        public float minimumPreferredDrop = 0.04f;
        public float maxAllowedRise = 0.35f;
        public float turnPenalty = 0.3f;
        public float stopHeight = -2f;

        [Header("Result")]
        public int minimumPoints = 6;
        public float nominalWidth = 8f;
        public float nominalDepth = 1.5f;
    }
}
