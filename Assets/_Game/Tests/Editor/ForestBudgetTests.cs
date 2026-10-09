using System;
using System.Collections.Generic;
using LittleCastle.World;
using NUnit.Framework;
using UnityEngine;

namespace LittleCastle.Tests
{
    /// <summary>
    /// Data-only EditMode checks. These are NOT Frame Debugger/GPU tests:
    /// the presenter is deliberately not integrated in any scene.
    /// </summary>
    public sealed class ForestBudgetTests
    {
        private static ForestPresentationSettings Settings(
            int full = 2,
            int optionalColliders = 1,
            float retention = 1f)
        {
            return new ForestPresentationSettings
            {
                nearDistanceMeters = 10f,
                distantDistanceMeters = 50f,
                optionalColliderDistanceMeters = 4f,
                maxFullPrefabInstances = full,
                maxOptionalDecorativeColliders = optionalColliders,
                clusterCoveredFarRetention = retention
            };
        }

        private static ForestPresentationCandidate Tree(
            long id,
            float x,
            bool gameplay = false,
            bool silhouette = true,
            bool cluster = false)
        {
            return new ForestPresentationCandidate(
                id, new Vector2(x, 0f), gameplay,
                silhouette, cluster);
        }

        private static List<ForestPresentationChoice> Evaluate(
            IList<ForestPresentationCandidate> items,
            ForestPresentationSettings settings,
            out ForestPresentationMetrics metrics,
            int visualSeed = 11,
            Vector2 viewpoint = default)
        {
            var output = new List<ForestPresentationChoice>();
            metrics = new ForestPresentationBudget().Evaluate(
                new List<ForestPresentationCandidate>(items),
                viewpoint, visualSeed, settings, output);
            return output;
        }

        [Test]
        public void NearAndGameplayTreesRemainPresent_EvenWhenBudgetExceeded()
        {
            var trees = new[]
            {
                Tree(5, 90f, false, true, true),
                Tree(3, 20f),
                Tree(1, 1f),
                Tree(4, 100f, true, true, true),
                Tree(2, 2f)
            };
            List<ForestPresentationChoice> result = Evaluate(
                trees, Settings(retention: 0f), out var metrics);

            Assert.That(result.Count, Is.EqualTo(5));
            for (int i = 0; i < result.Count; i++)
                Assert.That(result[i].stableId, Is.EqualTo(i + 1));

            Assert.That(result[0].presentation,
                Is.EqualTo(ForestPresentationKind.AuthoredPrefab));
            Assert.That(result[1].presentation,
                Is.EqualTo(ForestPresentationKind.AuthoredPrefab));
            Assert.That(result[2].presentation,
                Is.EqualTo(ForestPresentationKind.ApprovedSilhouette));
            Assert.That(result[3].presentation,
                Is.EqualTo(ForestPresentationKind.AuthoredPrefab));
            Assert.That(result[3].retainGameplayCollider, Is.True);
            Assert.That(result[4].presentation,
                Is.EqualTo(ForestPresentationKind.CoveredByForestCluster));

            Assert.That(metrics.candidates, Is.EqualTo(5));
            Assert.That(metrics.fullPrefabs, Is.EqualTo(3));
            Assert.That(metrics.silhouettes, Is.EqualTo(1));
            Assert.That(metrics.clusterCovered, Is.EqualTo(1));
            Assert.That(metrics.gameplayProtected, Is.EqualTo(1));
            Assert.That(metrics.fullPrefabBudgetOverflow,
                Is.GreaterThanOrEqualTo(1));
            Assert.That(metrics.optionalColliders, Is.EqualTo(1));
            Assert.That(result[0].allowOptionalVisualCollider, Is.True);
            Assert.That(result[1].allowOptionalVisualCollider, Is.False);
        }

        [Test]
        public void ShuffledChunkArrivalAndEqualDistances_DoNotChangeAssignments()
        {
            var original = new[]
            {
                Tree(20, 20f), Tree(-40, -20f), Tree(5, 5f),
                Tree(30, -5f), Tree(1, 60f, false, false, true)
            };
            var reversed = new List<ForestPresentationCandidate>(original);
            reversed.Reverse();

            List<ForestPresentationChoice> a = Evaluate(
                original, Settings(full: 1, retention: 0.5f), out var ma);
            List<ForestPresentationChoice> b = Evaluate(
                reversed, Settings(full: 1, retention: 0.5f), out var mb);

            Assert.That(a.Count, Is.EqualTo(b.Count));
            for (int i = 0; i < a.Count; i++)
            {
                Assert.That(a[i].stableId, Is.EqualTo(b[i].stableId));
                Assert.That(a[i].presentation,
                    Is.EqualTo(b[i].presentation));
                Assert.That(a[i].allowOptionalVisualCollider,
                    Is.EqualTo(b[i].allowOptionalVisualCollider));
            }
            Assert.That(ma.fullPrefabs, Is.EqualTo(mb.fullPrefabs));
            Assert.That(ma.optionalColliders, Is.EqualTo(mb.optionalColliders));
        }

