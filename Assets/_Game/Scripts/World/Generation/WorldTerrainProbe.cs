using System.Collections.Generic;
using UnityEngine;

namespace LittleCastle.World
{
    /// <summary>
    /// Samples terrain-only generated data for arbitrary world positions.
    /// Uses phase-limited generation so trees/resources are not created just
    /// to decide whether a macro feature can exist at a location.
    /// </summary>
    public sealed class WorldTerrainProbe
    {
        private readonly Dictionary<ChunkCoordinate, WorldChunkData> cache =
            new Dictionary<ChunkCoordinate, WorldChunkData>();

        private readonly WorldGenerationPipeline pipeline;
        private readonly WorldGenerationSettings settings;
        private readonly int worldSeed;

        public WorldTerrainProbe(
            WorldGenerationPipeline pipeline,
            WorldGenerationSettings settings,
            int worldSeed)
        {
            this.pipeline = pipeline;
            this.settings = settings;
            this.worldSeed = worldSeed;
        }

        public WorldTerrainSample Sample(Vector2 worldPosition)
        {
            ChunkCoordinate coordinate =
                WorldChunkCoordinateUtility.FromWorldPosition(
                    worldPosition.x,
                    worldPosition.y,
                    settings.ChunkWorldSize);

            if (!cache.TryGetValue(
                coordinate,
                out WorldChunkData chunk))
            {
                chunk =
                    pipeline.GenerateChunkThroughPhase(
                        worldSeed,
                        coordinate,
                        WorldGenerationStagePhase.TerrainAnalysis);

                cache.Add(coordinate, chunk);
            }

            float height =
                WorldChunkSampling.SampleHeight(
                    chunk,
                    settings,
                    worldPosition.x,
                    worldPosition.y);

            float slope =
                WorldChunkSampling.SampleSlope(
                    chunk,
                    settings,
                    worldPosition.x,
                    worldPosition.y);

            TerrainClass terrainClass =
                WorldChunkSampling.SampleTerrainClass(
                    chunk,
                    settings,
                    worldPosition.x,
                    worldPosition.y);

            return new WorldTerrainSample(
                height,
                slope,
                terrainClass);
        }

        public void Clear()
        {
            cache.Clear();
        }
    }
}
