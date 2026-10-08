using System;
using System.Collections.Generic;
using System.Reflection;
using LittleCastle.World;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace LittleCastle.Tests
{
    public sealed class FixedBridgeGenerationTests
    {
        private const float Epsilon = 0.0001f;

        public sealed class LinearHeightStage : WorldGenerationStage
        {
            public float baseHeight;
            public float xGradient;
            public float zGradient;

            public override WorldGenerationStagePhase Phase =>
                WorldGenerationStagePhase.TerrainBase;

            public override void Generate(
                GenerationContext context,
                WorldChunkData chunk)
            {
                float chunkSize = context.Settings.ChunkWorldSize;
                float cellSize = context.Settings.CellWorldSize;
                float originX = chunk.Coordinate.x * chunkSize;
                float originZ = chunk.Coordinate.z * chunkSize;

                for (int z = 0; z < chunk.SamplesPerSide; z++)
                {
                    for (int x = 0; x < chunk.SamplesPerSide; x++)
                    {
                        float worldX = originX + x * cellSize;
                        float worldZ = originZ + z * cellSize;

                        chunk.SetHeight(
                            x,
                            z,
                            baseHeight +
                            worldX * xGradient +
                            worldZ * zGradient);
                    }
                }
            }
        }

        private readonly List<Object> createdObjects =
            new List<Object>();

        [TearDown]
        public void TearDown()
        {
            for (int i = createdObjects.Count - 1; i >= 0; i--)
            {
                if (createdObjects[i] != null)
                    Object.DestroyImmediate(createdObjects[i]);
            }

            createdObjects.Clear();
        }

        [Test]
        public void FixedProfile_MatchesV002ContractAndYawAxes()
        {
            Assert.That(FixedBridgeSiteProfile.AssetId, Is.EqualTo("ENV_Bridge_Stone_A"));
            Assert.That(FixedBridgeSiteProfile.ContractVersion, Is.EqualTo("v002"));
            Assert.That(FixedBridgeSiteProfile.BridgeLength, Is.EqualTo(10.8f));
            Assert.That(FixedBridgeSiteProfile.StructuralWidth, Is.EqualTo(3.6f));
            Assert.That(FixedBridgeSiteProfile.CapWidth, Is.EqualTo(3.78f));
            Assert.That(FixedBridgeSiteProfile.ClearPathWidth, Is.EqualTo(2.86f));
            Assert.That(FixedBridgeSiteProfile.DeckCrownHeight, Is.EqualTo(1.05f));
            Assert.That(FixedBridgeSiteProfile.WaterHeightOffset, Is.EqualTo(-0.85f));
            Assert.That(FixedBridgeSiteProfile.SupportHalfExtentX, Is.EqualTo(7f));
            Assert.That(FixedBridgeSiteProfile.SupportHalfExtentZ, Is.EqualTo(8f));

            Assert.That(
                FixedBridgeSiteProfile.TerrainHeight(0f, 0f),
                Is.EqualTo(-1.185f).Within(0.00001f));

            Assert.That(
                FixedBridgeSiteProfile.TerrainHeight(3.25f, 1.75f),
                Is.EqualTo(-1.2025466f).Within(0.00001f));

            Assert.That(
                FixedBridgeSiteProfile.StampWeight(-6.1f, 7.1f),
                Is.EqualTo(0.275562f).Within(0.00001f));

            Assert.That(
                FixedBridgeSiteProfile.TerrainHeight(0f, 5.4f),
                Is.Zero.Within(0.00001f));

            Assert.That(FixedBridgeSiteProfile.StampWeight(7f, 0f), Is.Zero);
            Assert.That(FixedBridgeSiteProfile.StampWeight(0f, -8f), Is.Zero);

            var origin = new Vector2(-17f, -9f);
            var local = new Vector2(3.25f, 1.75f);
            Vector2 world =
                FixedBridgeSiteProfile.LocalToWorld(
                    local,
                    origin,
                    37f);

            Vector2 roundTrip =
                FixedBridgeSiteProfile.WorldToLocal(
                    world,
                    origin,
                    37f);

            Assert.That(roundTrip.x, Is.EqualTo(local.x).Within(Epsilon));
            Assert.That(roundTrip.y, Is.EqualTo(local.y).Within(Epsilon));

            FixedBridgeSiteProfile.SampleWorld(
                world.x,
                world.y,
                origin,
                37f,
                4.25f,
                out float height,
                out float weight);

            Assert.That(
                height,
                Is.EqualTo(
                    4.25f +
                    FixedBridgeSiteProfile.TerrainHeight(
                        local.x,
                        local.y)).Within(Epsilon));

            Assert.That(
                weight,
                Is.EqualTo(
                    FixedBridgeSiteProfile.StampWeight(
                        local.x,
                        local.y)).Within(Epsilon));
        }

        [Test]
        public void FixedPlanner_IsDeterministicUsesAuthoredElevationAndNoStretch()
        {
            WorldTerrainProbe probe =
                CreateProbe(
                    3.25f,
                    0f,
                    0f);

            BridgePlannerSettings settings = CreateFixedPlannerSettings();
            MacroWorldPlan first = CreateSingleCrossingPlan(8102026, 5f);
            MacroWorldPlan second = CreateSingleCrossingPlan(8102026, 5f);

            BridgeSitePlanner.BuildBridgeSites(
                8102026,
                first,
                settings,
                probe);

            BridgeSitePlanner.BuildBridgeSites(
                8102026,
                second,
                settings,
                probe);

            Assert.That(first.BridgeSites.Count, Is.EqualTo(1));
            Assert.That(second.BridgeSites.Count, Is.EqualTo(1));

            WorldBridgeSiteData a = first.BridgeSites[0];
            WorldBridgeSiteData b = second.BridgeSites[0];

            Assert.That(a.stableId, Is.EqualTo(b.stableId));
            Assert.That(a.worldPosition, Is.EqualTo(b.worldPosition));
            Assert.That(a.yawDegrees, Is.EqualTo(0f).Within(Epsilon));
            Assert.That(a.baseElevation, Is.EqualTo(3.25f).Within(Epsilon));
            Assert.That(a.requiredSpan, Is.EqualTo(10.8f));
            Assert.That(a.archetypeId, Is.EqualTo(FixedBridgeSiteProfile.AssetId));
            Assert.That(a.contractVersion, Is.EqualTo(FixedBridgeSiteProfile.ContractVersion));
            Assert.That(a.isFixedSite, Is.True);
        }

        [Test]
        public void FixedPlanner_RejectsBadWidthAngleGradeAndMixedRoadAtomically()
        {
            BridgePlannerSettings settings = CreateFixedPlannerSettings();
            WorldTerrainProbe flatProbe = CreateProbe(2f, 0f, 0f);

            MacroWorldPlan widthPlan = CreateSingleCrossingPlan(1, 7f);
            BridgeSitePlanner.BuildBridgeSites(1, widthPlan, settings, flatProbe);
            AssertRemovedCrossingRoad(widthPlan);

            MacroWorldPlan anglePlan =
                CreateSingleCrossingPlan(
                    2,
                    5f,
                    new Vector2(-20f, -20f),
                    new Vector2(20f, 20f));

            BridgeSitePlanner.BuildBridgeSites(2, anglePlan, settings, flatProbe);
            AssertRemovedCrossingRoad(anglePlan);

            WorldTerrainProbe steepProbe = CreateProbe(0f, 0f, 0.5f);
            MacroWorldPlan gradePlan = CreateSingleCrossingPlan(3, 5f);
            BridgeSitePlanner.BuildBridgeSites(3, gradePlan, settings, steepProbe);
            AssertRemovedCrossingRoad(gradePlan);

            MacroWorldPlan mixedPlan = CreateSingleCrossingPlan(4, 5f, riverZ: -10f);
            AddRiver(
                mixedPlan,
                302,
                new Vector2(-20f, 10f),
                new Vector2(20f, 10f),
                8f);

            BridgeSitePlanner.BuildBridgeSites(4, mixedPlan, settings, flatProbe);
            AssertRemovedCrossingRoad(mixedPlan);
        }

        [Test]
        public void FixedPlanner_RejectsRoadRunningAlongRiverWithAndWithoutTerrain()
        {
            BridgePlannerSettings settings = CreateFixedPlannerSettings();
            MacroWorldPlan withTerrain = CreateSingleCrossingPlan(
                51, 5f, new Vector2(0f, -20f), new Vector2(0f, 20f));
            MacroWorldPlan withoutTerrain = CreateSingleCrossingPlan(
                52, 5f, new Vector2(0f, -20f), new Vector2(0f, 20f));

            BridgeSitePlanner.BuildBridgeSites(
                51, withTerrain, settings, CreateProbe(0f, 0f, 0f));
            BridgeSitePlanner.BuildBridgeSites(52, withoutTerrain, settings);

            AssertRemovedCrossingRoad(withTerrain);
            AssertRemovedCrossingRoad(withoutTerrain);
        }

        [Test]
        public void FixedPlanner_GentleRiverCornerGetsOneBridgeButSharpCornerRejectsRoad()
        {
            BridgePlannerSettings settings = CreateFixedPlannerSettings();
            WorldTerrainProbe probe = CreateProbe(0f, 0f, 0f);
            MacroWorldPlan gentle = CreateSingleCrossingPlan(53, 5f);
            MacroWorldPlan sharp = CreateSingleCrossingPlan(54, 5f);

            gentle.Rivers[0].centerline[1] = Vector2.zero;
            gentle.Rivers[0].centerline.Add(new Vector2(20f, 3.5f));
            gentle.Rivers[0].widths.Add(5f);
            sharp.Rivers[0].centerline[1] = Vector2.zero;
            sharp.Rivers[0].centerline.Add(new Vector2(20f, 20f));
            sharp.Rivers[0].widths.Add(5f);

            BridgeSitePlanner.BuildBridgeSites(53, gentle, settings, probe);
            BridgeSitePlanner.BuildBridgeSites(54, sharp, settings, probe);

            Assert.That(gentle.Roads.Count, Is.EqualTo(1));
            Assert.That(gentle.BridgeSites.Count, Is.EqualTo(1));
            Assert.That(gentle.BridgeSites[0].worldPosition, Is.EqualTo(Vector2.zero));
            AssertRemovedCrossingRoad(sharp);
        }

        [Test]
        public void FixedPlanner_RejectsOverlappingSitesWithoutLeavingOrphanRoads()
        {
            WorldTerrainProbe probe = CreateProbe(0f, 0f, 0f);
            BridgePlannerSettings settings = CreateFixedPlannerSettings();
            var plan = new MacroWorldPlan(77);

            AddRoad(plan, 101, 0f);
            AddRoad(plan, 102, 4f);
            AddRiver(
                plan,
                201,
                new Vector2(-20f, 0f),
                new Vector2(20f, 0f),
                5f);

            BridgeSitePlanner.BuildBridgeSites(77, plan, settings, probe);

            Assert.That(plan.Roads.Count, Is.EqualTo(1));
            Assert.That(plan.RoadConnections.Count, Is.EqualTo(1));
            Assert.That(plan.BridgeSites.Count, Is.EqualTo(1));
            Assert.That(
                plan.BridgeSites[0].roadId,
                Is.EqualTo(plan.Roads[0].stableId));
        }

        [Test]
        public void LegacyPlanner_OptOutKeepsRoadsSpansAndDataDefaults()
        {
            var settings =
                new BridgePlannerSettings
                {
                    enabled = true,
                    archetypeId = "bridge_wood_small",
                    extraSpan = 2f,
                    useFixedStoneBridgeSites = false
                };

            MacroWorldPlan plan = CreateSingleCrossingPlan(99, 8f);
            BridgeSitePlanner.BuildBridgeSites(99, plan, settings);

            Assert.That(plan.Roads.Count, Is.EqualTo(1));
            Assert.That(plan.RoadConnections.Count, Is.EqualTo(1));
            Assert.That(plan.BridgeSites.Count, Is.EqualTo(1));

            WorldBridgeSiteData bridge = plan.BridgeSites[0];
            Assert.That(bridge.archetypeId, Is.EqualTo("bridge_wood_small"));
            Assert.That(bridge.requiredSpan, Is.EqualTo(10f));
            Assert.That(bridge.baseElevation, Is.Zero);
            Assert.That(bridge.contractVersion, Is.Empty);
            Assert.That(bridge.isFixedSite, Is.False);
        }

        [Test]
        public void TerrainStamp_IsDeterministicAndSeamlessAcrossNegativeChunks()
        {
            WorldGenerationSettings settings = CreateSettings(16f, 8);
            FixedBridgeTerrainStage stage =
                Track(
                    ScriptableObject.CreateInstance<FixedBridgeTerrainStage>());

            var plan = new MacroWorldPlan(5);
            plan.AddBridgeSite(
                CreateFixedSite(
                    501,
                    new Vector2(-16f, -8f),
                    37f,
                    4.25f));

            var context = new GenerationContext(5, settings, plan);
            var westA = new WorldChunkData(new ChunkCoordinate(-2, -1), 8);
            var westB = new WorldChunkData(new ChunkCoordinate(-2, -1), 8);
            var east = new WorldChunkData(new ChunkCoordinate(-1, -1), 8);

            FillHeights(westA, 9f);
            FillHeights(westB, 9f);
            FillHeights(east, 9f);

            stage.Generate(context, westA);
            stage.Generate(context, westB);
            stage.Generate(context, east);

            for (int z = 0; z < westA.SamplesPerSide; z++)
            {
                Assert.That(
                    westA.GetHeight(8, z),
                    Is.EqualTo(east.GetHeight(0, z)).Within(Epsilon));

                for (int x = 0; x < westA.SamplesPerSide; x++)
                {
                    Assert.That(
                        westA.GetHeight(x, z),
                        Is.EqualTo(westB.GetHeight(x, z)).Within(Epsilon));
                }
            }
        }

        [Test]
        public void Projection_UsesAuthoredRootScaleOneAndReservesRotatedSupport()
        {
            WorldGenerationSettings settings = CreateSettings(32f, 32);
            MacroFeatureProjectionStage stage =
                Track(
                    ScriptableObject.CreateInstance<MacroFeatureProjectionStage>());

            var plan = new MacroWorldPlan(8);
            WorldBridgeSiteData bridge =
                CreateFixedSite(
                    601,
                    new Vector2(-8f, -8f),
                    37f,
                    4.5f);

            plan.AddBridgeSite(bridge);

            var chunk =
                new WorldChunkData(
                    new ChunkCoordinate(-1, -1),
                    32);

            FillHeights(chunk, -12f);
            stage.Generate(
                new GenerationContext(8, settings, plan),
                chunk);

            Assert.That(chunk.Spawns.Count, Is.EqualTo(1));
            WorldSpawnData spawn = chunk.Spawns[0];
            Assert.That(spawn.worldPosition.y, Is.EqualTo(4.5f).Within(Epsilon));
            Assert.That(spawn.yawDegrees, Is.EqualTo(37f).Within(Epsilon));
            Assert.That(spawn.uniformScale, Is.EqualTo(1f));

            float originX = -32f;
            float originZ = -32f;

            for (int z = 0; z < chunk.CellsPerSide; z++)
            {
                for (int x = 0; x < chunk.CellsPerSide; x++)
                {
                    var world =
                        new Vector2(
                            originX + x + 0.5f,
                            originZ + z + 0.5f);

                    Vector2 local =
                        FixedBridgeSiteProfile.WorldToLocal(
                            world,
                            bridge.worldPosition,
                            bridge.yawDegrees);

                    if (Mathf.Abs(local.x) <= 6.5f &&
                        Mathf.Abs(local.y) <= 7.5f)
                    {
                        Assert.That(
                            chunk.GetPlacementBlocks(x, z),
                            Is.EqualTo(PlacementBlockFlags.All));
                    }
                }
            }
        }

        [Test]
        public void Projection_LegacySiteStillSamplesTerrainAndKeepsOffset()
        {
            WorldGenerationSettings settings = CreateSettings(32f, 32);
            MacroFeatureProjectionStage stage =
                Track(
                    ScriptableObject.CreateInstance<MacroFeatureProjectionStage>());

            var plan = new MacroWorldPlan(9);
            plan.AddBridgeSite(
                new WorldBridgeSiteData(
                    701,
                    101,
                    201,
                    "bridge_wood_small",
                    new Vector2(8f, 8f),
                    0f,
                    7f));

            var chunk =
                new WorldChunkData(
                    new ChunkCoordinate(0, 0),
                    32);

            FillHeights(chunk, 10f);
            stage.Generate(
                new GenerationContext(9, settings, plan),
                chunk);

            Assert.That(chunk.Spawns.Count, Is.EqualTo(1));
            Assert.That(
                chunk.Spawns[0].worldPosition.y,
                Is.EqualTo(10.15f).Within(Epsilon));
            Assert.That(chunk.Spawns[0].uniformScale, Is.EqualTo(1f));
        }

        private WorldTerrainProbe CreateProbe(
            float baseHeight,
            float xGradient,
            float zGradient)
        {
            LinearHeightStage heightStage =
                Track(
                    ScriptableObject.CreateInstance<LinearHeightStage>());

            heightStage.baseHeight = baseHeight;
            heightStage.xGradient = xGradient;
            heightStage.zGradient = zGradient;

            WorldGenerationSettings settings =
                CreateSettings(
                    32f,
                    32,
                    heightStage);

            var pipeline =
                new WorldGenerationPipeline(
                    settings,
                    null);

            return
                new WorldTerrainProbe(
                    pipeline,
                    settings,
                    8102026);
        }

        private WorldGenerationSettings CreateSettings(
            float chunkSize,
            int cellsPerSide,
            params WorldGenerationStage[] stages)
        {
            WorldGenerationSettings settings =
                Track(
                    ScriptableObject.CreateInstance<WorldGenerationSettings>());

            SetPrivateField(settings, "chunkWorldSize", chunkSize);
            SetPrivateField(settings, "cellsPerSide", cellsPerSide);

            var stageList =
                (List<WorldGenerationStage>)GetPrivateField(
                    settings,
                    "stages");

            stageList.Clear();
            stageList.AddRange(stages);
            return settings;
        }

        private static BridgePlannerSettings CreateFixedPlannerSettings()
        {
            return
                new BridgePlannerSettings
                {
                    enabled = true,
                    useFixedStoneBridgeSites = true,
                    minimumCompatibleRiverWidth = 1.5f,
                    maximumCompatibleRiverWidth = 6.5f,
                    maximumCrossingAngleDeviation = 20f,
                    maximumApproachGrade = 0.2f,
                    maximumApproachSlope = 28f,
                    maximumRiverGrade = 0.2f,
                    fixedSiteSeparationPadding = 1f
                };
        }

        private static MacroWorldPlan CreateSingleCrossingPlan(
            int seed,
            float width,
            Vector2? riverStart = null,
            Vector2? riverEnd = null,
            float riverZ = 0f)
        {
            var plan = new MacroWorldPlan(seed);
            AddRoad(plan, 101, 0f);
            AddRiver(
                plan,
                201,
                riverStart ?? new Vector2(-20f, riverZ),
                riverEnd ?? new Vector2(20f, riverZ),
                width);
            return plan;
        }

        private static void AddRoad(
            MacroWorldPlan plan,
            long stableId,
            float x)
        {
            plan.AddRoadConnection(
                new WorldRoadConnectionData(
                    stableId,
                    stableId * 10,
                    stableId * 10 + 1,
                    RoadKind.DirtRoad));

            plan.AddRoad(
                new WorldRoadData
                {
                    stableId = stableId,
                    roadKind = RoadKind.DirtRoad,
                    width = 5f,
                    centerline =
                        new List<Vector2>
                        {
                            new Vector2(x, -30f),
                            new Vector2(x, 30f)
                        }
                });
        }

        private static void AddRiver(
            MacroWorldPlan plan,
            long stableId,
            Vector2 start,
            Vector2 end,
            float width)
        {
            plan.AddRiver(
                new WorldRiverData
                {
                    stableId = stableId,
                    nominalWidth = width,
                    nominalDepth = 1.5f,
                    centerline =
                        new List<Vector2>
                        {
                            start,
                            end
                        },
                    widths =
                        new List<float>
                        {
                            width,
                            width
                        },
                    depths =
                        new List<float>
                        {
                            1.5f,
                            1.5f
                        }
                });
        }

        private static WorldBridgeSiteData CreateFixedSite(
            long stableId,
            Vector2 position,
            float yaw,
            float baseElevation)
        {
            return
                new WorldBridgeSiteData(
                    stableId,
                    101,
                    201,
                    FixedBridgeSiteProfile.AssetId,
                    position,
                    yaw,
                    FixedBridgeSiteProfile.BridgeLength,
                    baseElevation,
                    FixedBridgeSiteProfile.ContractVersion,
                    true);
        }

        private static void AssertRemovedCrossingRoad(
            MacroWorldPlan plan)
        {
            Assert.That(plan.Roads.Count, Is.Zero);
            Assert.That(plan.RoadConnections.Count, Is.Zero);
            Assert.That(plan.BridgeSites.Count, Is.Zero);
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

        private T Track<T>(T value)
            where T : Object
        {
            createdObjects.Add(value);
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

            Assert.That(field, Is.Not.Null, "Missing field: " + name);
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

            Assert.That(field, Is.Not.Null, "Missing field: " + name);
            return field.GetValue(target);
        }
    }
}
