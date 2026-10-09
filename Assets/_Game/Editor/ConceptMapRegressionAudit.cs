using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using LittleCastle.World;
using UnityEditor;
using UnityEngine;

namespace LittleCastle.Editor
{
    /// <summary>
    /// Explicitly bounded, read-only map regression sampler for the existing
    /// saved ConceptWorld_v001 profile. Does not modify settings or run a
    /// large multiplayer matrix. Only a user-invoked menu/executeMethod
    /// performs four river-source runs and one small macro generation.
    /// </summary>
    public static class ConceptMapRegressionAudit
    {
        public const string DefinitionPath =
            "Assets/_Game/Settings/World/ConceptWorld_v001/" +
            "ConceptWorldDefinition.asset";
        public const string SmallPresetId = "concept_slice";
        public const string LargePresetId = "concept_preview";
        public const int SmallMacroSeed = -10101;
        public const float SmallMacroPlayableMeters = 256f;
        public static readonly int[] SourceSeeds =
            { 12345, 54321, -10101, 777 };

        public const string NotRun = "NOT_RUN";
        public const string NoPassClaim = "NOT_AN_ACCEPTANCE_RESULT";

        [Serializable]
        public sealed class SourceResult
        {
            public int seed;
            public string status = NotRun;
            public string failure = string.Empty;
            public int riverCount;
            public int repeatedRiverIds;
            public string sortedRiverIds = string.Empty;
            public long elapsedMilliseconds;
            public RiverPlannerDiagnostics diagnostics;
            public string graphStatus = NotRun;
            public string geometricPathStatus = NotRun;
            public string startExitStatus = NotRun;
            public string seamStatus = NotRun;
            public string evictionStatus = NotRun;
        }

        [Serializable]
        public sealed class MacroResult
        {
            public int seed;
            public float requestedPlayableWidthMeters;
            public float requestedPlayableHeightMeters;
            public float planningHaloMeters;
            public string status = NotRun;
            public string failure = string.Empty;
            public long elapsedMilliseconds;
            public int features;
            public int logicalConnections;
            public int geometricRoads;
            public int rivers;
            public int bridgeSites;
            public bool strictRoutingAttempted;
            public bool strictRoutingSatisfied;
            public string sortedFeatureIds = string.Empty;
            public string sortedRoadIds = string.Empty;
            public string graphStatus = NotRun;
            public string geometricPathStatus = NotRun;
            public int missingRealizedConnections;
            public int disconnectedComponents;
            public int unbridgedCrossings;
            public int orphanBridges;
            public int routingErrors;
            public string startExitStatus = NotRun;
            public string seamStatus = NotRun;
            public string evictionStatus = NotRun;
            public string fairnessStatus = NotRun;
            public string balancedForMultiplayer = NoPassClaim;
        }

        [Serializable]
        public sealed class PresetRecord
        {
            public string id;
            public int minimumPlayers;
            public int maximumPlayers;
            public int widthChunks;
            public int heightChunks;
            public int visualPaddingChunks;
            public bool allowsTwo;
            public bool allowsEight;
            public bool allowsSixteen;
            public string gameplayBalanceStatus = NotRun;
        }

        [Serializable]
        public sealed class Report
        {
            public string utc;
            public string definitionPath;
            public string definitionAssetGuid;
            public string worldId;
            public string generationProfile;
            public int generationVersion;
            public float savedChunkWorldSize;
            public int savedCellsPerSide;
            public string macroSettingsAssetGuid;
            public string status = "DIAGNOSTIC_ONLY";
            public string warnings = string.Empty;
            public string scope =
                "Four saved-policy river SOURCE runs at small preset bounds " +
                "and ONE 256m macro run using the saved settings and halo. " +
                "No 2/8/16-player start fairness or balanced full maps.";
            public List<PresetRecord> presets = new List<PresetRecord>();
            public List<SourceResult> sources = new List<SourceResult>();
            public MacroResult macro = new MacroResult();
            public string playerStartFairness = NotRun;
            public string exitRoutes = NotRun;
            public string visualParity = NotRun;
            public string multiplayerReadiness = NoPassClaim;
        }

