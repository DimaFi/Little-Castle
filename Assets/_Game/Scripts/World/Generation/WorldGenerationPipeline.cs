using System;

namespace LittleCastle.World
{
    /// <summary>
    /// Generates authoritative data for a chunk by executing ordered stages.
    /// </summary>
    public sealed class WorldGenerationPipeline
    {
        private readonly WorldGenerationSettings settings;
        private readonly MacroWorldPlan macroPlan;

        public WorldGenerationPipeline(
            WorldGenerationSettings settings,
            MacroWorldPlan macroPlan = null)
        {
            this.settings =
                settings ??
                throw new ArgumentNullException(nameof(settings));

            this.macroPlan = macroPlan;
        }

        public WorldChunkData GenerateChunk(
            int worldSeed,
            ChunkCoordinate coordinate)
        {
            var chunk =
                new WorldChunkData(
                    coordinate,
                    settings.CellsPerSide);

            var context =
                new GenerationContext(
                    worldSeed,
                    settings,
                    macroPlan);

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
