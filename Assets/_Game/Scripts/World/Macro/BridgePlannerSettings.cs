using System;
using UnityEngine;

namespace LittleCastle.World
{
    [Serializable]
    public sealed class BridgePlannerSettings
    {
        public bool enabled = true;
        public string archetypeId = "bridge_wood_small";
        public float extraSpan = 2f;

        [Header("Fixed stone bridge sites (opt in)")]
        [Tooltip(
            "Uses the authored ENV_Bridge_Stone_A v002 site contract. " +
            "Existing profiles stay on the legacy variable-span planner " +
            "until this is explicitly enabled.")]
        public bool useFixedStoneBridgeSites;

        [Min(0.1f)]
        public float minimumCompatibleRiverWidth = 1.5f;

        [Min(0.1f)]
        public float maximumCompatibleRiverWidth =
            FixedBridgeSiteProfile.ArchOpeningWidth;

        [Range(0f, 45f)]
        [Tooltip(
            "Maximum deviation from a perpendicular road/river crossing. " +
            "The fixed site's local +Z is the road and local +X is the river.")]
        public float maximumCrossingAngleDeviation = 20f;

        [Min(0f)]
        [Tooltip(
            "Maximum rise/run measured between the two outer road approach " +
            "edges of the fixed support footprint.")]
        public float maximumApproachGrade = 0.2f;

        [Range(0f, 90f)]
        public float maximumApproachSlope = 28f;

        [Min(0f)]
        [Tooltip(
            "Maximum terrain rise/run along the river through the support " +
            "footprint. This rejects waterfall-like fixed sites.")]
        public float maximumRiverGrade = 0.2f;

        [Min(0f)]
        [Tooltip(
            "Additional world-space gap required between rotated fixed-site " +
            "support rectangles.")]
        public float fixedSiteSeparationPadding = 1f;
    }
}
