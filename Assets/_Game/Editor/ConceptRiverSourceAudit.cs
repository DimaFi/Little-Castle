using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using LittleCastle.World;
using UnityEditor;
using UnityEngine;

namespace LittleCastle.Editor
{
    /// <summary>
    /// Runs the saved concept river-source policy without roads, bridges, or
    /// full macro planning. Intended for deterministic batch-mode evidence.
    /// </summary>
    public static class ConceptRiverSourceAudit
    {
        private const string DefinitionPath =
            "Assets/_Game/Settings/World/ConceptWorld_v001/" +
            "ConceptWorldDefinition.asset";

        private const string OutputPath =
            "docs/reports/" +
            "concept-river-source-pass-2026-10-09.json";

        [Serializable]
        private sealed class RiverRecord
        {
            public string stableId;
            public int pointCount;
            public float sourceX;
            public float sourceZ;
            public float terminalX;
            public float terminalZ;
            public float sourceHeight;
            public float terminalHeight;
            public float netDrop;
        }

        [Serializable]
        private sealed class Record
        {
            public int seed;
            public float playableSizeMeters = 1024f;
            public float planningHalo;
            public float planningBoundsSizeMeters;

            public float sourceSpacing;
            public float sourceChance;
            public int sourceTerrainMask;
            public float minSourceHeight;
            public float maxSourceSlope;

            public bool useNaturalSourceFallback;
            public float fallbackSourceSpacing;
            public int fallbackMaximumAttempts;
            public int fallbackSourceTerrainMask;
            public float fallbackMinSourceHeight;
            public float fallbackMinimumNetDrop;

            public RiverPlannerDiagnostics diagnostics;
            public int actualRivers;
            public string result;
            public long riverBuildMilliseconds;
            public List<RiverRecord> rivers =
                new List<RiverRecord>();
        }

        [Serializable]
        private sealed class Results
        {
            public string utc;
            public string profile;
            public string output;
            public string scope =
                "Saved concept river settings only; no settlements, roads, " +
                "bridges, carving, rendering, or full macro planner.";
            public string stableIdPolicy =
                "Ordinary IDs retain river_source. Fallback IDs use the " +
                "versioned river_source_fallback_v1 salt and source-grid " +
                "coordinates.";
            public List<Record> records =
                new List<Record>();
        }

