using System;
using System.Collections.Generic;
using System.Reflection;
using LittleCastle.World;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace LittleCastle.Tests
{
    public sealed class BridgeAwareRoutingTests
    {
        public sealed class FlatHeightStage : WorldGenerationStage
        {
            public override WorldGenerationStagePhase Phase =>
                WorldGenerationStagePhase.TerrainBase;

            public override void Generate(
                GenerationContext context,
                WorldChunkData chunk)
            {
                for (int z = 0; z < chunk.SamplesPerSide; z++)
                    for (int x = 0; x < chunk.SamplesPerSide; x++)
                        chunk.SetHeight(x, z, 0f);
            }
        }

        private readonly List<Object> owned = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            for (int i = owned.Count - 1; i >= 0; i--)
                if (owned[i] != null)
                    Object.DestroyImmediate(owned[i]);
            owned.Clear();
        }

        [Test]
        public void Connectivity_ValidFixedCrossingAndNegativeCoordinates()
        {
            MacroWorldPlan plan = CreateCrossing(-80f, -40f);
            plan.AddBridgeSite(
                new WorldBridgeSiteData(
                    501, 101, 201, FixedBridgeSiteProfile.AssetId,
                    new Vector2(-80f, -40f), 0f,
                    FixedBridgeSiteProfile.BridgeLength, 0f,
                    FixedBridgeSiteProfile.ContractVersion, true));

            var report = WorldRouteConnectivityValidator.Validate(plan, 1, true);
            Assert.That(report.IsValid, Is.True, report.Summary +
                " " + string.Join("; ", report.errors));
            Assert.That(report.fixedBridgeCount, Is.EqualTo(1));
            Assert.That(report.unbridgedCrossings, Is.Zero);
            Assert.That(report.components, Is.EqualTo(1));
        }

        [Test]
        public void Connectivity_ReportsUnsupportedCrossingAndMissingMinimum()
        {
            MacroWorldPlan plan = CreateCrossing(0f, 0f);
            var report = WorldRouteConnectivityValidator.Validate(plan, 1, true);
            Assert.That(report.IsValid, Is.False);
            Assert.That(report.unbridgedCrossings, Is.GreaterThan(0));
            Assert.That(report.fixedBridgeCount, Is.Zero);
            Assert.That(report.errors.Count, Is.GreaterThan(0));
        }

        [Test]
        public void Connectivity_RejectsOrphanSiteNotOnRealPolyline()
        {
            MacroWorldPlan plan = CreateCrossing(0f, 0f);
            plan.AddBridgeSite(
                new WorldBridgeSiteData(
                    502, 101, 201, FixedBridgeSiteProfile.AssetId,
                    new Vector2(4f, 4f), 0f,
                    FixedBridgeSiteProfile.BridgeLength, 0f,
                    FixedBridgeSiteProfile.ContractVersion, true));

            var report = WorldRouteConnectivityValidator.Validate(plan);
            Assert.That(report.orphanBridgeSites, Is.GreaterThan(0));
            Assert.That(report.unbridgedCrossings, Is.GreaterThan(0));
        }

        [Test]
        public void Connectivity_DetectsMissingRealizedRoadAndDisconnectedFeature()
        {
            MacroWorldPlan plan = CreateCrossing(0f, 0f);
            plan.AddPointFeature(
                new WorldPointFeatureData(
                    1003, WorldFeatureKind.NeutralSettlement, "settlement",
                    new Vector2(100f, 100f), 0f));

            Assert.That(WorldRouteConnectivityValidator.Validate(plan).IsValid, Is.False,
                "Missing bridge remains invalid, even without connectivity requirement.");

            plan.AddBridgeSite(new WorldBridgeSiteData(
                503, 101, 201, FixedBridgeSiteProfile.AssetId,
                Vector2.zero, 0f, FixedBridgeSiteProfile.BridgeLength,
                0f, FixedBridgeSiteProfile.ContractVersion, true));

            var connected = WorldRouteConnectivityValidator.Validate(plan, 0, true);
            Assert.That(connected.IsValid, Is.False);
            Assert.That(connected.components, Is.EqualTo(2));

            plan.AddRoadConnection(
                new WorldRoadConnectionData(102, 1002, 1003, RoadKind.Trail));
            var missing = WorldRouteConnectivityValidator.Validate(plan);
            Assert.That(missing.missingRealizedConnections, Is.EqualTo(1));
        }

        [Test]
        public void Recovery_IsDeterministicAndRetainsMissingLogicalConnection()
        {
            WorldTerrainProbe probe = CreateFlatProbe();
            var roadSettings = new TerrainRoadPathPlannerSettings
            {
                enabled = true,
                gridStep = 8f,
                searchPadding = 64f,
                maxExpandedNodes = 2000
            };
            var bridgeSettings = new BridgePlannerSettings
            {
                enabled = true,
                useFixedStoneBridgeSites = true,
                enableBridgeAwareRouting = true,
                maxBridgeRoutingAttempts = 2
            };

            var a = CreateMissingRoad(-64f);
            var b = CreateMissingRoad(-64f);
            WorldRoadConnectionData[] originals =
            {
                new WorldRoadConnectionData(101, 1001, 1002, RoadKind.Trail)
            };

            var resultA = BridgeAwareRoutingPlanner.RecoverRejectedConnections(
                8102026, a, originals, probe, roadSettings, bridgeSettings);
            var resultB = BridgeAwareRoutingPlanner.RecoverRejectedConnections(
                8102026, b, originals, probe, roadSettings, bridgeSettings);

            Assert.That(resultA.recoveredConnections, Is.EqualTo(1));
            Assert.That(resultA.failedConnections, Is.Zero);
            Assert.That(resultB.recoveredConnections, Is.EqualTo(1));
            Assert.That(a.Roads.Count, Is.EqualTo(1));
            Assert.That(a.RoadConnections.Count, Is.EqualTo(1));
            Assert.That(a.Roads[0].centerline, Is.EqualTo(b.Roads[0].centerline));
            Assert.That(WorldRouteConnectivityValidator.Validate(a, 0, true).IsValid,
                Is.True);
        }

        [Test]
        public void Recovery_BoundedFailureDoesNotInventRoadOrResource()
        {
            WorldTerrainProbe probe = CreateFlatProbe();
            var plan = CreateMissingRoad(-64f);
            var roadSettings = new TerrainRoadPathPlannerSettings
            {
                enabled = true,
                gridStep = 8f,
                maxExpandedNodes = 100
            };
            var bridgeSettings = new BridgePlannerSettings
            {
                useFixedStoneBridgeSites = true,
                enableBridgeAwareRouting = true,
                maxBridgeRoutingAttempts = 2
            };

            var unknown = new WorldRoadConnectionData(
                912, 1999, 2999, RoadKind.Trail);
            var result = BridgeAwareRoutingPlanner.RecoverRejectedConnections(
                5, plan, new[] { unknown }, probe, roadSettings, bridgeSettings);
            Assert.That(result.failedConnections, Is.EqualTo(1));
            Assert.That(result.recoveredConnections, Is.Zero);
            Assert.That(result.pathAttempts, Is.Zero);
            Assert.That(plan.Roads.Count, Is.Zero);
        }

        [TestCase(2, 12345)]
        [TestCase(8, -12345)]
        [TestCase(16, 777)]
        public void Connectivity_HandlesDifferentFeatureCountsAndSeeds(
            int playerLikeFeatureCount,
            int seed)
        {
            // A deterministic synthetic graph contract, not an in-game
            // player-start or full finite-map benchmark.
            var plan = new MacroWorldPlan(seed);
            for (int i = 0; i < playerLikeFeatureCount; i++)
            {
                plan.AddPointFeature(new WorldPointFeatureData(
                    1000 + i, WorldFeatureKind.NeutralSettlement,
                    "settlement", new Vector2(-128f + i * 16f, -32f), 0f));
                if (i == 0)
                    continue;
                long roadId = 5000 + i;
                plan.AddRoadConnection(new WorldRoadConnectionData(
                    roadId, 1000 + i - 1, 1000 + i, RoadKind.Trail));
                plan.AddRoad(new WorldRoadData
                {
                    stableId = roadId,
                    roadKind = RoadKind.Trail,
                    width = 2f,
                    centerline = new List<Vector2>
                    {
                        new Vector2(-128f + (i - 1) * 16f, -32f),
                        new Vector2(-128f + i * 16f, -32f)
                    }
                });
            }
            var report = WorldRouteConnectivityValidator.Validate(plan, 0, true);
            Assert.That(report.IsValid, Is.True, report.Summary);
            Assert.That(report.components, Is.EqualTo(1));
            Assert.That(report.realizedRoadCount, Is.EqualTo(playerLikeFeatureCount - 1));
        }

        private static MacroWorldPlan CreateCrossing(float x, float z)
        {
            var plan = new MacroWorldPlan(8102026);
            plan.AddPointFeature(new WorldPointFeatureData(
                1001, WorldFeatureKind.NeutralSettlement,
                "settlement", new Vector2(x, z - 30f), 0f));
            plan.AddPointFeature(new WorldPointFeatureData(
                1002, WorldFeatureKind.NeutralSettlement,
                "settlement", new Vector2(x, z + 30f), 0f));
            plan.AddRoadConnection(
                new WorldRoadConnectionData(101, 1001, 1002, RoadKind.DirtRoad));
            plan.AddRoad(new WorldRoadData
            {
                stableId = 101,
                roadKind = RoadKind.DirtRoad,
                width = 5f,
                centerline = new List<Vector2>
                {
                    new Vector2(x, z - 30f),
                    new Vector2(x, z + 30f)
                }
            });
            plan.AddRiver(new WorldRiverData
            {
                stableId = 201,
                nominalWidth = 5f,
                centerline = new List<Vector2>
                {
                    new Vector2(x - 20f, z),
                    new Vector2(x + 20f, z)
                }
            });
            return plan;
        }

        private static MacroWorldPlan CreateMissingRoad(float x)
        {
            var plan = new MacroWorldPlan(8102026);
            plan.AddPointFeature(new WorldPointFeatureData(
                1001, WorldFeatureKind.NeutralSettlement,
                "settlement", new Vector2(x, -64f), 0f));
            plan.AddPointFeature(new WorldPointFeatureData(
                1002, WorldFeatureKind.NeutralSettlement,
                "settlement", new Vector2(x, 64f), 0f));
            // Logical connection survives when the initial solver failed.
            plan.AddRoadConnection(
                new WorldRoadConnectionData(101, 1001, 1002, RoadKind.Trail));
            return plan;
        }

        private WorldTerrainProbe CreateFlatProbe()
        {
            var stage = ScriptableObject.CreateInstance<FlatHeightStage>();
            owned.Add(stage);
            var settings = ScriptableObject.CreateInstance<WorldGenerationSettings>();
            owned.Add(settings);

            typeof(WorldGenerationSettings).GetField(
                "chunkWorldSize", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(settings, 32f);
            typeof(WorldGenerationSettings).GetField(
                "cellsPerSide", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(settings, 32);
            var stages = (List<WorldGenerationStage>)
                typeof(WorldGenerationSettings).GetField(
                    "stages", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(settings);
            stages.Clear();
            stages.Add(stage);

            var pipeline = new WorldGenerationPipeline(settings, null);
            return new WorldTerrainProbe(pipeline, settings, 8102026);
        }
    }
}
