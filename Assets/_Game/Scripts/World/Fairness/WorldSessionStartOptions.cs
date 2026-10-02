using System;
using UnityEngine;

namespace LittleCastle.World
{
    /// <summary>
    /// Safe host-facing start-generation controls stored with session settings.
    ///
    /// These are intentionally higher-level than developer tuning values in
    /// WorldStartFairnessSettings.
    /// </summary>
    [Serializable]
    public sealed class WorldSessionStartOptions
    {
        [Header("World information")]
        [Tooltip(
            "FullMapLive lets clients receive current world-state information " +
            "for the whole map while still rendering only nearby chunks. " +
            "FogOfWarLastKnown keeps explored areas at their last known state.")]
        public WorldInformationMode informationMode =
            WorldInformationMode.FullMapLive;

        [Header("Layout")]
        public WorldStartPlacementMode placementMode =
            WorldStartPlacementMode.RandomScattered;

        [Header("Randomness / fairness")]
        public WorldStartFairnessMode fairnessMode =
            WorldStartFairnessMode.Light;

        [Tooltip(
            "0 = starts closely follow the selected formation, " +
            "1 = terrain/resources may move them farther from ideal targets.")]
        [Range(0f, 1f)]
        public float layoutFreedom = 0.65f;

        [Tooltip(
            "How strongly resource quality is allowed to compensate players " +
            "who start in more pressured/interior positions.")]
        [Range(0f, 1f)]
        public float pressureResourceCompensation = 0.55f;

        [Tooltip(
            "Additional spacing preference. 1 = designer default.")]
        [Range(0.5f, 1.5f)]
        public float separationMultiplier = 1f;

        [Header("Local exits")]
        [Tooltip(
            "0 = automatic by map/player count. Otherwise requires at least " +
            "this many independent local exit corridors.")]
        [Range(0, 8)]
        public int minimumExitRoutesOverride = 0;

        [Tooltip(
            "When enabled, nearby river barriers count as blocked exits unless " +
            "a generated bridge provides a usable crossing.")]
        public bool riversRequireBridgeForExit = true;

        [Header("Formation")]
        [Tooltip(
            "Radius of polygon/ring layouts as a fraction of half-map size.")]
        [Range(0.2f, 0.95f)]
        public float ringRadius = 0.72f;

        [Tooltip(
            "Outer radius for RadialStar.")]
        [Range(0.2f, 0.95f)]
        public float starOuterRadius = 0.78f;

        [Tooltip(
            "Inner radius for RadialStar.")]
        [Range(0.05f, 0.8f)]
        public float starInnerRadius = 0.42f;

        [Tooltip(
            "Seeded rotation prevents the same formation orientation every match.")]
        public bool randomizeFormationRotation = true;

        public WorldSessionStartOptions Clone()
        {
            return new WorldSessionStartOptions
            {
                informationMode = informationMode,
                placementMode = placementMode,
                fairnessMode = fairnessMode,
                layoutFreedom = layoutFreedom,
                pressureResourceCompensation = pressureResourceCompensation,
                separationMultiplier = separationMultiplier,
                minimumExitRoutesOverride = minimumExitRoutesOverride,
                riversRequireBridgeForExit = riversRequireBridgeForExit,
                ringRadius = ringRadius,
                starOuterRadius = starOuterRadius,
                starInnerRadius = starInnerRadius,
                randomizeFormationRotation = randomizeFormationRotation
            };
        }
    }
}
