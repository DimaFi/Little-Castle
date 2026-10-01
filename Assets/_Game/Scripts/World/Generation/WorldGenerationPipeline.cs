using System;

namespace LittleCastle.World
{
    /// <summary>
    /// Generates authoritative data for a chunk by executing ordered stages.
    /// </summary>
    public sealed class WorldGenerationPipeline
    {
        private readonly WorldGenerationSettings settings;

        public WorldGenerationPipeline(WorldGenerationSettings settings)
        {
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public WorldChunkData GenerateChunk(int worldSeed, ChunkCoordinate coordinate)
        {
            var chunk = new WorldChunkData(coordinate, settings.CellsPerSide);
            var context = new GenerationContext(worldSeed, settings);

            foreach (WorldGenerationStage stage in settings.Stages)
            {
                if (stage == null || !stage.Enabled)
                    continue;

                stage.Generate(context, chunk);
            }

            return chunk;
        }
    }
}
