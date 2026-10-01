using System;
using System.Collections.Generic;
using UnityEngine;

namespace LittleCastle.World
{
    /// <summary>
    /// Early coarse terrain-aware road solver.
    ///
    /// It converts logical WorldRoadConnectionData into WorldRoadData using
    /// deterministic A* on a coarse world grid. It is intentionally isolated so
    /// a faster/stronger solver can replace it later without changing road data.
    /// </summary>
    public static class TerrainRoadPathPlanner
    {
        private struct GridNode : IEquatable<GridNode>
        {
            public int x;
            public int z;

            public GridNode(int x, int z)
            {
                this.x = x;
                this.z = z;
            }

            public bool Equals(GridNode other)
            {
                return x == other.x && z == other.z;
            }

            public override bool Equals(object obj)
            {
                return obj is GridNode other &&
                       Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (x * 397) ^ z;
                }
            }
        }

        private static readonly int[] NeighborX =
        {
            -1, 0, 1,
            -1,    1,
            -1, 0, 1
        };

        private static readonly int[] NeighborZ =
        {
            -1, -1, -1,
             0,      0,
             1,  1,  1
        };

        public static void BuildRoadPaths(
            MacroWorldPlan plan,
            WorldTerrainProbe terrainProbe,
            TerrainRoadPathPlannerSettings settings)
        {
            if (plan == null ||
                terrainProbe == null ||
                settings == null ||
                !settings.enabled)
            {
                return;
            }

            for (int i = 0; i < plan.RoadConnections.Count; i++)
            {
                WorldRoadConnectionData connection =
                    plan.RoadConnections[i];

                if (!plan.TryGetPointFeature(
                        connection.fromFeatureId,
                        out WorldPointFeatureData from) ||
                    !plan.TryGetPointFeature(
                        connection.toFeatureId,
                        out WorldPointFeatureData to))
                {
                    continue;
                }

                List<Vector2> path =
                    Solve(
                        from.worldPosition,
                        to.worldPosition,
                        terrainProbe,
                        settings,
                        plan.Rivers);

                if (path == null || path.Count < 2)
                    continue;

                var road =
                    new WorldRoadData
                    {
                        stableId = connection.stableId,
                        roadKind = connection.roadKind,
                        width = Mathf.Max(
                            0.5f,
                            settings.GetWidth(
                                connection.roadKind))
                    };

                road.centerline.AddRange(path);
                plan.AddRoad(road);
            }
        }

        public static List<Vector2> Solve(
            Vector2 start,
            Vector2 end,
            WorldTerrainProbe terrainProbe,
            TerrainRoadPathPlannerSettings settings)
        {
            return Solve(
                start,
                end,
                terrainProbe,
                settings,
                null);
        }

