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

            float safeRadius = Mathf.Max(0f, radius);
            float cellSize = settings.CellWorldSize;
            float chunkSize = settings.ChunkWorldSize;
            float originX = chunk.Coordinate.x * chunkSize;
            float originZ = chunk.Coordinate.z * chunkSize;
            float radiusSqr = safeRadius * safeRadius;

            for (int z = 0; z < chunk.CellsPerSide; z++)
            {
                for (int x = 0; x < chunk.CellsPerSide; x++)
                {
                    var cellCenter = new Vector2(
                        originX + (x + 0.5f) * cellSize,
                        originZ + (z + 0.5f) * cellSize);

                    if ((cellCenter - center).sqrMagnitude <= radiusSqr)
                        chunk.AddPlacementBlocks(x, z, flags);
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

            float safeHalfWidth = Mathf.Max(0f, halfWidth);
            float cellSize = settings.CellWorldSize;
            float chunkSize = settings.ChunkWorldSize;
            float originX = chunk.Coordinate.x * chunkSize;
            float originZ = chunk.Coordinate.z * chunkSize;
            float maxDistanceSqr = safeHalfWidth * safeHalfWidth;

            for (int z = 0; z < chunk.CellsPerSide; z++)
            {
                for (int x = 0; x < chunk.CellsPerSide; x++)
                {
                    var cellCenter = new Vector2(
                        originX + (x + 0.5f) * cellSize,
                        originZ + (z + 0.5f) * cellSize);

                    for (int p = 0; p < points.Count - 1; p++)
                    {
                        float distanceSqr =
                            DistancePointSegmentSqr(
                                cellCenter,
                                points[p],
                                points[p + 1],
                                out float ignoredT);

                        if (distanceSqr <= maxDistanceSqr)
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
                fullWidths.Count == points.Count;

            float cellSize = settings.CellWorldSize;
            float chunkSize = settings.ChunkWorldSize;
            float originX = chunk.Coordinate.x * chunkSize;
            float originZ = chunk.Coordinate.z * chunkSize;
            float safeClearance = Mathf.Max(0f, extraClearance);

            for (int z = 0; z < chunk.CellsPerSide; z++)
            {
                for (int x = 0; x < chunk.CellsPerSide; x++)
                {
                    var cellCenter = new Vector2(
                        originX + (x + 0.5f) * cellSize,
                        originZ + (z + 0.5f) * cellSize);

                    for (int p = 0; p < points.Count - 1; p++)
                    {
                        float distanceSqr =
                            DistancePointSegmentSqr(
                                cellCenter,
                                points[p],
                                points[p + 1],
                                out float segmentT);

                        float widthA =
                            hasProfile
                                ? Mathf.Max(0f, fullWidths[p])
                                : 0f;

                        float widthB =
                            hasProfile
                                ? Mathf.Max(0f, fullWidths[p + 1])
                                : widthA;

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

        private static float DistancePointSegmentSqr(
            Vector2 point,
            Vector2 a,
            Vector2 b,
            out float segmentT)
        {
            Vector2 ab = b - a;
            float lengthSqr = ab.sqrMagnitude;

            if (lengthSqr <= 0.000001f)
            {
                segmentT = 0f;
                return (point - a).sqrMagnitude;
            }

            segmentT =
                Mathf.Clamp01(
                    Vector2.Dot(point - a, ab) /
                    lengthSqr);

            Vector2 closest = a + ab * segmentT;
            return (point - closest).sqrMagnitude;
        }
    }
}
