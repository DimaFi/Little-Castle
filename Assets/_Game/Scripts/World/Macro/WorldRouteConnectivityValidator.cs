using System;
using System.Collections.Generic;
using UnityEngine;

namespace LittleCastle.World
{
    /// <summary>
    /// Data-only diagnostics for realized macro routes and fixed bridge sites.
    /// Does not instantiate chunks, game objects, or alter a world plan.
    /// </summary>
    public static class WorldRouteConnectivityValidator
    {
        public sealed class Report
        {
            public int featureCount;
            public int logicalConnectionCount;
            public int realizedRoadCount;
            public int fixedBridgeCount;
            public int missingRealizedConnections;
            public int orphanBridgeSites;
            public int unbridgedCrossings;
            public int disconnectedFeatures;
            public int components;
            public int minimumBridgeCount;
            public readonly List<string> errors = new List<string>();
            public bool IsValid => errors.Count == 0;

            public string Summary =>
                "routes=" + realizedRoadCount +
                " connections=" + logicalConnectionCount +
                " bridges=" + fixedBridgeCount +
                " minimum=" + minimumBridgeCount +
                " unbridged=" + unbridgedCrossings +
                " orphan=" + orphanBridgeSites +
                " missing=" + missingRealizedConnections +
                " components=" + components +
                " disconnected=" + disconnectedFeatures +
                " issues=" + errors.Count;

            internal void Fail(string message)
            {
                // Keep bounded diagnostics on large maps. Counters remain complete.
                if (errors.Count < 32)
                    errors.Add(message);
            }
        }

        public static Report Validate(
            MacroWorldPlan plan,
            int minimumFixedBridges = 0,
            bool requireConnectedFeatures = false)
        {
            var report = new Report
            {
                minimumBridgeCount = Mathf.Max(0, minimumFixedBridges)
            };

            if (plan == null)
            {
                report.Fail("MacroWorldPlan is null.");
                return report;
            }

            report.featureCount = plan.PointFeatures.Count;
            report.logicalConnectionCount = plan.RoadConnections.Count;
            report.realizedRoadCount = plan.Roads.Count;

            var featureIds = new HashSet<long>();
            for (int i = 0; i < plan.PointFeatures.Count; i++)
                featureIds.Add(plan.PointFeatures[i].stableId);

            var roads = new Dictionary<long, WorldRoadData>();
            for (int i = 0; i < plan.Roads.Count; i++)
            {
                WorldRoadData road = plan.Roads[i];
                if (road == null || road.centerline == null ||
                    road.centerline.Count < 2)
                {
                    report.Fail("Null or invalid realized road at index " + i);
                    continue;
                }
                roads[road.stableId] = road;
            }

            var rivers = new HashSet<long>();
            for (int i = 0; i < plan.Rivers.Count; i++)
                if (plan.Rivers[i] != null)
                    rivers.Add(plan.Rivers[i].stableId);

            var servedBridges = new Dictionary<long, List<WorldBridgeSiteData>>();
            for (int i = 0; i < plan.BridgeSites.Count; i++)
            {
                WorldBridgeSiteData bridge = plan.BridgeSites[i];
                bool orphan = !bridge.isFixedSite ||
                              !roads.ContainsKey(bridge.roadId) ||
                              !rivers.Contains(bridge.riverId);
                if (bridge.isFixedSite)
                {
                    report.fixedBridgeCount++;
                    orphan |= bridge.archetypeId != FixedBridgeSiteProfile.AssetId ||
                              bridge.contractVersion != FixedBridgeSiteProfile.ContractVersion ||
                              Mathf.Abs(bridge.requiredSpan - FixedBridgeSiteProfile.BridgeLength) > 0.001f;
                }

                if (orphan)
                {
                    report.orphanBridgeSites++;
                    report.Fail("Orphan/incompatible bridge " + bridge.stableId);
                    continue;
                }

                if (!servedBridges.TryGetValue(bridge.roadId, out var list))
                {
                    list = new List<WorldBridgeSiteData>();
                    servedBridges.Add(bridge.roadId, list);
                }
                list.Add(bridge);
            }

            var parent = new Dictionary<long, long>();
            foreach (long id in featureIds)
                parent.Add(id, id);

            var coveredRoadIds = new HashSet<long>();
            for (int i = 0; i < plan.RoadConnections.Count; i++)
            {
                WorldRoadConnectionData connection = plan.RoadConnections[i];
                coveredRoadIds.Add(connection.stableId);
                if (!roads.ContainsKey(connection.stableId) ||
                    !featureIds.Contains(connection.fromFeatureId) ||
                    !featureIds.Contains(connection.toFeatureId))
                {
                    report.missingRealizedConnections++;
                    report.Fail("Unrealized or invalid logical road " + connection.stableId);
                    continue;
                }

                long a = Find(parent, connection.fromFeatureId);
                long b = Find(parent, connection.toFeatureId);
                if (a != b)
                    parent[b] = a;
            }

            foreach (long roadId in roads.Keys)
            {
                if (!coveredRoadIds.Contains(roadId))
                    report.Fail("Realized road has no logical connection: " + roadId);
            }

            for (int i = 0; i < plan.Roads.Count; i++)
            {
                WorldRoadData road = plan.Roads[i];
                if (road == null || road.centerline == null ||
                    road.centerline.Count < 2)
                    continue;

                for (int j = 0; j < plan.Rivers.Count; j++)
                {
                    WorldRiverData river = plan.Rivers[j];
                    if (river == null || river.centerline == null ||
                        river.centerline.Count < 2)
                        continue;

                    for (int rs = 0; rs < road.centerline.Count - 1; rs++)
                    {
                        for (int ws = 0; ws < river.centerline.Count - 1; ws++)
                        {
                            if (!TryIntersection(
                                road.centerline[rs], road.centerline[rs + 1],
                                river.centerline[ws], river.centerline[ws + 1],
                                out Vector2 crossing,
                                out bool collinearOverlap))
                                continue;

                            bool served = false;
                            if (!collinearOverlap &&
                                servedBridges.TryGetValue(road.stableId, out var candidates))
                            {
                                for (int b = 0; b < candidates.Count; b++)
                                {
                                    WorldBridgeSiteData site = candidates[b];
                                    if (site.riverId == river.stableId &&
                                        (site.worldPosition - crossing).sqrMagnitude < 0.01f &&
                                        IsAlignedWithRoad(site, road.centerline[rs],
                                            road.centerline[rs + 1]))
                                    {
                                        served = true;
                                        break;
                                    }
                                }
                            }
                            if (!served)
                            {
                                report.unbridgedCrossings++;
                                report.Fail((collinearOverlap
                                    ? "Road overlaps river centerline: road "
                                    : "Unbridged crossing: road ") + road.stableId +
                                    " river " + river.stableId);
                            }
                        }
                    }
                }
            }

            // A bridge that has no actual crossing is an orphan too.
            foreach (var pair in servedBridges)
            {
                for (int b = 0; b < pair.Value.Count; b++)
                {
                    WorldBridgeSiteData bridge = pair.Value[b];
                    bool intersects = false;
                    WorldRoadData road = roads[pair.Key];
                    for (int r = 0; r < plan.Rivers.Count && !intersects; r++)
                    {
                        WorldRiverData river = plan.Rivers[r];
                        if (river == null || river.stableId != bridge.riverId ||
                            river.centerline == null)
                            continue;
                        for (int a = 0; a < road.centerline.Count - 1 && !intersects; a++)
                            for (int z = 0; z < river.centerline.Count - 1 && !intersects; z++)
                                if (TryIntersection(
                                    road.centerline[a], road.centerline[a + 1],
                                    river.centerline[z], river.centerline[z + 1],
                                    out Vector2 location,
                                    out bool collinearOverlap) &&
                                    !collinearOverlap &&
                                    (location - bridge.worldPosition).sqrMagnitude < 0.01f &&
                                    IsAlignedWithRoad(bridge, road.centerline[a],
                                        road.centerline[a + 1]))
                                    intersects = true;
                    }
                    if (!intersects)
                    {
                        report.orphanBridgeSites++;
                        report.Fail("Bridge has no real road/river crossing: " + bridge.stableId);
                    }
                }
            }

            var roots = new HashSet<long>();
            foreach (long featureId in featureIds)
                roots.Add(Find(parent, featureId));
            report.components = roots.Count;
            report.disconnectedFeatures = report.components > 1 ? report.components - 1 : 0;

            if (requireConnectedFeatures && report.components > 1)
                report.Fail("Feature graph is disconnected into " + report.components + " components.");
            if (report.fixedBridgeCount < report.minimumBridgeCount)
                report.Fail("Minimum bridge count unmet: " + report.fixedBridgeCount +
                            " < " + report.minimumBridgeCount);

            return report;
        }

