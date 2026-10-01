using System.Collections.Generic;
using UnityEngine;

namespace LittleCastle.World
{
    /// <summary>
    /// Deterministic downhill river-network tracer.
    ///
    /// Rivers are generated in stable source-grid order. A later tributary may
    /// join an already generated river. After tracing, relative flow is
    /// propagated through confluences and converted into local width/depth
    /// profiles along each river.
    ///
    /// This is still a lightweight procedural model rather than a full
    /// watershed simulation, but it preserves a data contract that can later
    /// be replaced by a more advanced hydrology solver.
    /// </summary>
    public static class RiverNetworkPlanner
    {
        private sealed class TraceResult
        {
            public readonly List<Vector2> path =
                new List<Vector2>();

            public long downstreamRiverId;
            public int downstreamJoinPointIndex = -1;
            public Vector2 confluencePosition;
        }

        public static void BuildRivers(
            int worldSeed,
            Rect sourceBounds,
            WorldTerrainProbe terrainProbe,
            RiverPlannerSettings settings,
            MacroWorldPlan plan)
        {
            if (terrainProbe == null ||
                settings == null ||
                !settings.enabled ||
                plan == null)
            {
                return;
            }

            float spacing =
                Mathf.Max(
                    100f,
                    settings.sourceSpacing);

            int salt =
                DeterministicHash.String32(
                    "river_source");

            int minGridX =
                Mathf.FloorToInt(
                    sourceBounds.xMin / spacing) - 1;

            int maxGridX =
                Mathf.FloorToInt(
                    sourceBounds.xMax / spacing) + 1;

            int minGridZ =
                Mathf.FloorToInt(
                    sourceBounds.yMin / spacing) - 1;

            int maxGridZ =
                Mathf.FloorToInt(
                    sourceBounds.yMax / spacing) + 1;

            for (int gz = minGridZ; gz <= maxGridZ; gz++)
            {
                for (int gx = minGridX; gx <= maxGridX; gx++)
                {
                    float roll =
                        DeterministicHash.Hash01(
                            worldSeed,
                            gx,
                            gz,
                            salt ^ 0x201);

                    if (roll > settings.sourceChance)
                        continue;

                    float jx =
                        DeterministicHash.Hash01(
                            worldSeed,
                            gx,
                            gz,
                            salt ^ 0x202);

                    float jz =
                        DeterministicHash.Hash01(
                            worldSeed,
                            gx,
                            gz,
                            salt ^ 0x203);

                    var source =
                        new Vector2(
                            (gx + jx) * spacing,
                            (gz + jz) * spacing);

                    if (!sourceBounds.Contains(source))
                        continue;

                    WorldTerrainSample sourceSample =
                        terrainProbe.Sample(source);

                    if (!settings.sourceTerrain.Contains(
                            sourceSample.terrainClass) ||
                        sourceSample.height <
                            settings.minSourceHeight ||
                        sourceSample.slope >
                            settings.maxSourceSlope)
                    {
                        continue;
                    }

                    long riverId =
                        DeterministicHash.StableId(
                            worldSeed,
                            gx,
                            gz,
                            salt);

                    TraceResult trace =
                        TraceRiver(
                            worldSeed,
                            riverId,
                            source,
                            terrainProbe,
                            settings,
                            plan.Rivers);

                    if (trace == null ||
                        trace.path.Count <
                            Mathf.Max(
                                2,
                                settings.minimumPoints))
                    {
                        continue;
                    }

                    var river =
                        new WorldRiverData
                        {
                            stableId = riverId,
                            nominalWidth =
                                Mathf.Max(
                                    0.5f,
                                    settings.nominalWidth),
                            nominalDepth =
                                Mathf.Max(
                                    0.1f,
                                    settings.nominalDepth),
                            downstreamRiverId =
                                trace.downstreamRiverId,
                            downstreamJoinPointIndex =
                                trace.downstreamJoinPointIndex,
                            confluencePosition =
                                trace.confluencePosition
                        };

                    river.centerline.AddRange(
                        trace.path);

                    plan.AddRiver(river);
                }
            }

            BuildFlowProfiles(
                plan.Rivers,
                settings);
        }

