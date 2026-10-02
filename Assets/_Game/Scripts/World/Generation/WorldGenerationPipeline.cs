using System;
using System.Collections.Generic;

namespace LittleCastle.World
{
    /// <summary>
    /// One cooperative main-thread generation request.
    ///
    /// The current generation stages are ScriptableObjects and some use
    /// AnimationCurve, so this deliberately stays on Unity's main thread.
    /// Stepping one stage at a time prevents the streamer from executing the
    /// whole chunk pipeline in one frame.
    /// </summary>
    public sealed class WorldChunkGenerationWork
    {
        private readonly IReadOnlyList<WorldGenerationStage> stages;
        private readonly GenerationContext context;
        private readonly WorldGenerationStagePhase maximumPhase;

        private int nextStageIndex;

        public WorldChunkData Result { get; }

        public bool IsCompleted
        {
            get
            {
                SkipUnavailableStages();
                return nextStageIndex >= stages.Count;
            }
        }

        public string LastStageName { get; private set; } = string.Empty;

        internal WorldChunkGenerationWork(
            WorldGenerationSettings settings,
            MacroWorldPlan macroPlan,
            int worldSeed,
            ChunkCoordinate coordinate,
            WorldGenerationStagePhase maximumPhase)
        {
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));

            stages = settings.Stages;
            context =
                new GenerationContext(
                    worldSeed,
                    settings,
                    macroPlan);

            this.maximumPhase = maximumPhase;

            Result =
                new WorldChunkData(
                    coordinate,
                    settings.CellsPerSide);

            SkipUnavailableStages();
        }

        /// <summary>
        /// Runs at most one enabled generation stage.
        /// Returns true when a stage was executed.
        /// </summary>
        public bool StepNextStage()
        {
            SkipUnavailableStages();

            if (nextStageIndex >= stages.Count)
                return false;

            WorldGenerationStage stage =
                stages[nextStageIndex++];

            LastStageName =
                stage != null
                    ? stage.name
                    : string.Empty;

            stage.Generate(
                context,
                Result);

            SkipUnavailableStages();

            return true;
        }

        private void SkipUnavailableStages()
        {
            while (nextStageIndex < stages.Count)
            {
                WorldGenerationStage stage =
                    stages[nextStageIndex];

                if (stage != null &&
                    stage.Enabled &&
                    stage.Phase <= maximumPhase)
                {
                    break;
                }

                nextStageIndex++;
            }
        }
    }

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
            WorldChunkGenerationWork work =
                BeginIncrementalGeneration(
                    worldSeed,
                    coordinate,
                    maximumPhase);

            while (work.StepNextStage())
            {
                // Synchronous callers deliberately complete the whole pipeline.
            }

            return work.Result;
        }

        public WorldChunkGenerationWork BeginIncrementalGeneration(
            int worldSeed,
            ChunkCoordinate coordinate,
            WorldGenerationStagePhase maximumPhase =
                WorldGenerationStagePhase.PostProcess)
        {
            return
                new WorldChunkGenerationWork(
                    settings,
                    macroPlan,
                    worldSeed,
                    coordinate,
                    maximumPhase);
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
