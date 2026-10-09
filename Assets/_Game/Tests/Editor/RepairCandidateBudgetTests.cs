using System.Collections.Generic;
using System.Reflection;
using LittleCastle.World;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace LittleCastle.Tests
{
    /// <summary>
    /// Source-level coverage of bounded realized-road candidate preparation.
    /// These are not a substitute for Unity Player or large-map profiling.
    /// </summary>
    public sealed class RepairCandidateBudgetTests
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

        [TestCase(100)]
        [TestCase(1000)]
        public void DenseFeatures_KeepOnlyBoundedClosestCandidates(int count)
        {
            const int seed = 31032026;
            var plan = CreateGrid(seed, count);
            var outcome = Repair(plan, seed, 1, 0f);

            long eligible = (long)count * (count - 1) / 2;
            Assert.That(outcome.initialComponents, Is.EqualTo(count));
            Assert.That(outcome.eligibleCandidatePairs, Is.EqualTo(eligible));
            Assert.That(outcome.candidatePairChecks, Is.EqualTo(eligible));
            Assert.That(outcome.preparedCandidateCount,
                Is.EqualTo(RealizedRoadConnectivityRepair.MaxPreparedCandidates));
            Assert.That(outcome.discardedCandidatePairs,
                Is.EqualTo(eligible -
                    RealizedRoadConnectivityRepair.MaxPreparedCandidates));
            Assert.That(outcome.candidateSelectionTruncated, Is.True);
            Assert.That(outcome.candidatesConsidered, Is.EqualTo(1));
            Assert.That(outcome.attemptedRoadIds.Count, Is.EqualTo(1));
            Assert.That(outcome.remainingComponents, Is.GreaterThan(1),
                "One alternative cannot connect 100+ isolated features.");
            Assert.That(outcome.attemptBudgetExhausted, Is.True);
        }

        [Test]
        public void EqualCostPairs_UseStableIdTieBreak_IndependentOfInsertionOrder()
        {
            const int seed = -3103;
            var a = CreateSquare(seed, false);
            var b = CreateSquare(seed, true);
            var first = Repair(a, seed, 1, 0f);
            var second = Repair(b, seed, 1, 0f);
            long expected = DeterministicHash.StablePairId(
                seed, 10, 20, DeterministicHash.String32(
                    "road_network_connection"));

            Assert.That(first.eligibleCandidatePairs, Is.EqualTo(6));
            Assert.That(first.preparedCandidateCount, Is.EqualTo(6));
            Assert.That(first.discardedCandidatePairs, Is.Zero);
            Assert.That(first.attemptedRoadIds, Is.EqualTo(new[] { expected }));
            Assert.That(second.attemptedRoadIds,
                Is.EqualTo(first.attemptedRoadIds));
        }

        [Test]
        public void ZeroAttemptBudget_SkipsCandidatePreparationAndLeavesFailureVisible()
        {
            const int seed = 31337;
            var plan = CreateGrid(seed, 1000);
            var outcome = Repair(plan, seed, 0, 0f);

            Assert.That(outcome.initialComponents, Is.EqualTo(1000));
            Assert.That(outcome.remainingComponents, Is.EqualTo(1000));
            Assert.That(outcome.candidatePairChecks, Is.Zero);
            Assert.That(outcome.eligibleCandidatePairs, Is.Zero);
            Assert.That(outcome.preparedCandidateCount, Is.Zero);
            Assert.That(outcome.discardedCandidatePairs, Is.Zero);
            Assert.That(outcome.attemptBudgetExhausted, Is.False);
            Assert.That(outcome.attemptedRoadIds, Is.Empty);
            Assert.That(plan.Roads.Count, Is.Zero);
        }

        [Test]
        public void SmallDistance_SpatialSweepAvoidsQuadraticPairEnumeration()
        {
            const int seed = 777;
            var plan = new MacroWorldPlan(seed);
            for (int i = 0; i < 1000; i++)
                Assert.That(plan.AddPointFeature(new WorldPointFeatureData(
                    10000 + i, WorldFeatureKind.Ruin, "r",
                    new Vector2(i * 100f, -32f), 0f)), Is.True);

            var outcome = Repair(plan, seed, 1, 1f);

            Assert.That(outcome.initialComponents, Is.EqualTo(1000));
            Assert.That(outcome.remainingComponents, Is.EqualTo(1000));
            Assert.That(outcome.candidatePairChecks, Is.Zero);
            Assert.That(outcome.eligibleCandidatePairs, Is.Zero);
            Assert.That(outcome.preparedCandidateCount, Is.Zero);
            Assert.That(outcome.attemptedRoadIds, Is.Empty);
        }

        [Test]
        public void InvalidMaterializedRoad_RemainsAnExplicitGraphFailure()
        {
            const int seed = 8008;
            var plan = new MacroWorldPlan(seed);
            plan.AddPointFeature(new WorldPointFeatureData(
                10, WorldFeatureKind.Ruin, "r", Vector2.zero, 0f));
            plan.AddPointFeature(new WorldPointFeatureData(
                20, WorldFeatureKind.Ruin, "r", new Vector2(40f, 0f), 0f));
            var connection = new WorldRoadConnectionData(
                9001, 10, 20, RoadKind.DirtRoad);
            plan.AddRoadConnection(connection);
            plan.AddRoad(new WorldRoadData
            {
                stableId = connection.stableId,
                roadKind = RoadKind.DirtRoad,
                width = 3f,
                centerline = new List<Vector2>
                {
                    new Vector2(500f, 500f),
                    new Vector2(550f, 500f)
                }
            });

            var outcome = Repair(plan, seed, 0, 0f);
            var report = WorldRouteConnectivityValidator.Validate(
                plan, 0, true);

            Assert.That(outcome.initialComponents, Is.EqualTo(2));
            Assert.That(outcome.remainingComponents, Is.EqualTo(2));
            Assert.That(plan.Roads.Count, Is.EqualTo(1),
                "Never delete a materialized road during rejected-edge cleanup.");
            Assert.That(report.IsValid, Is.False);
            Assert.That(report.missingRealizedConnections, Is.EqualTo(1));
            Assert.That(report.components, Is.EqualTo(2));
        }

        private RealizedRoadConnectivityRepair.Result Repair(
            MacroWorldPlan plan, int seed, int attempts, float maxDistance)
        {
            var bridges = new BridgePlannerSettings
            {
                enabled = true,
                useFixedStoneBridgeSites = true,
                enableBridgeAwareRouting = true,
                requireConnectedFeatureGraph = true,
                maxBridgeRoutingAttempts = 1,
                maxGuidedCrossingAttempts = 0,
                maxConnectivityRepairCandidates = attempts
            };
            return RealizedRoadConnectivityRepair.Repair(
                seed,
                plan,
                CreateFlatProbe(seed),
                new RoadNetworkPlannerSettings
                {
                    enabled = true,
                    maxConnectionDistance = maxDistance
                },
                new TerrainRoadPathPlannerSettings
                {
                    enabled = true,
                    gridStep = 8f,
                    searchPadding = 8f,
                    maxExpandedNodes = 32
                },
                bridges);
        }

        private static MacroWorldPlan CreateGrid(int seed, int count)
        {
            var plan = new MacroWorldPlan(seed);
            for (int i = 0; i < count; i++)
                Assert.That(plan.AddPointFeature(new WorldPointFeatureData(
                    10000 + i, WorldFeatureKind.Ruin, "r",
                    new Vector2((i % 25) * 4f, (i / 25) * 4f), 0f)), Is.True);
            return plan;
        }

        private static MacroWorldPlan CreateSquare(int seed, bool reverse)
        {
            var plan = new MacroWorldPlan(seed);
            var features = new[]
            {
                new WorldPointFeatureData(10, WorldFeatureKind.Ruin, "r",
                    new Vector2(0f, 0f), 0f),
                new WorldPointFeatureData(20, WorldFeatureKind.Ruin, "r",
                    new Vector2(10f, 0f), 0f),
                new WorldPointFeatureData(30, WorldFeatureKind.Ruin, "r",
                    new Vector2(0f, 10f), 0f),
                new WorldPointFeatureData(40, WorldFeatureKind.Ruin, "r",
                    new Vector2(10f, 10f), 0f)
            };
            for (int i = 0; i < features.Length; i++)
                plan.AddPointFeature(features[reverse
                    ? features.Length - i - 1 : i]);
            return plan;
        }

        private WorldTerrainProbe CreateFlatProbe(int seed)
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
            return new WorldTerrainProbe(
                new WorldGenerationPipeline(settings, null),
                settings, seed);
        }
    }
}
