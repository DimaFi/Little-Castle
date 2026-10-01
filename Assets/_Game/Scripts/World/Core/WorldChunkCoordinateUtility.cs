using UnityEngine;

namespace LittleCastle.World
{
    public static class WorldChunkCoordinateUtility
    {
        public static ChunkCoordinate FromWorldPosition(
            float worldX,
            float worldZ,
            float chunkWorldSize)
        {
            float safeSize = Mathf.Max(0.0001f, chunkWorldSize);

            return new ChunkCoordinate(
                Mathf.FloorToInt(worldX / safeSize),
                Mathf.FloorToInt(worldZ / safeSize));
        }

        public static Rect GetWorldBounds(
            ChunkCoordinate coordinate,
            float chunkWorldSize)
        {
            float size = Mathf.Max(0.0001f, chunkWorldSize);

            return new Rect(
                coordinate.x * size,
                coordinate.z * size,
                size,
                size);
        }
    }
}
