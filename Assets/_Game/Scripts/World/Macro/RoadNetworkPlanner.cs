using System.Collections.Generic;
using UnityEngine;

namespace LittleCastle.World
{
    /// <summary>
    /// Builds deterministic logical road connections between neutral settlements.
    /// A separate path solver must turn these connections into WorldRoadData.
    /// </summary>
    public static class RoadNetworkPlanner
    {
        private struct BackboneCandidate
        {
            public long fromId;
            public long toId;
            public float distanceSqr;
        }

        private struct Candidate
        {
            public int featureIndex;
            public float distanceSqr;
            public long stableId;
        }

        public static void BuildConnections(
            int worldSeed,
            MacroWorldPlan plan,
            RoadNetworkPlannerSettings settings)
        {
            if (plan == null ||
                settings == null ||
                !settings.enabled)
            {
                return;
            }

            var settlements =
                new List<WorldPointFeatureData>();

            for (int i = 0; i < plan.PointFeatures.Count; i++)
            {
                WorldPointFeatureData feature =
                    plan.PointFeatures[i];

                if (feature.kind ==
                    WorldFeatureKind.NeutralSettlement)
                {
                    settlements.Add(feature);
                }
            }

            int connectionCount =
                Mathf.Max(
                    0,
                    settings.nearestConnectionsPerSettlement);

            float maxDistance =
                Mathf.Max(
                    0f,
                    settings.maxConnectionDistance);

            float maxDistanceSqr =
                maxDistance * maxDistance;

            int salt = DeterministicHash.String32(
                "road_network_connection");

            for (int i = 0; i < settlements.Count; i++)
            {
                var candidates =
                    new List<Candidate>();

                for (int j = 0; j < settlements.Count; j++)
                {
                    if (i == j)
                        continue;

                    float distanceSqr =
                        (
                            settlements[i].worldPosition -
                            settlements[j].worldPosition
                        ).sqrMagnitude;

                    if (maxDistance > 0f &&
                        distanceSqr > maxDistanceSqr)
                    {
                        continue;
                    }

                    candidates.Add(
                        new Candidate
                        {
                            featureIndex = j,
                            distanceSqr = distanceSqr,
                            stableId =
                                settlements[j].stableId
                        });
                }

                candidates.Sort(
                    delegate(Candidate a, Candidate b)
                    {
                        int distanceCompare =
                            a.distanceSqr.CompareTo(
                                b.distanceSqr);

                        if (distanceCompare != 0)
                            return distanceCompare;

                        return a.stableId.CompareTo(
                            b.stableId);
                    });

                int take =
                    Mathf.Min(
                        connectionCount,
                        candidates.Count);

                for (int c = 0; c < take; c++)
                {
                    WorldPointFeatureData from =
                        settlements[i];

                    WorldPointFeatureData to =
                        settlements[
                            candidates[c].featureIndex];

                    long connectionId =
                        DeterministicHash.StablePairId(
                            worldSeed,
                            from.stableId,
                            to.stableId,
                            salt);

                    plan.AddRoadConnection(
                        new WorldRoadConnectionData(
                            connectionId,
                            from.stableId,
                            to.stableId,
                            settings.roadKind));
                }
            }
        }

        /// <summary>
        /// Opt-in logical backbone for every point feature. Existing edges form
        /// the initial components; shortest allowed edges join only components.
        /// Terrain and fixed-site validation still decide whether roads realize.
        /// </summary>
        public static void BuildConnectivityBackbone(
            int worldSeed,
            MacroWorldPlan plan,
            RoadNetworkPlannerSettings settings)
        {
            if (plan == null || settings == null || !settings.enabled ||
                plan.PointFeatures.Count < 2)
                return;

            var features = new List<WorldPointFeatureData>(plan.PointFeatures);
            features.Sort((a, b) => a.stableId.CompareTo(b.stableId));
            var parent = new Dictionary<long, long>();
            for (int i = 0; i < features.Count; i++)
                parent[features[i].stableId] = features[i].stableId;

            int components = features.Count;
            for (int i = 0; i < plan.RoadConnections.Count; i++)
            {
                WorldRoadConnectionData edge = plan.RoadConnections[i];
                if (parent.ContainsKey(edge.fromFeatureId) &&
                    parent.ContainsKey(edge.toFeatureId) &&
                    Union(parent, edge.fromFeatureId, edge.toFeatureId))
                    components--;
            }
            if (components <= 1)
                return;

            float maxDistance = Mathf.Max(0f, settings.maxConnectionDistance);
            float maxDistanceSqr = maxDistance * maxDistance;
            var candidates = new List<BackboneCandidate>();
            for (int i = 0; i < features.Count; i++)
            {
                for (int j = i + 1; j < features.Count; j++)
                {
                    float distanceSqr = (features[i].worldPosition -
                        features[j].worldPosition).sqrMagnitude;
                    if (maxDistance > 0f && distanceSqr > maxDistanceSqr)
                        continue;
                    candidates.Add(new BackboneCandidate
                    {
                        fromId = features[i].stableId,
                        toId = features[j].stableId,
                        distanceSqr = distanceSqr
                    });
                }
            }

            candidates.Sort((a, b) =>
            {
                int order = a.distanceSqr.CompareTo(b.distanceSqr);
                if (order != 0) return order;
                order = a.fromId.CompareTo(b.fromId);
                return order != 0 ? order : a.toId.CompareTo(b.toId);
            });

            int salt = DeterministicHash.String32("road_network_connection");
            for (int i = 0; i < candidates.Count && components > 1; i++)
            {
                BackboneCandidate candidate = candidates[i];
                if (Find(parent, candidate.fromId) ==
                    Find(parent, candidate.toId))
                    continue;

                long id = DeterministicHash.StablePairId(
                    worldSeed, candidate.fromId, candidate.toId, salt);
                if (!plan.AddRoadConnection(new WorldRoadConnectionData(
                    id, candidate.fromId, candidate.toId, settings.roadKind)))
                    continue;

                Union(parent, candidate.fromId, candidate.toId);
                components--;
            }
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

        private static bool Union(Dictionary<long, long> parent, long a, long b)
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