        [Test]
        public void OptionalCollidersRespectNearestStableIdTieBreak()
        {
            var trees = new[]
            {
                Tree(20, 3f), Tree(10, -3f),
                Tree(30, 4f), Tree(40, 8f)
            };
            var result = Evaluate(trees,
                Settings(full: 0, optionalColliders: 1),
                out var metrics);

            Assert.That(result[0].stableId, Is.EqualTo(10));
            Assert.That(result[0].allowOptionalVisualCollider, Is.True);
            for (int i = 1; i < result.Count; i++)
                Assert.That(result[i].allowOptionalVisualCollider, Is.False);
            Assert.That(metrics.optionalColliders, Is.EqualTo(1));
            Assert.That(metrics.fullPrefabBudgetOverflow, Is.EqualTo(4),
                "Near trees must remain present even when visual quota is zero.");
        }

        [Test]
        public void NoVerifiedReplacement_KeepsDistantTreeAuthored()
        {
            var tree = Tree(8, 999f, false, false, false);
            List<ForestPresentationChoice> result = Evaluate(
                new[] { tree }, Settings(full: 0, retention: 0f),
                out var metrics);

            Assert.That(result[0].presentation,
                Is.EqualTo(ForestPresentationKind.AuthoredPrefab));
            Assert.That(metrics.fullPrefabBudgetOverflow, Is.EqualTo(1));
            Assert.That(metrics.clusterCovered, Is.Zero);
        }

        [Test]
        public void ClusterThinningRequiresExplicitCoverageAndNeverGameplay()
        {
            var trees = new[]
            {
                Tree(1, 90f, false, true, false),
                Tree(2, 90f, false, false, true),
                Tree(3, 90f, true, true, true)
            };
            var result = Evaluate(
                trees, Settings(full: 0, retention: 0f), out var metrics);

            Assert.That(result[0].presentation,
                Is.EqualTo(ForestPresentationKind.ApprovedSilhouette));
            Assert.That(result[1].presentation,
                Is.EqualTo(ForestPresentationKind.CoveredByForestCluster));
            Assert.That(result[2].presentation,
                Is.EqualTo(ForestPresentationKind.AuthoredPrefab));
            Assert.That(result[2].retainGameplayCollider, Is.True);
            Assert.That(metrics.gameplayProtected, Is.EqualTo(1));
        }

        [Test]
        public void FarDecisionsUseStableIdAndSeed_NotIterationOrderOrFrame()
        {
            var items = new List<ForestPresentationCandidate>();
            for (int i = 0; i < 80; i++)
                items.Add(Tree(-1000L - i, 200f + i, false, true, true));
            var baseline = Evaluate(items,
                Settings(full: 0, retention: 0.4f), out var first, -10101,
                new Vector2(-900f, -100f));
            var repeated = Evaluate(items,
                Settings(full: 0, retention: 0.4f), out var second, -10101,
                new Vector2(-900f, -100f));
            Assert.That(first.clusterCovered,
                Is.EqualTo(second.clusterCovered));
            Assert.That(first.clusterCovered, Is.GreaterThan(0));
            Assert.That(first.clusterCovered, Is.LessThan(80));

            for (int i = 0; i < baseline.Count; i++)
            {
                Assert.That(baseline[i].stableId,
                    Is.EqualTo(repeated[i].stableId));
                Assert.That(baseline[i].presentation,
                    Is.EqualTo(repeated[i].presentation));
            }
        }

        [Test]
        public void InvalidOrDuplicateInput_DoesNotMutatePreviousOutput()
        {
            var budget = new ForestPresentationBudget();
            var output = new List<ForestPresentationChoice>();
            budget.Evaluate(new[] { Tree(1, 1f) }, Vector2.zero, 5,
                Settings(), output);
            Assert.That(output.Count, Is.EqualTo(1));
            long priorId = output[0].stableId;

            Assert.Throws<ArgumentException>(() =>
                budget.Evaluate(new[] { Tree(2, 1f), Tree(2, 2f) },
                    Vector2.zero, 5, Settings(), output));
            Assert.That(output.Count, Is.EqualTo(1));
            Assert.That(output[0].stableId, Is.EqualTo(priorId));

            Assert.Throws<ArgumentException>(() =>
                budget.Evaluate(new[] { new ForestPresentationCandidate(
                    3, new Vector2(float.NaN, 0f), false, false, false) },
                    Vector2.zero, 5, Settings(), output));
            Assert.That(output[0].stableId, Is.EqualTo(priorId));
        }

