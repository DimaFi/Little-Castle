using UnityEngine;

namespace LittleCastle.World
{
    public static class WorldChunkSampling
    {
        public static float SampleHeight(
            WorldChunkData chunk,
            WorldGenerationSettings settings,
            float worldX,
            float worldZ)
        {
            GetLocalGridCoordinates(
                chunk,
                settings,
                worldX,
                worldZ,
                out float gx,
                out float gz);

            int x0 =
                Mathf.Clamp(
                    Mathf.FloorToInt(gx),
                    0,
                    chunk.CellsPerSide);

            int z0 =
                Mathf.Clamp(
                    Mathf.FloorToInt(gz),
                    0,
                    chunk.CellsPerSide);

            int x1 =
                Mathf.Min(
                    x0 + 1,
                    chunk.CellsPerSide);

            int z1 =
                Mathf.Min(
                    z0 + 1,
                    chunk.CellsPerSide);

            float tx =
                Mathf.Clamp01(
                    gx - x0);

            float tz =
                Mathf.Clamp01(
                    gz - z0);

            float a =
                Mathf.Lerp(
                    chunk.GetHeight(x0, z0),
                    chunk.GetHeight(x1, z0),
                    tx);

            float b =
                Mathf.Lerp(
                    chunk.GetHeight(x0, z1),
                    chunk.GetHeight(x1, z1),
                    tx);

            return
                Mathf.Lerp(
                    a,
                    b,
                    tz);
        }

        public static float SampleSlope(
            WorldChunkData chunk,
            WorldGenerationSettings settings,
            float worldX,
            float worldZ)
        {
            GetCellCoordinates(
                chunk,
                settings,
                worldX,
                worldZ,
                out int cellX,
                out int cellZ);

            return
                chunk.GetCellSlope(
                    cellX,
                    cellZ);
        }

        public static TerrainClass SampleTerrainClass(
            WorldChunkData chunk,
            WorldGenerationSettings settings,
            float worldX,
            float worldZ)
        {
            GetCellCoordinates(
                chunk,
                settings,
                worldX,
                worldZ,
                out int cellX,
                out int cellZ);

            return
                chunk.GetTerrainClass(
                    cellX,
                    cellZ);
        }

        public static float SampleTemperature(
            WorldChunkData chunk,
            WorldGenerationSettings settings,
            float worldX,
            float worldZ)
        {
            GetCellCoordinates(
                chunk,
                settings,
                worldX,
                worldZ,
                out int cellX,
                out int cellZ);

            return
                chunk.GetTemperature(
                    cellX,
                    cellZ);
        }

        public static float SampleMoisture(
            WorldChunkData chunk,
            WorldGenerationSettings settings,
            float worldX,
            float worldZ)
        {
            GetCellCoordinates(
                chunk,
                settings,
                worldX,
                worldZ,
                out int cellX,
                out int cellZ);

            return
                chunk.GetMoisture(
                    cellX,
                    cellZ);
        }

        public static BiomeKind SampleBiome(
            WorldChunkData chunk,
            WorldGenerationSettings settings,
            float worldX,
            float worldZ)
        {
            GetCellCoordinates(
                chunk,
                settings,
                worldX,
                worldZ,
                out int cellX,
                out int cellZ);

            return
                chunk.GetBiome(
                    cellX,
                    cellZ);
        }

        public static float SampleForestDensity(
            WorldChunkData chunk,
            WorldGenerationSettings settings,
            float worldX,
            float worldZ)
        {
            GetCellCoordinates(
                chunk,
                settings,
                worldX,
                worldZ,
                out int cellX,
                out int cellZ);

            return
                chunk.GetForestDensity(
                    cellX,
                    cellZ);
        }

        public static PlacementBlockFlags SamplePlacementBlocks(
            WorldChunkData chunk,
            WorldGenerationSettings settings,
            float worldX,
            float worldZ)
        {
            GetCellCoordinates(
                chunk,
                settings,
                worldX,
                worldZ,
                out int cellX,
                out int cellZ);

            return
                chunk.GetPlacementBlocks(
                    cellX,
                    cellZ);
        }

        private static void GetCellCoordinates(
            WorldChunkData chunk,
            WorldGenerationSettings settings,
            float worldX,
            float worldZ,
            out int cellX,
            out int cellZ)
        {
            GetLocalGridCoordinates(
                chunk,
                settings,
                worldX,
                worldZ,
                out float gx,
                out float gz);

            cellX =
                Mathf.Clamp(
                    Mathf.FloorToInt(gx),
                    0,
                    chunk.CellsPerSide - 1);

            cellZ =
                Mathf.Clamp(
                    Mathf.FloorToInt(gz),
                    0,
                    chunk.CellsPerSide - 1);
        }

        private static void GetLocalGridCoordinates(
            WorldChunkData chunk,
            WorldGenerationSettings settings,
            float worldX,
            float worldZ,
            out float gridX,
            out float gridZ)
        {
            float originX =
                chunk.Coordinate.x *
                settings.ChunkWorldSize;

            float originZ =
                chunk.Coordinate.z *
                settings.ChunkWorldSize;

            gridX =
                (worldX - originX) /
                settings.CellWorldSize;

            gridZ =
                (worldZ - originZ) /
                settings.CellWorldSize;
        }
    }
}
