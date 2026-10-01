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
                                points[p + 1]);

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

        private static float DistancePointSegmentSqr(
            Vector2 point,
            Vector2 a,
            Vector2 b)
        {
            Vector2 ab = b - a;
            float lengthSqr = ab.sqrMagnitude;

            if (lengthSqr <= 0.000001f)
                return (point - a).sqrMagnitude;

            float t = Mathf.Clamp01(
                Vector2.Dot(point - a, ab) /
                lengthSqr);

            Vector2 closest = a + ab * t;
            return (point - closest).sqrMagnitude;
        }
    }
}
