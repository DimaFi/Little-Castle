using System.Collections.Generic;
using UnityEngine;

namespace LittleCastle.World
{
    /// <summary>
    /// Finds geometric road/river crossings and records bridge candidates.
    /// </summary>
    public static class BridgeSitePlanner
    {
        private struct FixedCandidate
        {
            public long stableId;
            public long roadId;
            public long riverId;
            public Vector2 worldPosition;
            public float yawDegrees;
            public float baseElevation;
            public float angleDeviation;
            public float approachGrade;
        }

        private sealed class FixedRoadCandidates
        {
            public long roadId;
            public bool hasUnservedCrossing;
            public readonly List<FixedCandidate> sites =
                new List<FixedCandidate>();
        }

        public static void BuildBridgeSites(
            int worldSeed,
            MacroWorldPlan plan,
            BridgePlannerSettings settings)
        {
            BuildBridgeSites(
                worldSeed,
                plan,
                settings,
                null);
        }

        /// <summary>
        /// Terrain-aware overload used by the opt-in fixed site planner.
        /// The legacy planner deliberately does not depend on the probe.
        /// </summary>
        public static void BuildBridgeSites(
            int worldSeed,
            MacroWorldPlan plan,
            BridgePlannerSettings settings,
            WorldTerrainProbe terrainProbe)
        {
            if (plan == null ||
                settings == null ||
                !settings.enabled)
            {
                return;
            }

            if (!settings.useFixedStoneBridgeSites)
            {
                BuildLegacyBridgeSites(
                    worldSeed,
                    plan,
                    settings);

                return;
            }

            if (terrainProbe == null)
            {
                RemoveCrossingRoadsWithoutTerrainProbe(plan);
                return;
            }

            BuildFixedBridgeSites(
                worldSeed,
                plan,
                settings,
                terrainProbe);
        }

        private static void RemoveCrossingRoadsWithoutTerrainProbe(
            MacroWorldPlan plan)
        {
            var rejectedRoadIds = new List<long>();

            for (int r = 0; r < plan.Roads.Count; r++)
            {
                WorldRoadData road = plan.Roads[r];

                if (road == null ||
                    road.centerline == null)
                {
                    continue;
                }

                bool foundCrossing = false;

                for (int w = 0;
                     w < plan.Rivers.Count && !foundCrossing;
                     w++)
                {
                    WorldRiverData river = plan.Rivers[w];

                    if (river == null ||
                        river.centerline == null)
                    {
                        continue;
                    }

                    for (int rs = 0;
                         rs < road.centerline.Count - 1 && !foundCrossing;
                         rs++)
                    {
                        for (int ws = 0;
                             ws < river.centerline.Count - 1;
                             ws++)
                        {
                            if (SegmentsOverlapCollinearly(
                                road.centerline[rs],
                                road.centerline[rs + 1],
                                river.centerline[ws],
                                river.centerline[ws + 1]) ||
                                TrySegmentIntersection(
                                road.centerline[rs],
                                road.centerline[rs + 1],
                                river.centerline[ws],
                                river.centerline[ws + 1],
                                out _,
                                out _,
                                out _))
                            {
                                foundCrossing = true;
                                break;
                            }
                        }
                    }
                }

                if (foundCrossing)
                    rejectedRoadIds.Add(road.stableId);
            }

            for (int i = 0; i < rejectedRoadIds.Count; i++)
                plan.RemoveRoadAndConnection(rejectedRoadIds[i]);
        }

