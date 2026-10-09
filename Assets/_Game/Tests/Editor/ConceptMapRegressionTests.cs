using System;
using System.Collections.Generic;
using LittleCastle.Editor;
using LittleCastle.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace LittleCastle.Tests
{
    /// <summary>
    /// Low-cost Q15 Editor tests: configuration and pure analysis only.
    /// They intentionally do not call RunSmallInMemory, which performs four
    /// real source tracings and one real (potentially costly) macro planner.
    /// </summary>
    public sealed class ConceptMapRegressionTests
    {
        [Test]
        public void FourQuickSeeds_AreDistinctAndIncludeNegativeWorld()
        {
            Assert.That(ConceptMapRegressionAudit.SourceSeeds.Length,
                Is.EqualTo(4));
            CollectionAssert.AreEquivalent(
                new[] { 12345, 54321, -10101, 777 },
                ConceptMapRegressionAudit.SourceSeeds);
            Assert.That(new HashSet<int>(
                ConceptMapRegressionAudit.SourceSeeds).Count,
                Is.EqualTo(4));
            Assert.That(ConceptMapRegressionAudit.SmallMacroSeed,
                Is.EqualTo(-10101));
            Assert.That(ConceptMapRegressionAudit.SmallMacroPlayableMeters,
                Is.EqualTo(256f));
        }

        [Test]
        public void SavedConceptDefinition_ResolvesRealSettingsNotClones()
        {
            var definition =
                AssetDatabase.LoadAssetAtPath<WorldDefinition>(
                    ConceptMapRegressionAudit.DefinitionPath);
            Assert.That(definition, Is.Not.Null,
                "Q15 is a saved-concept audit, not a synthetic config suite.");
            Assert.DoesNotThrow(() =>
                ConceptMapRegressionAudit.ValidateSavedDefinition(definition));
            Assert.That(AssetDatabase.GetAssetPath(
                definition.GenerationSettings),
                Does.StartWith(
                    "Assets/_Game/Settings/World/ConceptWorld_v001/"));
            Assert.That(AssetDatabase.GetAssetPath(
                definition.MacroPlannerSettings),
                Does.StartWith(
                    "Assets/_Game/Settings/World/ConceptWorld_v001/"));
            Assert.That(definition.GenerationSettings.Stages.Count,
                Is.GreaterThan(0));
            Assert.That(definition.GenerationSettings.CellsPerSide,
                Is.EqualTo(64));
            Assert.That(definition.GenerationSettings.ChunkWorldSize,
                Is.EqualTo(32f));
            Assert.That(definition.MacroPlannerSettings.PlanningHalo,
                Is.GreaterThanOrEqualTo(0f));
        }

        [Test]
        public void SavedMapPreset_SeparatesTwoPlayerSliceFromSixteenPreview()
        {
            var definition =
                AssetDatabase.LoadAssetAtPath<WorldDefinition>(
                    ConceptMapRegressionAudit.DefinitionPath);
            ConceptMapRegressionAudit.ValidateSavedDefinition(definition);
            WorldMapSizePreset small =
                ConceptMapRegressionAudit.ResolveSmallPreset(definition);
            var snapshot =
                ConceptMapRegressionAudit.SnapshotPreset(small);

            Assert.That(snapshot.id, Is.EqualTo("concept_slice"));
            Assert.That(snapshot.widthChunks, Is.EqualTo(32));
            Assert.That(snapshot.heightChunks, Is.EqualTo(32));
            Assert.That(snapshot.allowsTwo, Is.True);
            Assert.That(snapshot.allowsEight, Is.False);
            Assert.That(snapshot.allowsSixteen, Is.False);
            Assert.That(snapshot.gameplayBalanceStatus,
                Is.EqualTo(ConceptMapRegressionAudit.NotRun));

            Assert.That(definition.MapRules.TryResolvePreset(
                ConceptMapRegressionAudit.LargePresetId, 16,
                out WorldMapSizePreset preview,
                out string error), Is.True, error);
            var large =
                ConceptMapRegressionAudit.SnapshotPreset(preview);
            Assert.That(large.allowsSixteen, Is.True);
            Assert.That(large.widthChunks, Is.EqualTo(96));
            Assert.That(large.heightChunks, Is.EqualTo(96));
            Assert.That(large.gameplayBalanceStatus,
                Is.EqualTo(ConceptMapRegressionAudit.NotRun));
        }

        [Test]
        public void FiniteSessionMap_UsesSavedPresetSizeNotFakeMetricLabel()
        {
            var definition =
                AssetDatabase.LoadAssetAtPath<WorldDefinition>(
                    ConceptMapRegressionAudit.DefinitionPath);
            WorldMapSizePreset preset =
                ConceptMapRegressionAudit.ResolveSmallPreset(definition);

            WorldSessionMap session = WorldSessionMapFactory.Create(
                -10101, 2, preset, new ChunkCoordinate(0, 0));
            Assert.That(session.mapSizePresetId,
                Is.EqualTo(preset.presetId));
            Assert.That(session.worldSeed, Is.EqualTo(-10101));
            Assert.That(session.playableChunks.SizeX,
                Is.EqualTo(preset.widthChunks));
            Assert.That(session.playableChunks.SizeZ,
                Is.EqualTo(preset.heightChunks));
            Assert.That(session.visualChunks.SizeX,
                Is.EqualTo(preset.widthChunks +
                    2 * preset.visualPaddingChunks));
            Assert.That(session.IsPlayableChunk(
                new ChunkCoordinate(0, 0)), Is.True);
            Assert.That(session.IsPlayableChunk(
                new ChunkCoordinate(999, 0)), Is.False);
        }

        [Test]
        public void SortedIds_AreRepeatableAcrossInputOrderAndDetectDuplicates()
        {
            string a = ConceptMapRegressionAudit.SortedUniqueIds(
                new long[] { 44, -5, 7, -5, 44, long.MinValue },
                out int duplicateA);
            string b = ConceptMapRegressionAudit.SortedUniqueIds(
                new long[] { 7, 44, long.MinValue, -5, -5, 44 },
                out int duplicateB);
            Assert.That(a, Is.EqualTo(b));
            Assert.That(a, Is.EqualTo(
                "-9223372036854775808,-5,7,44"));
            Assert.That(duplicateA, Is.EqualTo(2));
            Assert.That(duplicateB, Is.EqualTo(2));
        }

        [Test]
        public void MissingRealizedRoad_IsNotReportedAsNavigable()
        {
            var plan = new MacroWorldPlan(42);
            plan.AddPointFeature(new WorldPointFeatureData(
                17, WorldFeatureKind.NeutralSettlement,
                "test", new Vector2(-2f, -2f), 1f));
            plan.AddPointFeature(new WorldPointFeatureData(
                18, WorldFeatureKind.NeutralSettlement,
                "test", new Vector2(12f, 0f), 1f));
            plan.AddRoadConnection(new WorldRoadConnectionData(
                500, 17, 18, RoadKind.DirtRoad));

            var definition =
                AssetDatabase.LoadAssetAtPath<WorldDefinition>(
                    ConceptMapRegressionAudit.DefinitionPath);
            var result = ConceptMapRegressionAudit.InspectMacro(
                plan, 42, new Rect(-128, -128, 256, 256),
                definition.MacroPlannerSettings);

            Assert.That(result.status,
                Is.EqualTo("ROUTE_VALIDATOR_FAILED"));
            Assert.That(result.graphStatus,
                Is.EqualTo("VALIDATOR_FAIL"));
            Assert.That(result.geometricPathStatus,
                Is.EqualTo(ConceptMapRegressionAudit.NotRun));
            Assert.That(result.missingRealizedConnections,
                Is.GreaterThanOrEqualTo(1));
            Assert.That(result.startExitStatus,
                Is.EqualTo(ConceptMapRegressionAudit.NotRun));
            Assert.That(result.fairnessStatus,
                Is.EqualTo(ConceptMapRegressionAudit.NotRun));
            Assert.That(result.balancedForMultiplayer,
                Is.EqualTo(ConceptMapRegressionAudit.NoPassClaim));
        }

        [Test]
        public void EmptyMacro_DoesNotImplyPlayerStartOrGraphAcceptance()
        {
            var definition =
                AssetDatabase.LoadAssetAtPath<WorldDefinition>(
                    ConceptMapRegressionAudit.DefinitionPath);
            var result = ConceptMapRegressionAudit.InspectMacro(
                new MacroWorldPlan(5), 5,
                new Rect(0f, 0f, 256f, 256f),
                definition.MacroPlannerSettings);

            Assert.That(result.features, Is.Zero);
            Assert.That(result.geometricRoads, Is.Zero);
            Assert.That(result.bridgeSites, Is.Zero);
            Assert.That(result.geometricPathStatus,
                Is.EqualTo(ConceptMapRegressionAudit.NotRun));
            Assert.That(result.startExitStatus,
                Is.EqualTo(ConceptMapRegressionAudit.NotRun));
            Assert.That(result.evictionStatus,
                Is.EqualTo(ConceptMapRegressionAudit.NotRun));
            Assert.That(result.seamStatus,
                Is.EqualTo(ConceptMapRegressionAudit.NotRun));
            Assert.That(result.balancedForMultiplayer,
                Is.EqualTo(ConceptMapRegressionAudit.NoPassClaim));
        }

        [Test]
        public void NegativeToPositiveNeighbor_CanShareBitIdenticalHeights()
        {
            var left = new WorldChunkData(new ChunkCoordinate(-1, -1), 4);
            var right = new WorldChunkData(new ChunkCoordinate(0, -1), 4);
            for (int z = 0; z < left.SamplesPerSide; z++)
            {
                left.SetHeight(4, z, z * 1.125f);
                right.SetHeight(0, z, z * 1.125f);
            }
            Assert.That(ConceptMapRegressionAudit.HeightsShareXSeam(
                left, right), Is.True);

            right.SetHeight(0, 3, 12.5f);
            Assert.That(ConceptMapRegressionAudit.HeightsShareXSeam(
                left, right), Is.False);

            var distant = new WorldChunkData(
                new ChunkCoordinate(2, -1), 4);
            Assert.That(ConceptMapRegressionAudit.HeightsShareXSeam(
                left, distant), Is.False);
        }

        [Test]
        public void RevisitTerrainWithSameCoordinate_MustPreserveAllSamples()
        {
            var origin = new ChunkCoordinate(-3, 2);
            var a = new WorldChunkData(origin, 3);
            var b = new WorldChunkData(origin, 3);
            for (int z = 0; z < a.SamplesPerSide; z++)
                for (int x = 0; x < a.SamplesPerSide; x++)
                {
                    float height = (x * 0.5f) - z * 0.75f;
                    a.SetHeight(x, z, height);
                    b.SetHeight(x, z, height);
                }
            Assert.That(ConceptMapRegressionAudit.EqualChunkTerrain(
                a, b), Is.True);
            b.SetHeight(2, 2, 999f);
            Assert.That(ConceptMapRegressionAudit.EqualChunkTerrain(
                a, b), Is.False);
            Assert.That(ConceptMapRegressionAudit.EqualChunkTerrain(
                a, new WorldChunkData(
                    new ChunkCoordinate(0, 0), 3)), Is.False);
        }

        [Test]
        public void RemovedTreeDelta_PersistsAcrossSyntheticRegeneration()
        {
            const long removedId = -9031L;
            var original = new WorldChunkData(
                new ChunkCoordinate(-1, 0), 4);
            var regenerated = new WorldChunkData(
                new ChunkCoordinate(-1, 0), 4);
            var spawn = new WorldSpawnData(
                removedId, "oak", SpawnCategory.Tree,
                new Vector3(-12f, 0f, 1f), 42f, 1f);
            original.AddSpawn(spawn);
            regenerated.AddSpawn(spawn);
            var delta = new WorldRuntimeDeltaState();
            Assert.That(delta.MarkSpawnRemoved(
                removedId, original.Coordinate), Is.True);
            delta.RebuildIndexes();

            Assert.That(regenerated.Spawns[0].stableId,
                Is.EqualTo(original.Spawns[0].stableId));
            Assert.That(delta.IsSpawnRemoved(removedId), Is.True,
                "Eviction must not automatically resurrect removed trees.");
            Assert.That(ConceptMapRegressionAudit.NotRun,
                Is.EqualTo("NOT_RUN"),
                "This synthetic test is not a real WorldStreamer eviction test.");
        }

        [Test]
        public void ValidatorRefusesMissingSavedDefinitionAndInvalidInputs()
        {
            Assert.Throws<InvalidOperationException>(() =>
                ConceptMapRegressionAudit.ValidateSavedDefinition(null));
            Assert.Throws<ArgumentNullException>(() =>
                ConceptMapRegressionAudit.SnapshotPreset(null));
            Assert.Throws<ArgumentNullException>(() =>
                ConceptMapRegressionAudit.InspectMacro(
                    null, 0, new Rect(0, 0, 1, 1), null));
        }

        [Test]
        public void PlainSourceRecord_HasExplicitUnmeasuredFields()
        {
            var source = new ConceptMapRegressionAudit.SourceResult
            {
                seed = -10101
            };
            Assert.That(source.graphStatus,
                Is.EqualTo(ConceptMapRegressionAudit.NotRun));
            Assert.That(source.geometricPathStatus,
                Is.EqualTo(ConceptMapRegressionAudit.NotRun));
            Assert.That(source.startExitStatus,
                Is.EqualTo(ConceptMapRegressionAudit.NotRun));
            Assert.That(source.seamStatus,
                Is.EqualTo(ConceptMapRegressionAudit.NotRun));
            Assert.That(source.evictionStatus,
                Is.EqualTo(ConceptMapRegressionAudit.NotRun));
            var report = new ConceptMapRegressionAudit.Report();
            Assert.That(report.playerStartFairness,
                Is.EqualTo(ConceptMapRegressionAudit.NotRun));
            Assert.That(report.multiplayerReadiness,
                Is.EqualTo(ConceptMapRegressionAudit.NoPassClaim));
        }
    }
}
