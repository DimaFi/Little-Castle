using System;
using System.Collections.Generic;
using UnityEngine;

namespace LittleCastle.World
{
    /// <summary>
    /// Local accessibility probe used by player-start fairness.
    ///
    /// It estimates how many meaningfully different directions a player can
    /// leave the starting area without being trapped by steep terrain or a
    /// river crossing that has no generated bridge nearby.
    /// </summary>
    public static class WorldStartAccessEvaluator
    {
        public static int CountExitRoutes(
            Vector2 center,
            Rect playableRect,
            WorldStartFairnessSettings settings,
            WorldSessionStartOptions options,
            MacroWorldPlan macroPlan,
            WorldGenerationSettings generationSettings,
            Func<Vector2, WorldChunkData> getChunk)
        {
            if (settings == null ||
                generationSettings == null ||
                getChunk == null)
            {
                return 0;
            }

            int directionCount =
                settings.ExitDirectionSamples;

            var open =
                new bool[
                    directionCount];

            for (int i = 0;
                 i < directionCount;
                 i++)
            {
                float angle =
                    Mathf.PI *
                    2f *
                    i /
                    directionCount;

                Vector2 direction =
                    new Vector2(
                        Mathf.Cos(angle),
                        Mathf.Sin(angle));

                open[i] =
                    IsDirectionOpen(
                        center,
                        direction,
                        playableRect,
                        settings,
                        options,
                        macroPlan,
                        generationSettings,
                        getChunk);
            }

            return CountIndependentRoutes(
                open,
                settings.DirectionSamplesPerExitRoute);
        }

        private static bool IsDirectionOpen(
            Vector2 center,
            Vector2 direction,
            Rect playableRect,
            WorldStartFairnessSettings settings,
            WorldSessionStartOptions options,
            MacroWorldPlan macroPlan,
            WorldGenerationSettings generationSettings,
            Func<Vector2, WorldChunkData> getChunk)
        {
            float radius =
                settings.ExitProbeRadius;

            float step =
                settings.ExitSampleStep;

            int steps =
                Mathf.Max(
                    1,
                    Mathf.CeilToInt(
                        radius /
                        step));

            Vector2 previous =
                center;

            for (int i = 1;
                 i <= steps;
                 i++)
            {
                float distance =
                    Mathf.Min(
                        radius,
                        i * step);

                Vector2 point =
                    center +
                    direction *
                    distance;

                if (!playableRect.Contains(
                        point))
                {
                    return false;
                }

                WorldChunkData chunk =
                    getChunk(
                        point);

                float slope =
                    WorldChunkSampling.SampleSlope(
                        chunk,
                        generationSettings,
                        point.x,
                        point.y);

                if (slope >
                    settings.ExitMaximumSlope)
                {
                    return false;
                }

                if (options != null &&
                    options.riversRequireBridgeForExit &&
                    macroPlan != null &&
                    CrossesUnbridgedRiver(
                        previous,
                        point,
                        macroPlan,
                        settings.BridgeAcceptancePadding))
                {
                    return false;
                }

                previous = point;
            }

            return true;
        }

        private static bool CrossesUnbridgedRiver(
            Vector2 a,
            Vector2 b,
            MacroWorldPlan macroPlan,
            float bridgePadding)
        {
            for (int r = 0;
                 r < macroPlan.Rivers.Count;
                 r++)
            {
                WorldRiverData river =
                    macroPlan.Rivers[r];

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
                    if (!TrySegmentIntersection(
                            a,
                            b,
                            river.centerline[s],
                            river.centerline[s + 1],
                            out Vector2 intersection))
                    {
                        continue;
                    }

                    if (!HasBridgeNear(
                            intersection,
                            river.stableId,
                            macroWorldPlan: macroPlan,
                            extraPadding: bridgePadding))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool HasBridgeNear(
            Vector2 point,
            long riverId,
            MacroWorldPlan macroWorldPlan,
            float extraPadding)
        {
            for (int i = 0;
                 i < macroWorldPlan.BridgeSites.Count;
                 i++)
            {
                WorldBridgeSiteData bridge =
                    macroWorldPlan.BridgeSites[i];

                if (bridge.riverId !=
                    riverId)
                {
                    continue;
                }

                float acceptanceRadius =
                    Mathf.Max(
                        8f,
                        bridge.requiredSpan *
                        0.75f +
                        Mathf.Max(
                            0f,
                            extraPadding));

                if ((bridge.worldPosition -
                     point).sqrMagnitude <=
                    acceptanceRadius *
                    acceptanceRadius)
                {
                    return true;
                }
            }

            return false;
        }

        private static int CountIndependentRoutes(
            bool[] open,
            int samplesPerRoute)
        {
            if (open == null ||
                open.Length == 0)
            {
                return 0;
            }

            int safeSamples =
                Mathf.Max(
                    1,
                    samplesPerRoute);

            int openCount = 0;

            for (int i = 0;
                 i < open.Length;
                 i++)
            {
                if (open[i])
                    openCount++;
            }

            if (openCount == 0)
                return 0;

            if (openCount ==
                open.Length)
            {
                return
                    Mathf.Max(
                        1,
                        Mathf.CeilToInt(
                            openCount /
                            (float)safeSamples));
            }

            int firstClosed = -1;

            for (int i = 0;
                 i < open.Length;
                 i++)
            {
                if (!open[i])
                {
                    firstClosed = i;
                    break;
                }
            }

            int routes = 0;
            int runLength = 0;

            for (int offset = 1;
                 offset <= open.Length;
                 offset++)
            {
                int index =
                    (firstClosed + offset) %
                    open.Length;

                if (open[index])
                {
                    runLength++;
                    continue;
                }

                if (runLength > 0)
                {
                    routes +=
                        Mathf.CeilToInt(
                            runLength /
                            (float)safeSamples);

                    runLength = 0;
                }
            }

            if (runLength > 0)
            {
                routes +=
                    Mathf.CeilToInt(
                        runLength /
                        (float)safeSamples);
            }

            return routes;
        }

        private static bool TrySegmentIntersection(
            Vector2 p,
            Vector2 p2,
            Vector2 q,
            Vector2 q2,
            out Vector2 intersection)
        {
            Vector2 r =
                p2 - p;

            Vector2 s =
                q2 - q;

            float denominator =
                Cross(
                    r,
                    s);

            if (Mathf.Abs(
                    denominator) <=
                0.000001f)
            {
                intersection =
                    default(Vector2);

                return false;
            }

            Vector2 qp =
                q - p;

            float t =
                Cross(
                    qp,
                    s) /
                denominator;

            float u =
                Cross(
                    qp,
                    r) /
                denominator;

            if (t < 0f ||
                t > 1f ||
                u < 0f ||
                u > 1f)
            {
                intersection =
                    default(Vector2);

                return false;
            }

            intersection =
                p +
                r *
                t;

            return true;
        }

        private static float Cross(
            Vector2 a,
            Vector2 b)
        {
            return
                a.x *
                b.y -
                a.y *
                b.x;
        }
    }
}
