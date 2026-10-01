using UnityEngine;

namespace LittleCastle.World
{
    /// <summary>
    /// Lightweight runtime/editor diagnostics for protecting deterministic
    /// generation invariants without requiring the Unity Test Framework.
    /// </summary>
    public static class WorldGenerationDiagnostics
    {
        public static bool ValidateDeterminism(
            WorldGenerationPipeline pipeline,
            int worldSeed,
            ChunkCoordinate coordinate,
            float epsilon,
            out string message)
        {
            WorldChunkData a = pipeline.GenerateChunk(worldSeed, coordinate);
            WorldChunkData b = pipeline.GenerateChunk(worldSeed, coordinate);

            for (int z = 0; z < a.SamplesPerSide; z++)
            {
                for (int x = 0; x < a.SamplesPerSide; x++)
                {
                    if (Mathf.Abs(a.GetHeight(x, z) - b.GetHeight(x, z)) > epsilon)
                    {
                        message =
                            $"Determinism failed at chunk {coordinate}, sample ({x},{z}).";
                        return false;
                    }
                }
            }

            message = $"Determinism OK for chunk {coordinate}.";
            return true;
        }

        public static bool ValidateEastWestBorder(
            WorldGenerationPipeline pipeline,
            int worldSeed,
            ChunkCoordinate westCoordinate,
            float epsilon,
            out string message)
        {
            var eastCoordinate =
                new ChunkCoordinate(westCoordinate.x + 1, westCoordinate.z);

            WorldChunkData west = pipeline.GenerateChunk(worldSeed, westCoordinate);
            WorldChunkData east = pipeline.GenerateChunk(worldSeed, eastCoordinate);

            int westBorderX = west.SamplesPerSide - 1;

            for (int z = 0; z < west.SamplesPerSide; z++)
            {
                float a = west.GetHeight(westBorderX, z);
                float b = east.GetHeight(0, z);

                if (Mathf.Abs(a - b) > epsilon)
                {
                    message =
                        $"East/west seam failed between {westCoordinate} and {eastCoordinate} at z={z}. " +
                        $"Values: {a} vs {b}.";
                    return false;
                }
            }

            message =
                $"East/west border OK between {westCoordinate} and {eastCoordinate}.";
            return true;
        }

        public static bool ValidateNorthSouthBorder(
            WorldGenerationPipeline pipeline,
            int worldSeed,
            ChunkCoordinate southCoordinate,
            float epsilon,
            out string message)
        {
            var northCoordinate =
                new ChunkCoordinate(southCoordinate.x, southCoordinate.z + 1);

            WorldChunkData south = pipeline.GenerateChunk(worldSeed, southCoordinate);
            WorldChunkData north = pipeline.GenerateChunk(worldSeed, northCoordinate);

            int southBorderZ = south.SamplesPerSide - 1;

            for (int x = 0; x < south.SamplesPerSide; x++)
            {
                float a = south.GetHeight(x, southBorderZ);
                float b = north.GetHeight(x, 0);

                if (Mathf.Abs(a - b) > epsilon)
                {
                    message =
                        $"North/south seam failed between {southCoordinate} and {northCoordinate} at x={x}. " +
                        $"Values: {a} vs {b}.";
                    return false;
                }
            }

            message =
                $"North/south border OK between {southCoordinate} and {northCoordinate}.";
            return true;
        }
    }
}
