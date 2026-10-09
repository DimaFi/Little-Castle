using System;
using System.Collections.Generic;
using System.Reflection;
using LittleCastle.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace LittleCastle.Tests
{
    /// <summary>
    /// Lightweight Q15 EditMode tests. Editor code is accessed by reflection
    /// because the existing EditMode asmdef references Runtime only.
    /// No all-seed macro work occurs inside the ordinary test runner.
    /// </summary>
    public sealed class ConceptMapRegressionTests
    {
        private const string AuditName =
            "LittleCastle.Editor.ConceptMapRegressionAudit, LittleCastle.Editor";
        private const string ReportPrefix =
            "LittleCastle.Editor.ConceptMapRegressionAudit+";
        private const string EditorAssembly = ", LittleCastle.Editor";
        private const string Concept =
            "Assets/_Game/Settings/World/ConceptWorld_v001/" +
            "ConceptWorldDefinition.asset";
        private const string NotRun = "NOT_RUN";
        private const string NoPass = "NOT_AN_ACCEPTANCE_RESULT";

        [Test]
        public void FourQuickSeeds_AreDistinctAndIncludeNegativeWorld()
        {
            int[] seeds = Static<int[]>("SourceSeeds");
            Assert.That(seeds.Length, Is.EqualTo(4));
            CollectionAssert.AreEquivalent(
                new[] { 12345, 54321, -10101, 777 }, seeds);
            Assert.That(new HashSet<int>(seeds).Count, Is.EqualTo(4));
            Assert.That(Constant<int>("SmallMacroSeed"), Is.EqualTo(-10101));
            Assert.That(Constant<float>("SmallMacroPlayableMeters"),
                Is.EqualTo(256f));
        }

        [Test]
        public void SavedConceptDefinition_ResolvesRealSettingsNotClones()
        {
            WorldDefinition definition = Saved();
            Assert.DoesNotThrow(() => Call("ValidateSavedDefinition",
                definition));
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
        }

        [Test]
        public void Presets_DistinguishTwoPlayerSliceFromSixteenPreview()
        {
            WorldDefinition def = Saved();
            var small = (WorldMapSizePreset)Call("ResolveSmallPreset", def);
            object snapshot = Call("SnapshotPreset", small);
            Assert.That(Get<string>(snapshot, "id"),
                Is.EqualTo("concept_slice"));
            Assert.That(Get<int>(snapshot, "widthChunks"), Is.EqualTo(32));
            Assert.That(Get<bool>(snapshot, "allowsTwo"), Is.True);
            Assert.That(Get<bool>(snapshot, "allowsEight"), Is.False);
            Assert.That(Get<bool>(snapshot, "allowsSixteen"), Is.False);
            Assert.That(Get<string>(snapshot, "gameplayBalanceStatus"),
                Is.EqualTo(NotRun));

            Assert.That(def.MapRules.TryResolvePreset(
                "concept_preview", 16,
                out WorldMapSizePreset preview,
                out string error), Is.True, error);
            object large = Call("SnapshotPreset", preview);
            Assert.That(Get<bool>(large, "allowsSixteen"), Is.True);
            Assert.That(Get<int>(large, "widthChunks"), Is.EqualTo(96));
            Assert.That(Get<string>(large, "gameplayBalanceStatus"),
                Is.EqualTo(NotRun));
        }

        [Test]
        public void FiniteSessionMap_UsesActualSavedMapPreset()
        {
            WorldDefinition definition = Saved();
            var preset = (WorldMapSizePreset)Call(
                "ResolveSmallPreset", definition);

            WorldSessionMap session = WorldSessionMapFactory.Create(
                -10101, 2, preset, new ChunkCoordinate(0, 0));
            Assert.That(session.mapSizePresetId, Is.EqualTo(preset.presetId));
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
        public void StableIdDigest_IsRepeatableAndDetectsDuplicates()
        {
            object[] argsA = {
                new long[] { 44, -5, 7, -5, 44, long.MinValue }, 0
            };
            object[] argsB = {
                new long[] { 7, 44, long.MinValue, -5, -5, 44 }, 0
            };
            string a = (string)CallWithArgs("SortedUniqueIds", argsA);
            string b = (string)CallWithArgs("SortedUniqueIds", argsB);
            Assert.That(a, Is.EqualTo(b));
            Assert.That(a, Is.EqualTo(
                "-9223372036854775808,-5,7,44"));
            Assert.That((int)argsA[1], Is.EqualTo(2));
            Assert.That((int)argsB[1], Is.EqualTo(2));
        }

        [Test]
        public void MissingGeometricPath_NeverClaimsTraversableRoad()
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

            object output = Call("InspectMacro",
                plan, 42, new Rect(-128, -128, 256, 256),
                Saved().MacroPlannerSettings);
            Assert.That(Get<string>(output, "status"),
                Is.EqualTo("ROUTE_VALIDATOR_FAILED"));
            Assert.That(Get<string>(output, "graphStatus"),
                Is.EqualTo("VALIDATOR_FAIL"));
            Assert.That(Get<string>(output, "geometricPathStatus"),
                Is.EqualTo(NotRun));
            Assert.That(Get<int>(output, "missingRealizedConnections"),
                Is.GreaterThanOrEqualTo(1));
            Assert.That(Get<string>(output, "startExitStatus"),
                Is.EqualTo(NotRun));
            Assert.That(Get<string>(output, "fairnessStatus"),
                Is.EqualTo(NotRun));
            Assert.That(Get<string>(output, "balancedForMultiplayer"),
                Is.EqualTo(NoPass));
        }

        [Test]
        public void EmptyPlan_NotPromotedToMultiplayerReady()
        {
            object output = Call("InspectMacro",
                new MacroWorldPlan(5), 5,
                new Rect(0f, 0f, 256f, 256f),
                Saved().MacroPlannerSettings);

            Assert.That(Get<int>(output, "features"), Is.Zero);
            Assert.That(Get<int>(output, "geometricRoads"), Is.Zero);
            Assert.That(Get<string>(output, "geometricPathStatus"),
                Is.EqualTo(NotRun));
            Assert.That(Get<string>(output, "startExitStatus"),
                Is.EqualTo(NotRun));
            Assert.That(Get<string>(output, "seamStatus"),
                Is.EqualTo(NotRun));
            Assert.That(Get<string>(output, "evictionStatus"),
                Is.EqualTo(NotRun));
            Assert.That(Get<string>(output, "balancedForMultiplayer"),
                Is.EqualTo(NoPass));
        }

        [Test]
        public void NegativePositiveChunkSeam_UsesExactSharedHeight()
        {
            var left = new WorldChunkData(new ChunkCoordinate(-1, -1), 4);
            var right = new WorldChunkData(new ChunkCoordinate(0, -1), 4);
            for (int z = 0; z < left.SamplesPerSide; z++)
            {
                left.SetHeight(4, z, z * 1.125f);
                right.SetHeight(0, z, z * 1.125f);
            }
            Assert.That(Call("HeightsShareXSeam", left, right), Is.EqualTo(true));
            right.SetHeight(0, 3, 12.5f);
            Assert.That(Call("HeightsShareXSeam", left, right), Is.EqualTo(false));
            var distant = new WorldChunkData(
                new ChunkCoordinate(2, -1), 4);
            Assert.That(Call("HeightsShareXSeam", left, distant),
                Is.EqualTo(false));
        }

        [Test]
        public void TerrainAfterRevisit_MustMatchEverySample()
        {
            var coord = new ChunkCoordinate(-3, 2);
            var a = new WorldChunkData(coord, 3);
            var b = new WorldChunkData(coord, 3);
            for (int z = 0; z < a.SamplesPerSide; z++)
                for (int x = 0; x < a.SamplesPerSide; x++)
                {
                    float height = (x * 0.5f) - z * 0.75f;
                    a.SetHeight(x, z, height);
                    b.SetHeight(x, z, height);
                }

            Assert.That(Call("EqualChunkTerrain", a, b), Is.EqualTo(true));
            b.SetHeight(2, 2, 999f);
            Assert.That(Call("EqualChunkTerrain", a, b), Is.EqualTo(false));
            Assert.That(Call("EqualChunkTerrain", a, new WorldChunkData(
                new ChunkCoordinate(0, 0), 3)), Is.EqualTo(false));
        }

        [Test]
        public void RuntimeTreeRemovalSurvivesSyntheticRevisit()
        {
            const long removedId = -9031L;
            var coord = new ChunkCoordinate(-1, 0);
            var original = new WorldChunkData(coord, 4);
            var regenerated = new WorldChunkData(coord, 4);
            var spawn = new WorldSpawnData(
                removedId, "oak", SpawnCategory.Tree,
                new Vector3(-12f, 0f, 1f), 42f, 1f);
            original.AddSpawn(spawn);
            regenerated.AddSpawn(spawn);

            var delta = new WorldRuntimeDeltaState();
            Assert.That(delta.MarkSpawnRemoved(removedId, coord), Is.True);
            delta.RebuildIndexes();
            Assert.That(original.Spawns[0].stableId,
                Is.EqualTo(regenerated.Spawns[0].stableId));
            Assert.That(delta.IsSpawnRemoved(removedId), Is.True);
        }

        [Test]
        public void InvalidDefinitionsAndNullPlan_FailPredictably()
        {
            Assert.Throws<InvalidOperationException>(() =>
                Call("ValidateSavedDefinition", new object[] { null }));
            Assert.Throws<ArgumentNullException>(() =>
                Call("SnapshotPreset", new object[] { null }));
            Assert.Throws<ArgumentNullException>(() =>
                Call("InspectMacro", null, 0,
                    new Rect(0, 0, 1, 1), null));
        }

        [Test]
        public void ReportHasUnmeasuredFieldsNotFakeSuccess()
        {
            object source = Activator.CreateInstance(
                Nested("SourceResult"));
            foreach (string field in new[] {
                "graphStatus", "geometricPathStatus", "startExitStatus",
                "seamStatus", "evictionStatus"
            })
                Assert.That(Get<string>(source, field), Is.EqualTo(NotRun));

            object report = Activator.CreateInstance(Nested("Report"));
            Assert.That(Get<string>(report, "playerStartFairness"),
                Is.EqualTo(NotRun));
            Assert.That(Get<string>(report, "multiplayerReadiness"),
                Is.EqualTo(NoPass));
        }

        private static WorldDefinition Saved()
        {
            WorldDefinition result =
                AssetDatabase.LoadAssetAtPath<WorldDefinition>(Concept);
            Assert.That(result, Is.Not.Null,
                "Q15 requires the saved ConceptWorldDefinition asset.");
            return result;
        }

        private static Type Audit
        {
            get
            {
                Type type = Type.GetType(AuditName);
                Assert.That(type, Is.Not.Null,
                    "Expected LittleCastle.Editor assembly to be loaded.");
                return type;
            }
        }

        private static Type Nested(string shortName)
        {
            Type type = Type.GetType(
                ReportPrefix + shortName + EditorAssembly);
            Assert.That(type, Is.Not.Null, shortName);
            return type;
        }

        private static object Call(string method, params object[] args) =>
            CallWithArgs(method, args);

        private static object CallWithArgs(string method, object[] args)
        {
            MethodInfo info = Audit.GetMethod(
                method, BindingFlags.Public | BindingFlags.Static);
            Assert.That(info, Is.Not.Null, method);
            try
            {
                return info.Invoke(null, args);
            }
            catch (TargetInvocationException exception)
            {
                throw exception.InnerException ?? exception;
            }
        }

        private static T Get<T>(object obj, string field)
        {
            FieldInfo info = obj.GetType().GetField(
                field, BindingFlags.Public | BindingFlags.Instance);
            Assert.That(info, Is.Not.Null, field);
            return (T)info.GetValue(obj);
        }

        private static T Static<T>(string field)
        {
            FieldInfo info = Audit.GetField(
                field, BindingFlags.Public | BindingFlags.Static);
            Assert.That(info, Is.Not.Null, field);
            return (T)info.GetValue(null);
        }

        private static T Constant<T>(string field)
        {
            FieldInfo info = Audit.GetField(
                field, BindingFlags.Public | BindingFlags.Static);
            Assert.That(info, Is.Not.Null, field);
            return (T)info.GetRawConstantValue();
        }
    }
}
