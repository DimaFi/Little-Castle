using System;
using System.Collections.Generic;
using System.Reflection;
using LittleCastle.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace LittleCastle.Tests
{
    /// <summary>
    /// Independent, all-height-array numerical parity against the same
    /// source algorithm with chunk broadphase disabled. This is not
    /// profiling evidence; real times require Unity/Player capture.
    /// </summary>
    public sealed class RiverCarveBroadPhaseTests
    {
        private const string Concept =
            "Assets/_Game/Settings/World/ConceptWorld_v001/" +
            "Stages/02_RiverTerrainCarving.asset";
        private const string Main =
            "Assets/_Game/Settings/World/Stages/" +
            "02_RiverTerrainCarving.asset";

        [Test]
        public void LocalCandidateScan_EqualsFullEnvelope_AllHeightArrays()
        {
            var plan = MakePlan();
            WorldGenerationSettings settings = Settings(32f, 32);
            RiverTerrainCarvingStage legacy = Stage(true, false, 5f);
            RiverTerrainCarvingStage optimized = Stage(true, true, 5f);
            try
            {
                var context = new GenerationContext(-10101, settings, plan);
                foreach (ChunkCoordinate coordinate in new[]
                {
                    new ChunkCoordinate(-3, -2),
                    new ChunkCoordinate(-2, -1),
                    new ChunkCoordinate(-1, -1),
                    new ChunkCoordinate(0, -1),
                    new ChunkCoordinate(1, -1),
                    new ChunkCoordinate(-1, 0),
                    new ChunkCoordinate(0, 0),
                    new ChunkCoordinate(2, 2)
                })
                {
                    WorldChunkData expected = Filled(coordinate, 32);
                    WorldChunkData actual = Filled(coordinate, 32);
                    legacy.Generate(context, expected);
                    optimized.Generate(context, actual);

                    AssertSameHeights(expected, actual);
                    Assert.That(optimized.LastCandidateSegments,
                        Is.LessThanOrEqualTo(
                            optimized.LastScannedSegments));
                }
            }
            finally
            {
                Object.DestroyImmediate(legacy);
                Object.DestroyImmediate(optimized);
                Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void FarRiversExcluded_NoHeightChangesAndNoMeshOrIndex()
        {
            WorldGenerationSettings settings = Settings(32f, 32);
            RiverTerrainCarvingStage stage = Stage(true, true, 5f);
            try
            {
                var plan = new MacroWorldPlan(17);
                plan.AddRiver(River(
                    101, new[] { new Vector2(600f, 700f),
                        new Vector2(650f, 780f) },
                    new[] { 9f, 14f },
                    new[] { 2f, 4f }));

                var chunk = Filled(new ChunkCoordinate(-1, -1), 32);
                var original = Filled(new ChunkCoordinate(-1, -1), 32);
                stage.Generate(new GenerationContext(17, settings, plan), chunk);

                AssertSameHeights(original, chunk);
                Assert.That(stage.LastScannedSegments, Is.EqualTo(1));
                Assert.That(stage.LastCandidateSegments, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(stage);
                Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void SharpWideBend_StillCarvesAdjacentEnvelope()
        {
            WorldGenerationSettings settings = Settings(32f, 64);
            RiverTerrainCarvingStage full = Stage(true, false, 0f);
            RiverTerrainCarvingStage local = Stage(true, true, 0f);
            try
            {
                var plan = new MacroWorldPlan(123);
                plan.AddRiver(River(
                    301,
                    new[] { new Vector2(4f, 16f),
                        new Vector2(20f, 16f),
                        new Vector2(20f, 28f) },
                    new[] { 24f, 24f, 0.1f },
                    new[] { 4f, 4f, 0.01f }));
                var context = new GenerationContext(123, settings, plan);
                var a = new WorldChunkData(new ChunkCoordinate(0, 0), 64);
                var b = new WorldChunkData(new ChunkCoordinate(0, 0), 64);
                full.Generate(context, a);
                local.Generate(context, b);
                AssertSameHeights(a, b);
                Assert.That(b.GetHeight(47, 50), Is.LessThan(-0.05f));
                Assert.That(local.LastCandidateSegments, Is.EqualTo(2));
            }
            finally
            {
                Object.DestroyImmediate(full);
                Object.DestroyImmediate(local);
                Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void SharedNegativeBorder_BothOptimizedSidesMatch()
        {
            WorldGenerationSettings settings = Settings(32f, 32);
            RiverTerrainCarvingStage local = Stage(true, true, 5f);
            try
            {
                MacroWorldPlan plan = MakePlan();
                var context = new GenerationContext(-10101, settings, plan);
                var left = Filled(new ChunkCoordinate(-1, -1), 32);
                var right = Filled(new ChunkCoordinate(0, -1), 32);

                local.Generate(context, left);
                local.Generate(context, right);

                for (int z = 0; z <= 32; z++)
                    Assert.That(left.GetHeight(32, z),
                        Is.EqualTo(right.GetHeight(0, z)),
                        "Chunk seam at world x=0, z=" + z);
            }
            finally
            {
                Object.DestroyImmediate(local);
                Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void ToggleDoesNotAffectLegacyClosestSegmentPolicy()
        {
            WorldGenerationSettings settings = Settings(32f, 32);
            RiverTerrainCarvingStage legacy = Stage(false, false, 5f);
            RiverTerrainCarvingStage toggled = Stage(false, true, 5f);
            try
            {
                var plan = MakePlan();
                var context = new GenerationContext(777, settings, plan);
                var a = Filled(new ChunkCoordinate(0, -1), 32);
                var b = Filled(new ChunkCoordinate(0, -1), 32);
                legacy.Generate(context, a);
                toggled.Generate(context, b);
                AssertSameHeights(a, b);
                Assert.That(toggled.UseAnySegmentCarveEnvelope, Is.False);
                Assert.That(toggled.UseChunkLocalCarveBroadPhase, Is.True);
                Assert.That(toggled.LastScannedSegments, Is.Zero,
                    "Nearest-only policy must not select local candidate subset.");
            }
            finally
            {
                Object.DestroyImmediate(legacy);
                Object.DestroyImmediate(toggled);
                Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void ProfileFallbackAndTinyWidths_PreserveExactHeights()
        {
            WorldGenerationSettings settings = Settings(32f, 32);
            RiverTerrainCarvingStage full = Stage(true, false, 0f);
            RiverTerrainCarvingStage local = Stage(true, true, 0f);
            try
            {
                var plan = new MacroWorldPlan(-42);
                WorldRiverData nominal = River(
                    5,
                    new[] { new Vector2(-50f, -3f), new Vector2(50f, -3f) },
                    null, null);
                nominal.nominalWidth = 0.1f;
                nominal.nominalDepth = 0.01f;
                plan.AddRiver(nominal);
                WorldRiverData varied = River(
                    6,
                    new[] { new Vector2(-30f, -16f),
                        new Vector2(0f, -16f), new Vector2(30f, -16f) },
                    new[] { 0.1f, 0.1f, 0.1f },
                    new[] { 0.01f, 2f, 0.01f });
                plan.AddRiver(varied);
                var context = new GenerationContext(-42, settings, plan);

                foreach (ChunkCoordinate coord in new[]
                {
                    new ChunkCoordinate(-1, -1),
                    new ChunkCoordinate(0, -1)
                })
                {
                    var a = Filled(coord, 32);
                    var b = Filled(coord, 32);
                    full.Generate(context, a);
                    local.Generate(context, b);
                    AssertSameHeights(a, b);
                }
            }
            finally
            {
                Object.DestroyImmediate(full);
                Object.DestroyImmediate(local);
                Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void RepeatedPoints_AndZeroInfluenceBehaveIdentically()
        {
            WorldGenerationSettings settings = Settings(32f, 32);
            RiverTerrainCarvingStage full = Stage(true, false, 0f);
            RiverTerrainCarvingStage local = Stage(true, true, 0f);
            try
            {
                var plan = new MacroWorldPlan(2);
                plan.AddRiver(River(8, new[]
                    {
                        new Vector2(-40f, -16f),
                        new Vector2(0f, -16f),
                        new Vector2(0f, -16f),
                        new Vector2(40f, -16f)
                    },
                    new[] { 2f, 4f, 4f, 6f },
                    new[] { 1f, 3f, 3f, 1f }));
                var context = new GenerationContext(2, settings, plan);
                var a = Filled(new ChunkCoordinate(-1, -1), 32);
                var b = Filled(new ChunkCoordinate(-1, -1), 32);
                full.Generate(context, a);
                local.Generate(context, b);
                AssertSameHeights(a, b);
                Assert.That(local.LastScannedSegments, Is.EqualTo(3));
                Assert.That(local.LastCandidateSegments, Is.EqualTo(2));
            }
            finally
            {
                Object.DestroyImmediate(full);
                Object.DestroyImmediate(local);
                Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void OnlyIsolatedConceptProfileEnablesBroadPhase()
        {
            var concept =
                AssetDatabase.LoadAssetAtPath<RiverTerrainCarvingStage>(Concept);
            var main =
                AssetDatabase.LoadAssetAtPath<RiverTerrainCarvingStage>(Main);
            Assert.That(concept, Is.Not.Null);
            Assert.That(main, Is.Not.Null);
            Assert.That(concept.UseAnySegmentCarveEnvelope, Is.True);
            Assert.That(concept.UseChunkLocalCarveBroadPhase, Is.True);
            Assert.That(main.UseAnySegmentCarveEnvelope, Is.False);
            Assert.That(main.UseChunkLocalCarveBroadPhase, Is.False);
        }

        private static RiverTerrainCarvingStage Stage(
            bool anySegment,
            bool broadPhase,
            float bankFalloff)
        {
            var stage =
                ScriptableObject.CreateInstance<RiverTerrainCarvingStage>();
            Set(stage, "useAnySegmentCarveEnvelope", anySegment);
            Set(stage, "useChunkLocalCarveBroadPhase", broadPhase);
            Set(stage, "bankFalloff", bankFalloff);
            return stage;
        }

        private static WorldGenerationSettings Settings(
            float chunkSize, int cells)
        {
            var settings = ScriptableObject.CreateInstance<WorldGenerationSettings>();
            Set(settings, "chunkWorldSize", chunkSize);
            Set(settings, "cellsPerSide", cells);
            return settings;
        }

        private static void Set(object value, string field, object replacement)
        {
            FieldInfo info = value.GetType().GetField(
                field, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(info, Is.Not.Null, field);
            info.SetValue(value, replacement);
        }

        private static WorldRiverData River(
            long id,
            IList<Vector2> points,
            IList<float> widths,
            IList<float> depths)
        {
            return new WorldRiverData
            {
                stableId = id,
                nominalWidth = 5f,
                nominalDepth = 1f,
                centerline = new List<Vector2>(points),
                widths = widths != null ? new List<float>(widths) : null,
                depths = depths != null ? new List<float>(depths) : null
            };
        }

        private static MacroWorldPlan MakePlan()
        {
            var plan = new MacroWorldPlan(-10101);
            plan.AddRiver(River(10,
                new[] { new Vector2(-70f, -24f),
                    new Vector2(-21f, -20f),
                    new Vector2(-21f, -20f),
                    new Vector2(0f, -8f),
                    new Vector2(60f, -10f) },
                new[] { 3f, 8f, 8f, 18f, 6f },
                new[] { 1f, 2f, 2f, 4f, 1f }));
            plan.AddRiver(River(11,
                new[] { new Vector2(-25f, 75f),
                    new Vector2(-8f, 0f),
                    new Vector2(10f, -34f) },
                new[] { 4f, 10f, 2f },
                new[] { 1f, 4f, 1.4f }));
            plan.AddRiver(River(12,
                new[] { new Vector2(600f, 600f),
                    new Vector2(700f, 720f) },
                new[] { 2f, 10f },
                new[] { 2f, 2f }));
            return plan;
        }

        private static WorldChunkData Filled(
            ChunkCoordinate coordinate, int cells)
        {
            var chunk = new WorldChunkData(coordinate, cells);
            for (int z = 0; z <= cells; z++)
            {
                for (int x = 0; x <= cells; x++)
                {
                    float worldX = coordinate.x * 32f + x * 32f / cells;
                    float worldZ = coordinate.z * 32f + z * 32f / cells;
                    chunk.SetHeight(x, z,
                        2.5f + Mathf.Sin(worldX * 0.1f) +
                        Mathf.Cos(worldZ * 0.07f));
                }
            }
            return chunk;
        }

        private static void AssertSameHeights(
            WorldChunkData expected, WorldChunkData actual)
        {
            Assert.That(actual.SamplesPerSide, Is.EqualTo(expected.SamplesPerSide));
            for (int z = 0; z < expected.SamplesPerSide; z++)
            {
                for (int x = 0; x < expected.SamplesPerSide; x++)
                {
                    Assert.That(actual.GetHeight(x, z),
                        Is.EqualTo(expected.GetHeight(x, z)),
                        "Height mismatch at (" + x + "," + z + ")" +
                        " chunk " + expected.Coordinate.x +
                        "," + expected.Coordinate.z);
                }
            }
        }
    }
}