        [Test]
        public void InvalidSettingsAndFocus_FailBeforeWritingChoices()
        {
            var budget = new ForestPresentationBudget();
            var output = new List<ForestPresentationChoice>();
            var invalid = Settings();
            invalid.optionalColliderDistanceMeters = 15f;
            Assert.Throws<ArgumentException>(() =>
                budget.Evaluate(new[] { Tree(1, 1f) }, Vector2.zero,
                    1, invalid, output));
            Assert.Throws<ArgumentException>(() =>
                budget.Evaluate(new[] { Tree(1, 1f) },
                    new Vector2(float.PositiveInfinity, 0f),
                    1, Settings(), output));
            Assert.Throws<ArgumentNullException>(() =>
                budget.Evaluate(null, Vector2.zero, 1, Settings(), output));
            Assert.That(output, Is.Empty);
        }

        [Test]
        public void AdapterSkipsRemovedTrees_AndNonTreeSpawnsAcrossChunks()
        {
            var a = new WorldChunkData(new ChunkCoordinate(-1, 0), 4);
            var b = new WorldChunkData(new ChunkCoordinate(0, 0), 4);
            a.AddSpawn(Spawn(42, SpawnCategory.Tree, -20f));
            a.AddSpawn(Spawn(43, SpawnCategory.Tree, -10f));
            b.AddSpawn(Spawn(44, SpawnCategory.Rock, 10f));
            b.AddSpawn(Spawn(45, SpawnCategory.Tree, 20f));
            var delta = new WorldRuntimeDeltaState();
            delta.MarkSpawnRemoved(43);

            var adapter = new ForestPresentationAdapter();
            var output = new List<ForestPresentationChoice>();
            ForestPresentationMetrics metrics =
                adapter.EvaluateActiveChunks(
                    new[] { b, a }, delta,
                    _ => new ForestAssetPresentationEligibility(
                        false, true, false),
                    new Vector2(0f, 0f), 19, Settings(), output);

            Assert.That(metrics.candidates, Is.EqualTo(2));
            Assert.That(output.Count, Is.EqualTo(2));
            Assert.That(output[0].stableId, Is.EqualTo(42));
            Assert.That(output[1].stableId, Is.EqualTo(45));
            Assert.That(delta.IsSpawnRemoved(43), Is.True,
                "Presentation selection never restores a chopped tree.");
            Assert.That(a.Spawns.Count, Is.EqualTo(2));
            Assert.That(b.Spawns.Count, Is.EqualTo(2));
        }

        [Test]
        public void AdapterWithoutCatalogAuthorization_FailsOpenToGameplay()
        {
            var chunk = new WorldChunkData(new ChunkCoordinate(0, -1), 4);
            chunk.AddSpawn(Spawn(-44, SpawnCategory.Tree, 900f));
            var adapter = new ForestPresentationAdapter();
            var result = new List<ForestPresentationChoice>();
            var metrics = adapter.EvaluateActiveChunks(
                new[] { chunk }, null, null, Vector2.zero,
                42, Settings(full: 0, retention: 0f), result);

            Assert.That(metrics.gameplayProtected, Is.EqualTo(1));
            Assert.That(result[0].presentation,
                Is.EqualTo(ForestPresentationKind.AuthoredPrefab));
            Assert.That(result[0].retainGameplayCollider, Is.True);
        }

        [Test]
        public void AdapterDetectsDuplicateStableIdsAcrossChunkSeams()
        {
            var left = new WorldChunkData(new ChunkCoordinate(-1, 0), 4);
            var right = new WorldChunkData(new ChunkCoordinate(0, 0), 4);
            left.AddSpawn(Spawn(55, SpawnCategory.Tree, -1f));
            right.AddSpawn(Spawn(55, SpawnCategory.Tree, 1f));
            var output = new List<ForestPresentationChoice>();
            Assert.Throws<ArgumentException>(() =>
                new ForestPresentationAdapter().EvaluateActiveChunks(
                    new[] { left, right }, null, null,
                    Vector2.zero, 1, Settings(), output));
            Assert.That(output, Is.Empty);
        }

        [Test]
        public void ReusingBudgetInstance_RecomputesWithoutStaleCandidates()
        {
            var budget = new ForestPresentationBudget();
            var output = new List<ForestPresentationChoice>();
            budget.Evaluate(new[] { Tree(1, 3f), Tree(2, 20f) },
                Vector2.zero, 1, Settings(), output);
            Assert.That(output.Count, Is.EqualTo(2));
            budget.Evaluate(new[] { Tree(-2, -3f) },
                Vector2.zero, 1, Settings(), output);
            Assert.That(output.Count, Is.EqualTo(1));
            Assert.That(output[0].stableId, Is.EqualTo(-2));
        }

        private static WorldSpawnData Spawn(long id, SpawnCategory category,
            float x) =>
            new WorldSpawnData(id, "test_oak", category,
                new Vector3(x, 0f, 0f), 0f, 1f);
    }
}
