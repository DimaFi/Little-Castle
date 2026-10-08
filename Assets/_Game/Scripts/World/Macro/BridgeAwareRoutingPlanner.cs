using System;
using System.Collections.Generic;
using UnityEngine;

namespace LittleCastle.World
{
    /// <summary>
    /// Bounded, deterministic recovery for rejected fixed-bridge road paths.
    /// Operates on macro data only; the fixed bridge's authored geometry and
    /// site/profile are never changed or stretched.
    /// </summary>
    public static class BridgeAwareRoutingPlanner
    {
        public sealed class RecoveryResult
        {
            public int attemptedConnections;
            public int recoveredConnections;
            public int failedConnections;
            public int pathAttempts;
            public int guidedAttempts;
            public readonly List<long> failedIds = new List<long>();

            public string Summary =>
                "attempted=" + attemptedConnections +
                " recovered=" + recoveredConnections +
                " failed=" + failedConnections +
                " pathAttempts=" + pathAttempts +
                " guidedAttempts=" + guidedAttempts;
        }

        public static RecoveryResult RecoverRejectedConnections(
            int worldSeed,
            MacroWorldPlan plan,
            IReadOnlyList<WorldRoadConnectionData> originalConnections,
            WorldTerrainProbe terrainProbe,
            TerrainRoadPathPlannerSettings roadSettings,
            BridgePlannerSettings bridgeSettings)
        {
            var result = new RecoveryResult();
            if (plan == null || originalConnections == null ||
                terrainProbe == null || roadSettings == null ||
                bridgeSettings == null ||
                !bridgeSettings.enabled ||
                !bridgeSettings.useFixedStoneBridgeSites ||
                !bridgeSettings.enableBridgeAwareRouting)
                return result;

            var existing = new HashSet<long>();
            for (int i = 0; i < plan.Roads.Count; i++)
                if (plan.Roads[i] != null)
                    existing.Add(plan.Roads[i].stableId);

            var missing = new List<WorldRoadConnectionData>();
            for (int i = 0; i < originalConnections.Count; i++)
            {
                WorldRoadConnectionData connection = originalConnections[i];
                if (!existing.Contains(connection.stableId))
                    missing.Add(connection);
            }
            missing.Sort((a, b) => a.stableId.CompareTo(b.stableId));

            for (int c = 0; c < missing.Count; c++)
            {
                WorldRoadConnectionData connection = missing[c];
                result.attemptedConnections++;

                if (!plan.TryGetPointFeature(connection.fromFeatureId,
                        out WorldPointFeatureData from) ||
                    !plan.TryGetPointFeature(connection.toFeatureId,
                        out WorldPointFeatureData to))
                {
                    RecordFailed(result, connection.stableId);
                    continue;
                }

                bool recovered = false;
                int maximumAttempts =
                    Mathf.Clamp(bridgeSettings.maxBridgeRoutingAttempts, 1, 8);

                for (int attempt = 0; attempt < maximumAttempts; attempt++)
                {
                    result.pathAttempts++;
                    TerrainRoadPathPlannerSettings alternate =
                        BuildAlternateSettings(roadSettings, attempt);

                    // All nodes are derived from world coordinates and the
                    // fixed endpoints. No UnityEngine.Random or arbitrary
                    // non-deterministic salts are introduced.
                    List<Vector2> path = TerrainRoadPathPlanner.Solve(
                        from.worldPosition,
                        to.worldPosition,
                        terrainProbe,
                        alternate,
                        plan.Rivers);
                    if (path == null || path.Count < 2)
                        continue;

                    if (!TryAcceptCandidate(
                            worldSeed, plan, connection, path,
                            alternate, bridgeSettings, terrainProbe))
                        continue;

                    result.recoveredConnections++;
                    recovered = true;
                    break;
                }

                if (!recovered)
                {
                    List<CrossingAnchor> anchors = FindGuidedAnchors(
                        from.worldPosition, to.worldPosition, plan.Rivers,
                        bridgeSettings);

                    int guidedLimit = Mathf.Clamp(
                        bridgeSettings.maxGuidedCrossingAttempts, 0, 8);
                    for (int a = 0;
                         a < Mathf.Min(anchors.Count, guidedLimit);
                         a++)
                    {
                        result.pathAttempts++;
                        result.guidedAttempts++;
                        List<Vector2> guided = TryBuildGuidedPath(
                            from.worldPosition, to.worldPosition,
                            anchors[a], terrainProbe,
                            roadSettings, plan.Rivers);

                        if (guided == null ||
                            !TryAcceptCandidate(worldSeed, plan,
                                connection, guided, roadSettings,
                                bridgeSettings, terrainProbe))
                            continue;

                        result.recoveredConnections++;
                        recovered = true;
                        break;
                    }
                }

                if (!recovered)
                    RecordFailed(result, connection.stableId);
            }

            return result;
        }