        [MenuItem("Little Castle/World/Diagnostics/Q15 Small Map Regression")]
        public static void RunSmall()
        {
            Report result = RunSmallInMemory();
            string root = Path.GetFullPath(
                Path.Combine(Application.dataPath, ".."));
            string directory = Path.Combine(root, "Logs");
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory,
                "Q15-ConceptMap-" + DateTime.UtcNow.ToString(
                    "yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + ".json");
            File.WriteAllText(path, JsonUtility.ToJson(result, true),
                new UTF8Encoding(false));
            UnityEngine.Debug.Log(
                "[Q15] Bounded concept map diagnostic: " + path +
                " (NOT multiplayer balance or Player performance approval)");
        }

        public static Report RunSmallInMemory()
        {
            WorldDefinition definition =
                AssetDatabase.LoadAssetAtPath<WorldDefinition>(DefinitionPath);
            ValidateSavedDefinition(definition);

            var gen = definition.GenerationSettings;
            var macro = definition.MacroPlannerSettings;
            var result = new Report
            {
                utc = DateTime.UtcNow.ToString("o"),
                definitionPath = DefinitionPath,
                definitionAssetGuid = AssetDatabase.AssetPathToGUID(DefinitionPath),
                worldId = definition.WorldId,
                generationProfile = gen.ProfileId,
                generationVersion = gen.GenerationVersion,
                savedChunkWorldSize = gen.ChunkWorldSize,
                savedCellsPerSide = gen.CellsPerSide,
                macroSettingsAssetGuid = AssetDatabase.AssetPathToGUID(
                    AssetDatabase.GetAssetPath(macro))
            };

            for (int i = 0; i < definition.MapRules.Presets.Count; i++)
            {
                WorldMapSizePreset p = definition.MapRules.Presets[i];
                if (p == null)
                    continue;
                result.presets.Add(SnapshotPreset(p));
            }

            WorldMapSizePreset small = ResolveSmallPreset(definition);
            WorldChunkBounds bounds = WorldSessionMapFactory.Create(
                SmallMacroSeed, 2, small, new ChunkCoordinate(0, 0))
                .playableChunks;
            Rect sourcePlayable = bounds.ToWorldRect(gen.ChunkWorldSize);
            Rect sourceBounds = ExpandRect(sourcePlayable, macro.PlanningHalo);

            // NO hidden clones of saved macro settings or RiverPlannerSettings.
            // A fresh WorldTerrainProbe isolates per-seed terrain cache.
            for (int i = 0; i < SourceSeeds.Length; i++)
                result.sources.Add(ScanSource(
                    definition, sourceBounds, SourceSeeds[i]));

            result.macro = ScanSmallMacro(definition);
            result.warnings =
                "Graph/road/river diagnostics do not prove unit navigation, " +
                "start exits, balanced resource access, water visuals, " +
                "cache memory limits, multiplayer fairness or Player FPS.";
            return result;
        }

        public static void ValidateSavedDefinition(WorldDefinition definition)
        {
            if (definition == null ||
                definition.GenerationSettings == null ||
                definition.MacroPlannerSettings == null ||
                definition.MacroPlannerSettings.Rivers == null ||
                definition.MapRules == null)
                throw new InvalidOperationException(
                    "Q15 requires imported saved ConceptWorld_v001 " +
                    "world, generation, macro, river and map rule assets.");

            if (AssetDatabase.GetAssetPath(definition) != DefinitionPath)
                throw new InvalidOperationException(
                    "Q15 refuses any unsaved or swapped concept definition.");

            if (definition.GenerationSettings.Stages.Count == 0)
                throw new InvalidOperationException(
                    "Saved world generation pipeline is empty.");
        }

        public static WorldMapSizePreset ResolveSmallPreset(
            WorldDefinition definition)
        {
            ValidateSavedDefinition(definition);
            if (!definition.MapRules.TryResolvePreset(
                    SmallPresetId, 2,
                    out WorldMapSizePreset preset, out string error))
                throw new InvalidOperationException(
                    "Saved Q15 concept_slice not valid: " + error);
            return preset;
        }

        public static PresetRecord SnapshotPreset(WorldMapSizePreset preset)
        {
            if (preset == null)
                throw new ArgumentNullException(nameof(preset));
            return new PresetRecord
            {
                id = preset.presetId,
                minimumPlayers = preset.minimumPlayers,
                maximumPlayers = preset.maximumPlayers,
                widthChunks = preset.widthChunks,
                heightChunks = preset.heightChunks,
                visualPaddingChunks = preset.visualPaddingChunks,
                allowsTwo = preset.AllowsPlayerCount(2),
                allowsEight = preset.AllowsPlayerCount(8),
                allowsSixteen = preset.AllowsPlayerCount(16)
            };
        }

        public static string SortedUniqueIds(
            IEnumerable<long> ids,
            out int repeated)
        {
            repeated = 0;
            var unique = new HashSet<long>();
            foreach (long id in ids)
                if (!unique.Add(id))
                    repeated++;

            var ordered = new List<long>(unique);
            ordered.Sort();
            var sb = new StringBuilder();
            for (int i = 0; i < ordered.Count; i++)
            {
                if (i != 0)
                    sb.Append(",");
                sb.Append(ordered[i].ToString(
                    CultureInfo.InvariantCulture));
            }
            return sb.ToString();
        }

        public static MacroResult InspectMacro(
            MacroWorldPlan plan,
            int seed,
            Rect playableRect,
            MacroWorldPlannerSettings settings)
        {
            if (plan == null)
                throw new ArgumentNullException(nameof(plan));
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));