        private static long Find(Dictionary<long, long> parents, long id)
        {
            long original = id;
            while (parents[id] != id)
                id = parents[id];
            long root = id;
            id = original;
            while (parents[id] != id)
            {
                long next = parents[id];
                parents[id] = root;
                id = next;
            }
            return root;
        }

        private static bool IsAlignedWithRoad(
            WorldBridgeSiteData site, Vector2 roadA, Vector2 roadB)
        {
            Vector2 road = roadB - roadA;
            if (road.sqrMagnitude < 0.000001f)
                return false;

            float radians = site.yawDegrees * Mathf.Deg2Rad;
            Vector2 deck = new Vector2(Mathf.Sin(radians), Mathf.Cos(radians));
            return Mathf.Abs(Vector2.Dot(road.normalized, deck)) >=
                   Mathf.Cos(20f * Mathf.Deg2Rad);
        }

        private static bool TryIntersection(
            Vector2 a, Vector2 b, Vector2 c, Vector2 d,
            out Vector2 point, out bool collinearOverlap)
        {
            point = default;
            collinearOverlap = false;
            Vector2 r = b - a;
            Vector2 s = d - c;
            float denominator = r.x * s.y - r.y * s.x;
            if (Mathf.Abs(denominator) < 0.000001f)
            {
                float lengthSqr = r.sqrMagnitude;
                if (lengthSqr < 0.000001f || s.sqrMagnitude < 0.000001f)
                    return false;

                Vector2 offset = c - a;
                float distance = Mathf.Abs(offset.x * r.y - offset.y * r.x) /
                                 Mathf.Sqrt(lengthSqr);
                if (distance > 0.001f)
                    return false;

                float t0 = Vector2.Dot(offset, r) / lengthSqr;
                float t1 = Vector2.Dot(d - a, r) / lengthSqr;
                float overlapStart = Mathf.Max(0f, Mathf.Min(t0, t1));
                float overlapEnd = Mathf.Min(1f, Mathf.Max(t0, t1));
                if ((overlapEnd - overlapStart) * Mathf.Sqrt(lengthSqr) <= 0.001f)
                    return false;

                point = a + r * ((overlapStart + overlapEnd) * 0.5f);
                collinearOverlap = true;
                return true;
            }
            Vector2 delta = c - a;
            float t = (delta.x * s.y - delta.y * s.x) / denominator;
            float u = (delta.x * r.y - delta.y * r.x) / denominator;
            if (t < 0f || t > 1f || u < 0f || u > 1f)
                return false;
            point = a + r * t;
            return true;
        }
    }
}
