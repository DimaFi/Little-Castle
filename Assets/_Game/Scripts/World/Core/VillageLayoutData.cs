using System;
using System.Collections.Generic;
using UnityEngine;

namespace LittleCastle.World
{
    /// <summary>
    /// Serializable planning inputs in world metres. Values are owned by
    /// the root world-generation profile, not by a presentation prefab.
    /// </summary>
    [Serializable]
    public struct VillageLayoutSettings
    {
        public int houseCount;
        public BuildingFootprintDefinition houseFootprint;
        public float wellRadius;
        public float courtyardRadius;
        public float minimumBuildingClearance;
        public float approachHalfWidth;
        public float pathSampleInterval;
        public float maximumPathStep;
        public float maximumCourtyardSlope;

        public bool IsValid =>
            houseCount >= 3 && houseCount <= 5 &&
            houseFootprint.IsValid &&
            Finite(wellRadius) && wellRadius > 0f &&
            Finite(courtyardRadius) &&
            courtyardRadius > wellRadius + 0.1f &&
            Finite(minimumBuildingClearance) &&
            minimumBuildingClearance >= 0f &&
            Finite(approachHalfWidth) && approachHalfWidth > 0f &&
            approachHalfWidth <= houseFootprint.entranceWidth * 0.5f &&
            Finite(pathSampleInterval) &&
            pathSampleInterval >= 0.25f &&
            pathSampleInterval <= 3f &&
            Finite(maximumPathStep) && maximumPathStep >= 0f &&
            Finite(maximumCourtyardSlope) &&
            maximumCourtyardSlope >= 0f &&
            maximumCourtyardSlope <= 90f &&
            courtyardRadius >
                wellRadius + approachHalfWidth +
                minimumBuildingClearance;

        private static bool Finite(float x) =>
            !float.IsNaN(x) && !float.IsInfinity(x);
    }

    [Serializable]
    public struct VillageHousePlacement
    {
        public long stableId;
        public int slot;
        public Vector2 worldCenter;
        public float yawDegrees;
        public float minimumFoundationHeight;

        public VillageHousePlacement(
            long stableId,
            int slot,
            Vector2 worldCenter,
            float yawDegrees,
            float minimumFoundationHeight)
        {
            this.stableId = stableId;
            this.slot = slot;
            this.worldCenter = worldCenter;
            this.yawDegrees = yawDegrees;
            this.minimumFoundationHeight = minimumFoundationHeight;
        }
    }

    [Serializable]
    public struct VillageApproachPath
    {
        public long stableId;
        public long houseStableId;
        public Vector2 doorEdgeWorld;
        public Vector2 courtyardEdgeWorld;
        public float halfWidth;

        public VillageApproachPath(
            long stableId,
            long houseStableId,
            Vector2 doorEdgeWorld,
            Vector2 courtyardEdgeWorld,
            float halfWidth)
        {
            this.stableId = stableId;
            this.houseStableId = houseStableId;
            this.doorEdgeWorld = doorEdgeWorld;
            this.courtyardEdgeWorld = courtyardEdgeWorld;
            this.halfWidth = halfWidth;
        }
    }

    /// <summary>
    /// Only constructed/published if all 3–5 houses, center well, courtyard
    /// and local approach paths are validated together. Not a GameObject.
    /// Stable village ID is preserved from the authoritative macro feature.
    /// </summary>
    public sealed class VillageLayoutData
    {
        private readonly List<VillageHousePlacement> houses =
            new List<VillageHousePlacement>(5);

        private readonly List<VillageApproachPath> paths =
            new List<VillageApproachPath>(5);

        public int WorldSeed { get; }
        public long VillageStableId { get; }
        public Vector2 Center { get; }
        public Vector2 WellPosition => Center;
        public long WellStableId { get; }
        public float WellHeight { get; }
        public float CourtyardRadius { get; }

        public IReadOnlyList<VillageHousePlacement> Houses => houses;
        public IReadOnlyList<VillageApproachPath> Approaches => paths;

        internal VillageLayoutData(
            int worldSeed,
            long villageStableId,
            Vector2 center,
            long wellStableId,
            float wellHeight,
            float courtyardRadius)
        {
            WorldSeed = worldSeed;
            VillageStableId = villageStableId;
            Center = center;
            WellStableId = wellStableId;
            WellHeight = wellHeight;
            CourtyardRadius = courtyardRadius;
        }

        internal void Add(
            VillageHousePlacement house,
            VillageApproachPath approach)
        {
            houses.Add(house);
            paths.Add(approach);
        }
    }

    public enum VillageLayoutRejection : byte
    {
        None,
        InvalidRequest,
        WellOrCourtyardBlocked,
        UnsupportedHouseFootprint,
        HouseOverlapsBuilding,
        HouseOverlapsApproach,
        ApproachBlocked,
        ExhaustedAlternatives
    }

    public readonly struct VillageLayoutFailure
    {
        public readonly VillageLayoutRejection reason;
        public readonly BuildingFootprintRejection footprintRejection;
        public readonly int failedSlot;
        public readonly int candidatesAttempted;
        public readonly Vector2 rejectedAt;

        public VillageLayoutFailure(
            VillageLayoutRejection reason,
            BuildingFootprintRejection footprintRejection,
            int failedSlot,
            int candidatesAttempted,
            Vector2 rejectedAt)
        {
            this.reason = reason;
            this.footprintRejection = footprintRejection;
            this.failedSlot = failedSlot;
            this.candidatesAttempted = candidatesAttempted;
            this.rejectedAt = rejectedAt;
        }
    }
}
