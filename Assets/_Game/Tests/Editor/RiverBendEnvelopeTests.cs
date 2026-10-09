using System.Collections.Generic;
using System.Reflection;
using LittleCastle.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace LittleCastle.Tests
{
    public sealed class RiverBendEnvelopeTests
    {
        private const string ConceptStagePath =
            "Assets/_Game/Settings/World/ConceptWorld_v001/" +
            "Stages/02_RiverTerrainCarving.asset";

        private const string MainStagePath =
            "Assets/_Game/Settings/World/" +
            "Stages/02_RiverTerrainCarving.asset";

        [Test]
        public void SharpBend_WiderAdjacentSegmentContributesWhenEnabled()
        {
            WorldGenerationSettings settings = CreateSettings(32f, 64);
            RiverTerrainCarvingStage stage =
                ScriptableObject.CreateInstance<RiverTerrainCarvingStage>();
            try
            {
                SetPrivate(stage, "bankFalloff", 0f);

                var river = new WorldRiverData
                {
                    stableId = 301,
                    nominalWidth = 2f,
                    nominalDepth = 1f,
                    centerline = new List<Vector2>
                    {
                        new Vector2(4f, 16f),
                        new Vector2(20f, 16f),
                        new Vector2(20f, 28f)
                    },
                    widths = new List<float> { 24f, 24f, 0.1f },
                    depths = new List<float> { 4f, 4f, 0.01f }
                };
                GenerationContext context = CreateContext(settings, river);

                // World (23.5,25) is closest to the narrow vertical segment,
                // but remains inside the wider horizontal segment's rounded
                // endpoint envelope at this sharp bend.
                const int sampleX = 47;
                const int sampleZ = 50;
                var legacy = new WorldChunkData(
                    new ChunkCoordinate(0, 0), 64);
                stage.Generate(context, legacy);

                Assert.That(stage.UseAnySegmentCarveEnvelope, Is.False);
                Assert.That(legacy.GetHeight(sampleX, sampleZ), Is.Zero,
                    "Legacy closest-segment behavior must stay unchanged.");

                SetPrivate(stage, "useAnySegmentCarveEnvelope", true);
                var profiled = new WorldChunkData(
                    new ChunkCoordinate(0, 0), 64);
                stage.Generate(context, profiled);

                Assert.That(
                    profiled.GetHeight(sampleX, sampleZ),
                    Is.LessThan(-0.05f),
                    "The wider adjacent segment must contribute its carve.");
            }
            finally
            {
                Object.DestroyImmediate(stage);
                Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void LegacyNearestOnly_DefaultRetainsKnownUniformOutput()
        {
            WorldGenerationSettings settings = CreateSettings(8f, 8);
            RiverTerrainCarvingStage stage =
                ScriptableObject.CreateInstance<RiverTerrainCarvingStage>();
            try
            {
                SetPrivate(stage, "bankFalloff", 0f);
                var river = new WorldRiverData
                {
                    stableId = 302,
                    nominalWidth = 4f,
                    nominalDepth = 3f,
                    centerline = new List<Vector2>
                    {
                        new Vector2(0f, 4f),
                        new Vector2(8f, 4f)
                    }
                };
                var chunk = new WorldChunkData(
                    new ChunkCoordinate(0, 0), 8);
                FillHeights(chunk, 10f);

                Assert.That(stage.UseAnySegmentCarveEnvelope, Is.False);
                stage.Generate(CreateContext(settings, river), chunk);

                Assert.That(chunk.GetHeight(4, 4), Is.EqualTo(7f));
                Assert.That(chunk.GetHeight(4, 6), Is.EqualTo(10f));
            }
            finally
            {
                Object.DestroyImmediate(stage);
                Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void AnySegmentEnvelope_RepeatedPointIsDeterministicAcrossNegativeBorder()
        {
            WorldGenerationSettings settings = CreateSettings(32f, 32);
            RiverTerrainCarvingStage stage =
                ScriptableObject.CreateInstance<RiverTerrainCarvingStage>();
            try
            {
                SetPrivate(stage, "useAnySegmentCarveEnvelope", true);
                var river = new WorldRiverData
                {
                    stableId = 303,
                    nominalWidth = 5f,
                    nominalDepth = 2f,
                    centerline = new List<Vector2>
                    {
                        new Vector2(-30f, -20f),
                        new Vector2(0f, -16f),
                        new Vector2(0f, -16f),
                        new Vector2(30f, -12f)
                    },
                    widths = new List<float> { 3f, 7f, 7f, 10f },
                    depths = new List<float> { 1f, 2f, 2f, 3f }
                };
                GenerationContext context = CreateContext(settings, river);
                var leftA = new WorldChunkData(
                    new ChunkCoordinate(-1, -1), 32);
                var leftB = new WorldChunkData(
                    new ChunkCoordinate(-1, -1), 32);
                var right = new WorldChunkData(
                    new ChunkCoordinate(0, -1), 32);

                Assert.DoesNotThrow(() => stage.Generate(context, leftA));
                Assert.DoesNotThrow(() => stage.Generate(context, leftB));
                Assert.DoesNotThrow(() => stage.Generate(context, right));

                for (int z = 0; z < leftA.SamplesPerSide; z++)
                {
                    for (int x = 0; x < leftA.SamplesPerSide; x++)
                    {
                        Assert.That(
                            leftA.GetHeight(x, z),
                            Is.EqualTo(leftB.GetHeight(x, z)),
                            $"Repeat mismatch at ({x},{z}).");
                    }

                    Assert.That(
                        leftA.GetHeight(32, z),
                        Is.EqualTo(right.GetHeight(0, z)),
                        $"Negative/positive chunk seam mismatch at z={z}.");
                }

                Assert.That(leftA.GetHeight(32, 16), Is.LessThan(0f),
                    "The repeated bend point on the shared border must carve.");
            }
            finally
            {
                Object.DestroyImmediate(stage);
                Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void OnlyIsolatedConceptCarvingOptsIntoAnySegmentEnvelope()
        {
            RiverTerrainCarvingStage concept =
                AssetDatabase.LoadAssetAtPath<RiverTerrainCarvingStage>(
                    ConceptStagePath);
            RiverTerrainCarvingStage main =
                AssetDatabase.LoadAssetAtPath<RiverTerrainCarvingStage>(
                    MainStagePath);

            Assert.That(concept, Is.Not.Null);
            Assert.That(main, Is.Not.Null);
            Assert.That(concept.UseAnySegmentCarveEnvelope, Is.True);
            Assert.That(main.UseAnySegmentCarveEnvelope, Is.False);
        }

        private static WorldGenerationSettings CreateSettings(
            float chunkWorldSize,
            int cellsPerSide)
        {
            WorldGenerationSettings settings =
                ScriptableObject.CreateInstance<WorldGenerationSettings>();
            SetPrivate(settings, "chunkWorldSize", chunkWorldSize);
            SetPrivate(settings, "cellsPerSide", cellsPerSide);
            return settings;
        }

        private static GenerationContext CreateContext(
            WorldGenerationSettings settings,
            WorldRiverData river)
        {
            var plan = new MacroWorldPlan(12345);
            plan.AddRiver(river);
            return new GenerationContext(12345, settings, plan);
        }

        private static void FillHeights(
            WorldChunkData chunk,
            float height)
        {
            for (int z = 0; z < chunk.SamplesPerSide; z++)
            {
                for (int x = 0; x < chunk.SamplesPerSide; x++)
                    chunk.SetHeight(x, z, height);
            }
        }

        private static void SetPrivate(
            object target,
            string name,
            object value)
        {
            FieldInfo field = target.GetType().GetField(
                name,
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(field, Is.Not.Null, "Missing field " + name);
            field.SetValue(target, value);
        }
    }
}
