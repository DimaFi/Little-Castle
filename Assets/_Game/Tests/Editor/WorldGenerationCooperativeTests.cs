using LittleCastle.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace LittleCastle.Tests
{
    public sealed class WorldGenerationCooperativeTests
    {
        private sealed class CountingStage :
            WorldGenerationStage
        {
            public int calls;

            public override WorldGenerationStagePhase Phase =>
                WorldGenerationStagePhase.TerrainBase;

            public override void Generate(
                GenerationContext context,
                WorldChunkData chunk)
            {
                calls++;
            }
        }

        [Test]
        public void IncrementalGeneration_RunsAtMostOneStagePerStep()
        {
            WorldGenerationSettings settings =
                ScriptableObject.CreateInstance<
                    WorldGenerationSettings>();

            CountingStage stageA =
                ScriptableObject.CreateInstance<
                    CountingStage>();

            CountingStage stageB =
                ScriptableObject.CreateInstance<
                    CountingStage>();

            try
            {
                var serialized =
                    new SerializedObject(
                        settings);

                SerializedProperty stages =
                    serialized.FindProperty(
                        "stages");

                stages.arraySize = 2;

                stages.GetArrayElementAtIndex(0)
                    .objectReferenceValue =
                    stageA;

                stages.GetArrayElementAtIndex(1)
                    .objectReferenceValue =
                    stageB;

                serialized.ApplyModifiedPropertiesWithoutUndo();

                var pipeline =
                    new WorldGenerationPipeline(
                        settings);

                WorldChunkGenerationWork work =
                    pipeline.BeginIncrementalGeneration(
                        12345,
                        new ChunkCoordinate(
                            0,
                            0));

                Assert.That(
                    work.IsCompleted,
                    Is.False);

                Assert.That(
                    work.StepNextStage(),
                    Is.True);

                Assert.That(
                    stageA.calls,
                    Is.EqualTo(1));

                Assert.That(
                    stageB.calls,
                    Is.EqualTo(0));

                Assert.That(
                    work.StepNextStage(),
                    Is.True);

                Assert.That(
                    stageA.calls,
                    Is.EqualTo(1));

                Assert.That(
                    stageB.calls,
                    Is.EqualTo(1));

                Assert.That(
                    work.IsCompleted,
                    Is.True);

                Assert.That(
                    work.StepNextStage(),
                    Is.False);
            }
            finally
            {
                Object.DestroyImmediate(
                    stageA);

                Object.DestroyImmediate(
                    stageB);

                Object.DestroyImmediate(
                    settings);
            }
        }

        [Test]
        public void ChunkCache_CanStorePrefetchedDataWithoutPinning()
        {
            WorldGenerationSettings settings =
                ScriptableObject.CreateInstance<
                    WorldGenerationSettings>();

            try
            {
                var pipeline =
                    new WorldGenerationPipeline(
                        settings);

                var cache =
                    new WorldChunkCache(
                        pipeline,
                        12345,
                        8);

                var coordinate =
                    new ChunkCoordinate(
                        3,
                        -2);

                var chunk =
                    new WorldChunkData(
                        coordinate,
                        settings.CellsPerSide);

                cache.StoreGenerated(
                    chunk);

                Assert.That(
                    cache.Contains(
                        coordinate),
                    Is.True);

                Assert.That(
                    cache.IsPinned(
                        coordinate),
                    Is.False);

                Assert.That(
                    cache.TryGet(
                        coordinate,
                        out WorldChunkData cached),
                    Is.True);

                Assert.That(
                    cached,
                    Is.SameAs(
                        chunk));
            }
            finally
            {
                Object.DestroyImmediate(
                    settings);
            }
        }

        [Test]
        public void PrefetchRadius_ExtendsBeyondVisibleRadius()
        {
            WorldStreamingSettings settings =
                ScriptableObject.CreateInstance<
                    WorldStreamingSettings>();

            try
            {
                var serialized =
                    new SerializedObject(
                        settings);

                serialized.FindProperty(
                    "loadRadiusChunks").intValue = 4;

                serialized.FindProperty(
                    "prefetchPaddingChunks").intValue = 2;

                serialized.ApplyModifiedPropertiesWithoutUndo();

                Assert.That(
                    settings.LoadRadiusChunks,
                    Is.EqualTo(4));

                Assert.That(
                    settings.PrefetchRadiusChunks,
                    Is.EqualTo(6));

                Assert.That(
                    settings.MacroPlanRadiusChunks,
                    Is.GreaterThanOrEqualTo(8));
            }
            finally
            {
                Object.DestroyImmediate(
                    settings);
            }
        }
    }
}
