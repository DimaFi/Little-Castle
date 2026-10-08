using System;
using System.Collections.Generic;
using UnityEngine;

namespace LittleCastle.World
{
    public sealed class MacroWorldPlanner
    {
        private readonly MacroWorldPlannerSettings settings;

        public MacroWorldPlanner(
            MacroWorldPlannerSettings settings)
        {
            this.settings = settings;
        }

        public MacroWorldPlan GenerateForBounds(
            int worldSeed,
            Rect worldBounds,
            WorldTerrainProbe terrainProbe = null)
        {
            var plan = new MacroWorldPlan(worldSeed);

            if (settings == null)
                return plan;

            Rect planningBounds =
                ExpandRect(
                    worldBounds,
                    settings.PlanningHalo);

            for (int i = 0; i < settings.PointFeatureRules.Count; i++)
            {
                MacroPointFeatureRule rule =
                    settings.PointFeatureRules[i];

                if (rule == null)
                    continue;

                AppendRule(
                    worldSeed,
                    planningBounds,
                    rule,
                    terrainProbe,
                    plan);
            }

            if (terrainProbe != null)
            {
                RiverNetworkPlanner.BuildRivers(
                    worldSeed,
                    planningBounds,
                    terrainProbe,
                    settings.Rivers,
                    plan);
            }

            RoadNetworkPlanner.BuildConnections(
                worldSeed,
                plan,
                settings.RoadNetwork);

            bool bridgeAware =
                terrainProbe != null &&
                settings.RoadPaths != null &&
                settings.RoadPaths.enabled &&
                settings.Bridges != null &&
                settings.Bridges.enabled &&
                settings.Bridges.useFixedStoneBridgeSites &&
                settings.Bridges.enableBridgeAwareRouting;

            if (bridgeAware && settings.Bridges.requireConnectedFeatureGraph)
                RoadNetworkPlanner.BuildConnectivityBackbone(
                    worldSeed, plan, settings.RoadNetwork);

            if (terrainProbe != null)
            {
                TerrainRoadPathPlanner.BuildRoadPaths(
                    plan,
                    terrainProbe,
                    settings.RoadPaths);
            }

            // Capture desired edges before the fixed planner atomically
            // removes unsupported roads/connections. The recovery is opt-in.
            List<WorldRoadConnectionData> desiredConnections =
                bridgeAware
                    ? new List<WorldRoadConnectionData>(plan.RoadConnections)
                    : null;

            BridgeSitePlanner.BuildBridgeSites(
                worldSeed,
                plan,
                settings.Bridges,
                terrainProbe);

            if (bridgeAware)
            {
                BridgeAwareRoutingPlanner.RecoveryResult recovery =
                    BridgeAwareRoutingPlanner.RecoverRejectedConnections(
                        worldSeed,
                        plan,
                        desiredConnections,
                        terrainProbe,
                        settings.RoadPaths,
                        settings.Bridges);

                var diagnostics =
                    WorldRouteConnectivityValidator.Validate(
                        plan,
                        settings.Bridges.minimumFixedBridgeCount,
                        settings.Bridges.requireConnectedFeatureGraph);

                if (recovery.failedConnections > 0)
                    diagnostics.Fail(
                        "Unable to realize " + recovery.failedConnections +
                        " requested road connections after " +
                        recovery.pathAttempts + " bounded retries.");

                plan.RouteDiagnostics = diagnostics;
                plan.BridgeAwareRoutingAttempted = true;
                plan.BridgeAwareRoutingSatisfied = diagnostics.IsValid;

                if (!diagnostics.IsValid)
                {
                    string message =
                        "Bridge-aware macro routing failed: " +
                        recovery.Summary + "; " +
                        diagnostics.Summary + "; " +
                        string.Join("; ", diagnostics.errors);

                    if (settings.Bridges.failOnRoutingError)
                        throw new InvalidOperationException(message);

                    Debug.LogWarning(message);
                }
            }

            return plan;
        }

