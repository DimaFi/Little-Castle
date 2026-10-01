using UnityEngine;

namespace LittleCastle.World
{
    /// <summary>
    /// Finds geometric road/river crossings and records bridge candidates.
    /// </summary>
    public static class BridgeSitePlanner
    {
        public static void BuildBridgeSites(
            int worldSeed,
            MacroWorldPlan plan,
            BridgePlannerSettings settings)
        {
            if (plan == null ||
                settings == null ||
                !settings.enabled)
            {
                return;
            }

            int salt =
                DeterministicHash.String32(
                    "bridge_site");

            for (int r = 0; r < plan.Roads.Count; r++)
            {
                WorldRoadData road =
                    plan.Roads[r];

                if (road == null ||
                    road.centerline.Count < 2)
                {
                    continue;
                }

                for (int w = 0; w < plan.Rivers.Count; w++)
                {
                    WorldRiverData river =
                        plan.Rivers[w];

                    if (river == null ||
                        river.centerline.Count < 2)
                    {
                        continue;
                    }

                    for (int rs = 0;
                         rs < road.centerline.Count - 1;
                         rs++)
                    {
                        Vector2 roadA =
                            road.centerline[rs];

                        Vector2 roadB =
                            road.centerline[rs + 1];

                        for (int ws = 0;
                             ws < river.centerline.Count - 1;
                             ws++)
                        {
                            Vector2 riverA =
                                river.centerline[ws];

                            Vector2 riverB =
                                river.centerline[ws + 1];

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

                            long pairId =
                                DeterministicHash.StablePairId(
                                    worldSeed,
                                    road.stableId,
                                    river.stableId,
                                    salt);

                            int segmentSalt =
                                salt ^
                                (rs * 73856093) ^
                                (ws * 19349663) ^
                                (int)pairId;

                            long bridgeId =
                                DeterministicHash.StableId(
                                    worldSeed,
                                    rs,
                                    ws,
                                    segmentSalt);

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

            float denominator =
                Cross(r, s);

            if (Mathf.Abs(denominator) <
                0.000001f)
            {
                intersection = default(Vector2);
                roadT = 0f;
                riverT = 0f;
                return false;
            }

            Vector2 qp = q - p;

            float t =
                Cross(qp, s) /
                denominator;

            float u =
                Cross(qp, r) /
                denominator;

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

        private static float Cross(
            Vector2 a,
            Vector2 b)
        {
            return
                a.x * b.y -
                a.y * b.x;
        }
    }
}