        private static TraceResult TraceRiver(
            int worldSeed,
            long riverId,
            Vector2 source,
            WorldTerrainProbe terrainProbe,
            RiverPlannerSettings settings,
            IReadOnlyList<WorldRiverData> existingRivers)
        {
            float step =
                Mathf.Max(
                    4f,
                    settings.traceStep);

            int directionCount =
                Mathf.Clamp(
                    settings.directionSamples,
                    6,
                    32);

            int maxSteps =
                Mathf.Max(
                    2,
                    settings.maxSteps);

            var result =
                new TraceResult();

            var visited =
                new HashSet<long>();

            Vector2 current = source;

            WorldTerrainSample currentSample =
                terrainProbe.Sample(current);

            float initialAngle =
                StableUnitFromLong(riverId) *
                Mathf.PI *
                2f;

            Vector2 previousDirection =
                new Vector2(
                    Mathf.Cos(initialAngle),
                    Mathf.Sin(initialAngle));

            result.path.Add(current);

            visited.Add(
                TraceCellKey(
                    current,
                    step));

            for (int stepIndex = 0;
                 stepIndex < maxSteps;
                 stepIndex++)
            {
                if (currentSample.height <=
                    settings.stopHeight)
                {
                    break;
                }

                bool found = false;
                Vector2 bestPosition = current;
                Vector2 bestDirection = previousDirection;
                WorldTerrainSample bestSample = currentSample;
                float bestScore = float.PositiveInfinity;

                for (int d = 0; d < directionCount; d++)
                {
                    float angle =
                        (Mathf.PI * 2f * d) /
                        directionCount;

                    var direction =
                        new Vector2(
                            Mathf.Cos(angle),
                            Mathf.Sin(angle));

                    Vector2 candidate =
                        current +
                        direction * step;

                    long candidateKey =
                        TraceCellKey(
                            candidate,
                            step);

                    if (visited.Contains(candidateKey))
                        continue;

                    WorldTerrainSample sample =
                        terrainProbe.Sample(candidate);

                    float rise =
                        sample.height -
                        currentSample.height;

                    if (rise >
                        settings.maxAllowedRise)
                    {
                        continue;
                    }

                    float turnAmount =
                        1f -
                        Mathf.Clamp(
                            Vector2.Dot(
                                previousDirection,
                                direction),
                            -1f,
                            1f);

                    float score =
                        sample.height +
                        turnAmount *
                        Mathf.Max(
                            0f,
                            settings.turnPenalty);

                    if (rise <=
                        -Mathf.Max(
                            0f,
                            settings.minimumPreferredDrop))
                    {
                        score -= 0.25f;
                    }

                    score +=
                        DeterministicHash.Hash01(
                            worldSeed,
                            stepIndex,
                            d,
                            (int)riverId) *
                        0.0001f;

                    if (score < bestScore)
                    {
                        found = true;
                        bestScore = score;
                        bestPosition = candidate;
                        bestDirection = direction;
                        bestSample = sample;
                    }
                }

                if (!found)
                    break;

                current = bestPosition;
                currentSample = bestSample;
                previousDirection = bestDirection;

                long key =
                    TraceCellKey(
                        current,
                        step);

                if (!visited.Add(key))
                    break;

                result.path.Add(current);

                if (result.path.Count >=
                        Mathf.Max(
                            2,
                            settings.minimumStepsBeforeMerge + 1) &&
                    TryFindConfluence(
                        current,
                        existingRivers,
                        settings.mergeDistance,
                        out WorldRiverData downstream,
                        out int downstreamPointIndex,
                        out Vector2 confluence))
                {
                    if ((result.path[result.path.Count - 1] -
                         confluence).sqrMagnitude >
                        0.0001f)
                    {
                        result.path.Add(confluence);
                    }

                    result.downstreamRiverId =
                        downstream.stableId;

                    result.downstreamJoinPointIndex =
                        downstreamPointIndex;

                    result.confluencePosition =
                        confluence;

                    break;
                }
            }

            return result;
        }