        private static void AppendRule(
            int worldSeed,
            Rect bounds,
            MacroPointFeatureRule rule,
            WorldTerrainProbe terrainProbe,
            MacroWorldPlan plan)
        {
            float spacing =
                Mathf.Max(
                    50f,
                    rule.spacing);

            int salt =
                DeterministicHash.String32(
                    string.IsNullOrWhiteSpace(
                        rule.ruleId)
                        ? rule.kind.ToString()
                        : rule.ruleId);

            int minGridX =
                Mathf.FloorToInt(
                    bounds.xMin / spacing) - 1;

            int maxGridX =
                Mathf.FloorToInt(
                    bounds.xMax / spacing) + 1;

            int minGridZ =
                Mathf.FloorToInt(
                    bounds.yMin / spacing) - 1;

            int maxGridZ =
                Mathf.FloorToInt(
                    bounds.yMax / spacing) + 1;

            float margin =
                Mathf.Clamp(
                    rule.borderJitter,
                    0f,
                    0.45f);

            for (int gz = minGridZ; gz <= maxGridZ; gz++)
            {
                for (int gx = minGridX; gx <= maxGridX; gx++)
                {
                    float roll =
                        DeterministicHash.Hash01(
                            worldSeed,
                            gx,
                            gz,
                            salt ^ 0x101);

                    if (roll > rule.chance)
                        continue;

                    float jx =
                        Mathf.Lerp(
                            margin,
                            1f - margin,
                            DeterministicHash.Hash01(
                                worldSeed,
                                gx,
                                gz,
                                salt ^ 0x102));

                    float jz =
                        Mathf.Lerp(
                            margin,
                            1f - margin,
                            DeterministicHash.Hash01(
                                worldSeed,
                                gx,
                                gz,
                                salt ^ 0x103));

                    var position =
                        new Vector2(
                            (gx + jx) * spacing,
                            (gz + jz) * spacing);

                    if (!bounds.Contains(position))
                        continue;

                    if (terrainProbe != null)
                    {
                        WorldTerrainSample sample =
                            terrainProbe.Sample(position);

                        if (!rule.allowedTerrain.Contains(
                                sample.terrainClass) ||
                            sample.height < rule.minHeight ||
                            sample.height > rule.maxHeight ||
                            sample.slope > rule.maxSlope)
                        {
                            continue;
                        }
                    }

                    if (rule.avoidOtherPointFeatures &&
                        !HasSeparation(
                            plan,
                            position,
                            rule.influenceRadius,
                            rule.separationPadding))
                    {
                        continue;
                    }

                    long id =
                        DeterministicHash.StableId(
                            worldSeed,
                            gx,
                            gz,
                            salt);

                    plan.AddPointFeature(
                        new WorldPointFeatureData(
                            id,
                            rule.kind,
                            rule.archetypeId,
                            position,
                            rule.influenceRadius));
                }
            }
        }

        private static bool HasSeparation(
            MacroWorldPlan plan,
            Vector2 position,
            float influenceRadius,
            float padding)
        {
            for (int i = 0; i < plan.PointFeatures.Count; i++)
            {
                WorldPointFeatureData existing =
                    plan.PointFeatures[i];

                float required =
                    Mathf.Max(
                        0f,
                        influenceRadius) +
                    Mathf.Max(
                        0f,
                        existing.influenceRadius) +
                    Mathf.Max(
                        0f,
                        padding);

                if ((existing.worldPosition - position).sqrMagnitude <
                    required * required)
                {
                    return false;
                }
            }

            return true;
        }

        private static Rect ExpandRect(
            Rect rect,
            float padding)
        {
            float safe =
                Mathf.Max(
                    0f,
                    padding);

            rect.xMin -= safe;
            rect.xMax += safe;
            rect.yMin -= safe;
            rect.yMax += safe;
            return rect;
        }
    }
}
