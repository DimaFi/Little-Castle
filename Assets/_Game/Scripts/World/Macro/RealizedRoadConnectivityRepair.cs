using System.Collections.Generic;
using UnityEngine;

namespace LittleCastle.World
{
    /// <summary>
    /// Last opt-in pass for actually disconnected realized feature graphs.
    /// Retries alternative feature pairs after original desired edges have
    /// been rejected; every added edge must pass the existing fixed bridge
    /// geometry and terrain-aware route validator. No synthetic bridges,
    /// resource equality, full-world GameObjects or unbounded retries.
    /// </summary>
    public static class RealizedRoadConnectivityRepair
    {
        public sealed class Result
        {
            public int initialComponents;
            public int remainingComponents;
            public int candidatesConsidered;
            public int acceptedConnections;
            public int routeAttempts;
            public int rejectedConnections;
            public readonly List<long> addedRoadIds = new List<long>();

            public string Summary =>
                "initial=" + initialComponents +
                " remaining=" + remainingComponents +
                " candidates=" + candidatesConsidered +
                " accepted=" + acceptedConnections +
                " rejected=" + rejectedConnections +
                " pathAttempts=" + routeAttempts;
        }

        private struct Candidate
        {
            public long fromId;
            public long toId;
            public long roadId;
            public float distanceSquared;
        }

        public static Result Repair(
            int worldSeed,
            MacroWorldPlan plan,
            WorldTerrainProbe terrainProbe,
            RoadNetworkPlannerSettings networkSettings,
            TerrainRoadPathPlannerSettings pathSettings,
            BridgePlannerSettings bridgeSettings,
            IReadOnlyCollection<long> previouslyRejectedIds = null)
        {
            var result = new Result();
            if (plan == null)
                return result;

            var features = new List<WorldPointFeatureData>(plan.PointFeatures);
            features.Sort((a, b) => a.stableId.CompareTo(b.stableId));

            var parent = new Dictionary<long, long>(features.Count);
            for (int i = 0; i < features.Count; i++)
                parent.Add(features[i].stableId, features[i].stableId);

            var realizedRoadIds = new HashSet<long>();
            for (int i = 0; i < plan.Roads.Count; i++)
                if (plan.Roads[i] != null)
                    realizedRoadIds.Add(plan.Roads[i].stableId);

            int components = parent.Count;
            for (int i = 0; i < plan.RoadConnections.Count; i++)
            {
                WorldRoadConnectionData edge = plan.RoadConnections[i];
                if (realizedRoadIds.Contains(edge.stableId) &&
                    parent.ContainsKey(edge.fromFeatureId) &&
                    parent.ContainsKey(edge.toFeatureId) &&
                    Union(parent, edge.fromFeatureId, edge.toFeatureId))
                    components--;
            }

            result.initialComponents = components;
            result.remainingComponents = components;

            if (components <= 1 || terrainProbe == null ||
                networkSettings == null || !networkSettings.enabled ||
                pathSettings == null || !pathSettings.enabled ||
                bridgeSettings == null || !bridgeSettings.enabled ||
                !bridgeSettings.useFixedStoneBridgeSites ||
                !bridgeSettings.enableBridgeAwareRouting ||
                !bridgeSettings.requireConnectedFeatureGraph ||
                bridgeSettings.maxConnectivityRepairCandidates <= 0)
                return result;

            int maxCandidates = Mathf.Clamp(
                bridgeSettings.maxConnectivityRepairCandidates, 0, 32);
            float maximumDistance = Mathf.Max(
                0f, networkSettings.maxConnectionDistance);
            float maximumDistanceSquared = maximumDistance * maximumDistance;
            int salt = DeterministicHash.String32(
                "road_network_connection");
            var rejectedIds = previouslyRejectedIds != null
                ? new HashSet<long>(previouslyRejectedIds)
                : new HashSet<long>();

            // Build a finite, stable candidate set; sort by geometric cost
            // and stable identifiers. Feature insertion order has no effect.
            var candidates = new List<Candidate>();
            for (int i = 0; i < features.Count; i++)
            {
                for (int j = i + 1; j < features.Count; j++)
                {
                    WorldPointFeatureData from = features[i];
                    WorldPointFeatureData to = features[j];
                    if (Find(parent, from.stableId) ==
                        Find(parent, to.stableId))
                        continue;

                    float distanceSquared =
                        (from.worldPosition - to.worldPosition).sqrMagnitude;
                    if (maximumDistance > 0f &&
                        distanceSquared > maximumDistanceSquared)
                        continue;

                    long roadId = DeterministicHash.StablePairId(
                        worldSeed, from.stableId, to.stableId, salt);
                    if (rejectedIds.Contains(roadId) ||
                        realizedRoadIds.Contains(roadId))
                        continue;

                    candidates.Add(new Candidate
                    {
                        fromId = from.stableId,
                        toId = to.stableId,
                        distanceSquared = distanceSquared,
                        roadId = roadId
                    });
                }
            }
            candidates.Sort((a, b) =>
            {
                int order = a.distanceSquared.CompareTo(b.distanceSquared);
                if (order != 0) return order;
                order = a.fromId.CompareTo(b.fromId);
                if (order != 0) return order;
                return a.toId.CompareTo(b.toId);
            });

            for (int i = 0;
                 i < candidates.Count && components > 1 &&
                 result.candidatesConsidered < maxCandidates; i++)
            {
                Candidate candidate = candidates[i];
                // Recheck union after every accepted alternative to avoid
                // redundant roads and bridge footprints.
                if (Find(parent, candidate.fromId) ==
                    Find(parent, candidate.toId))
                    continue;

                result.candidatesConsidered++;
                var connection = new WorldRoadConnectionData(
                    candidate.roadId, candidate.fromId, candidate.toId,
                    networkSettings.roadKind);

                BridgeAwareRoutingPlanner.RecoveryResult attempt =
                    BridgeAwareRoutingPlanner.RecoverRejectedConnections(
                        worldSeed,
                        plan,
                        new[] { connection },
                        terrainProbe,
                        pathSettings,
                        bridgeSettings);
                result.routeAttempts += attempt.pathAttempts;

                if (attempt.recoveredConnections != 1)
                {
                    result.rejectedConnections++;
                    continue;
                }

                // Only a truly materialized, accepted road connects nodes.
                bool materialized = false;
                for (int j = 0; j < plan.Roads.Count; j++)
                    if (plan.Roads[j] != null &&
                        plan.Roads[j].stableId == candidate.roadId)
                    {
                        materialized = true;
                        break;
                    }

                if (!materialized)
                {
                    result.rejectedConnections++;
                    continue;
                }

                result.acceptedConnections++;
                result.addedRoadIds.Add(candidate.roadId);
                if (Union(parent, candidate.fromId, candidate.toId))
                    components--;
            }

            result.remainingComponents = components;
            return result;
        }

        private static long Find(Dictionary<long, long> parent, long id)
        {
            long root = id;
            while (parent[root] != root)
                root = parent[root];

            while (parent[id] != id)
            {
                long next = parent[id];
                parent[id] = root;
                id = next;
            }
            return root;
        }

        private static bool Union(
            Dictionary<long, long> parent, long a, long b)
        {
            a = Find(parent, a);
            b = Find(parent, b);
            if (a == b)
                return false;
            if (a < b)
                parent[b] = a;
            else
                parent[a] = b;
            return true;
        }
    }
}