        private static void BuildLegacyBridgeSites(
            int worldSeed,
            MacroWorldPlan plan,
            BridgePlannerSettings settings)
        {
            int salt =
                DeterministicHash.String32(
                    "bridge_site");

            for (int r = 0; r < plan.Roads.Count; r++)
            {
                WorldRoadData road = plan.Roads[r];

                if (road == null ||
                    road.centerline.Count < 2)
                {
                    continue;
                }

                for (int w = 0; w < plan.Rivers.Count; w++)
                {
                    WorldRiverData river = plan.Rivers[w];

                    if (river == null ||
                        river.centerline.Count < 2)
                    {
                        continue;
                    }

                    for (int rs = 0;
                         rs < road.centerline.Count - 1;
                         rs++)
                    {
                        Vector2 roadA = road.centerline[rs];
                        Vector2 roadB = road.centerline[rs + 1];

                        for (int ws = 0;
                             ws < river.centerline.Count - 1;
                             ws++)
                        {
                            Vector2 riverA = river.centerline[ws];
                            Vector2 riverB = river.centerline[ws + 1];

                            if (!TrySegmentIntersection(
                                roadA,
                                roadB,
                                riverA,
                                riverB,
                                out Vector2 intersection,
                                out float roadT,
                                out float riverT))
                            {
                                continue;
                            }

                            long bridgeId =
                                GetBridgeId(
                                    worldSeed,
                                    road.stableId,
                                    river.stableId,
                                    rs,
                                    ws,
                                    salt);

                            Vector2 roadDirection =
                                (roadB - roadA).normalized;

                            float yaw =
                                Mathf.Atan2(
                                    roadDirection.x,
                                    roadDirection.y) *
                                Mathf.Rad2Deg;

                            plan.AddBridgeSite(
                                new WorldBridgeSiteData(
                                    bridgeId,
                                    road.stableId,
                                    river.stableId,
                                    settings.archetypeId,
                                    intersection,
                                    yaw,
                                    Mathf.Max(
                                        0.5f,
                                        river.GetWidthAtSegment(
                                            ws,
                                            riverT) +
                                        settings.extraSpan)));
                        }
                    }
                }
            }
        }

