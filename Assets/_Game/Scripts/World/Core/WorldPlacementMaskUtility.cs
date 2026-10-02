using System.Collections.Generic;
using UnityEngine;

namespace LittleCastle.World
{
    public static class WorldPlacementMaskUtility
    {
        public static void BlockCircle(
            WorldChunkData chunk,
            WorldGenerationSettings settings,
            Vector2 center,
            float radius,
            PlacementBlockFlags flags)
        {
            if (chunk == null ||
                settings == null ||
                flags == PlacementBlockFlags.None)
            {
                return;
            }

            float safeRadius =
                Mathf.Max(
                    0f,
                    radius);

            if (!TryGetCellBoundsForWorldRect(
                    chunk,
                    settings,
                    center.x - safeRadius,
                    center.y - safeRadius,
                    center.x + safeRadius,
                    center.y + safeRadius,
                    out int minCellX,
                    out int minCellZ,
                    out int maxCellX,
                    out int maxCellZ))
            {
                return;
            }

            float cellSize =
                settings.CellWorldSize;

            float chunkSize =
                settings.ChunkWorldSize;

            float originX =
                chunk.Coordinate.x *
                chunkSize;

            float originZ =
                chunk.Coordinate.z *
                chunkSize;

            float radiusSqr =
                safeRadius *
                safeRadius;

            for (int z = minCellZ;
                 z <= maxCellZ;
                 z++)
            {
                float worldZ =
                    originZ +
                    (z + 0.5f) *
                    cellSize;

                for (int x = minCellX;
                     x <= maxCellX;
                     x++)
                {
                    float worldX =
                        originX +
                        (x + 0.5f) *
                        cellSize;

                    float dx =
                        worldX -
                        center.x;

                    float dz =
                        worldZ -
                        center.y;

                    if (dx * dx +
                        dz * dz <=
                        radiusSqr)
                    {
                        chunk.AddPlacementBlocks(
                            x,
                            z,
                            flags);
                    }
                }
            }
        }

