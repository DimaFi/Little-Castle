using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using LittleCastle.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace LittleCastle.Tests
{
    public sealed class WorldGenerationIntegrationTests
    {
        private const string DefinitionPath =
            "Assets/_Game/Settings/World/MainWorldDefinition.asset";
        private const float Epsilon = 0.0001f;

        private WorldDefinition definition;

        [SetUp]
        public void SetUp()
        {
            definition = AssetDatabase.LoadAssetAtPath<WorldDefinition>(DefinitionPath);
            Assert.That(definition, Is.Not.Null, "Run the integration bootstrap first.");
        }

        [Test]
        public void Configuration_IsValidAndUsesExpectedPipelineOrder()
        {
            WorldConfigurationValidationReport report =
                WorldGenerationConfigurationValidator.Validate(definition);

            Assert.That(report.IsValid, Is.True, report.ToMultilineString());
            Assert.That(definition.GenerationSettings.CellsPerSide, Is.EqualTo(32));
            Assert.That(definition.GenerationSettings.ChunkWorldSize, Is.EqualTo(64f));
            Assert.That(definition.GenerationSettings.Stages.Count, Is.EqualTo(11));

            WorldGenerationStagePhase previous = WorldGenerationStagePhase.TerrainBase;
            foreach (WorldGenerationStage stage in definition.GenerationSettings.Stages)
            {
                Assert.That(stage, Is.Not.Null);
                Assert.That(stage.Phase, Is.GreaterThanOrEqualTo(previous));
                previous = stage.Phase;
            }
        }

        [Test]
        public void Generation_IsDeterministicSeamlessAndSupportsNegativeCoordinates()
        {
            MacroWorldPlan plan = BuildPlan(12345);
            var pipeline = new WorldGenerationPipeline(definition.GenerationSettings, plan);

            ChunkCoordinate[] coordinates =
            {
                new ChunkCoordinate(0, 0),
                new ChunkCoordinate(-1, 0),
                new ChunkCoordinate(0, -1),
                new ChunkCoordinate(-1, -1),
                new ChunkCoordinate(-10, 4)
            };

            foreach (ChunkCoordinate coordinate in coordinates)
            {
                AssertDiagnostic(
                    WorldGenerationDiagnostics.ValidateDeterminism(
                        pipeline, 12345, coordinate, Epsilon, out string message),
                    message);

                AssertDiagnostic(
                    WorldGenerationDiagnostics.ValidateSpawnDeterminism(
                        pipeline, 12345, coordinate, Epsilon, out message),
                    message);

                AssertDiagnostic(
                    WorldGenerationDiagnostics.ValidateEastWestBorder(
                        pipeline, 12345, coordinate, Epsilon, out message),
                    message);

                AssertDiagnostic(
                    WorldGenerationDiagnostics.ValidateNorthSouthBorder(
                        pipeline, 12345, coordinate, Epsilon, out message),
                    message);
            }

            Assert.That(
                WorldChunkCoordinateUtility.FromWorldPosition(-0.01f, -0.01f, 64f),
                Is.EqualTo(new ChunkCoordinate(-1, -1)));
            Assert.That(
                WorldChunkCoordinateUtility.FromWorldPosition(-640f, 256f, 64f),
                Is.EqualTo(new ChunkCoordinate(-10, 4)));

            WorldChunkData seedA = pipeline.GenerateChunk(12345, new ChunkCoordinate(-1, -1));
            WorldChunkData seedB = pipeline.GenerateChunk(54321, new ChunkCoordinate(-1, -1));
            Assert.That(HashChunk(seedA), Is.Not.EqualTo(HashChunk(seedB)));
        }

        [Test]
        public void EnvironmentSpawnsAndResources_ArePopulatedAndStable()
        {
            int[] seeds = { 12345, 54321, -10101 };
            var biomeKinds = new HashSet<BiomeKind>();
            int totalSpawns = 0;
            int totalDeposits = 0;
            float forestMin = 1f;
            float forestMax = 0f;

            foreach (int seed in seeds)
            {
                MacroWorldPlan plan = BuildPlan(seed);
                var pipeline = new WorldGenerationPipeline(definition.GenerationSettings, plan);

                foreach (ChunkCoordinate coordinate in new[]
                {
                    new ChunkCoordinate(0, 0),
                    new ChunkCoordinate(1, 0),
                    new ChunkCoordinate(-1, -1),
                    new ChunkCoordinate(-2, 1)
                })
                {
                    WorldChunkData chunk = pipeline.GenerateChunk(seed, coordinate);
                    var ids = new HashSet<long>();

                    foreach (WorldSpawnData spawn in chunk.Spawns)
                    {
                        Assert.That(ids.Add(spawn.stableId), Is.True,
                            "Duplicate stable spawn ID in " + coordinate + ".");
                    }

                    totalSpawns += chunk.Spawns.Count;
                    totalDeposits += chunk.ResourceDeposits.Count;

                    for (int z = 0; z < chunk.CellsPerSide; z++)
                    {
                        for (int x = 0; x < chunk.CellsPerSide; x++)
                        {
                            float temperature = chunk.GetTemperature(x, z);
                            float moisture = chunk.GetMoisture(x, z);
                            float forest = chunk.GetForestDensity(x, z);
                            float grass = chunk.GetGrassDensity(x, z);

                            Assert.That(temperature, Is.InRange(0f, 1f));
                            Assert.That(moisture, Is.InRange(0f, 1f));
                            Assert.That(forest, Is.InRange(0f, 1f));
                            Assert.That(grass, Is.InRange(0f, 1f));

                            biomeKinds.Add(chunk.GetBiome(x, z));
                            forestMin = Mathf.Min(forestMin, forest);
                            forestMax = Mathf.Max(forestMax, forest);
                        }
                    }
                }
            }

            if (totalDeposits == 0)
            {
                const int resourceSearchSeed = 12345;
                var resourcePipeline = new WorldGenerationPipeline(
                    definition.GenerationSettings,
                    BuildPlan(resourceSearchSeed));

                for (int z = -5; z <= 5 && totalDeposits == 0; z++)
                {
                    for (int x = -5; x <= 5 && totalDeposits == 0; x++)
                    {
                        totalDeposits += resourcePipeline.GenerateChunk(
                            resourceSearchSeed,
                            new ChunkCoordinate(x, z)).ResourceDeposits.Count;
                    }
                }
            }

            Assert.That(totalSpawns, Is.GreaterThan(0));
            Assert.That(totalDeposits, Is.GreaterThan(0));
            Assert.That(biomeKinds.Count, Is.GreaterThan(1));
            Assert.That(forestMax - forestMin, Is.GreaterThan(0.01f));

            UnityEngine.Debug.Log(
                $"WORLD_VERIFY environment spawns={totalSpawns}, deposits={totalDeposits}, " +
                $"biomes={biomeKinds.Count}, forestRange={forestMin:F3}..{forestMax:F3}");
        }

        [Test]
        public void MacroPlan_RiversRoadsBridgesAndCarvingRemainConsistent()
        {
            int[] seeds = { 12345, 54321, -10101, 777 };
            int riverCount = 0;
            int roadCount = 0;
            int bridgeCount = 0;
            int confluenceCount = 0;
            bool carvingObserved = false;

            foreach (int seed in seeds)
            {
                MacroWorldPlan first = BuildPlan(seed);
                MacroWorldPlan second = BuildPlan(seed);

                Assert.That(HashMacroPlan(first), Is.EqualTo(HashMacroPlan(second)),
                    "Macro plan changed between identical runs for seed " + seed + ".");

                riverCount += first.Rivers.Count;
                roadCount += first.Roads.Count;
                bridgeCount += first.BridgeSites.Count;

                foreach (WorldRiverData river in first.Rivers)
                {
                    Assert.That(river.centerline.Count, Is.GreaterThanOrEqualTo(5));
                    Assert.That(river.widths.Count, Is.EqualTo(river.centerline.Count));
                    Assert.That(river.depths.Count, Is.EqualTo(river.centerline.Count));
                    Assert.That(river.flow.Count, Is.EqualTo(river.centerline.Count));
                    Assert.That(river.GetWidthAtPoint(0), Is.GreaterThan(0f));
                    Assert.That(river.GetDepthAtPoint(0), Is.GreaterThan(0f));
                    Assert.That(river.GetTerminalFlow(), Is.GreaterThanOrEqualTo(river.GetFlowAtPoint(0)));
                    Assert.That(
                        river.GetWidthAtPoint(river.centerline.Count - 1),
                        Is.GreaterThanOrEqualTo(river.GetWidthAtPoint(0) - Epsilon));

                    if (river.HasConfluence)
                    {
                        confluenceCount++;
                        Assert.That(river.downstreamRiverId, Is.Not.EqualTo(0));
                        Assert.That(river.downstreamJoinPointIndex, Is.GreaterThanOrEqualTo(0));
                    }
                }

                foreach (WorldRoadData road in first.Roads)
                {
                    Assert.That(road.centerline.Count, Is.GreaterThanOrEqualTo(2));
                    Assert.That(road.width, Is.GreaterThan(0f));
                }

                foreach (WorldBridgeSiteData bridge in first.BridgeSites)
                {
                    Assert.That(bridge.requiredSpan, Is.GreaterThan(0f));
                    Assert.That(FindById(first.Rivers, bridge.riverId), Is.Not.Null);
                    Assert.That(FindRoadById(first.Roads, bridge.roadId), Is.Not.Null);
                }

                if (!carvingObserved && first.Rivers.Count > 0)
                    carvingObserved = HasObservableRiverCarving(seed, first);
            }

            Assert.That(riverCount, Is.GreaterThan(0));
            Assert.That(roadCount, Is.GreaterThan(0));
            Assert.That(confluenceCount, Is.GreaterThan(0),
                "No tributary confluence was generated across the verification seeds.");
            Assert.That(carvingObserved, Is.True, "No river terrain carving was observed.");

            UnityEngine.Debug.Log(
                $"Macro totals: rivers={riverCount}, confluences={confluenceCount}, " +
                $"roads={roadCount}, bridges={bridgeCount}.");
        }

        [Test]
        public void ChunkCache_PinUnpinLruAndRuntimeDeltaSurviveRegeneration()
        {
            MacroWorldPlan plan = BuildPlan(12345);
            var pipeline = new WorldGenerationPipeline(definition.GenerationSettings, plan);
            var cache = new WorldChunkCache(pipeline, 12345, 2);

            var a = new ChunkCoordinate(0, 0);
            var b = new ChunkCoordinate(1, 0);
            var c = new ChunkCoordinate(2, 0);

            WorldChunkData original = cache.GetOrGeneratePinned(a);
            cache.GetOrGeneratePinned(b);
            cache.GetOrGeneratePinned(c);

            Assert.That(cache.Count, Is.EqualTo(3), "Pinned entries must make capacity a soft limit.");
            Assert.That(cache.IsPinned(a), Is.True);
            Assert.That(cache.IsPinned(b), Is.True);
            Assert.That(cache.IsPinned(c), Is.True);

            cache.Unpin(a);
            Assert.That(cache.Count, Is.EqualTo(2));
            Assert.That(cache.TryGet(a, out _), Is.False, "Oldest unpinned entry should be evicted.");

            WorldChunkData regenerated = pipeline.GenerateChunk(12345, a);
            Assert.That(HashChunk(regenerated), Is.EqualTo(HashChunk(original)));

            Assert.That(regenerated.Spawns.Count, Is.GreaterThan(0));
            long removedId = regenerated.Spawns[0].stableId;
            var delta = new WorldRuntimeDeltaState();
            Assert.That(delta.MarkSpawnRemoved(removedId), Is.True);

            string json = JsonUtility.ToJson(delta);
            WorldRuntimeDeltaState restored = JsonUtility.FromJson<WorldRuntimeDeltaState>(json);
            restored.RebuildIndexes();
            Assert.That(restored.IsSpawnRemoved(removedId), Is.True);

            if (regenerated.ResourceDeposits.Count > 0)
            {
                WorldResourceDepositData deposit = regenerated.ResourceDeposits[0];
                restored.SetRemainingCapacity(deposit.stableId, 7);
                Assert.That(restored.GetRemainingCapacity(deposit), Is.EqualTo(7));
            }
        }

        [Test]
        public void Streamer_LoadsNearestFirstUnloadsCleansMeshesAndKeepsChunksStationary()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/_Game/Materials/WorldTerrainDebug.mat");

            var root = new GameObject("StreamingTestRoot");
            var presentation = new GameObject("StreamingPresentationRoot");
            var focus = new GameObject("StreamingFocus");
            presentation.transform.SetParent(root.transform, false);

            WorldStreamer streamer = root.AddComponent<WorldStreamer>();
            SetPrivateField(streamer, "worldSeed", 12345);
            SetPrivateField(streamer, "worldDefinition", definition);
            SetPrivateField(streamer, "focus", focus.transform);
            SetPrivateField(streamer, "chunkPresentationRoot", presentation.transform);
            SetPrivateField(streamer, "terrainMaterial", material);

            try
            {
                streamer.InitializeStreaming();
                Assert.That(streamer.ActiveChunkCount, Is.EqualTo(0));

                InvokePrivate(streamer, "ProcessLoads");
                Assert.That(streamer.ActiveChunkCount, Is.EqualTo(1),
                    "Load budget should allow exactly one first chunk.");

                StreamedChunkView firstView =
                    presentation.GetComponentInChildren<StreamedChunkView>();
                Assert.That(firstView.Coordinate, Is.EqualTo(new ChunkCoordinate(0, 0)),
                    "Nearest chunk must load first.");

                for (int i = 0; i < 8; i++)
                    InvokePrivate(streamer, "ProcessLoads");

                Assert.That(streamer.ActiveChunkCount, Is.EqualTo(5));
                Vector3 presentationPosition = presentation.transform.position;

                StreamedChunkView[] oldViews =
                    presentation.GetComponentsInChildren<StreamedChunkView>();
                var oldMeshes = new List<Mesh>();
                foreach (StreamedChunkView view in oldViews)
                {
                    Mesh mesh = view.GetComponent<MeshFilter>().sharedMesh;
                    oldMeshes.Add(mesh);
                    Assert.That(
                        view.transform.position,
                        Is.EqualTo(view.Coordinate.GetWorldOrigin(64f)));
                }

                focus.transform.position = new Vector3(64f * 4f, 0f, -64f);
                Assert.That(presentation.transform.position, Is.EqualTo(presentationPosition));
                foreach (StreamedChunkView view in oldViews)
                    Assert.That(view.transform.position, Is.EqualTo(view.Coordinate.GetWorldOrigin(64f)));

                streamer.RefreshStreamingNow();
                for (int i = 0; i < 12; i++)
                {
                    InvokePrivate(streamer, "ProcessUnloads");
                    InvokePrivate(streamer, "ProcessLoads");
                }

                Assert.That(streamer.ActiveChunkCount, Is.EqualTo(5));
                Assert.That(streamer.CachedChunkCount, Is.LessThanOrEqualTo(9));

                foreach (Mesh oldMesh in oldMeshes)
                    Assert.That(oldMesh == null, Is.True, "Unloaded runtime mesh was not destroyed.");

                var cache = (WorldChunkCache)GetPrivateField(streamer, "chunkCache");
                foreach (StreamedChunkView view in
                    presentation.GetComponentsInChildren<StreamedChunkView>())
                {
                    Assert.That(cache.IsPinned(view.Coordinate), Is.True);
                    Assert.That(view.transform.position,
                        Is.EqualTo(view.Coordinate.GetWorldOrigin(64f)));
                }

                VerifyRemovedSpawnSurvivesStreamerRegeneration(
                    streamer,
                    focus.transform,
                    presentation.transform,
                    cache);

                streamer.ShutdownStreaming();
                Assert.That(streamer.ActiveChunkCount, Is.EqualTo(0));
                Assert.That(presentation.GetComponentsInChildren<StreamedChunkView>().Length, Is.EqualTo(0));
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(focus);
            }
        }

        [Test]
        public void ChunkGeneration_PerformanceSanity()
        {
            MacroWorldPlan plan = BuildPlan(12345);
            var pipeline = new WorldGenerationPipeline(definition.GenerationSettings, plan);
            var timings = new List<double>();

            for (int z = -1; z <= 1; z++)
            {
                for (int x = -1; x <= 1; x++)
                {
                    var watch = Stopwatch.StartNew();
                    pipeline.GenerateChunk(12345, new ChunkCoordinate(x, z));
                    watch.Stop();
                    timings.Add(watch.Elapsed.TotalMilliseconds);
                }
            }

            timings.Sort();
            double maximum = timings[timings.Count - 1];
            double median = timings[timings.Count / 2];
            UnityEngine.Debug.Log(
                $"Chunk generation timing (Editor): median={median:F2} ms, max={maximum:F2} ms.");

            Assert.That(maximum, Is.LessThan(2000d),
                "A single development chunk took more than two seconds to generate.");
        }

        private MacroWorldPlan BuildPlan(int seed)
        {
            var terrainOnly = new WorldGenerationPipeline(definition.GenerationSettings, null);
            var probe = new WorldTerrainProbe(
                terrainOnly,
                definition.GenerationSettings,
                seed);
            var planner = new MacroWorldPlanner(definition.MacroPlannerSettings);
            return planner.GenerateForBounds(
                seed,
                new Rect(-320f, -320f, 640f, 640f),
                probe);
        }

        private bool HasObservableRiverCarving(int seed, MacroWorldPlan plan)
        {
            var basePipeline = new WorldGenerationPipeline(definition.GenerationSettings, null);
            var carvedPipeline = new WorldGenerationPipeline(definition.GenerationSettings, plan);

            foreach (WorldRiverData river in plan.Rivers)
            {
                foreach (Vector2 point in river.centerline)
                {
                    ChunkCoordinate coordinate = WorldChunkCoordinateUtility.FromWorldPosition(
                        point.x,
                        point.y,
                        definition.GenerationSettings.ChunkWorldSize);
                    WorldChunkData baseChunk = basePipeline.GenerateChunk(seed, coordinate);
                    WorldChunkData carvedChunk = carvedPipeline.GenerateChunk(seed, coordinate);
                    float before = WorldChunkSampling.SampleHeight(
                        baseChunk, definition.GenerationSettings, point.x, point.y);
                    float after = WorldChunkSampling.SampleHeight(
                        carvedChunk, definition.GenerationSettings, point.x, point.y);

                    if (after < before - 0.001f)
                    {
                        PlacementBlockFlags blocks = WorldChunkSampling.SamplePlacementBlocks(
                            carvedChunk, definition.GenerationSettings, point.x, point.y);
                        Assert.That(blocks, Is.EqualTo(PlacementBlockFlags.All));
                        return true;
                    }
                }
            }

            return false;
        }

        private static void VerifyRemovedSpawnSurvivesStreamerRegeneration(
            WorldStreamer streamer,
            Transform focus,
            Transform presentation,
            WorldChunkCache cache)
        {
            GeneratedWorldObject generated =
                presentation.GetComponentInChildren<GeneratedWorldObject>();
            Assert.That(generated, Is.Not.Null, "Streaming test needs at least one generated object.");

            long removedId = generated.StableId;
            StreamedChunkView owningView = generated.GetComponentInParent<StreamedChunkView>();
            ChunkCoordinate removedCoordinate = owningView.Coordinate;
            Assert.That(streamer.RuntimeDelta.MarkSpawnRemoved(removedId), Is.True);

            focus.position = new Vector3(
                (removedCoordinate.x + 5) * 64f,
                0f,
                removedCoordinate.z * 64f);
            streamer.RefreshStreamingNow();
            for (int i = 0; i < 12; i++)
            {
                InvokePrivate(streamer, "ProcessUnloads");
                InvokePrivate(streamer, "ProcessLoads");
            }

            Assert.That(cache.IsPinned(removedCoordinate), Is.False);
            cache.Remove(removedCoordinate);

            focus.position = removedCoordinate.GetWorldOrigin(64f);
            streamer.RefreshStreamingNow();
            for (int i = 0; i < 12; i++)
            {
                InvokePrivate(streamer, "ProcessUnloads");
                InvokePrivate(streamer, "ProcessLoads");
            }

            foreach (GeneratedWorldObject worldObject in
                presentation.GetComponentsInChildren<GeneratedWorldObject>())
            {
                Assert.That(worldObject.StableId, Is.Not.EqualTo(removedId),
                    "Removed generated object returned after unload, eviction and regeneration.");
            }
        }

        private static WorldRiverData FindById(
            IReadOnlyList<WorldRiverData> rivers,
            long stableId)
        {
            for (int i = 0; i < rivers.Count; i++)
                if (rivers[i].stableId == stableId)
                    return rivers[i];
            return null;
        }

        private static WorldRoadData FindRoadById(
            IReadOnlyList<WorldRoadData> roads,
            long stableId)
        {
            for (int i = 0; i < roads.Count; i++)
                if (roads[i].stableId == stableId)
                    return roads[i];
            return null;
        }

        private static long HashChunk(WorldChunkData chunk)
        {
            unchecked
            {
                long hash = 1469598103934665603L;
                for (int z = 0; z < chunk.SamplesPerSide; z++)
                    for (int x = 0; x < chunk.SamplesPerSide; x++)
                        hash = (hash ^ BitConverter.SingleToInt32Bits(chunk.GetHeight(x, z))) * 1099511628211L;

                foreach (WorldSpawnData spawn in chunk.Spawns)
                    hash = (hash ^ spawn.stableId) * 1099511628211L;
                foreach (WorldResourceDepositData deposit in chunk.ResourceDeposits)
                    hash = (hash ^ deposit.stableId) * 1099511628211L;
                return hash;
            }
        }

        private static long HashMacroPlan(MacroWorldPlan plan)
        {
            unchecked
            {
                long hash = plan.WorldSeed;
                foreach (WorldPointFeatureData feature in plan.PointFeatures)
                    hash = (hash * 397) ^ feature.stableId;
                foreach (WorldRiverData river in plan.Rivers)
                {
                    hash = (hash * 397) ^ river.stableId;
                    foreach (Vector2 point in river.centerline)
                    {
                        hash = (hash * 397) ^ BitConverter.SingleToInt32Bits(point.x);
                        hash = (hash * 397) ^ BitConverter.SingleToInt32Bits(point.y);
                    }
                }
                foreach (WorldRoadData road in plan.Roads)
                    hash = (hash * 397) ^ road.stableId;
                foreach (WorldBridgeSiteData bridge in plan.BridgeSites)
                    hash = (hash * 397) ^ bridge.stableId;
                return hash;
            }
        }

        private static void AssertDiagnostic(bool passed, string message)
        {
            Assert.That(passed, Is.True, message);
        }

        private static void SetPrivateField(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(
                name,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, "Missing private field: " + name);
            field.SetValue(target, value);
        }

        private static object GetPrivateField(object target, string name)
        {
            FieldInfo field = target.GetType().GetField(
                name,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, "Missing private field: " + name);
            return field.GetValue(target);
        }

        private static void InvokePrivate(object target, string name)
        {
            MethodInfo method = target.GetType().GetMethod(
                name,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, "Missing private method: " + name);
            method.Invoke(target, null);
        }
    }
}