        private static void BuildFixedBridgeSites(
            int worldSeed,
            MacroWorldPlan plan,
            BridgePlannerSettings settings,
            WorldTerrainProbe terrainProbe)
        {
            var roadGroups = new List<FixedRoadCandidates>();
            var rejectedRoadIds = new List<long>();
            int salt =
                DeterministicHash.String32(
                    "bridge_site");

            float minimumWidth =
                Mathf.Clamp(
                    settings.minimumCompatibleRiverWidth,
                    0.1f,
                    FixedBridgeSiteProfile.ArchOpeningWidth);

            float maximumWidth =
                Mathf.Clamp(
                    settings.maximumCompatibleRiverWidth,
                    minimumWidth,
                    FixedBridgeSiteProfile.ArchOpeningWidth);

            for (int r = 0; r < plan.Roads.Count; r++)
            {
                WorldRoadData road = plan.Roads[r];

                if (road == null ||
                    road.centerline == null ||
                    road.centerline.Count < 2)
                {
                    continue;
                }

                var roadGroup =
                    new FixedRoadCandidates
                    {
                        roadId = road.stableId
                    };

                for (int w = 0; w < plan.Rivers.Count; w++)
                {
                    WorldRiverData river = plan.Rivers[w];

                    if (river == null ||
                        river.centerline == null ||
                        river.centerline.Count < 2)
                    {
                        continue;
                    }

                    for (int rs = 0;
                         rs < road.centerline.Count - 1;
                         rs++)
                    {
                        Vector2 roadA = road.centerline[rs];
                        Vector2 roadB = road.centerline[rs + 1];
                        Vector2 roadVector = roadB - roadA;

                        if (roadVector.sqrMagnitude <= 0.000001f)
                            continue;

                        Vector2 roadDirection = roadVector.normalized;

                        for (int ws = 0;
                             ws < river.centerline.Count - 1;
                             ws++)
                        {
                            Vector2 riverA = river.centerline[ws];
                            Vector2 riverB = river.centerline[ws + 1];
                            Vector2 riverVector = riverB - riverA;

                            if (riverVector.sqrMagnitude <= 0.000001f)
                                continue;

                            // A road sharing the river centerline cannot be
                            // served by a transverse fixed bridge.
                            if (SegmentsOverlapCollinearly(
                                roadA, roadB, riverA, riverB))
                            {
                                roadGroup.hasUnservedCrossing = true;
                                continue;
                            }

                            if (!TrySegmentIntersection(
                                    roadA,
                                    roadB,
                                    riverA,
                                    riverB,
                                    out Vector2 intersection,
                                    out float roadT,
                                    out float riverT))
                            {
                                continue;
                            }

                            float localWidth =
                                river.GetWidthAtSegment(
                                    ws,
                                    riverT);

                            // Keep each incident segment at a corner subject
                            // to the full geometry check. One valid segment
                            // cannot excuse an invalid sharp bend.
                            if (localWidth < minimumWidth ||
                                localWidth > maximumWidth)
                            {
                                roadGroup.hasUnservedCrossing = true;
                                continue;
                            }

                            Vector2 riverDirection = riverVector.normalized;
                            float crossingAngle =
                                Vector2.Angle(
                                    roadDirection,
                                    riverDirection);

                            float angleDeviation =
                                Mathf.Abs(90f - crossingAngle);

                            if (angleDeviation >
                                Mathf.Clamp(
                                    settings.maximumCrossingAngleDeviation,
                                    0f,
                                    45f))
                            {
                                roadGroup.hasUnservedCrossing = true;
                                continue;
                            }

                            float yaw =
                                Mathf.Atan2(
                                    roadDirection.x,
                                    roadDirection.y) *
                                Mathf.Rad2Deg;

                            if (!HasCompatibleLocalGeometry(
                                road.centerline,
                                rs,
                                roadT,
                                river.centerline,
                                ws,
                                riverT,
                                intersection,
                                yaw))
                            {
                                roadGroup.hasUnservedCrossing = true;
                                continue;
                            }

                            if (!TryResolveTerrainFit(
                                intersection,
                                yaw,
                                terrainProbe,
                                settings,
                                out float baseElevation,
                                out float approachGrade))
                            {
                                roadGroup.hasUnservedCrossing = true;
                                continue;
                            }

                            AddOrReplaceDuplicate(
                                roadGroup.sites,
                                new FixedCandidate
                                {
                                    stableId =
                                        GetBridgeId(
                                            worldSeed,
                                            road.stableId,
                                            river.stableId,
                                            rs,
                                            ws,
                                            salt),
                                    roadId = road.stableId,
                                    riverId = river.stableId,
                                    worldPosition = intersection,
                                    yawDegrees = yaw,
                                    baseElevation = baseElevation,
                                    angleDeviation = angleDeviation,
                                    approachGrade = approachGrade
                                });
                        }
                    }
                }

                if (roadGroup.hasUnservedCrossing)
                {
                    rejectedRoadIds.Add(road.stableId);
                }
                else if (roadGroup.sites.Count > 0)
                {
                    roadGroup.sites.Sort(CompareCandidates);
                    roadGroups.Add(roadGroup);
                }
            }

            for (int i = 0; i < rejectedRoadIds.Count; i++)
                plan.RemoveRoadAndConnection(rejectedRoadIds[i]);

            roadGroups.Sort(CompareRoadGroups);

            for (int i = 0; i < roadGroups.Count; i++)
            {
                FixedRoadCandidates group = roadGroups[i];

                if (GroupHasOverlappingSites(
                    group,
                    plan,
                    settings.fixedSiteSeparationPadding))
                {
                    plan.RemoveRoadAndConnection(group.roadId);
                    continue;
                }

                for (int s = 0; s < group.sites.Count; s++)
                {
                    FixedCandidate candidate = group.sites[s];

                    plan.AddBridgeSite(
                        new WorldBridgeSiteData(
                            candidate.stableId,
                            candidate.roadId,
                            candidate.riverId,
                            FixedBridgeSiteProfile.AssetId,
                            candidate.worldPosition,
                            candidate.yawDegrees,
                            FixedBridgeSiteProfile.BridgeLength,
                            candidate.baseElevation,
                            FixedBridgeSiteProfile.ContractVersion,
                            true));
                }
            }
        }