            var result = new MacroResult
            {
                seed = seed,
                requestedPlayableWidthMeters = playableRect.width,
                requestedPlayableHeightMeters = playableRect.height,
                planningHaloMeters = settings.PlanningHalo,
                status = "PLAN_BUILT",
                features = plan.PointFeatures.Count,
                logicalConnections = plan.RoadConnections.Count,
                geometricRoads = plan.Roads.Count,
                rivers = plan.Rivers.Count,
                bridgeSites = plan.BridgeSites.Count,
                strictRoutingAttempted = plan.BridgeAwareRoutingAttempted,
                strictRoutingSatisfied = plan.BridgeAwareRoutingSatisfied
            };

            var featureIds = new List<long>(plan.PointFeatures.Count);
            for (int i = 0; i < plan.PointFeatures.Count; i++)
                featureIds.Add(plan.PointFeatures[i].stableId);
            var roadIds = new List<long>(plan.Roads.Count);
            for (int i = 0; i < plan.Roads.Count; i++)
                if (plan.Roads[i] != null)
                    roadIds.Add(plan.Roads[i].stableId);

            result.sortedFeatureIds = SortedUniqueIds(
                featureIds, out int repeatedFeatures);
            result.sortedRoadIds = SortedUniqueIds(
                roadIds, out int repeatedRoads);
            if (repeatedFeatures != 0 || repeatedRoads != 0)
            {
                result.status = "INVALID_DUPLICATE_IDENTITIES";
                result.failure = "Duplicate feature IDs=" + repeatedFeatures +
                    ", road IDs=" + repeatedRoads;
            }

            int requiredBridges = settings.Bridges != null &&
                settings.Bridges.enabled
                ? settings.Bridges.minimumFixedBridgeCount : 0;
            bool requireConnected = settings.Bridges != null &&
                settings.Bridges.enabled &&
                settings.Bridges.requireConnectedFeatureGraph;
            WorldRouteConnectivityValidator.Report route =
                WorldRouteConnectivityValidator.Validate(
                    plan, requiredBridges, requireConnected);

            result.graphStatus = route.IsValid
                ? "VALIDATOR_PASS" : "VALIDATOR_FAIL";
            result.geometricPathStatus =
                plan.Roads.Count > 0
                    ? (route.missingRealizedConnections == 0
                        ? "PATH_RECORDS_VALIDATED_NOT_NAVMESH"
                        : "MISSING_REALIZED_ROAD_CONNECTIONS")
                    : NotRun;
            result.missingRealizedConnections =
                route.missingRealizedConnections;
            result.disconnectedComponents = route.components;
            result.unbridgedCrossings = route.unbridgedCrossings;
            result.orphanBridges = route.orphanBridgeSites;
            result.routingErrors = route.errors.Count;

            if (!route.IsValid && result.status == "PLAN_BUILT")
            {
                result.status = "ROUTE_VALIDATOR_FAILED";
                result.failure = route.Summary;
            }
            return result;
        }

