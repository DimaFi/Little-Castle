using System.Collections.Generic;
using System.Reflection;
using LittleCastle.World;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace LittleCastle.Tests
{
    public sealed class RiverSourceFallbackTests
    {
        public sealed class LinearTerrainStage : WorldGenerationStage
        {
            public float baseHeight;
            public float xGradient;
            public float zGradient;
            public float slope;
            public TerrainClass terrainClass = TerrainClass.Plains;

            public override WorldGenerationStagePhase Phase =>
                WorldGenerationStagePhase.TerrainBase;

            public override void Generate(
                GenerationContext context,
                WorldChunkData chunk)
            {
                float chunkSize =
                    context.Settings.ChunkWorldSize;

                float cellSize =
                    context.Settings.CellWorldSize;

                float originX =
                    chunk.Coordinate.x *
                    chunkSize;

                float originZ =
                    chunk.Coordinate.z *
                    chunkSize;

                for (int z = 0;
                     z < chunk.SamplesPerSide;
                     z++)
                {
                    for (int x = 0;
                         x < chunk.SamplesPerSide;
                         x++)
                    {
                        float worldX =
                            originX +
                            x * cellSize;

                        float worldZ =
                            originZ +
                            z * cellSize;

                        chunk.SetHeight(
                            x,
                            z,
                            baseHeight +
                            worldX * xGradient +
                            worldZ * zGradient);
                    }
                }

                for (int z = 0;
                     z < chunk.CellsPerSide;
                     z++)
                {
                    for (int x = 0;
                         x < chunk.CellsPerSide;
                         x++)
                    {
                        chunk.SetCellSlope(
                            x,
                            z,
                            slope);

                        chunk.SetTerrainClass(
                            x,
                            z,
                            terrainClass);
                    }
                }
            }
        }

        private readonly List<Object> ownedObjects =
            new List<Object>();

        [TearDown]
        public void TearDown()
        {
            for (int i = ownedObjects.Count - 1;
                 i >= 0;
                 i--)
            {
                if (ownedObjects[i] != null)
                {
                    Object.DestroyImmediate(
                        ownedObjects[i]);
                }
            }

            ownedObjects.Clear();
        }

        [Test]
        public void DisabledFallback_PreservesLegacyOutputExactly()
        {
            const int seed = 12345;

            Rect bounds =
                new Rect(
                    -128f,
                    -128f,
                    256f,
                    256f);

            RiverPlannerSettings firstSettings =
                CreateOrdinarySettings();

            RiverPlannerSettings secondSettings =
                CreateOrdinarySettings();

            secondSettings.useNaturalSourceFallback = false;
            secondSettings.fallbackSourceSpacing = 777f;
            secondSettings.fallbackMaximumAttempts = 1;
            secondSettings.fallbackSourceTerrain =
                TerrainClassMask.None;
            secondSettings.fallbackMinSourceHeight = 9999f;
            secondSettings.fallbackMinimumNetDrop = 9999f;

            var first = new MacroWorldPlan(seed);
            var second = new MacroWorldPlan(seed);
            var diagnostics = new RiverPlannerDiagnostics();

            RiverNetworkPlanner.BuildRivers(
                seed,
                bounds,
                CreateProbe(seed, 20f, -0.04f, 0f),
                firstSettings,
                first);

            RiverNetworkPlanner.BuildRivers(
                seed,
                bounds,
                CreateProbe(seed, 20f, -0.04f, 0f),
                secondSettings,
                second,
                diagnostics);

            Assert.That(first.Rivers.Count, Is.GreaterThan(0));
            Assert.That(diagnostics.fallbackTriggered, Is.False);
            AssertPlansEqual(first, second);
        }

        [Test]
        public void Fallback_IsRepeatableAcrossNegativeCoordinates()
        {
            const int seed = -10101;

            Rect bounds =
                new Rect(
                    -320f,
                    -288f,
                    192f,
                    160f);

            RiverPlannerSettings settings =
                CreateFallbackSettings();

            var first = new MacroWorldPlan(seed);
            var second = new MacroWorldPlan(seed);
            var firstDiagnostics = new RiverPlannerDiagnostics();
            var secondDiagnostics = new RiverPlannerDiagnostics();

            RiverNetworkPlanner.BuildRivers(
                seed,
                bounds,
                CreateProbe(seed, 20f, -0.05f, 0f),
                settings,
                first,
                firstDiagnostics);

            RiverNetworkPlanner.BuildRivers(
                seed,
                bounds,
                CreateProbe(seed, 20f, -0.05f, 0f),
                settings,
                second,
                secondDiagnostics);

            Assert.That(firstDiagnostics.fallbackTriggered, Is.True);
            Assert.That(firstDiagnostics.fallbackAcceptedRivers, Is.EqualTo(1));
            Assert.That(
                firstDiagnostics.fallbackTraceAttempts,
                Is.LessThanOrEqualTo(
                    settings.fallbackMaximumAttempts));

            Assert.That(first.Rivers.Count, Is.EqualTo(1));
            Assert.That(first.Rivers[0].centerline[0].x, Is.LessThan(0f));
            Assert.That(first.Rivers[0].centerline[0].y, Is.LessThan(0f));
            AssertPlansEqual(first, second);

            Assert.That(
                firstDiagnostics.fallbackAcceptedRiverId,
                Is.EqualTo(
                    secondDiagnostics.fallbackAcceptedRiverId));
        }

        [Test]
        public void Fallback_AttemptBudgetCapsCandidateProbesAndTraces()
        {
            const int seed = 777;

            RiverPlannerSettings settings =
                CreateFallbackSettings();

            settings.fallbackMaximumAttempts = 3;
            settings.maxSteps = 4;

            var plan = new MacroWorldPlan(seed);
            var diagnostics = new RiverPlannerDiagnostics();

            RiverNetworkPlanner.BuildRivers(
                seed,
                new Rect(-512f, -512f, 1024f, 1024f),
                CreateProbe(seed, 5f, 0f, 0f),
                settings,
                plan,
                diagnostics);

            Assert.That(diagnostics.fallbackTriggered, Is.True);
            Assert.That(diagnostics.fallbackAttemptLimit, Is.EqualTo(3));
            Assert.That(diagnostics.fallbackCandidatesEvaluated, Is.EqualTo(3));
            Assert.That(diagnostics.fallbackTraceAttempts, Is.EqualTo(3));
            Assert.That(plan.Rivers.Count, Is.Zero);
        }

        [Test]
        public void Fallback_FlatTerrainDoesNotInventRiver()
        {
            const int seed = 54321;

            RiverPlannerSettings settings =
                CreateFallbackSettings();

            settings.fallbackMaximumAttempts = 8;
            settings.fallbackMinimumNetDrop = 0.25f;

            var plan = new MacroWorldPlan(seed);
            var diagnostics = new RiverPlannerDiagnostics();

            RiverNetworkPlanner.BuildRivers(
                seed,
                new Rect(-192f, -192f, 384f, 384f),
                CreateProbe(seed, 3f, 0f, 0f),
                settings,
                plan,
                diagnostics);

            Assert.That(plan.Rivers.Count, Is.Zero);
            Assert.That(diagnostics.fallbackAcceptedRivers, Is.Zero);
            Assert.That(
                diagnostics.fallbackRejectedInsufficientDrop,
                Is.GreaterThan(0));
        }

        [Test]
        public void Fallback_CanFormRealDownhillRiverWhenOrdinarySourcesMiss()
        {
            const int seed = 54321;

            RiverPlannerSettings settings =
                CreateFallbackSettings();

            settings.sourceSpacing = 1400f;
            settings.sourceTerrain =
                TerrainClassMask.Highlands;

            var plan = new MacroWorldPlan(seed);
            var diagnostics = new RiverPlannerDiagnostics();
            WorldTerrainProbe probe =
                CreateProbe(seed, 20f, -0.05f, 0f);

            RiverNetworkPlanner.BuildRivers(
                seed,
                new Rect(-192f, -192f, 384f, 384f),
                probe,
                settings,
                plan,
                diagnostics);

            Assert.That(diagnostics.ordinaryAcceptedRivers, Is.Zero);
            Assert.That(diagnostics.fallbackAcceptedRivers, Is.EqualTo(1));
            Assert.That(plan.Rivers.Count, Is.EqualTo(1));

            WorldRiverData river = plan.Rivers[0];
            Assert.That(
                river.centerline.Count,
                Is.GreaterThanOrEqualTo(
                    settings.minimumPoints));

            float sourceHeight =
                probe.Sample(
                    river.centerline[0]).height;

            float terminalHeight =
                probe.Sample(
                    river.centerline[
                        river.centerline.Count - 1]).height;

            Assert.That(
                sourceHeight - terminalHeight,
                Is.GreaterThanOrEqualTo(
                    settings.fallbackMinimumNetDrop));

            Assert.That(
                river.flow.Count,
                Is.EqualTo(
                    river.centerline.Count));

            Assert.That(
                river.widths.Count,
                Is.EqualTo(
                    river.centerline.Count));

            Assert.That(
                river.depths.Count,
                Is.EqualTo(
                    river.centerline.Count));
        }

        private static RiverPlannerSettings CreateOrdinarySettings()
        {
            return
                new RiverPlannerSettings
                {
                    enabled = true,
                    sourceSpacing = 100f,
                    sourceChance = 1f,
                    sourceTerrain = TerrainClassMask.Plains,
                    minSourceHeight = -1000f,
                    maxSourceSlope = 90f,
                    useNaturalSourceFallback = false,
                    traceStep = 8f,
                    directionSamples = 8,
                    maxSteps = 8,
                    minimumPreferredDrop = 0.01f,
                    maxAllowedRise = 0f,
                    turnPenalty = 0f,
                    stopHeight = -1000f,
                    mergeDistance = 8f,
                    minimumStepsBeforeMerge = 2,
                    minimumPoints = 4
                };
        }

        private static RiverPlannerSettings CreateFallbackSettings()
        {
            RiverPlannerSettings settings =
                CreateOrdinarySettings();

            settings.sourceChance = 0f;
            settings.sourceTerrain =
                TerrainClassMask.Highlands;
            settings.useNaturalSourceFallback = true;
            settings.fallbackSourceSpacing = 100f;
            settings.fallbackMaximumAttempts = 8;
            settings.fallbackSourceTerrain =
                TerrainClassMask.Plains;
            settings.fallbackMinSourceHeight = -1000f;
            settings.fallbackMinimumNetDrop = 0.5f;
            return settings;
        }

        private WorldTerrainProbe CreateProbe(
            int seed,
            float baseHeight,
            float xGradient,
            float zGradient)
        {
            LinearTerrainStage stage =
                Track(
                    ScriptableObject.CreateInstance<
                        LinearTerrainStage>());

            stage.baseHeight = baseHeight;
            stage.xGradient = xGradient;
            stage.zGradient = zGradient;
            stage.slope = 0f;
            stage.terrainClass = TerrainClass.Plains;

            WorldGenerationSettings settings =
                Track(
                    ScriptableObject.CreateInstance<
                        WorldGenerationSettings>());

            SetPrivateField(
                settings,
                "chunkWorldSize",
                32f);

            SetPrivateField(
                settings,
                "cellsPerSide",
                32);

            var stages =
                (List<WorldGenerationStage>)GetPrivateField(
                    settings,
                    "stages");

            stages.Clear();
            stages.Add(stage);

            return
                new WorldTerrainProbe(
                    new WorldGenerationPipeline(settings),
                    settings,
                    seed);
        }

        private T Track<T>(T value)
            where T : Object
        {
            ownedObjects.Add(value);
            return value;
        }

        private static void SetPrivateField(
            object target,
            string name,
            object value)
        {
            FieldInfo field =
                target.GetType().GetField(
                    name,
                    BindingFlags.Instance |
                    BindingFlags.NonPublic);

            Assert.That(
                field,
                Is.Not.Null,
                "Missing field: " + name);

            field.SetValue(target, value);
        }

        private static object GetPrivateField(
            object target,
            string name)
        {
            FieldInfo field =
                target.GetType().GetField(
                    name,
                    BindingFlags.Instance |
                    BindingFlags.NonPublic);

            Assert.That(
                field,
                Is.Not.Null,
                "Missing field: " + name);

            return field.GetValue(target);
        }

        private static void AssertPlansEqual(
            MacroWorldPlan expected,
            MacroWorldPlan actual)
        {
            Assert.That(
                actual.Rivers.Count,
                Is.EqualTo(
                    expected.Rivers.Count));

            for (int r = 0;
                 r < expected.Rivers.Count;
                 r++)
            {
                WorldRiverData a = expected.Rivers[r];
                WorldRiverData b = actual.Rivers[r];

                Assert.That(b.stableId, Is.EqualTo(a.stableId));
                Assert.That(b.nominalWidth, Is.EqualTo(a.nominalWidth));
                Assert.That(b.nominalDepth, Is.EqualTo(a.nominalDepth));
                Assert.That(
                    b.downstreamRiverId,
                    Is.EqualTo(
                        a.downstreamRiverId));
                Assert.That(
                    b.downstreamJoinPointIndex,
                    Is.EqualTo(
                        a.downstreamJoinPointIndex));
                Assert.That(
                    b.confluencePosition,
                    Is.EqualTo(
                        a.confluencePosition));

                AssertFloatListsEqual(a.centerline, b.centerline);
                AssertFloatListsEqual(a.flow, b.flow);
                AssertFloatListsEqual(a.widths, b.widths);
                AssertFloatListsEqual(a.depths, b.depths);
            }
        }

        private static void AssertFloatListsEqual(
            IReadOnlyList<Vector2> expected,
            IReadOnlyList<Vector2> actual)
        {
            Assert.That(actual.Count, Is.EqualTo(expected.Count));

            for (int i = 0; i < expected.Count; i++)
            {
                Assert.That(
                    actual[i].x,
                    Is.EqualTo(expected[i].x));
                Assert.That(
                    actual[i].y,
                    Is.EqualTo(expected[i].y));
            }
        }

        private static void AssertFloatListsEqual(
            IReadOnlyList<float> expected,
            IReadOnlyList<float> actual)
        {
            Assert.That(actual.Count, Is.EqualTo(expected.Count));

            for (int i = 0; i < expected.Count; i++)
            {
                Assert.That(
                    actual[i],
                    Is.EqualTo(expected[i]));
            }
        }
    }
}
