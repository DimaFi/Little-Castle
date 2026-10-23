using System.Collections.Generic;
using UnityEngine;

namespace LittleCastle.World
{
    /// <summary>
    /// Finds geometric road/river crossings and records bridge candidates.
    /// </summary>
    public static class BridgeSitePlanner
    {
        private sealed class RiverSegmentSpatialIndex
        {
            private const float CellSize = 64f;

            private readonly Dictionary<Vector2Int, List<int>> buckets =
                new Dictionary<Vector2Int, List<int>>();

            private readonly HashSet<int> seen =
                new HashSet<int>();

            public RiverSegmentSpatialIndex(
                WorldRiverData river)
            {
                if (river == null ||
                    river.centerline == null)
                {
                    return;
                }

                for (int i = 0;
                     i < river.centerline.Count - 1;
                     i++)
                {
                    AddSegment(
                        i,
                        river.centerline[i],
                        river.centerline[i + 1]);
                }
            }

            public void Query(
                Vector2 a,
                Vector2 b,
                List<int> output)
            {
                output.Clear();
                seen.Clear();

                GetCellBounds(
                    a,
                    b,
                    out int minX,
                    out int maxX,
                    out int minZ,
                    out int maxZ);

                for (int z = minZ;
                     z <= maxZ;
                     z++)
                {
                    for (int x = minX;
                         x <= maxX;
                         x++)
                    {
                        if (!buckets.TryGetValue(
                                new Vector2Int(x, z),
                                out List<int> bucket))
                        {
                            continue;
                        }

                        for (int i = 0;
                             i < bucket.Count;
                             i++)
                        {
                            int segmentIndex =
                                bucket[i];

                            if (seen.Add(
                                    segmentIndex))
                            {
                                output.Add(
                                    segmentIndex);
                            }
                        }
                    }
                }

                output.Sort();
            }

            private void AddSegment(
                int segmentIndex,
                Vector2 a,
                Vector2 b)
            {
                GetCellBounds(
                    a,
                    b,
                    out int minX,
                    out int maxX,
                    out int minZ,
                    out int maxZ);

                for (int z = minZ;
                     z <= maxZ;
                     z++)
                {
                    for (int x = minX;
                         x <= maxX;
                         x++)
                    {
                        var key =
                            new Vector2Int(
                                x,
                                z);

                        if (!buckets.TryGetValue(
                                key,
                                out List<int> bucket))
                        {
                            bucket =
                                new List<int>();

                            buckets.Add(
                                key,
                                bucket);
                        }

                        bucket.Add(
                            segmentIndex);
                    }
                }
            }

            private static void GetCellBounds(
                Vector2 a,
                Vector2 b,
                out int minX,
                out int maxX,
                out int minZ,
                out int maxZ)
            {
                minX =
                    Mathf.FloorToInt(
                        Mathf.Min(
                            a.x,
                            b.x) /
                        CellSize);

                maxX =
                    Mathf.FloorToInt(
                        Mathf.Max(
                            a.x,
                            b.x) /
                        CellSize);

                minZ =
                    Mathf.FloorToInt(
                        Mathf.Min(
                            a.y,
                            b.y) /
                        CellSize);

                maxZ =
                    Mathf.FloorToInt(
                        Mathf.Max(
                            a.y,
                            b.y) /
                        CellSize);
            }
        }

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

            var riverIndexes =
                new RiverSegmentSpatialIndex[
                    plan.Rivers.Count];

            for (int i = 0;
                 i < plan.Rivers.Count;
                 i++)
            {
                riverIndexes[i] =
                    new RiverSegmentSpatialIndex(
                        plan.Rivers[i]);
            }

            var candidateRiverSegments =
                new List<int>();

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

                        RiverSegmentSpatialIndex riverIndex =
                            riverIndexes[w];

                        if (riverIndex == null)
                            continue;

                        riverIndex.Query(
                            roadA,
                            roadB,
                            candidateRiverSegments);

                        for (int candidateIndex = 0;
                             candidateIndex <
                                candidateRiverSegments.Count;
                             candidateIndex++)
                        {
                            int ws =
                                candidateRiverSegments[
                                    candidateIndex];

                            if (ws < 0 ||
                                ws >=
                                    river.centerline.Count - 1)
                            {
                                continue;
                            }

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

                            float requiredSpan;

                            if (settings.standardizeCrossings)
                            {
                                NormalizeRiverCrossing(
                                    river,
                                    ws,
                                    settings);

                                requiredSpan =
                                    Mathf.Max(
                                        settings.standardRiverCrossingWidth,
                                        settings.standardBridgeSpan);
                            }
                            else
                            {
                                requiredSpan =
                                    Mathf.Max(
                                        0.5f,
                                        river.GetWidthAtSegment(
                                            ws,
                                            riverT) +
                                        settings.extraSpan);
                            }

                            plan.AddBridgeSite(
                                new WorldBridgeSiteData(
                                    bridgeId,
                                    road.stableId,
                                    river.stableId,
                                    settings.archetypeId,
                                    intersection,
                                    yaw,
                                    requiredSpan));
                        }
                    }
                }
            }
        }

        private static void NormalizeRiverCrossing(
            WorldRiverData river,
            int segmentIndex,
            BridgePlannerSettings settings)
        {
            if (river == null ||
                river.centerline == null ||
                river.centerline.Count < 2)
            {
                return;
            }

            float targetWidth =
                Mathf.Max(
                    0.5f,
                    settings.standardRiverCrossingWidth);

            if (river.widths == null)
                return;

            if (river.widths.Count !=
                river.centerline.Count)
            {
                river.widths.Clear();

                for (int i = 0;
                     i < river.centerline.Count;
                     i++)
                {
                    river.widths.Add(
                        Mathf.Max(
                            0.1f,
                            river.nominalWidth));
                }
            }

            int a =
                Mathf.Clamp(
                    segmentIndex,
                    0,
                    river.widths.Count - 2);

            int b = a + 1;

            river.widths[a] =
                targetWidth;

            river.widths[b] =
                targetWidth;

            int radius =
                Mathf.Max(
                    0,
                    settings.crossingWidthBlendPointRadius);

            for (int distance = 1;
                 distance <= radius;
                 distance++)
            {
                float t =
                    1f -
                    (float)distance /
                    (radius + 1f);

                int left = a - distance;
                int right = b + distance;

                if (left >= 0)
                {
                    river.widths[left] =
                        Mathf.Lerp(
                            river.widths[left],
                            targetWidth,
                            t);
                }

                if (right < river.widths.Count)
                {
                    river.widths[right] =
                        Mathf.Lerp(
                            river.widths[right],
                            targetWidth,
                            t);
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