        private struct CrossingAnchor
        {
            public Vector2 crossing;
            public Vector2 roadAxis;
            public float score;
            public long riverId;
            public int segment;
            public int sample;
        }

        private static List<CrossingAnchor> FindGuidedAnchors(
            Vector2 start,
            Vector2 end,
            IReadOnlyList<WorldRiverData> rivers,
            BridgePlannerSettings settings)
        {
            var result = new List<CrossingAnchor>();
            Vector2 midpoint = (start + end) * 0.5f;
            float minimumWidth = Mathf.Max(0.1f,
                settings.minimumCompatibleRiverWidth);
            float maximumWidth = Mathf.Min(
                FixedBridgeSiteProfile.ArchOpeningWidth,
                Mathf.Max(minimumWidth, settings.maximumCompatibleRiverWidth));

            for (int r = 0; r < rivers.Count; r++)
            {
                WorldRiverData river = rivers[r];
                if (river == null || river.centerline == null)
                    continue;

                for (int s = 0; s < river.centerline.Count - 1; s++)
                {
                    Vector2 a = river.centerline[s];
                    Vector2 b = river.centerline[s + 1];
                    Vector2 tangent = b - a;
                    if (tangent.sqrMagnitude < 0.000001f)
                        continue;

                    tangent.Normalize();
                    Vector2 normal = new Vector2(-tangent.y, tangent.x);
                    for (int sample = 1; sample <= 3; sample++)
                    {
                        float t = sample * 0.25f;
                        float width = river.GetWidthAtSegment(s, t);
                        if (width < minimumWidth || width > maximumWidth)
                            continue;

                        Vector2 center = Vector2.Lerp(a, b, t);
                        Vector2 axis = normal;
                        if (Vector2.Dot(end - start, axis) < 0f)
                            axis = -axis;

                        // A crossing is only relevant when endpoints are
                        // on opposite sides with enough room for the fixed
                        // ±5.4 m road sockets.
                        if (Vector2.Dot(start - center, axis) >
                            -FixedBridgeSiteProfile.HalfLength - 0.1f ||
                            Vector2.Dot(end - center, axis) <
                            FixedBridgeSiteProfile.HalfLength + 0.1f)
                            continue;

                        result.Add(new CrossingAnchor
                        {
                            crossing = center,
                            roadAxis = axis,
                            score = (center - midpoint).sqrMagnitude,
                            riverId = river.stableId,
                            segment = s,
                            sample = sample
                        });
                    }
                }
            }

            result.Sort((a, b) =>
            {
                int c = a.score.CompareTo(b.score);
                if (c != 0) return c;
                c = a.riverId.CompareTo(b.riverId);
                if (c != 0) return c;
                c = a.segment.CompareTo(b.segment);
                return c != 0 ? c : a.sample.CompareTo(b.sample);
            });
            return result;
        }

        private static List<Vector2> TryBuildGuidedPath(
            Vector2 start,
            Vector2 end,
            CrossingAnchor anchor,
            WorldTerrainProbe terrain,
            TerrainRoadPathPlannerSettings settings,
            IReadOnlyList<WorldRiverData> rivers)
        {
            Vector2 entry = anchor.crossing -
                anchor.roadAxis * FixedBridgeSiteProfile.HalfLength;
            Vector2 exit = anchor.crossing +
                anchor.roadAxis * FixedBridgeSiteProfile.HalfLength;

            // Keep the authored crossing straight and full-length; allow
            // terrain-aware A* to find each approach independently.
            List<Vector2> approach = TerrainRoadPathPlanner.Solve(
                start, entry, terrain, settings, rivers);
            if (approach == null || approach.Count < 2)
                return null;
            List<Vector2> departure = TerrainRoadPathPlanner.Solve(
                exit, end, terrain, settings, rivers);
            if (departure == null || departure.Count < 2)
                return null;

            var route = new List<Vector2>();
            for (int i = 0; i < approach.Count; i++)
                AppendDistinct(route, approach[i]);
            AppendDistinct(route, anchor.crossing);
            AppendDistinct(route, exit);
            for (int i = 0; i < departure.Count; i++)
                AppendDistinct(route, departure[i]);
            return route.Count >= 2 ? route : null;
        }

        private static void AppendDistinct(List<Vector2> points, Vector2 point)
        {
            if (points.Count == 0 ||
                (points[points.Count - 1] - point).sqrMagnitude > 0.00000001f)
                points.Add(point);
        }