        [MenuItem(
            "Little Castle/Diagnostics/" +
            "Run Concept River Source Audit")]
        public static void Run()
        {
            WorldDefinition definition =
                AssetDatabase.LoadAssetAtPath<WorldDefinition>(
                    DefinitionPath);

            if (definition == null ||
                definition.GenerationSettings == null ||
                definition.MacroPlannerSettings == null ||
                definition.MacroPlannerSettings.Rivers == null)
            {
                throw new InvalidOperationException(
                    "Saved concept world definition is incomplete.");
            }

            MacroWorldPlannerSettings macro =
                definition.MacroPlannerSettings;

            RiverPlannerSettings settings =
                macro.Rivers;

            var results =
                new Results
                {
                    utc = DateTime.UtcNow.ToString("o"),
                    profile = DefinitionPath,
                    output = OutputPath
                };

            int[] seeds =
            {
                12345,
                54321,
                -10101,
                777
            };

            for (int i = 0; i < seeds.Length; i++)
            {
                int seed = seeds[i];
                float half = 512f + macro.PlanningHalo;

                var bounds =
                    new Rect(
                        -half,
                        -half,
                        half * 2f,
                        half * 2f);

                var probe =
                    new WorldTerrainProbe(
                        new WorldGenerationPipeline(
                            definition.GenerationSettings),
                        definition.GenerationSettings,
                        seed);

                var plan =
                    new MacroWorldPlan(seed);

                var diagnostics =
                    new RiverPlannerDiagnostics();

                var record =
                    new Record
                    {
                        seed = seed,
                        planningHalo = macro.PlanningHalo,
                        planningBoundsSizeMeters = half * 2f,
                        sourceSpacing = settings.sourceSpacing,
                        sourceChance = settings.sourceChance,
                        sourceTerrainMask =
                            (int)settings.sourceTerrain,
                        minSourceHeight =
                            settings.minSourceHeight,
                        maxSourceSlope =
                            settings.maxSourceSlope,
                        useNaturalSourceFallback =
                            settings.useNaturalSourceFallback,
                        fallbackSourceSpacing =
                            settings.fallbackSourceSpacing,
                        fallbackMaximumAttempts =
                            settings.fallbackMaximumAttempts,
                        fallbackSourceTerrainMask =
                            (int)settings.fallbackSourceTerrain,
                        fallbackMinSourceHeight =
                            settings.fallbackMinSourceHeight,
                        fallbackMinimumNetDrop =
                            settings.fallbackMinimumNetDrop,
                        diagnostics = diagnostics
                    };

                var stopwatch = Stopwatch.StartNew();

                RiverNetworkPlanner.BuildRivers(
                    seed,
                    bounds,
                    probe,
                    settings,
                    plan,
                    diagnostics);

                stopwatch.Stop();
                record.riverBuildMilliseconds =
                    stopwatch.ElapsedMilliseconds;
                record.actualRivers = plan.Rivers.Count;
                record.result =
                    ResolveResult(
                        diagnostics,
                        plan.Rivers.Count);

                for (int riverIndex = 0;
                     riverIndex < plan.Rivers.Count;
                     riverIndex++)
                {
                    WorldRiverData river =
                        plan.Rivers[riverIndex];

                    if (river == null ||
                        river.centerline == null ||
                        river.centerline.Count == 0)
                    {
                        continue;
                    }

                    Vector2 source = river.centerline[0];
                    Vector2 terminal =
                        river.centerline[
                            river.centerline.Count - 1];

                    float sourceHeight =
                        probe.Sample(source).height;

                    float terminalHeight =
                        probe.Sample(terminal).height;

                    record.rivers.Add(
                        new RiverRecord
                        {
                            stableId =
                                river.stableId.ToString(
                                    CultureInfo.InvariantCulture),
                            pointCount = river.centerline.Count,
                            sourceX = source.x,
                            sourceZ = source.y,
                            terminalX = terminal.x,
                            terminalZ = terminal.y,
                            sourceHeight = sourceHeight,
                            terminalHeight = terminalHeight,
                            netDrop = sourceHeight - terminalHeight
                        });
                }

                probe.Clear();
                results.records.Add(record);

                UnityEngine.Debug.Log(
                    "CONCEPT_RIVER_SOURCE " +
                    $"seed={seed} result={record.result} " +
                    $"ordinary={diagnostics.ordinaryAcceptedRivers} " +
                    $"fallback={diagnostics.fallbackAcceptedRivers} " +
                    $"evaluated={diagnostics.fallbackCandidatesEvaluated}/" +
                    $"{diagnostics.fallbackAttemptLimit} " +
                    $"rivers={record.actualRivers} " +
                    $"ms={record.riverBuildMilliseconds}");
            }

            string absoluteOutput =
                Path.GetFullPath(OutputPath);

            Directory.CreateDirectory(
                Path.GetDirectoryName(
                    absoluteOutput));

            File.WriteAllText(
                absoluteOutput,
                JsonUtility.ToJson(results, true) +
                "\n");

            UnityEngine.Debug.Log(
                "CONCEPT_RIVER_SOURCE_AUDIT " +
                absoluteOutput);
        }

        private static string ResolveResult(
            RiverPlannerDiagnostics diagnostics,
            int riverCount)
        {
            if (diagnostics.ordinaryAcceptedRivers > 0)
                return "ordinary-river";

            if (diagnostics.fallbackAcceptedRivers > 0)
                return "fallback-downhill-river";

            return
                riverCount > 0
                    ? "preexisting-river"
                    : "explicit-zero-no-valid-route";
        }
    }
}