        /// <summary>
        /// Compare physical samples at the same world X/Z chunk boundary.
        /// Caller must supply adjacent chunks at identical seed/plan/stages.
        /// This checks height values only, NOT normals/water/roads/NavMesh.
        /// </summary>
        public static bool HeightsShareXSeam(
            WorldChunkData left, WorldChunkData right)
        {
            if (left == null || right == null ||
                left.Coordinate.x + 1 != right.Coordinate.x ||
                left.Coordinate.z != right.Coordinate.z ||
                left.SamplesPerSide != right.SamplesPerSide)
                return false;

            int end = left.SamplesPerSide - 1;
            for (int z = 0; z <= end; z++)
            {
                if (left.GetHeight(end, z) != right.GetHeight(0, z))
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Deterministic terrain identity after regeneration. Does not
        /// simulate unloading a live WorldStreamer or scene GameObjects.
        /// </summary>
        public static bool EqualChunkTerrain(
            WorldChunkData a, WorldChunkData b)
        {
            if (a == null || b == null ||
                a.Coordinate != b.Coordinate ||
                a.SamplesPerSide != b.SamplesPerSide)
                return false;
            for (int z = 0; z < a.SamplesPerSide; z++)
                for (int x = 0; x < a.SamplesPerSide; x++)
                    if (a.GetHeight(x, z) != b.GetHeight(x, z))
                        return false;
            return true;
        }

        private static SourceResult ScanSource(
            WorldDefinition definition, Rect bounds, int seed)
        {
            var record = new SourceResult
            {
                seed = seed,
                diagnostics = new RiverPlannerDiagnostics()
            };
            WorldGenerationSettings gen = definition.GenerationSettings;
            var probe = new WorldTerrainProbe(
                new WorldGenerationPipeline(gen), gen, seed);
            var plan = new MacroWorldPlan(seed);
            var stopwatch = Stopwatch.StartNew();

            try
            {
                RiverNetworkPlanner.BuildRivers(
                    seed, bounds, probe,
                    definition.MacroPlannerSettings.Rivers,
                    plan, record.diagnostics);
                record.riverCount = plan.Rivers.Count;
                var ids = new List<long>(plan.Rivers.Count);
                for (int r = 0; r < plan.Rivers.Count; r++)
                    ids.Add(plan.Rivers[r].stableId);
                record.sortedRiverIds =
                    SortedUniqueIds(ids, out int repetitions);
                record.repeatedRiverIds = repetitions;
                record.status = repetitions > 0
                    ? "DUPLICATE_RIVER_ID"
                    : plan.Rivers.Count > 0
                        ? "RIVER_SOURCES_ACCEPTED"
                        : "NO_RIVER_SOURCES_ACCEPTED";
            }
            catch (Exception exception)
            {
                record.status = "RIVER_SOURCE_EXCEPTION";
                record.failure = exception.ToString();
            }
            finally
            {
                stopwatch.Stop();
                record.elapsedMilliseconds = stopwatch.ElapsedMilliseconds;
                probe.Clear();
            }
            return record;
        }

        private static MacroResult ScanSmallMacro(WorldDefinition definition)
        {
            var result = new MacroResult
            {
                seed = SmallMacroSeed,
                requestedPlayableWidthMeters = SmallMacroPlayableMeters,
                requestedPlayableHeightMeters = SmallMacroPlayableMeters,
                planningHaloMeters =
                    definition.MacroPlannerSettings.PlanningHalo
            };
            var probe = new WorldTerrainProbe(
                new WorldGenerationPipeline(definition.GenerationSettings),
                definition.GenerationSettings, SmallMacroSeed);

            var clock = Stopwatch.StartNew();
            try
            {
                float half = SmallMacroPlayableMeters * 0.5f;
                var playable = new Rect(
                    -half, -half, SmallMacroPlayableMeters,
                    SmallMacroPlayableMeters);
                var planner = new MacroWorldPlanner(
                    definition.MacroPlannerSettings);
                MacroWorldPlan plan = planner.GenerateForBounds(
                    SmallMacroSeed, playable, probe);

                result = InspectMacro(
                    plan, SmallMacroSeed, playable,
                    definition.MacroPlannerSettings);
            }
            catch (Exception exception)
            {
                result.status = "SMALL_MACRO_EXCEPTION";
                result.failure = exception.ToString();
            }
            finally
            {
                clock.Stop();
                result.elapsedMilliseconds = clock.ElapsedMilliseconds;
                probe.Clear();
            }
            return result;
        }

        private static Rect ExpandRect(Rect rect, float padding)
        {
            float safe = Mathf.Max(0f, padding);
            rect.xMin -= safe;
            rect.xMax += safe;
            rect.yMin -= safe;
            rect.yMax += safe;
            return rect;
        }
    }
}
