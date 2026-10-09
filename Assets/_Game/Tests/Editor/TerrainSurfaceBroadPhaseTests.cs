using System.Collections.Generic;
using System.Reflection;
using LittleCastle.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace LittleCastle.Tests
{
    public sealed class TerrainSurfaceBroadPhaseTests
    {
        private const string ConceptStagePath =
            "Assets/_Game/Settings/World/ConceptWorld_v001/" +
            "Stages/07_TerrainSurface.asset";

        private const string MainStagePath =
            "Assets/_Game/Settings/World/" +
            "Stages/07_TerrainSurface.asset";

        [Test]
        public void BroadPhase_MatchesLegacyForPriorityAndSharpBend()
        {
            WorldGenerationSettings settings = CreateSettings(8f, 8);
            TerrainSurfaceStage legacy = CreateStage(true, false);
            TerrainSurfaceStage broadPhase = CreateStage(true, true);
            try
            {
                MacroWorldPlan plan = BuildPriorityAndBendPlan();
                var context = new GenerationContext(771, settings, plan);
                var coordinates = new[]
                {
                    new ChunkCoordinate(-2, -1),
                    new ChunkCoordinate(-1, -1),
                    new ChunkCoordinate(0, -1)
                };

                for (int i = 0; i < coordinates.Length; i++)
                {
                    WorldChunkData expected =
                        Generate(legacy, context, coordinates[i], 8);
                    WorldChunkData actual =
                        Generate(broadPhase, context, coordinates[i], 8);
                    AssertSameSurfaces(expected, actual);
                }

                WorldChunkData priorityChunk = Generate(
                    broadPhase,
                    context,
                    new ChunkCoordinate(0, -1),
                    8);
                Assert.That(
                    priorityChunk.GetSurface(2, 4),
                    Is.EqualTo(SurfaceKind.Trail),
                    "The first overlapping road must retain priority.");

                WorldChunkData bendEast = Generate(
                    broadPhase,
                    context,
                    new ChunkCoordinate(-1, -1),
                    8);
                WorldChunkData bendWest = Generate(
                    broadPhase,
                    context,
                    new ChunkCoordinate(-2, -1),
                    8);
                Assert.That(bendEast.GetSurface(0, 1),
                    Is.EqualTo(SurfaceKind.Riverbed));
                Assert.That(bendWest.GetSurface(7, 1),
                    Is.EqualTo(SurfaceKind.Riverbed));
            }
            finally
            {
                Object.DestroyImmediate(broadPhase);
                Object.DestroyImmediate(legacy);
                Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void BroadPhase_DistantSegmentsDoNotRejectWidthEdge()
        {
            WorldGenerationSettings settings = CreateSettings(8f, 8);
            TerrainSurfaceStage legacy = CreateStage(true, false);
            TerrainSurfaceStage broadPhase = CreateStage(true, true);
            try
            {
                var plan = new MacroWorldPlan(772);
                plan.AddRoad(new WorldRoadData
                {
                    stableId = 10,
                    roadKind = RoadKind.FortifiedRoute,
                    width = 20f,
                    centerline = new List<Vector2>
                    {
                        new Vector2(100f, 100f),
                        new Vector2(120f, 100f)
                    }
                });
                plan.AddRoad(new WorldRoadData
                {
                    stableId = 11,
                    roadKind = RoadKind.Trail,
                    width = 2f,
                    centerline = new List<Vector2>
                    {
                        new Vector2(1f, -8f),
                        new Vector2(1f, 0f)
                    }
                });
                plan.AddRiver(new WorldRiverData
                {
                    stableId = 20,
                    nominalWidth = 20f,
                    centerline = new List<Vector2>
                    {
                        new Vector2(-120f, -100f),
                        new Vector2(-100f, -100f)
                    },
                    widths = new List<float> { 20f, 20f }
                });
                plan.AddRiver(new WorldRiverData
                {
                    stableId = 21,
                    nominalWidth = 2f,
                    centerline = new List<Vector2>
                    {
                        new Vector2(-8f, 1f),
                        new Vector2(0f, 1f)
                    },
                    widths = new List<float> { 2f, 2f }
                });

                var context = new GenerationContext(772, settings, plan);
                var coordinate = new ChunkCoordinate(-1, -1);
                WorldChunkData expected =
                    Generate(legacy, context, coordinate, 8);
                WorldChunkData actual =
                    Generate(broadPhase, context, coordinate, 8);

                AssertSameSurfaces(expected, actual);
                Assert.That(actual.GetSurface(7, 3),
                    Is.EqualTo(SurfaceKind.Trail),
                    "Road width touching the eastmost cell center is local.");
                Assert.That(actual.GetSurface(3, 7),
                    Is.EqualTo(SurfaceKind.Riverbed),
                    "River width touching the northmost cell center is local.");
                Assert.That(actual.GetSurface(0, 0),
                    Is.EqualTo(SurfaceKind.Grass),
                    "Distant macro segments must not affect the chunk.");
            }
            finally
            {
                Object.DestroyImmediate(broadPhase);
                Object.DestroyImmediate(legacy);
                Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void BroadPhase_LegacyNominalRiverKeepsDegeneratePointSegment()
        {
            WorldGenerationSettings settings = CreateSettings(8f, 8);
            TerrainSurfaceStage legacy = CreateStage(false, false);
            TerrainSurfaceStage broadPhase = CreateStage(false, true);
            try
            {
                var plan = new MacroWorldPlan(773);
                plan.AddRiver(new WorldRiverData
                {
                    stableId = 30,
                    nominalWidth = 2f,
                    centerline = new List<Vector2>
                    {
                        new Vector2(-0.5f, -0.5f),
                        new Vector2(-0.5f, -0.5f)
                    }
                });
                var context = new GenerationContext(773, settings, plan);
                var coordinate = new ChunkCoordinate(-1, -1);
                WorldChunkData expected =
                    Generate(legacy, context, coordinate, 8);
                WorldChunkData actual =
                    Generate(broadPhase, context, coordinate, 8);

                AssertSameSurfaces(expected, actual);
                Assert.That(actual.GetSurface(7, 7),
                    Is.EqualTo(SurfaceKind.Riverbed));
            }
            finally
            {
                Object.DestroyImmediate(broadPhase);
                Object.DestroyImmediate(legacy);
                Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void OnlyIsolatedConceptSurfaceEnablesBroadPhase()
        {
            TerrainSurfaceStage concept =
                AssetDatabase.LoadAssetAtPath<TerrainSurfaceStage>(
                    ConceptStagePath);
            TerrainSurfaceStage main =
                AssetDatabase.LoadAssetAtPath<TerrainSurfaceStage>(
                    MainStagePath);

            Assert.That(concept, Is.Not.Null);
            Assert.That(main, Is.Not.Null);
            Assert.That(concept.UseSegmentBroadPhase, Is.True);
            Assert.That(main.UseSegmentBroadPhase, Is.False);
        }

        private static MacroWorldPlan BuildPriorityAndBendPlan()
        {
            var plan = new MacroWorldPlan(771);
            plan.AddRoad(new WorldRoadData
            {
                stableId = 1,
                roadKind = RoadKind.Trail,
                width = 1f,
                centerline = new List<Vector2>
                {
                    new Vector2(-15f, -3.5f),
                    new Vector2(-5f, -3.5f),
                    new Vector2(-5f, -3.5f),
                    new Vector2(7f, -3.5f)
                }
            });
            plan.AddRoad(new WorldRoadData
            {
                stableId = 2,
                roadKind = RoadKind.FortifiedRoute,
                width = 4f,
                centerline = new List<Vector2>
                {
                    new Vector2(-15f, -3.5f),
                    new Vector2(7f, -3.5f)
                }
            });
            plan.AddRiver(new WorldRiverData
            {
                stableId = 3,
                nominalWidth = 2f,
                centerline = new List<Vector2>
                {
                    new Vector2(-14f, -7.5f),
                    new Vector2(-8f, -7.5f),
                    new Vector2(-8f, -7.5f),
                    new Vector2(-8f, -0.5f)
                },
                widths = new List<float> { 2f, 8f, 8f, 2f }
            });
            return plan;
        }

        private static TerrainSurfaceStage CreateStage(
            bool variableRiverWidth,
            bool broadPhase)
        {
            TerrainSurfaceStage stage =
                ScriptableObject.CreateInstance<TerrainSurfaceStage>();
            SetPrivate(stage, "useVariableRiverWidth", variableRiverWidth);
            SetPrivate(stage, "useSegmentBroadPhase", broadPhase);
            return stage;
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

        private static WorldChunkData Generate(
            TerrainSurfaceStage stage,
            GenerationContext context,
            ChunkCoordinate coordinate,
            int cellsPerSide)
        {
            var chunk = new WorldChunkData(coordinate, cellsPerSide);
            stage.Generate(context, chunk);
            return chunk;
        }

        private static void AssertSameSurfaces(
            WorldChunkData expected,
            WorldChunkData actual)
        {
            for (int z = 0; z < expected.CellsPerSide; z++)
            {
                for (int x = 0; x < expected.CellsPerSide; x++)
                {
                    Assert.That(
                        actual.GetSurface(x, z),
                        Is.EqualTo(expected.GetSurface(x, z)),
                        $"Surface mismatch in {expected.Coordinate} " +
                        $"at cell ({x},{z}).");
                }
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