        private static bool TryFindConfluence(
            Vector2 point,
            IReadOnlyList<WorldRiverData> rivers,
            float mergeDistance,
            out WorldRiverData downstreamRiver,
            out int downstreamPointIndex,
            out Vector2 confluencePosition)
        {
            downstreamRiver = null;
            downstreamPointIndex = -1;
            confluencePosition = point;

            if (rivers == null ||
                rivers.Count == 0 ||
                mergeDistance <= 0f)
            {
                return false;
            }

            float maxDistanceSqr =
                mergeDistance *
                mergeDistance;

            float bestDistanceSqr =
                maxDistanceSqr;

            for (int r = 0; r < rivers.Count; r++)
            {
                WorldRiverData river =
                    rivers[r];

                if (river == null ||
                    river.centerline == null ||
                    river.centerline.Count < 2)
                {
                    continue;
                }

                for (int s = 0;
                     s < river.centerline.Count - 1;
                     s++)
                {
                    Vector2 closest;

                    float distanceSqr =
                        DistancePointSegmentSqr(
                            point,
                            river.centerline[s],
                            river.centerline[s + 1],
                            out closest,
                            out float segmentT);

                    if (distanceSqr >
                        bestDistanceSqr)
                    {
                        continue;
                    }

                    // Flow joins after the geometric confluence. Using the
                    // downstream endpoint keeps tributary contribution from
                    // artificially widening the upstream side of this segment.
                    int joinPointIndex =
                        Mathf.Min(
                            s + 1,
                            river.centerline.Count - 1);

                    if (distanceSqr <
                            bestDistanceSqr -
                            0.0001f ||
                        downstreamRiver == null ||
                        river.stableId <
                            downstreamRiver.stableId)
                    {
                        bestDistanceSqr =
                            distanceSqr;

                        downstreamRiver =
                            river;

                        downstreamPointIndex =
                            joinPointIndex;

                        confluencePosition =
                            closest;
                    }
                }
            }

            return downstreamRiver != null;
        }

        private static void BuildFlowProfiles(
            IReadOnlyList<WorldRiverData> rivers,
            RiverPlannerSettings settings)
        {
            if (rivers == null ||
                settings == null)
            {
                return;
            }

            float baseFlow =
                Mathf.Max(
                    0.01f,
                    settings.baseSourceFlow);

            float naturalGain =
                Mathf.Max(
                    0f,
                    settings.downstreamBaseFlowGain);

            for (int r = 0; r < rivers.Count; r++)
            {
                WorldRiverData river =
                    rivers[r];

                if (river == null ||
                    river.centerline == null)
                {
                    continue;
                }

                river.flow.Clear();
                river.widths.Clear();
                river.depths.Clear();

                int count =
                    river.centerline.Count;

                if (count == 0)
                    continue;

                float totalLength =
                    CalculateTotalLength(
                        river.centerline);

                float travelled = 0f;

                for (int i = 0; i < count; i++)
                {
                    if (i > 0)
                    {
                        travelled +=
                            Vector2.Distance(
                                river.centerline[i - 1],
                                river.centerline[i]);
                    }

                    float progress =
                        totalLength > 0.0001f
                            ? Mathf.Clamp01(
                                travelled /
                                totalLength)
                            : 0f;

                    river.flow.Add(
                        baseFlow *
                        (1f +
                         progress *
                         naturalGain));
                }
            }

            // A river can only join one that already existed when it was traced.
            // Reverse generation order therefore propagates nested tributary
            // contribution before the receiving river contributes farther down.
            for (int r = rivers.Count - 1;
                 r >= 0;
                 r--)
            {
                WorldRiverData tributary =
                    rivers[r];

                if (tributary == null ||
                    !tributary.HasConfluence)
                {
                    continue;
                }

                WorldRiverData downstream =
                    FindRiverById(
                        rivers,
                        tributary.downstreamRiverId);

                if (downstream == null ||
                    downstream.flow == null ||
                    downstream.flow.Count == 0)
                {
                    continue;
                }

                int joinIndex =
                    Mathf.Clamp(
                        tributary.downstreamJoinPointIndex,
                        0,
                        downstream.flow.Count - 1);

                float contribution =
                    tributary.GetTerminalFlow();

                for (int i = joinIndex;
                     i < downstream.flow.Count;
                     i++)
                {
                    downstream.flow[i] +=
                        contribution;
                }
            }

            for (int r = 0; r < rivers.Count; r++)
            {
                BuildChannelProfile(
                    rivers[r],
                    settings);
            }
        }