        public static List<Vector2> Solve(
            Vector2 start,
            Vector2 end,
            WorldTerrainProbe terrainProbe,
            TerrainRoadPathPlannerSettings settings,
            IReadOnlyList<WorldRiverData> rivers)
        {
            float step =
                Mathf.Max(
                    4f,
                    settings.gridStep);

            GridNode startNode =
                ToGrid(start, step);

            GridNode endNode =
                ToGrid(end, step);

            float minWorldX =
                Mathf.Min(start.x, end.x) -
                Mathf.Max(0f, settings.searchPadding);

            float maxWorldX =
                Mathf.Max(start.x, end.x) +
                Mathf.Max(0f, settings.searchPadding);

            float minWorldZ =
                Mathf.Min(start.y, end.y) -
                Mathf.Max(0f, settings.searchPadding);

            float maxWorldZ =
                Mathf.Max(start.y, end.y) +
                Mathf.Max(0f, settings.searchPadding);

            int minX =
                Mathf.FloorToInt(minWorldX / step);

            int maxX =
                Mathf.CeilToInt(maxWorldX / step);

            int minZ =
                Mathf.FloorToInt(minWorldZ / step);

            int maxZ =
                Mathf.CeilToInt(maxWorldZ / step);

            var open =
                new List<GridNode>();

            var closed =
                new HashSet<GridNode>();

            var cameFrom =
                new Dictionary<GridNode, GridNode>();

            var gScore =
                new Dictionary<GridNode, float>();

            open.Add(startNode);
            gScore[startNode] = 0f;

            int expanded = 0;
            int maxExpanded =
                Mathf.Max(
                    100,
                    settings.maxExpandedNodes);

            while (open.Count > 0 &&
                   expanded < maxExpanded)
            {
                int bestIndex =
                    FindBestOpenIndex(
                        open,
                        gScore,
                        endNode,
                        step);

                GridNode current =
                    open[bestIndex];

                open.RemoveAt(bestIndex);

                if (closed.Contains(current))
                    continue;

                if (current.Equals(endNode))
                {
                    return BuildPath(
                        start,
                        end,
                        current,
                        cameFrom,
                        step);
                }

                closed.Add(current);
                expanded++;

                for (int n = 0; n < NeighborX.Length; n++)
                {
                    GridNode neighbor =
                        new GridNode(
                            current.x + NeighborX[n],
                            current.z + NeighborZ[n]);

                    if (neighbor.x < minX ||
                        neighbor.x > maxX ||
                        neighbor.z < minZ ||
                        neighbor.z > maxZ ||
                        closed.Contains(neighbor))
                    {
                        continue;
                    }

                    Vector2 neighborWorld =
                        ToWorld(neighbor, step);

                    WorldTerrainSample sample =
                        terrainProbe.Sample(neighborWorld);

                    if (sample.slope >
                        Mathf.Max(
                            0f,
                            settings.maxSlope))
                    {
                        continue;
                    }

                    bool diagonal =
                        NeighborX[n] != 0 &&
                        NeighborZ[n] != 0;

                    float moveDistance =
                        diagonal
                            ? step * 1.41421356f
                            : step;

                    float slopeRatio =
                        settings.maxSlope > 0.0001f
                            ? Mathf.Clamp01(
                                sample.slope /
                                settings.maxSlope)
                            : 0f;

                    float terrainMultiplier =
                        1f +
                        slopeRatio *
                        Mathf.Max(
                            0f,
                            settings.slopeCostMultiplier);

                    if (sample.terrainClass ==
                        TerrainClass.Highlands)
                    {
                        terrainMultiplier *=
                            Mathf.Max(
                                1f,
                                settings.highlandCostMultiplier);
                    }

                    float waterPenalty =
                        IsInsideRiverCorridor(
                            neighborWorld,
                            rivers,
                            settings.riverAvoidancePadding)
                            ? Mathf.Max(
                                0f,
                                settings.riverCrossingPenalty)
                            : 0f;

                    float tentative =
                        GetScore(
                            gScore,
                            current) +
                        moveDistance *
                        terrainMultiplier +
                        waterPenalty;

                    float previous =
                        GetScore(
                            gScore,
                            neighbor);

                    if (tentative >= previous)
                        continue;

                    cameFrom[neighbor] = current;
                    gScore[neighbor] = tentative;

                    if (!open.Contains(neighbor))
                        open.Add(neighbor);
                }
            }

            return null;
        }

