using System;

namespace LittleCastle.World
{
    /// <summary>
    /// Generates authoritative chunk data through an ordered deterministic pipeline.
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

            ValidateStageOrder();
        }

        public WorldChunkData GenerateChunk(
            int worldSeed,
            ChunkCoordinate coordinate)
        {
            return GenerateChunkThroughPhase(
                worldSeed,
                coordinate,
                WorldGenerationStagePhase.PostProcess);
        }

        public WorldChunkData GenerateChunkThroughPhase(
            int worldSeed,
            ChunkCoordinate coordinate,
            WorldGenerationStagePhase maximumPhase)
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

                if (stage.Phase > maximumPhase)
                    continue;

                stage.Generate(context, chunk);
            }

            return chunk;
        }

        private void ValidateStageOrder()
        {
            bool hasPrevious = false;
            WorldGenerationStagePhase previous =
                WorldGenerationStagePhase.TerrainBase;

            for (int i = 0; i < settings.Stages.Count; i++)
            {
                WorldGenerationStage stage =
                    settings.Stages[i];

                if (stage == null || !stage.Enabled)
                    continue;

                if (hasPrevious && stage.Phase < previous)
                {
                    throw new InvalidOperationException(
                        "World generation stage order is invalid. " +
                        $"Stage '{stage.name}' ({stage.Phase}) appears after " +
                        $"a later phase ({previous}). Reorder the stages in " +
                        "WorldGenerationSettings.");
                }

                previous = stage.Phase;
                hasPrevious = true;
            }
        }
    }
}
