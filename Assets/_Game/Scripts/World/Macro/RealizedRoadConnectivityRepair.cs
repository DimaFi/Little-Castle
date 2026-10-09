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
        // This bounds memory even when the configured search distance is unlimited.
        // Failed or now-redundant candidates may consume slots; incompleteness is reported.
        public const int MaxPreparedCandidates = 512;
        public sealed class Result
        {
            public int initialComponents;
            public int remainingComponents;
            public int candidatesConsidered;
            public int acceptedConnections;
            public int routeAttempts;
            public int rejectedConnections;
            public long candidatePairChecks;
            public long eligibleCandidatePairs;
            public long discardedCandidatePairs;
            public int preparedCandidateCount;
            public bool attemptBudgetExhausted;
            public readonly List<long> attemptedRoadIds = new List<long>();
            public readonly List<long> addedRoadIds = new List<long>();

            public bool candidateSelectionTruncated =>
                discardedCandidatePairs > 0;


            public string Summary =>
                "initial=" + initialComponents +
                " remaining=" + remainingComponents +
                " candidates=" + candidatesConsidered +
                " accepted=" + acceptedConnections +
                " rejected=" + rejectedConnections +
                " pathAttempts=" + routeAttempts +
                " pairChecks=" + candidatePairChecks +
                " eligiblePairs=" + eligibleCandidatePairs +
                " prepared=" + preparedCandidateCount +
                " discarded=" + discardedCandidatePairs +
                " attemptBudgetExhausted=" + attemptBudgetExhausted;
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

            var rejectedIds = previouslyRejectedIds != null
                ? new HashSet<long>(previouslyRejectedIds)
                : new HashSet<long>();

            bool strictBridgeAware =
                bridgeSettings != null &&
                bridgeSettings.enabled &&
                bridgeSettings.useFixedStoneBridgeSites &&
                bridgeSettings.enableBridgeAwareRouting &&
                bridgeSettings.requireConnectedFeatureGraph;

            // A failed requested edge is abandoned only when the strict
            // bridge-aware recovery explicitly reports its ID. Retire that
            // stale logical edge before validation, but never use cleanup to
            // remove a materialized road (valid or otherwise).
            if (strictBridgeAware && rejectedIds.Count > 0)
                RemoveUnrealizedRejectedConnections(plan, rejectedIds);

            var features = new List<WorldPointFeatureData>(plan.PointFeatures);
            features.Sort((a, b) => a.stableId.CompareTo(b.stableId));

            var parent = new Dictionary<long, long>(features.Count);
            for (int i = 0; i < features.Count; i++)
                parent.Add(features[i].stableId, features[i].stableId);

            var roadsById = new Dictionary<long, WorldRoadData>();
            for (int i = 0; i < plan.Roads.Count; i++)
                if (plan.Roads[i] != null)
                    roadsById[plan.Roads[i].stableId] = plan.Roads[i];

            int components = parent.Count;
            for (int i = 0; i < plan.RoadConnections.Count; i++)
            {
                WorldRoadConnectionData edge = plan.RoadConnections[i];
                if (roadsById.TryGetValue(
                        edge.stableId, out WorldRoadData road) &&
                    parent.ContainsKey(edge.fromFeatureId) &&
                    parent.ContainsKey(edge.toFeatureId) &&
                    WorldRouteConnectivityValidator.RoadReachesFeatures(
                        plan, edge, road) &&
                    Union(parent, edge.fromFeatureId, edge.toFeatureId))
                    components--;
            }

            result.initialComponents = components;
            result.remainingComponents = components;

            if (components <= 1 || terrainProbe == null ||
                networkSettings == null || !networkSettings.enabled ||
                pathSettings == null || !pathSettings.enabled ||
                !strictBridgeAware ||
                bridgeSettings.maxConnectivityRepairCandidates <= 0)
                return result;

            int maxCandidates = Mathf.Clamp(
                bridgeSettings.maxConnectivityRepairCandidates, 0, 32);
            float maximumDistance = Mathf.Max(
                0f, networkSettings.maxConnectionDistance);
            float maximumDistanceSquared = maximumDistance * maximumDistance;
            int salt = DeterministicHash.String32(
                "road_network_connection");
            // Keep only the closest pairs, not an O(featureCount^2) list.
            // An X-sorted sweep also avoids distant pairs when a distance
            // restriction is configured; unrestricted dense cases still need
            // O(N^2) comparisons, but only O(N + MaxPreparedCandidates) memory.
            var candidates = PrepareCandidates(
                worldSeed, features, parent, rejectedIds, roadsById,
                maximumDistance, maximumDistanceSquared, salt, result);

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
                result.attemptedRoadIds.Add(candidate.roadId);
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
                WorldRoadData materialized = null;
                for (int j = 0; j < plan.Roads.Count; j++)
                    if (plan.Roads[j] != null &&
                        plan.Roads[j].stableId == candidate.roadId)
                    {
                        materialized = plan.Roads[j];
                        break;
                    }

                if (!WorldRouteConnectivityValidator.RoadReachesFeatures(
                        plan, connection, materialized))
                {
                    result.rejectedConnections++;
                    continue;
                }

                roadsById[candidate.roadId] = materialized;
                result.acceptedConnections++;
                result.addedRoadIds.Add(candidate.roadId);
                if (Union(parent, candidate.fromId, candidate.toId))
                    components--;
            }

            result.remainingComponents = components;
            result.attemptBudgetExhausted =
                components > 1 && result.candidatesConsidered >= maxCandidates;
            return result;
        }

        private static List<Candidate> PrepareCandidates(
            int worldSeed,
            List<WorldPointFeatureData> features,
            Dictionary<long, long> parent,
            HashSet<long> rejectedIds,
            Dictionary<long, WorldRoadData> roadsById,
            float maximumDistance,
            float maximumDistanceSquared,
            int salt,
            Result result)
        {
            // Sort spatially for an inexpensive, conservative X-distance
            // rejection. Canonicalize pair IDs independently of this order.
            var spatial = new List<WorldPointFeatureData>(features);
            spatial.Sort((a, b) =>
            {
                int order = a.worldPosition.x.CompareTo(b.worldPosition.x);
                if (order != 0) return order;
                order = a.worldPosition.y.CompareTo(b.worldPosition.y);
                if (order != 0) return order;
                return a.stableId.CompareTo(b.stableId);
            });

            var candidates = new List<Candidate>(
                System.Math.Min(MaxPreparedCandidates, spatial.Count));
            for (int i = 0; i < spatial.Count; i++)
            {
                WorldPointFeatureData first = spatial[i];
                long firstComponent = Find(parent, first.stableId);
                for (int j = i + 1; j < spatial.Count; j++)
                {
                    WorldPointFeatureData second = spatial[j];
                    if (maximumDistance > 0f &&
                        second.worldPosition.x - first.worldPosition.x >
                        maximumDistance)
                        break;

                    result.candidatePairChecks++;
                    if (firstComponent == Find(parent, second.stableId))
                        continue;

                    float distanceSquared =
                        (first.worldPosition - second.worldPosition).sqrMagnitude;
                    if (maximumDistance > 0f &&
                        distanceSquared > maximumDistanceSquared)
                        continue;

                    long fromId = System.Math.Min(first.stableId, second.stableId);
                    long toId = System.Math.Max(first.stableId, second.stableId);
                    long roadId = DeterministicHash.StablePairId(
                        worldSeed, fromId, toId, salt);
                    if (rejectedIds.Contains(roadId) ||
                        roadsById.ContainsKey(roadId))
                        continue;

                    result.eligibleCandidatePairs++;
                    var candidate = new Candidate
                    {
                        fromId = fromId,
                        toId = toId,
                        roadId = roadId,
                        distanceSquared = distanceSquared
                    };

                    // Sorted bounded insertion preserves the previous
                    // distance/fromId/toId ordering, independent of input
                    // insertion and spatial traversal order.
                    if (candidates.Count == MaxPreparedCandidates &&
                        CompareCandidates(
                            candidate, candidates[candidates.Count - 1]) >= 0)
                    {
                        result.discardedCandidatePairs++;
                        continue;
                    }

                    int low = 0;
                    int high = candidates.Count;
                    while (low < high)
                    {
                        int mid = low + (high - low) / 2;
                        if (CompareCandidates(candidate, candidates[mid]) > 0)
                            low = mid + 1;
                        else
                            high = mid;
                    }

                    if (candidates.Count == MaxPreparedCandidates)
                    {
                        candidates.RemoveAt(candidates.Count - 1);
                        result.discardedCandidatePairs++;
                    }

                    candidates.Insert(low, candidate);
                }
            }

            result.preparedCandidateCount = candidates.Count;
            return candidates;
        }

        private static int CompareCandidates(Candidate a, Candidate b)
        {
            int order = a.distanceSquared.CompareTo(b.distanceSquared);
            if (order != 0) return order;
            order = a.fromId.CompareTo(b.fromId);
            if (order != 0) return order;
            return a.toId.CompareTo(b.toId);
        }

        private static void RemoveUnrealizedRejectedConnections(
            MacroWorldPlan plan,
            HashSet<long> rejectedIds)
        {
            var materializedIds = new HashSet<long>();
            for (int i = 0; i < plan.Roads.Count; i++)
                if (plan.Roads[i] != null)
                    materializedIds.Add(plan.Roads[i].stableId);

            var abandonedIds = new List<long>();
            for (int i = 0; i < plan.RoadConnections.Count; i++)
            {
                long id = plan.RoadConnections[i].stableId;
                if (rejectedIds.Contains(id) &&
                    !materializedIds.Contains(id))
                    abandonedIds.Add(id);
            }

            abandonedIds.Sort();
            for (int i = 0; i < abandonedIds.Count; i++)
                plan.RemoveRoadAndConnection(abandonedIds[i]);
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