        private static bool IsInsideRiverCorridor(
            Vector2 point,
            IReadOnlyList<WorldRiverData> rivers,
            float extraPadding)
        {
            if (rivers == null)
                return false;

            for (int r = 0; r < rivers.Count; r++)
            {
                WorldRiverData river = rivers[r];

                if (river == null ||
                    river.centerline.Count < 2)
                {
                    continue;
                }

                float radius =
                    Mathf.Max(
                        0.1f,
                        river.nominalWidth * 0.5f +
                        Mathf.Max(0f, extraPadding));

                float radiusSqr =
                    radius * radius;

                for (int i = 0;
                     i < river.centerline.Count - 1;
                     i++)
                {
                    if (DistancePointSegmentSqr(
                            point,
                            river.centerline[i],
                            river.centerline[i + 1]) <=
                        radiusSqr)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static float DistancePointSegmentSqr(
            Vector2 point,
            Vector2 a,
            Vector2 b)
        {
            Vector2 ab = b - a;
            float lengthSqr = ab.sqrMagnitude;

            if (lengthSqr <= 0.000001f)
                return (point - a).sqrMagnitude;

            float t =
                Mathf.Clamp01(
                    Vector2.Dot(
                        point - a,
                        ab) /
                    lengthSqr);

            Vector2 closest =
                a + ab * t;

            return
                (point - closest).sqrMagnitude;
        }

        private static int FindBestOpenIndex(
            List<GridNode> open,
            Dictionary<GridNode, float> gScore,
            GridNode goal,
            float step)
        {
            int bestIndex = 0;
            float bestF = float.PositiveInfinity;
            float bestH = float.PositiveInfinity;

            for (int i = 0; i < open.Count; i++)
            {
                GridNode node = open[i];
                float h = Heuristic(node, goal, step);
                float f = GetScore(gScore, node) + h;

                if (f < bestF - 0.0001f ||
                    (
                        Mathf.Abs(f - bestF) <= 0.0001f &&
                        (
                            h < bestH - 0.0001f ||
                            (
                                Mathf.Abs(h - bestH) <= 0.0001f &&
                                IsDeterministicallyBefore(
                                    node,
                                    open[bestIndex])
                            )
                        )
                    ))
                {
                    bestIndex = i;
                    bestF = f;
                    bestH = h;
                }
            }

            return bestIndex;
        }

        private static bool IsDeterministicallyBefore(
            GridNode a,
            GridNode b)
        {
            if (a.x != b.x)
                return a.x < b.x;

            return a.z < b.z;
        }

        private static float GetScore(
            Dictionary<GridNode, float> scores,
            GridNode node)
        {
            if (scores.TryGetValue(
                    node,
                    out float score))
            {
                return score;
            }

            return float.PositiveInfinity;
        }

        private static float Heuristic(
            GridNode a,
            GridNode b,
            float step)
        {
            float dx = (a.x - b.x) * step;
            float dz = (a.z - b.z) * step;

            return Mathf.Sqrt(
                dx * dx +
                dz * dz);
        }

        private static GridNode ToGrid(
            Vector2 world,
            float step)
        {
            return new GridNode(
                Mathf.RoundToInt(world.x / step),
                Mathf.RoundToInt(world.y / step));
        }

        private static Vector2 ToWorld(
            GridNode node,
            float step)
        {
            return new Vector2(
                node.x * step,
                node.z * step);
        }

        private static List<Vector2> BuildPath(
            Vector2 exactStart,
            Vector2 exactEnd,
            GridNode endNode,
            Dictionary<GridNode, GridNode> cameFrom,
            float step)
        {
            var reversed =
                new List<GridNode>();

            GridNode current = endNode;
            reversed.Add(current);

            while (cameFrom.TryGetValue(
                current,
                out GridNode parent))
            {
                current = parent;
                reversed.Add(current);
            }

            reversed.Reverse();

            var raw =
                new List<Vector2>();

            raw.Add(exactStart);

            for (int i = 1; i < reversed.Count - 1; i++)
                raw.Add(ToWorld(reversed[i], step));

            raw.Add(exactEnd);

            return SimplifyCollinear(raw);
        }

        private static List<Vector2> SimplifyCollinear(
            List<Vector2> points)
        {
            if (points.Count <= 2)
                return points;

            var result =
                new List<Vector2>();

            result.Add(points[0]);

            for (int i = 1; i < points.Count - 1; i++)
            {
                Vector2 a =
                    points[i] -
                    points[i - 1];

                Vector2 b =
                    points[i + 1] -
                    points[i];

                float cross =
                    a.x * b.y -
                    a.y * b.x;

                if (Mathf.Abs(cross) > 0.001f)
                    result.Add(points[i]);
            }

            result.Add(points[points.Count - 1]);
            return result;
        }
    }
}