        private static bool HasCompatibleLocalGeometry(
            IReadOnlyList<Vector2> road,
            int roadSegment,
            float roadT,
            IReadOnlyList<Vector2> river,
            int riverSegment,
            float riverT,
            Vector2 intersection,
            float yawDegrees)
        {
            if (!TrySamplePolylineOffset(
                    road,
                    roadSegment,
                    roadT,
                    -FixedBridgeSiteProfile.HalfLength,
                    out Vector2 roadBack) ||
                !TrySamplePolylineOffset(
                    road,
                    roadSegment,
                    roadT,
                    FixedBridgeSiteProfile.HalfLength,
                    out Vector2 roadForward) ||
                !TrySamplePolylineOffset(
                    river,
                    riverSegment,
                    riverT,
                    -FixedBridgeSiteProfile.SupportHalfExtentX,
                    out Vector2 riverBack) ||
                !TrySamplePolylineOffset(
                    river,
                    riverSegment,
                    riverT,
                    FixedBridgeSiteProfile.SupportHalfExtentX,
                    out Vector2 riverForward))
            {
                return false;
            }

            Vector2 localRoadBack =
                FixedBridgeSiteProfile.WorldToLocal(
                    roadBack,
                    intersection,
                    yawDegrees);

            Vector2 localRoadForward =
                FixedBridgeSiteProfile.WorldToLocal(
                    roadForward,
                    intersection,
                    yawDegrees);

            float maximumRoadOffset =
                FixedBridgeSiteProfile.ClearPathWidth *
                0.5f;

            if (Mathf.Abs(localRoadBack.x) > maximumRoadOffset ||
                Mathf.Abs(localRoadForward.x) > maximumRoadOffset ||
                localRoadBack.y >
                    -FixedBridgeSiteProfile.HalfLength * 0.9f ||
                localRoadForward.y <
                    FixedBridgeSiteProfile.HalfLength * 0.9f)
            {
                return false;
            }

            Vector2 localRiverBack =
                FixedBridgeSiteProfile.WorldToLocal(
                    riverBack,
                    intersection,
                    yawDegrees);

            Vector2 localRiverForward =
                FixedBridgeSiteProfile.WorldToLocal(
                    riverForward,
                    intersection,
                    yawDegrees);

            bool oppositeSides =
                localRiverBack.x *
                localRiverForward.x < 0f;

            return
                oppositeSides &&
                Mathf.Abs(localRiverBack.x) >=
                    FixedBridgeSiteProfile.ProtectedCoreHalfExtentX &&
                Mathf.Abs(localRiverForward.x) >=
                    FixedBridgeSiteProfile.ProtectedCoreHalfExtentX &&
                Mathf.Abs(localRiverBack.y) <=
                    FixedBridgeSiteProfile.ChannelHalfWidth(
                        localRiverBack.x) &&
                Mathf.Abs(localRiverForward.y) <=
                    FixedBridgeSiteProfile.ChannelHalfWidth(
                        localRiverForward.x);
        }

        private static bool TrySamplePolylineOffset(
            IReadOnlyList<Vector2> points,
            int segmentIndex,
            float segmentT,
            float signedDistance,
            out Vector2 sample)
        {
            sample = default(Vector2);

            if (points == null ||
                points.Count < 2 ||
                segmentIndex < 0 ||
                segmentIndex >= points.Count - 1)
            {
                return false;
            }

            int direction = signedDistance < 0f ? -1 : 1;
            float remaining = Mathf.Abs(signedDistance);
            int segment = segmentIndex;
            float t = Mathf.Clamp01(segmentT);

            while (segment >= 0 &&
                   segment < points.Count - 1)
            {
                Vector2 a = points[segment];
                Vector2 b = points[segment + 1];
                float length = Vector2.Distance(a, b);

                if (length <= 0.000001f)
                {
                    segment += direction;
                    t = direction < 0 ? 1f : 0f;
                    continue;
                }

                float available =
                    direction < 0
                        ? length * t
                        : length * (1f - t);

                if (remaining <= available)
                {
                    float deltaT = remaining / length;
                    float resultT =
                        direction < 0
                            ? t - deltaT
                            : t + deltaT;

                    sample = Vector2.Lerp(a, b, resultT);
                    return true;
                }

                remaining -= available;
                segment += direction;
                t = direction < 0 ? 1f : 0f;
            }

            return remaining <= 0.0001f;
        }