        private static void BuildChannelProfile(
            WorldRiverData river,
            RiverPlannerSettings settings)
        {
            if (river == null ||
                river.centerline == null ||
                river.centerline.Count == 0)
            {
                return;
            }

            river.widths.Clear();
            river.depths.Clear();

            float totalLength =
                CalculateTotalLength(
                    river.centerline);

            float travelled = 0f;

            for (int i = 0;
                 i < river.centerline.Count;
                 i++)
            {
                if (i > 0)
                {
                    travelled +=
                        Vector2.Distance(
                            river.centerline[i - 1],
                            river.centerline[i]);
                }

                float progress =
                    totalLength > 0.0001f
                        ? Mathf.Clamp01(
                            travelled /
                            totalLength)
                        : 0f;

                float flow =
                    Mathf.Max(
                        0.01f,
                        river.GetFlowAtPoint(i));

                float widthProgressMultiplier =
                    Mathf.Lerp(
                        Mathf.Max(
                            0.05f,
                            settings.headwaterWidthMultiplier),
                        Mathf.Max(
                            0.05f,
                            settings.downstreamWidthMultiplier),
                        progress);

                float depthProgressMultiplier =
                    Mathf.Lerp(
                        Mathf.Max(
                            0.05f,
                            settings.headwaterDepthMultiplier),
                        Mathf.Max(
                            0.05f,
                            settings.downstreamDepthMultiplier),
                        progress);

                float widthFlowMultiplier =
                    Mathf.Pow(
                        flow,
                        Mathf.Clamp01(
                            settings.flowWidthExponent));

                float depthFlowMultiplier =
                    Mathf.Pow(
                        flow,
                        Mathf.Clamp01(
                            settings.flowDepthExponent));

                river.widths.Add(
                    Mathf.Max(
                        0.1f,
                        river.nominalWidth *
                        widthProgressMultiplier *
                        widthFlowMultiplier));

                river.depths.Add(
                    Mathf.Max(
                        0.01f,
                        river.nominalDepth *
                        depthProgressMultiplier *
                        depthFlowMultiplier));
            }
        }

        private static WorldRiverData FindRiverById(
            IReadOnlyList<WorldRiverData> rivers,
            long stableId)
        {
            for (int i = 0; i < rivers.Count; i++)
            {
                WorldRiverData river =
                    rivers[i];

                if (river != null &&
                    river.stableId == stableId)
                {
                    return river;
                }
            }

            return null;
        }

        private static float CalculateTotalLength(
            IReadOnlyList<Vector2> points)
        {
            float length = 0f;

            if (points == null)
                return length;

            for (int i = 1; i < points.Count; i++)
            {
                length +=
                    Vector2.Distance(
                        points[i - 1],
                        points[i]);
            }

            return length;
        }

        private static long TraceCellKey(
            Vector2 position,
            float step)
        {
            int x =
                Mathf.RoundToInt(
                    position.x / step);

            int z =
                Mathf.RoundToInt(
                    position.y / step);

            unchecked
            {
                return
                    ((long)x << 32) |
                    (uint)z;
            }
        }

        private static float DistancePointSegmentSqr(
            Vector2 point,
            Vector2 a,
            Vector2 b,
            out Vector2 closest,
            out float segmentT)
        {
            Vector2 ab = b - a;
            float lengthSqr = ab.sqrMagnitude;

            if (lengthSqr <= 0.000001f)
            {
                segmentT = 0f;
                closest = a;
                return (point - a).sqrMagnitude;
            }

            segmentT =
                Mathf.Clamp01(
                    Vector2.Dot(
                        point - a,
                        ab) /
                    lengthSqr);

            closest =
                a +
                ab *
                segmentT;

            return
                (point - closest).sqrMagnitude;
        }

        private static float StableUnitFromLong(
            long value)
        {
            unchecked
            {
                ulong v = (ulong)value;

                uint mixed =
                    (uint)(
                        v ^
                        (v >> 32));

                return
                    (mixed & 0x00FFFFFFu) /
                    16777215f;
            }
        }
    }
}