        public static void BlockPolyline(
            WorldChunkData chunk,
            WorldGenerationSettings settings,
            IReadOnlyList<Vector2> points,
            float halfWidth,
            PlacementBlockFlags flags)
        {
            if (chunk == null ||
                settings == null ||
                points == null ||
                points.Count < 2 ||
                flags == PlacementBlockFlags.None)
            {
                return;
            }

            float safeHalfWidth =
                Mathf.Max(
                    0f,
                    halfWidth);

            if (!TryGetExpandedPolylineCellBounds(
                    chunk,
                    settings,
                    points,
                    safeHalfWidth,
                    out int minCellX,
                    out int minCellZ,
                    out int maxCellX,
                    out int maxCellZ))
            {
                return;
            }

            float cellSize =
                settings.CellWorldSize;

            float chunkSize =
                settings.ChunkWorldSize;

            float originX =
                chunk.Coordinate.x *
                chunkSize;

            float originZ =
                chunk.Coordinate.z *
                chunkSize;

            float maxDistanceSqr =
                safeHalfWidth *
                safeHalfWidth;

            for (int z = minCellZ;
                 z <= maxCellZ;
                 z++)
            {
                float worldZ =
                    originZ +
                    (z + 0.5f) *
                    cellSize;

                for (int x = minCellX;
                     x <= maxCellX;
                     x++)
                {
                    var cellCenter =
                        new Vector2(
                            originX +
                            (x + 0.5f) *
                            cellSize,
                            worldZ);

                    for (int p = 0;
                         p < points.Count - 1;
                         p++)
                    {
                        if (!SegmentExpandedBoundsContainPoint(
                                points[p],
                                points[p + 1],
                                safeHalfWidth,
                                cellCenter))
                        {
                            continue;
                        }

                        float distanceSqr =
                            DistancePointSegmentSqr(
                                cellCenter,
                                points[p],
                                points[p + 1],
                                out float ignoredT);

                        if (distanceSqr <=
                            maxDistanceSqr)
                        {
                            chunk.AddPlacementBlocks(
                                x,
                                z,
                                flags);

                            break;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Blocks placement around a polyline whose full width varies at each
        /// centerline point. Width is linearly interpolated along segments.
        /// </summary>
        public static void BlockVariableWidthPolyline(
            WorldChunkData chunk,
            WorldGenerationSettings settings,
            IReadOnlyList<Vector2> points,
            IReadOnlyList<float> fullWidths,
            float extraClearance,
            PlacementBlockFlags flags)
        {
            if (chunk == null ||
                settings == null ||
                points == null ||
                points.Count < 2 ||
                flags == PlacementBlockFlags.None)
            {
                return;
            }

            bool hasProfile =
                fullWidths != null &&
                fullWidths.Count ==
                points.Count;

            float safeClearance =
                Mathf.Max(
                    0f,
                    extraClearance);

            float maximumHalfWidth =
                safeClearance;

            if (hasProfile)
            {
                for (int i = 0;
                     i < fullWidths.Count;
                     i++)
                {
                    maximumHalfWidth =
                        Mathf.Max(
                            maximumHalfWidth,
                            Mathf.Max(
                                0f,
                                fullWidths[i]) *
                            0.5f +
                            safeClearance);
                }
            }

            if (!TryGetExpandedPolylineCellBounds(
                    chunk,
                    settings,
                    points,
                    maximumHalfWidth,
                    out int minCellX,
                    out int minCellZ,
                    out int maxCellX,
                    out int maxCellZ))
            {
                return;
            }

            float cellSize =
                settings.CellWorldSize;

            float chunkSize =
                settings.ChunkWorldSize;

            float originX =
                chunk.Coordinate.x *
                chunkSize;

            float originZ =
                chunk.Coordinate.z *
                chunkSize;

            for (int z = minCellZ;
                 z <= maxCellZ;
                 z++)
            {
                float worldZ =
                    originZ +
                    (z + 0.5f) *
                    cellSize;

                for (int x = minCellX;
                     x <= maxCellX;
                     x++)
                {
                    var cellCenter =
                        new Vector2(
                            originX +
                            (x + 0.5f) *
                            cellSize,
                            worldZ);

                    for (int p = 0;
                         p < points.Count - 1;
                         p++)
                    {
                        float widthA =
                            hasProfile
                                ? Mathf.Max(
                                    0f,
                                    fullWidths[p])
                                : 0f;

                        float widthB =
                            hasProfile
                                ? Mathf.Max(
                                    0f,
                                    fullWidths[p + 1])
                                : widthA;

                        float segmentMaxHalfWidth =
                            Mathf.Max(
                                widthA,
                                widthB) *
                            0.5f +
                            safeClearance;

                        if (!SegmentExpandedBoundsContainPoint(
                                points[p],
                                points[p + 1],
                                segmentMaxHalfWidth,
                                cellCenter))
                        {
                            continue;
                        }

                        float distanceSqr =
                            DistancePointSegmentSqr(
                                cellCenter,
                                points[p],
                                points[p + 1],
                                out float segmentT);

                        float localHalfWidth =
                            Mathf.Lerp(
                                widthA,
                                widthB,
                                segmentT) *
                            0.5f +
                            safeClearance;

                        if (distanceSqr >
                            localHalfWidth *
                            localHalfWidth)
                        {
                            continue;
                        }

                        chunk.AddPlacementBlocks(
                            x,
                            z,
                            flags);

                        break;
                    }
                }
            }
        }

        private static bool TryGetExpandedPolylineCellBounds(
            WorldChunkData chunk,
            WorldGenerationSettings settings,
            IReadOnlyList<Vector2> points,
            float expansion,
            out int minCellX,
            out int minCellZ,
            out int maxCellX,
            out int maxCellZ)
        {
            float minX =
                float.PositiveInfinity;

            float minZ =
                float.PositiveInfinity;

            float maxX =
                float.NegativeInfinity;

            float maxZ =
                float.NegativeInfinity;

            for (int i = 0;
                 i < points.Count;
                 i++)
            {
                Vector2 point =
                    points[i];

                minX =
                    Mathf.Min(
                        minX,
                        point.x);

                minZ =
                    Mathf.Min(
                        minZ,
                        point.y);

                maxX =
                    Mathf.Max(
                        maxX,
                        point.x);

                maxZ =
                    Mathf.Max(
                        maxZ,
                        point.y);
            }

            return
                TryGetCellBoundsForWorldRect(
                    chunk,
                    settings,
                    minX - expansion,
                    minZ - expansion,
                    maxX + expansion,
                    maxZ + expansion,
                    out minCellX,
                    out minCellZ,
                    out maxCellX,
                    out maxCellZ);
        }

        private static bool TryGetCellBoundsForWorldRect(
            WorldChunkData chunk,
            WorldGenerationSettings settings,
            float minWorldX,
            float minWorldZ,
            float maxWorldX,
            float maxWorldZ,
            out int minCellX,
            out int minCellZ,
            out int maxCellX,
            out int maxCellZ)
        {
            float cellSize =
                settings.CellWorldSize;

            float chunkSize =
                settings.ChunkWorldSize;

            float originX =
                chunk.Coordinate.x *
                chunkSize;

            float originZ =
                chunk.Coordinate.z *
                chunkSize;

            float chunkMaxX =
                originX +
                chunkSize;

            float chunkMaxZ =
                originZ +
                chunkSize;

            if (maxWorldX < originX ||
                maxWorldZ < originZ ||
                minWorldX > chunkMaxX ||
                minWorldZ > chunkMaxZ)
            {
                minCellX = 0;
                minCellZ = 0;
                maxCellX = -1;
                maxCellZ = -1;

                return false;
            }

            minCellX =
                Mathf.Clamp(
                    Mathf.FloorToInt(
                        (minWorldX -
                         originX) /
                        cellSize),
                    0,
                    chunk.CellsPerSide - 1);

            minCellZ =
                Mathf.Clamp(
                    Mathf.FloorToInt(
                        (minWorldZ -
                         originZ) /
                        cellSize),
                    0,
                    chunk.CellsPerSide - 1);

            maxCellX =
                Mathf.Clamp(
                    Mathf.FloorToInt(
                        (maxWorldX -
                         originX) /
                        cellSize),
                    0,
                    chunk.CellsPerSide - 1);

            maxCellZ =
                Mathf.Clamp(
                    Mathf.FloorToInt(
                        (maxWorldZ -
                         originZ) /
                        cellSize),
                    0,
                    chunk.CellsPerSide - 1);

            return
                minCellX <= maxCellX &&
                minCellZ <= maxCellZ;
        }

        private static bool SegmentExpandedBoundsContainPoint(
            Vector2 a,
            Vector2 b,
            float expansion,
            Vector2 point)
        {
            float minX =
                Mathf.Min(
                    a.x,
                    b.x) -
                expansion;

            float maxX =
                Mathf.Max(
                    a.x,
                    b.x) +
                expansion;

            float minZ =
                Mathf.Min(
                    a.y,
                    b.y) -
                expansion;

            float maxZ =
                Mathf.Max(
                    a.y,
                    b.y) +
                expansion;

            return
                point.x >= minX &&
                point.x <= maxX &&
                point.y >= minZ &&
                point.y <= maxZ;
        }

        private static float DistancePointSegmentSqr(
            Vector2 point,
            Vector2 a,
            Vector2 b,
            out float segmentT)
        {
            Vector2 ab =
                b -
                a;

            float lengthSqr =
                ab.sqrMagnitude;

            if (lengthSqr <=
                0.000001f)
            {
                segmentT = 0f;
                return
                    (point - a).sqrMagnitude;
            }

            segmentT =
                Mathf.Clamp01(
                    Vector2.Dot(
                        point - a,
                        ab) /
                    lengthSqr);

            Vector2 closest =
                a +
                ab *
                segmentT;

            return
                (point -
                 closest).sqrMagnitude;
        }
    }
}
