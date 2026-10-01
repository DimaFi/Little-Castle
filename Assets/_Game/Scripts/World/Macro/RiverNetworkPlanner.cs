using System.Collections.Generic;
using UnityEngine;

namespace LittleCastle.World
{
    /// <summary>
    /// Early deterministic downhill river tracer.
    ///
    /// It is deliberately isolated from chunk rendering. A future hydrology
    /// solver can replace it while preserving WorldRiverData.
    /// </summary>
    public static class RiverNetworkPlanner
    {
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

                    List<Vector2> path =
                        TraceRiver(
                            worldSeed,
                            riverId,
                            source,
                            terrainProbe,
                            settings);

                    if (path == null ||
                        path.Count <
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
                                    settings.nominalDepth)
                        };

                    river.centerline.AddRange(path);
                    plan.AddRiver(river);
                }
            }
        }

        private static List<Vector2> TraceRiver(
            int worldSeed,
            long riverId,
            Vector2 source,
            WorldTerrainProbe terrainProbe,
            RiverPlannerSettings settings)
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
                new List<Vector2>();

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

            result.Add(current);
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

                    // Stable microscopic tie-breaker.
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

                result.Add(current);
            }

            return result;
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
