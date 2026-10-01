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
    }
}
