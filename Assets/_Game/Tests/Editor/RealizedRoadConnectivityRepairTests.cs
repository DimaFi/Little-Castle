using System.Collections.Generic;
using System.Reflection;
using LittleCastle.World;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace LittleCastle.Tests
{
    /// <summary>
    /// Regression tests for the realized-graph repair after an unsupported
    /// original connection was rejected. Not a 2/8/16-player PlayMode test.
    /// </summary>
    public sealed class RealizedRoadConnectivityRepairTests
    {
        private readonly List<Object> owned = new List<Object>();

        public sealed class FlatHeightStage : WorldGenerationStage
        {
            public override WorldGenerationStagePhase Phase =>
                WorldGenerationStagePhase.TerrainBase;

            public override void Generate(
                GenerationContext context, WorldChunkData chunk)
            {
                for (int z = 0; z < chunk.SamplesPerSide; z++)
                    for (int x = 0; x < chunk.SamplesPerSide; x++)
                        chunk.SetHeight(x, z, 0f);
            }
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = owned.Count - 1; i >= 0; i--)
                if (owned[i] != null)
                    Object.DestroyImmediate(owned[i]);
            owned.Clear();
        }

        [Test]
        public void Repair_CleansRejectedLogicalEdgeAfterAlternativeConnectsGraph()
        {
            const int seed = 8102026;
            var probe = CreateFlatProbe(seed);
            var a = TwoComponents(seed, false, out _, out _);
            var b = TwoComponents(seed, true, out _, out _);
            WorldRoadConnectionData rejectedA = AddRejectedCrossConnection(a, seed);
            WorldRoadConnectionData rejectedB = AddRejectedCrossConnection(b, seed);

            var network = new RoadNetworkPlannerSettings
            {
                enabled = true,
                roadKind = RoadKind.DirtRoad,
                maxConnectionDistance = 120f
            };
            var paths = new TerrainRoadPathPlannerSettings
            {
                enabled = true, gridStep = 8f,
                searchPadding = 32f, maxExpandedNodes = 2000
            };
            var bridges = new BridgePlannerSettings
            {
                enabled = true,
                useFixedStoneBridgeSites = true,
                enableBridgeAwareRouting = true,
                requireConnectedFeatureGraph = true,
                maxBridgeRoutingAttempts = 1,
                maxGuidedCrossingAttempts = 0,
                maxConnectivityRepairCandidates = 4
            };

            var repaired = RealizedRoadConnectivityRepair.Repair(
                seed, a, probe, network, paths, bridges,
                new[] { rejectedA.stableId });
            var repeated = RealizedRoadConnectivityRepair.Repair(
                seed, b, probe, network, paths, bridges,
                new[] { rejectedB.stableId });

            Assert.That(repaired.initialComponents, Is.EqualTo(2));
            Assert.That(repaired.remainingComponents, Is.EqualTo(1),
                repaired.Summary);
            Assert.That(repaired.acceptedConnections, Is.EqualTo(1));
            Assert.That(repeated.addedRoadIds,
                Is.EqualTo(repaired.addedRoadIds),
                "Feature insertion order must not influence chosen edges.");
            Assert.That(a.Roads.Count, Is.EqualTo(3));
            Assert.That(a.RoadConnections.Count, Is.EqualTo(3),
                "The stale rejected edge should be replaced, not retained.");
            Assert.That(HasRoadConnection(a, rejectedA.stableId), Is.False);
            Assert.That(a.BridgeSites.Count, Is.Zero,
                "No river => no fake bridge.");
            var connected = WorldRouteConnectivityValidator.Validate(a, 0, true);
            Assert.That(connected.IsValid, Is.True,
                string.Join("; ", connected.errors));
            Assert.That(connected.missingRealizedConnections, Is.Zero);
            Assert.That(connected.components, Is.EqualTo(1));
            // Reconnected graph is not permission to invent a required
            // bridge when no river even exists in this fixture.
            var minimumBridge = WorldRouteConnectivityValidator.Validate(
                a, 1, true);
            Assert.That(minimumBridge.IsValid, Is.False);
            Assert.That(minimumBridge.fixedBridgeCount, Is.Zero);
            Assert.That(RealizedRoadConnectivityRepair.Repair(
                seed, a, probe, network, paths, bridges).acceptedConnections,
                Is.Zero, "Repair should be idempotent on an already connected graph.");
        }

        [Test]
        public void Repair_RespectsMaximumDistanceWithoutInventingConnections()
        {
            const int seed = 12345;
            var probe = CreateFlatProbe(seed);
            var plan = TwoComponents(seed, false, out _, out _);
            int before = plan.Roads.Count;
            var settings = new BridgePlannerSettings
            {
                useFixedStoneBridgeSites = true,
                enableBridgeAwareRouting = true,
                requireConnectedFeatureGraph = true,
                maxConnectivityRepairCandidates = 2
            };

            var outcome = RealizedRoadConnectivityRepair.Repair(
                seed, plan, probe,
                new RoadNetworkPlannerSettings
                {
                    maxConnectionDistance = 1f
                },
                new TerrainRoadPathPlannerSettings(),
                settings);

            Assert.That(outcome.initialComponents, Is.EqualTo(2));
            Assert.That(outcome.remainingComponents, Is.EqualTo(2));
            Assert.That(outcome.candidatesConsidered, Is.Zero);
            Assert.That(plan.Roads.Count, Is.EqualTo(before));
            Assert.That(plan.BridgeSites.Count, Is.Zero);
            Assert.That(WorldRouteConnectivityValidator.Validate(plan, 0, true)
                .IsValid, Is.False);
        }

        [Test]
        public void Repair_ZeroBudgetAndRejectedCandidatesStayExplicitFailures()
        {
            const int seed = -10101;
            var probe = CreateFlatProbe(seed);
            var plan = TwoComponents(seed, false, out _, out _);
            WorldRoadConnectionData failed = AddRejectedCrossConnection(plan, seed);
            var network = new RoadNetworkPlannerSettings
            {
                maxConnectionDistance = 120f
            };
            var pathSettings = new TerrainRoadPathPlannerSettings
            {
                gridStep = 8f, maxExpandedNodes = 1000
            };
            var bridgeSettings = new BridgePlannerSettings
            {
                useFixedStoneBridgeSites = true,
                enableBridgeAwareRouting = true,
                requireConnectedFeatureGraph = true,
                maxConnectivityRepairCandidates = 0
            };

            var none = RealizedRoadConnectivityRepair.Repair(
                seed, plan, probe, network, pathSettings, bridgeSettings,
                new[] { failed.stableId });
            Assert.That(none.remainingComponents, Is.EqualTo(2));
            Assert.That(none.candidatesConsidered, Is.Zero);
            Assert.That(HasRoadConnection(plan, failed.stableId), Is.False,
                "Explicitly abandoned logical edges are cleanup-safe even with no repair budget.");
            var disconnected = WorldRouteConnectivityValidator.Validate(
                plan, 0, true);
            Assert.That(disconnected.IsValid, Is.False);
            Assert.That(disconnected.components, Is.EqualTo(2),
                "Cleanup must not conceal a disconnected realized graph.");

            bridgeSettings.maxConnectivityRepairCandidates = 1;
            var allCandidates = new List<long>();
            var features = new List<WorldPointFeatureData>(plan.PointFeatures);
            int salt = DeterministicHash.String32("road_network_connection");
            for (int i = 0; i < features.Count; i++)
                for (int j = i + 1; j < features.Count; j++)
                    allCandidates.Add(DeterministicHash.StablePairId(
                        seed, features[i].stableId, features[j].stableId, salt));

            var rejected = RealizedRoadConnectivityRepair.Repair(
                seed, plan, probe, network, pathSettings,
                bridgeSettings, allCandidates);
            Assert.That(rejected.remainingComponents, Is.EqualTo(2));
            Assert.That(rejected.candidatesConsidered, Is.Zero);
            Assert.That(plan.Roads.Count, Is.EqualTo(2));
        }

        [Test]
        public void Repair_NonStrictModePreservesRejectedLogicalEdge()
        {
            const int seed = 7001;
            var probe = CreateFlatProbe(seed);
            var plan = TwoComponents(seed, false, out _, out _);
            WorldRoadConnectionData failed = AddRejectedCrossConnection(plan, seed);
            var bridges = new BridgePlannerSettings
            {
                useFixedStoneBridgeSites = true,
                enableBridgeAwareRouting = true,
                requireConnectedFeatureGraph = false,
                maxConnectivityRepairCandidates = 4
            };

            var outcome = RealizedRoadConnectivityRepair.Repair(
                seed, plan, probe,
                new RoadNetworkPlannerSettings { maxConnectionDistance = 120f },
                new TerrainRoadPathPlannerSettings(), bridges,
                new[] { failed.stableId });

            Assert.That(outcome.acceptedConnections, Is.Zero);
            Assert.That(HasRoadConnection(plan, failed.stableId), Is.True,
                "Legacy/non-strict reporting retains its desired-edge contract.");
            Assert.That(WorldRouteConnectivityValidator.Validate(plan).IsValid,
                Is.False);
        }

        [Test]
        public void Repair_DoesNotRemoveMaterializedRoadWithRejectedId()
        {
            const int seed = 7002;
            var probe = CreateFlatProbe(seed);
            var plan = TwoComponents(seed, false, out _, out _);
            WorldRoadConnectionData connection = AddRejectedCrossConnection(plan, seed);
            var acceptedRoad = new WorldRoadData
            {
                stableId = connection.stableId,
                roadKind = RoadKind.DirtRoad,
                width = 3f,
                centerline = new List<Vector2>
                {
                    new Vector2(-48f, -32f),
                    new Vector2(32f, -32f)
                }
            };
            Assert.That(plan.AddRoad(acceptedRoad), Is.True);

            var outcome = RealizedRoadConnectivityRepair.Repair(
                seed, plan, probe,
                new RoadNetworkPlannerSettings(),
                new TerrainRoadPathPlannerSettings(),
                StrictBridgeSettings(0),
                new[] { connection.stableId });

            Assert.That(outcome.initialComponents, Is.EqualTo(1));
            Assert.That(HasRoadConnection(plan, connection.stableId), Is.True);
            Assert.That(HasRoad(plan, connection.stableId), Is.True,
                "A failed-ID report must never discard a materialized road.");
            Assert.That(WorldRouteConnectivityValidator.Validate(plan, 0, true)
                .IsValid, Is.True);
        }

        [Test]
        public void Repair_MatchingIdWithWrongEndpointsDoesNotConnectComponents()
        {
            const int seed = 7003;
            var probe = CreateFlatProbe(seed);
            var plan = TwoComponents(seed, false, out _, out _);
            WorldRoadConnectionData connection = AddRejectedCrossConnection(plan, seed);
            Assert.That(plan.AddRoad(new WorldRoadData
            {
                stableId = connection.stableId,
                roadKind = RoadKind.DirtRoad,
                width = 3f,
                centerline = new List<Vector2>
                {
                    new Vector2(100f, 100f),
                    new Vector2(120f, 100f)
                }
            }), Is.True);

            var outcome = RealizedRoadConnectivityRepair.Repair(
                seed, plan, probe,
                new RoadNetworkPlannerSettings(),
                new TerrainRoadPathPlannerSettings(),
                StrictBridgeSettings(0),
                new[] { connection.stableId });

            Assert.That(outcome.initialComponents, Is.EqualTo(2));
            Assert.That(outcome.remainingComponents, Is.EqualTo(2));
            Assert.That(HasRoad(plan, connection.stableId), Is.True,
                "Conservative cleanup leaves suspect materialized data for validation.");
            var report = WorldRouteConnectivityValidator.Validate(plan, 0, true);
            Assert.That(report.IsValid, Is.False);
            Assert.That(report.missingRealizedConnections, Is.EqualTo(1));
            Assert.That(report.components, Is.EqualTo(2));
        }

        private static BridgePlannerSettings StrictBridgeSettings(int budget)
        {
            return new BridgePlannerSettings
            {
                useFixedStoneBridgeSites = true,
                enableBridgeAwareRouting = true,
                requireConnectedFeatureGraph = true,
                maxConnectivityRepairCandidates = budget
            };
        }

        private static WorldRoadConnectionData AddRejectedCrossConnection(
            MacroWorldPlan plan, int seed)
        {
            long id = DeterministicHash.StablePairId(
                seed, 22, 33,
                DeterministicHash.String32("road_network_connection"));
            var connection = new WorldRoadConnectionData(
                id, 22, 33, RoadKind.DirtRoad);
            Assert.That(plan.AddRoadConnection(connection), Is.True);
            return connection;
        }

        private static bool HasRoadConnection(MacroWorldPlan plan, long id)
        {
            for (int i = 0; i < plan.RoadConnections.Count; i++)
                if (plan.RoadConnections[i].stableId == id)
                    return true;
            return false;
        }

        private static bool HasRoad(MacroWorldPlan plan, long id)
        {
            for (int i = 0; i < plan.Roads.Count; i++)
                if (plan.Roads[i] != null && plan.Roads[i].stableId == id)
                    return true;
            return false;
        }

        private static MacroWorldPlan TwoComponents(
            int seed, bool reverse,
            out WorldRoadData first, out WorldRoadData second)
        {
            var plan = new MacroWorldPlan(seed);
            var features = new[]
            {
                new WorldPointFeatureData(11, WorldFeatureKind.NeutralSettlement,
                    "v", new Vector2(-80f, -32f), 0f),
                new WorldPointFeatureData(22, WorldFeatureKind.NeutralSettlement,
                    "v", new Vector2(-48f, -32f), 0f),
                new WorldPointFeatureData(33, WorldFeatureKind.NeutralSettlement,
                    "v", new Vector2(32f, -32f), 0f),
                new WorldPointFeatureData(44, WorldFeatureKind.Ruin,
                    "r", new Vector2(64f, -32f), 0f)
            };
            for (int i = 0; i < features.Length; i++)
                plan.AddPointFeature(features[reverse
                    ? features.Length - 1 - i : i]);

            first = new WorldRoadData
            {
                stableId = 101,
                roadKind = RoadKind.DirtRoad,
                width = 3f,
                centerline = new List<Vector2>
                {
                    features[0].worldPosition, features[1].worldPosition
                }
            };
            second = new WorldRoadData
            {
                stableId = 102,
                roadKind = RoadKind.DirtRoad,
                width = 3f,
                centerline = new List<Vector2>
                {
                    features[2].worldPosition, features[3].worldPosition
                }
            };
            plan.AddRoadConnection(
                new WorldRoadConnectionData(101, 11, 22, RoadKind.DirtRoad));
            plan.AddRoadConnection(
                new WorldRoadConnectionData(102, 33, 44, RoadKind.DirtRoad));
            plan.AddRoad(first);
            plan.AddRoad(second);
            return plan;
        }

        private WorldTerrainProbe CreateFlatProbe(int seed)
        {
            var stage = ScriptableObject.CreateInstance<FlatHeightStage>();
            owned.Add(stage);
            var settings =
                ScriptableObject.CreateInstance<WorldGenerationSettings>();
            owned.Add(settings);
            typeof(WorldGenerationSettings).GetField(
                "chunkWorldSize",
                BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(settings, 32f);
            typeof(WorldGenerationSettings).GetField(
                "cellsPerSide",
                BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(settings, 32);
            var stages = (List<WorldGenerationStage>)
                typeof(WorldGenerationSettings).GetField(
                    "stages",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(settings);
            stages.Clear();
            stages.Add(stage);
            return new WorldTerrainProbe(
                new WorldGenerationPipeline(settings, null),
                settings, seed);
        }
    }
}
