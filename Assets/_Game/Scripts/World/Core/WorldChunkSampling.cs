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

            // ChunkMeshBuilder uses (a,c,b), (b,c,d), with diagonal b--c.
            // Bilinear interpolation describes a curved patch, not those triangles.
            float a = chunk.GetHeight(x0, z0), b = chunk.GetHeight(x1, z0);
            float c = chunk.GetHeight(x0, z1), d = chunk.GetHeight(x1, z1);
            return tx + tz <= 1f ? a + (b-a)*tx + (c-a)*tz
                : d + (c-d)*(1f-tx) + (b-d)*(1f-tz);
        }

        public static Vector3 SampleTriangleNormal(WorldChunkData chunk,
            WorldGenerationSettings settings, float worldX, float worldZ)
            => SampleTriangleNormal(chunk,settings.ChunkWorldSize,worldX,worldZ);

        public static Vector3 SampleTriangleNormal(WorldChunkData chunk,
            float chunkWorldSize, float worldX, float worldZ)
        {
            float step=chunkWorldSize/chunk.CellsPerSide;
            float gx=(worldX-chunk.Coordinate.x*chunkWorldSize)/step;
            float gz=(worldZ-chunk.Coordinate.z*chunkWorldSize)/step;
            int x = Mathf.Clamp(Mathf.FloorToInt(gx), 0, chunk.CellsPerSide-1);
            int z = Mathf.Clamp(Mathf.FloorToInt(gz), 0, chunk.CellsPerSide-1);
            float a=chunk.GetHeight(x,z), b=chunk.GetHeight(x+1,z);
            float c=chunk.GetHeight(x,z+1), d=chunk.GetHeight(x+1,z+1);
            return gx-x+gz-z<=1f ? new Vector3(a-b,step,a-c).normalized
                : new Vector3(c-d,step,b-d).normalized;
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

        public static SurfaceKind SampleSurface(
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
                chunk.GetSurface(
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

        public static float SampleGrassDensity(
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
                chunk.GetGrassDensity(
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
