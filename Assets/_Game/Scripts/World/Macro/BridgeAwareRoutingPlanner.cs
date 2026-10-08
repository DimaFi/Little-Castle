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
            public readonly List<long> failedIds = new List<long>();

            public string Summary =>
                "attempted=" + attemptedConnections +
                " recovered=" + recoveredConnections +
                " failed=" + failedConnections +
                " pathAttempts=" + pathAttempts;
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

                    var proposedRoad = new WorldRoadData
                    {
                        stableId = connection.stableId,
                        roadKind = connection.roadKind,
                        width = Mathf.Max(0.5f,
                            alternate.GetWidth(connection.roadKind))
                    };
                    proposedRoad.centerline.AddRange(path);

                    // Sandbox only one proposed road, but include already
                    // accepted bridge sites so their footprints cannot overlap.
                    var candidatePlan = new MacroWorldPlan(worldSeed);
                    for (int r = 0; r < plan.Rivers.Count; r++)
                        candidatePlan.AddRiver(plan.Rivers[r]);
                    for (int b = 0; b < plan.BridgeSites.Count; b++)
                        candidatePlan.AddBridgeSite(plan.BridgeSites[b]);
                    candidatePlan.AddRoad(proposedRoad);
                    candidatePlan.AddRoadConnection(connection);

                    BridgeSitePlanner.BuildBridgeSites(
                        worldSeed, candidatePlan, bridgeSettings, terrainProbe);

                    if (candidatePlan.Roads.Count != 1 ||
                        candidatePlan.RoadConnections.Count != 1)
                        continue;

                    // Guard against any future change in the candidate bridge
                    // planner that might retain an unsupported river crossing.
                    if (!HasSupportedCrossings(proposedRoad, plan.Rivers,
                        candidatePlan.BridgeSites))
                        continue;

                    bool connectionPresent = false;
                    for (int p = 0; p < plan.RoadConnections.Count; p++)
                    {
                        if (plan.RoadConnections[p].stableId == connection.stableId)
                        {
                            connectionPresent = true;
                            break;
                        }
                    }
                    if (!connectionPresent && !plan.AddRoadConnection(connection))
                        continue;
                    if (!plan.AddRoad(proposedRoad))
                    {
                        if (!connectionPresent)
                            plan.RemoveRoadAndConnection(connection.stableId);
                        continue;
                    }

                    for (int b = 0; b < candidatePlan.BridgeSites.Count; b++)
                    {
                        WorldBridgeSiteData site = candidatePlan.BridgeSites[b];
                        if (site.roadId == connection.stableId)
                            plan.AddBridgeSite(site);
                    }

                    result.recoveredConnections++;
                    recovered = true;
                    break;
                }

                if (!recovered)
                    RecordFailed(result, connection.stableId);
            }

            return result;
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