        private static bool TryAcceptCandidate(
            int seed,
            MacroWorldPlan plan,
            WorldRoadConnectionData connection,
            List<Vector2> path,
            TerrainRoadPathPlannerSettings settings,
            BridgePlannerSettings bridgeSettings,
            WorldTerrainProbe terrainProbe)
        {
            var road = new WorldRoadData
            {
                stableId = connection.stableId,
                roadKind = connection.roadKind,
                width = Mathf.Max(0.5f, settings.GetWidth(connection.roadKind))
            };
            road.centerline.AddRange(path);

            // Sandbox the proposed route with current bridge footprints,
            // but do not mutate authoritative plan data until accepted.
            var candidatePlan = new MacroWorldPlan(seed);
            for (int i = 0; i < plan.Rivers.Count; i++)
                candidatePlan.AddRiver(plan.Rivers[i]);
            for (int i = 0; i < plan.BridgeSites.Count; i++)
                candidatePlan.AddBridgeSite(plan.BridgeSites[i]);
            candidatePlan.AddRoad(road);
            candidatePlan.AddRoadConnection(connection);

            BridgeSitePlanner.BuildBridgeSites(
                seed, candidatePlan, bridgeSettings, terrainProbe);

            if (candidatePlan.Roads.Count != 1 ||
                candidatePlan.RoadConnections.Count != 1 ||
                !HasSupportedCrossings(road, plan.Rivers, candidatePlan.BridgeSites))
                return false;

            bool connectionPresent = false;
            for (int i = 0; i < plan.RoadConnections.Count; i++)
                if (plan.RoadConnections[i].stableId == connection.stableId)
                {
                    connectionPresent = true;
                    break;
                }
            if (!connectionPresent && !plan.AddRoadConnection(connection))
                return false;
            if (!plan.AddRoad(road))
            {
                if (!connectionPresent)
                    plan.RemoveRoadAndConnection(connection.stableId);
                return false;
            }

            for (int i = 0; i < candidatePlan.BridgeSites.Count; i++)
            {
                WorldBridgeSiteData site = candidatePlan.BridgeSites[i];
                if (site.roadId == connection.stableId)
                    plan.AddBridgeSite(site);
            }
            return true;
        }

        private static void RecordFailed(RecoveryResult result, long id)
        {
            result.failedConnections++;
            result.failedIds.Add(id);
        }

        private static TerrainRoadPathPlannerSettings BuildAlternateSettings(
            TerrainRoadPathPlannerSettings source, int attempt)
        {
            // Multiple bounded coarse-to-fine passes, with changing search
            // envelope and river penalty. Keep slope limits and road widths.
            return new TerrainRoadPathPlannerSettings
            {
                enabled = source.enabled,
                gridStep = Mathf.Max(4f, source.gridStep / (1f + attempt * 0.5f)),
                searchPadding = Mathf.Max(0f, source.searchPadding) +
                    attempt * Mathf.Max(4f, source.gridStep) * 3f,
                maxSlope = source.maxSlope,
                slopeCostMultiplier = source.slopeCostMultiplier,
                highlandCostMultiplier = source.highlandCostMultiplier,
                riverCrossingPenalty = Mathf.Max(0f,
                    source.riverCrossingPenalty / (1f + attempt)),
                riverAvoidancePadding = source.riverAvoidancePadding,
                maxExpandedNodes = source.maxExpandedNodes,
                trailWidth = source.trailWidth,
                dirtRoadWidth = source.dirtRoadWidth,
                improvedRoadWidth = source.improvedRoadWidth,
                settlementStreetWidth = source.settlementStreetWidth,
                fortifiedRouteWidth = source.fortifiedRouteWidth
            };
        }

        private static bool HasSupportedCrossings(
            WorldRoadData road,
            IReadOnlyList<WorldRiverData> rivers,
            IReadOnlyList<WorldBridgeSiteData> bridges)
        {
            // Reuse the comprehensive public crossing validator with a
            // temporary self-contained plan, never a scene/world materialization.
            var check = new MacroWorldPlan(0);
            check.AddPointFeature(new WorldPointFeatureData(
                100, default(WorldFeatureKind), string.Empty,
                road.centerline[0], 0f));
            check.AddPointFeature(new WorldPointFeatureData(
                101, default(WorldFeatureKind), string.Empty,
                road.centerline[road.centerline.Count - 1], 0f));
            check.AddRoadConnection(new WorldRoadConnectionData(
                road.stableId, 100, 101, road.roadKind));
            check.AddRoad(road);
            for (int i = 0; i < rivers.Count; i++)
                check.AddRiver(rivers[i]);
            for (int i = 0; i < bridges.Count; i++)
                if (bridges[i].roadId == road.stableId)
                    check.AddBridgeSite(bridges[i]);
            return WorldRouteConnectivityValidator.Validate(check).IsValid;
        }
    }
}
