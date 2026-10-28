using LittleCastle.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace LittleCastle.Tests
{
    /// <summary>
    /// Static budget expectations only, not a runtime frame-time benchmark.
    /// Actual perf acceptance requires a PlayMode CSV and Unity Profiler.
    /// </summary>
    public sealed class ConceptWorldPerformanceTests
    {
        private const string Root =
            "Assets/_Game/Settings/World/ConceptWorld_v001/";

        [Test]
        public void ConceptProfile_HasHalfMeterGridAndFiniteChunkBudgets()
        {
            WorldDefinition definition =
                AssetDatabase.LoadAssetAtPath<WorldDefinition>(
                    Root + "ConceptWorldDefinition.asset");
            Assert.That(definition, Is.Not.Null);
            WorldGenerationSettings world = definition.GenerationSettings;
            WorldStreamingSettings streaming = definition.StreamingSettings;
            Assert.That(world, Is.Not.Null);
            Assert.That(streaming, Is.Not.Null);

            Assert.That(world.ChunkWorldSize, Is.EqualTo(32f));
            Assert.That(world.CellsPerSide, Is.EqualTo(64));
            Assert.That(world.CellWorldSize, Is.EqualTo(0.5f));
            Assert.That(streaming.LoadRadiusChunks, Is.GreaterThan(0));
            Assert.That(streaming.UnloadRadiusChunks,
                Is.GreaterThanOrEqualTo(streaming.LoadRadiusChunks));
            Assert.That(streaming.PrefetchRadiusChunks,
                Is.GreaterThanOrEqualTo(streaming.LoadRadiusChunks));

            Assert.That(streaming.MaxChunkLoadsPerFrame,
                Is.GreaterThan(0).And.LessThanOrEqualTo(8));
            Assert.That(streaming.UrgentGenerationStagesPerFrame,
                Is.GreaterThan(0).And.LessThanOrEqualTo(8));
            Assert.That(streaming.ColliderRadiusChunks,
                Is.LessThanOrEqualTo(streaming.LoadRadiusChunks));
            Assert.That(streaming.MaxCachedChunks,
                Is.GreaterThan(0), "Unlimited cached chunks are not acceptable for concept.");
        }

        [Test]
        public void ConceptProfile_DoesNotRequestFullFiniteMapAtStartup()
        {
            WorldStreamingSettings settings =
                AssetDatabase.LoadAssetAtPath<WorldStreamingSettings>(
                    Root + "Concept_MainWorldStreamingSettings.asset");
            Assert.That(settings, Is.Not.Null);

            int diameter = settings.LoadRadiusChunks * 2 + 1;
            int worstSquareVisible = diameter * diameter;
            int finiteMapArea = 96 * 96;
            Assert.That(worstSquareVisible, Is.LessThan(finiteMapArea));
            Assert.That(settings.MaxCachedChunks, Is.LessThan(finiteMapArea));
            Assert.That(settings.LoadRadiusChunks * 32f,
                Is.GreaterThan(0f), "Physical visibility radius must be measured in meters.");

            // This checks only static settings, not actual instantiation.
            // Verify spawned GameObjects and active colliders from CSV/Profiler later.
        }
    }
}