        private static void AddOrReplaceDuplicate(
            List<FixedCandidate> candidates,
            FixedCandidate candidate)
        {
            const float duplicateDistanceSqr = 0.0001f;

            for (int i = 0; i < candidates.Count; i++)
            {
                FixedCandidate existing = candidates[i];

                if (existing.riverId != candidate.riverId ||
                    (existing.worldPosition -
                     candidate.worldPosition).sqrMagnitude >
                    duplicateDistanceSqr)
                {
                    continue;
                }

                if (CompareCandidates(candidate, existing) < 0)
                    candidates[i] = candidate;

                return;
            }

            candidates.Add(candidate);
        }

        private static int CompareRoadGroups(
            FixedRoadCandidates a,
            FixedRoadCandidates b)
        {
            FixedCandidate aBest = a.sites[0];
            FixedCandidate bBest = b.sites[0];
            int site = CompareCandidates(aBest, bBest);

            if (site != 0)
                return site;

            return a.roadId.CompareTo(b.roadId);
        }

        private static bool GroupHasOverlappingSites(
            FixedRoadCandidates group,
            MacroWorldPlan plan,
            float padding)
        {
            float halfPadding =
                Mathf.Max(0f, padding) *
                0.5f;

            float halfX =
                FixedBridgeSiteProfile.SupportHalfExtentX +
                halfPadding;

            float halfZ =
                FixedBridgeSiteProfile.SupportHalfExtentZ +
                halfPadding;

            for (int i = 0; i < group.sites.Count; i++)
            {
                FixedCandidate candidate = group.sites[i];

                if (OverlapsExistingSite(
                    candidate,
                    plan,
                    padding))
                {
                    return true;
                }

                for (int j = i + 1; j < group.sites.Count; j++)
                {
                    FixedCandidate other = group.sites[j];

                    if (OrientedRectanglesOverlap(
                        candidate.worldPosition,
                        candidate.yawDegrees,
                        other.worldPosition,
                        other.yawDegrees,
                        halfX,
                        halfZ))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static int CompareCandidates(
            FixedCandidate a,
            FixedCandidate b)
        {
            int angle =
                a.angleDeviation.CompareTo(
                    b.angleDeviation);

            if (angle != 0)
                return angle;

            int grade =
                a.approachGrade.CompareTo(
                    b.approachGrade);

            if (grade != 0)
                return grade;

            return a.stableId.CompareTo(b.stableId);
        }

        private static bool TryResolveTerrainFit(
            Vector2 intersection,
            float yawDegrees,
            WorldTerrainProbe terrainProbe,
            BridgePlannerSettings settings,
            out float baseElevation,
            out float approachGrade)
        {
            Vector2 south =
                FixedBridgeSiteProfile.LocalToWorld(
                    new Vector2(
                        0f,
                        -FixedBridgeSiteProfile.SupportHalfExtentZ),
                    intersection,
                    yawDegrees);

            Vector2 north =
                FixedBridgeSiteProfile.LocalToWorld(
                    new Vector2(
                        0f,
                        FixedBridgeSiteProfile.SupportHalfExtentZ),
                    intersection,
                    yawDegrees);

            WorldTerrainSample southSample = terrainProbe.Sample(south);
            WorldTerrainSample northSample = terrainProbe.Sample(north);

            baseElevation =
                (southSample.height + northSample.height) *
                0.5f;

            approachGrade =
                Mathf.Abs(
                    northSample.height -
                    southSample.height) /
                (2f *
                 FixedBridgeSiteProfile.SupportHalfExtentZ);

            if (!IsFinite(baseElevation) ||
                !IsFinite(approachGrade) ||
                approachGrade >
                Mathf.Max(
                    0f,
                    settings.maximumApproachGrade) ||
                southSample.slope >
                Mathf.Clamp(
                    settings.maximumApproachSlope,
                    0f,
                    90f) ||
                northSample.slope >
                Mathf.Clamp(
                    settings.maximumApproachSlope,
                    0f,
                    90f))
            {
                return false;
            }

            Vector2 riverIn =
                FixedBridgeSiteProfile.LocalToWorld(
                    new Vector2(
                        -FixedBridgeSiteProfile.SupportHalfExtentX,
                        0f),
                    intersection,
                    yawDegrees);

            Vector2 riverOut =
                FixedBridgeSiteProfile.LocalToWorld(
                    new Vector2(
                        FixedBridgeSiteProfile.SupportHalfExtentX,
                        0f),
                    intersection,
                    yawDegrees);

            WorldTerrainSample riverInSample =
                terrainProbe.Sample(riverIn);

            WorldTerrainSample riverOutSample =
                terrainProbe.Sample(riverOut);

            float riverGrade =
                Mathf.Abs(
                    riverOutSample.height -
                    riverInSample.height) /
                (2f *
                 FixedBridgeSiteProfile.SupportHalfExtentX);

            return
                IsFinite(riverGrade) &&
                riverGrade <=
                Mathf.Max(
                    0f,
                    settings.maximumRiverGrade);
        }

        private static bool OverlapsExistingSite(
            FixedCandidate candidate,
            MacroWorldPlan plan,
            float padding)
        {
            float halfPadding =
                Mathf.Max(0f, padding) *
                0.5f;

            float halfX =
                FixedBridgeSiteProfile.SupportHalfExtentX +
                halfPadding;

            float halfZ =
                FixedBridgeSiteProfile.SupportHalfExtentZ +
                halfPadding;

            for (int i = 0;
                 i < plan.BridgeSites.Count;
                 i++)
            {
                WorldBridgeSiteData existing = plan.BridgeSites[i];

                if (OrientedRectanglesOverlap(
                    candidate.worldPosition,
                    candidate.yawDegrees,
                    existing.worldPosition,
                    existing.yawDegrees,
                    halfX,
                    halfZ))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool OrientedRectanglesOverlap(
            Vector2 centerA,
            float yawA,
            Vector2 centerB,
            float yawB,
            float halfX,
            float halfZ)
        {
            GetAxes(yawA, out Vector2 axisAX, out Vector2 axisAZ);
            GetAxes(yawB, out Vector2 axisBX, out Vector2 axisBZ);
            Vector2 delta = centerB - centerA;

            return
                OverlapsOnAxis(
                    delta,
                    axisAX,
                    axisAX,
                    axisAZ,
                    axisBX,
                    axisBZ,
                    halfX,
                    halfZ) &&
                OverlapsOnAxis(
                    delta,
                    axisAZ,
                    axisAX,
                    axisAZ,
                    axisBX,
                    axisBZ,
                    halfX,
                    halfZ) &&
                OverlapsOnAxis(
                    delta,
                    axisBX,
                    axisAX,
                    axisAZ,
                    axisBX,
                    axisBZ,
                    halfX,
                    halfZ) &&
                OverlapsOnAxis(
                    delta,
                    axisBZ,
                    axisAX,
                    axisAZ,
                    axisBX,
                    axisBZ,
                    halfX,
                    halfZ);
        }

        private static bool OverlapsOnAxis(
            Vector2 delta,
            Vector2 testAxis,
            Vector2 axisAX,
            Vector2 axisAZ,
            Vector2 axisBX,
            Vector2 axisBZ,
            float halfX,
            float halfZ)
        {
            float distance =
                Mathf.Abs(
                    Vector2.Dot(
                        delta,
                        testAxis));

            float radiusA =
                halfX * Mathf.Abs(
                    Vector2.Dot(
                        axisAX,
                        testAxis)) +
                halfZ * Mathf.Abs(
                    Vector2.Dot(
                        axisAZ,
                        testAxis));

            float radiusB =
                halfX * Mathf.Abs(
                    Vector2.Dot(
                        axisBX,
                        testAxis)) +
                halfZ * Mathf.Abs(
                    Vector2.Dot(
                        axisBZ,
                        testAxis));

            return distance < radiusA + radiusB;
        }

        private static void GetAxes(
            float yawDegrees,
            out Vector2 axisX,
            out Vector2 axisZ)
        {
            float radians = yawDegrees * Mathf.Deg2Rad;
            float cosine = Mathf.Cos(radians);
            float sine = Mathf.Sin(radians);

            axisX = new Vector2(cosine, -sine);
            axisZ = new Vector2(sine, cosine);
        }

        private static long GetBridgeId(
            int worldSeed,
            long roadId,
            long riverId,
            int roadSegment,
            int riverSegment,
            int salt)
        {
            long pairId =
                DeterministicHash.StablePairId(
                    worldSeed,
                    roadId,
                    riverId,
                    salt);

            int segmentSalt =
                salt ^
                (roadSegment * 73856093) ^
                (riverSegment * 19349663) ^
                (int)pairId;

            return
                DeterministicHash.StableId(
                    worldSeed,
                    roadSegment,
                    riverSegment,
                    segmentSalt);
        }

        private static bool SegmentsOverlapCollinearly(
            Vector2 a, Vector2 b, Vector2 c, Vector2 d)
        {
            Vector2 road = b - a;
            Vector2 river = d - c;
            float lengthSqr = road.sqrMagnitude;
            if (lengthSqr < 0.000001f || river.sqrMagnitude < 0.000001f)
                return false;

            float cross = road.x * river.y - road.y * river.x;
            if (Mathf.Abs(cross) >= 0.000001f)
                return false;

            Vector2 offset = c - a;
            float distance = Mathf.Abs(offset.x * road.y - offset.y * road.x) /
                             Mathf.Sqrt(lengthSqr);
            if (distance > 0.001f)
                return false;

            float t0 = Vector2.Dot(offset, road) / lengthSqr;
            float t1 = Vector2.Dot(d - a, road) / lengthSqr;
            float overlapStart = Mathf.Max(0f, Mathf.Min(t0, t1));
            float overlapEnd = Mathf.Min(1f, Mathf.Max(t0, t1));
            return (overlapEnd - overlapStart) * Mathf.Sqrt(lengthSqr) > 0.001f;
        }

        private static bool TrySegmentIntersection(
            Vector2 p,
            Vector2 p2,
            Vector2 q,
            Vector2 q2,
            out Vector2 intersection,
            out float roadT,
            out float riverT)
        {
            Vector2 r = p2 - p;
            Vector2 s = q2 - q;
            float denominator = Cross(r, s);

            if (Mathf.Abs(denominator) < 0.000001f)
            {
                intersection = default(Vector2);
                roadT = 0f;
                riverT = 0f;
                return false;
            }

            Vector2 qp = q - p;
            float t = Cross(qp, s) / denominator;
            float u = Cross(qp, r) / denominator;

            if (t < 0f || t > 1f ||
                u < 0f || u > 1f)
            {
                intersection = default(Vector2);
                roadT = 0f;
                riverT = 0f;
                return false;
            }

            intersection = p + r * t;
            roadT = t;
            riverT = u;
            return true;
        }

        private static float Cross(Vector2 a, Vector2 b)
        {
            return a.x * b.y - a.y * b.x;
        }

        private static bool IsFinite(float value)
        {
            return
                !float.IsNaN(value) &&
                !float.IsInfinity(value);
        }
    }
}
