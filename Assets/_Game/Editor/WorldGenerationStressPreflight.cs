using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using LittleCastle.World;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace LittleCastle.Editor
{
    public sealed class WorldGenerationStressReport
    {
        private readonly List<string> errors =
            new List<string>();

        private readonly List<string> warnings =
            new List<string>();

        private readonly List<string> info =
            new List<string>();

        public IReadOnlyList<string> Errors => errors;
        public IReadOnlyList<string> Warnings => warnings;
        public IReadOnlyList<string> Info => info;
        public bool IsValid => errors.Count == 0;

        public void AddError(string message)
        {
            if (!string.IsNullOrWhiteSpace(message))
                errors.Add(message);
        }

        public void AddWarning(string message)
        {
            if (!string.IsNullOrWhiteSpace(message))
                warnings.Add(message);
        }

        public void AddInfo(string message)
        {
            if (!string.IsNullOrWhiteSpace(message))
                info.Add(message);
        }

        public string ToMultilineString()
        {
            var builder =
                new StringBuilder();

            builder.AppendLine(
                IsValid
                    ? "World generation stress preflight passed."
                    : "World generation stress preflight failed.");

            builder.AppendLine(
                "Errors: " +
                errors.Count +
                ", warnings: " +
                warnings.Count +
                ", info: " +
                info.Count);

            Append(
                builder,
                "ERROR",
                errors);

            Append(
                builder,
                "WARN",
                warnings);

            Append(
                builder,
                "INFO",
                info);

            return builder.ToString();
        }

        private static void Append(
            StringBuilder builder,
            string label,
            IReadOnlyList<string> messages)
        {
            for (int i = 0;
                 i < messages.Count;
                 i++)
            {
                builder.Append("[");
                builder.Append(label);
                builder.Append("] ");
                builder.AppendLine(
                    messages[i]);
            }
        }
    }

    /// <summary>
    /// Data-only stress verification for the production generation contract.
    ///
    /// No scene GameObjects are created. The audit deliberately exercises
    /// multiple seeds and chunks, then reports stage timings so optimization can
    /// target measured generation work rather than guesses.
    /// </summary>
    public static class WorldGenerationStressPreflight
    {
        private const string DefinitionPath =
            "Assets/_Game/Settings/World/MainWorldDefinition.asset";

        private const int ChunkRadius = 2;
        private const float PositionEpsilon = 0.01f;

        private static readonly int[] Seeds =
        {
            12345,
            54321,
            -10101,
            777,
            20261003
        };

        [MenuItem(
            "Little Castle/World/Run Generation Stress Preflight")]
        public static void RunFromMenu()
        {
            WorldGenerationStressReport report =
                Run();

            if (report.IsValid)
            {
                if (report.Warnings.Count > 0)
                    Debug.LogWarning(report.ToMultilineString());
                else
                    Debug.Log(report.ToMultilineString());
            }
            else
            {
                Debug.LogError(
                    report.ToMultilineString());
            }
        }

        public static WorldGenerationStressReport Run()
        {
            var report =
                new WorldGenerationStressReport();

            WorldDefinition definition =
                AssetDatabase.LoadAssetAtPath<
                    WorldDefinition>(
                    DefinitionPath);

            if (definition == null)
            {
                report.AddError(
                    "MainWorldDefinition is missing: " +
                    DefinitionPath);

                return report;
            }

            WorldConfigurationValidationReport
                configuration =
                    WorldGenerationConfigurationValidator.Validate(
                        definition);

            if (!configuration.IsValid)
            {
                report.AddError(
                    configuration.ToMultilineString());

                return report;
            }

            WorldGenerationSettings generation =
                definition.GenerationSettings;

            MacroWorldPlannerSettings macroSettings =
                definition.MacroPlannerSettings;

            WorldSpawnCatalog catalog =
                definition.SpawnCatalog;

            if (generation == null)
            {
                report.AddError(
                    "WorldGenerationSettings is missing.");

                return report;
            }

            var chunkTimes =
                new List<double>();

            var stageMaximums =
                new Dictionary<string, double>(
                    StringComparer.Ordinal);

            var stageTotals =
                new Dictionary<string, double>(
                    StringComparer.Ordinal);

            var stageCounts =
                new Dictionary<string, int>(
                    StringComparer.Ordinal);

            var categoryCounts =
                new Dictionary<SpawnCategory, int>();

            var unresolvedArchetypes =
                new HashSet<string>(
                    StringComparer.Ordinal);

            int chunksGenerated = 0;
            int totalSpawns = 0;
            int maximumChunkSpawns = 0;
            int totalDeposits = 0;
            int mountainBarrierCells = 0;
            int macroPointFeatures = 0;
            int rivers = 0;
            int roads = 0;
            int bridges = 0;

            double maximumMacroMilliseconds = 0.0;

            for (int seedIndex = 0;
                 seedIndex < Seeds.Length;
                 seedIndex++)
            {
                int seed =
                    Seeds[seedIndex];

                WorldGenerationPipeline terrainOnly =
                    new WorldGenerationPipeline(
                        generation,
                        null);

                var terrainProbe =
                    new WorldTerrainProbe(
                        terrainOnly,
                        generation,
                        seed);

                float halfExtent =
                    generation.ChunkWorldSize *
                    (ChunkRadius + 2);

                Rect bounds =
                    new Rect(
                        -halfExtent,
                        -halfExtent,
                        halfExtent * 2f,
                        halfExtent * 2f);

                var macroWatch =
                    Stopwatch.StartNew();

                MacroWorldPlan plan;

                try
                {
                    var planner =
                        new MacroWorldPlanner(
                            macroSettings);

                    plan =
                        planner.GenerateForBounds(
                            seed,
                            bounds,
                            terrainProbe);
                }
                catch (Exception exception)
                {
                    report.AddError(
                        "Macro generation threw for seed " +
                        seed +
                        ": " +
                        exception);

                    terrainProbe.Clear();
                    continue;
                }

                macroWatch.Stop();

                double macroMilliseconds =
                    macroWatch.Elapsed.TotalMilliseconds;

                maximumMacroMilliseconds =
                    Math.Max(
                        maximumMacroMilliseconds,
                        macroMilliseconds);

                ValidateMacroPlan(
                    seed,
                    plan,
                    terrainProbe,
                    macroSettings,
                    report);

                macroPointFeatures +=
                    plan.PointFeatures.Count;

                rivers +=
                    plan.Rivers.Count;

                roads +=
                    plan.Roads.Count;

                bridges +=
                    plan.BridgeSites.Count;

                long firstMacroHash =
                    HashMacroPlan(
                        plan);

                MacroWorldPlan secondPlan =
                    new MacroWorldPlanner(
                        macroSettings)
                        .GenerateForBounds(
                            seed,
                            bounds,
                            terrainProbe);

                long secondMacroHash =
                    HashMacroPlan(
                        secondPlan);

                if (firstMacroHash !=
                    secondMacroHash)
                {
                    report.AddError(
                        "Macro plan is not deterministic for seed " +
                        seed +
                        ".");
                }

                var pipeline =
                    new WorldGenerationPipeline(
                        generation,
                        plan);

                var stableIds =
                    new HashSet<long>();

                for (int z = -ChunkRadius;
                     z <= ChunkRadius;
                     z++)
                {
                    for (int x = -ChunkRadius;
                         x <= ChunkRadius;
                         x++)
                    {
                        var coordinate =
                            new ChunkCoordinate(
                                x,
                                z);

                        double chunkMilliseconds;

                        WorldChunkData chunk =
                            GenerateMeasuredChunk(
                                pipeline,
                                seed,
                                coordinate,
                                stageMaximums,
                                stageTotals,
                                stageCounts,
                                out chunkMilliseconds);

                        chunkTimes.Add(
                            chunkMilliseconds);

                        chunksGenerated++;

                        ValidateChunk(
                            chunk,
                            generation,
                            catalog,
                            stableIds,
                            unresolvedArchetypes,
                            categoryCounts,
                            report,
                            ref mountainBarrierCells);

                        totalSpawns +=
                            chunk.Spawns.Count;

                        totalDeposits +=
                            chunk.ResourceDeposits.Count;

                        maximumChunkSpawns =
                            Math.Max(
                                maximumChunkSpawns,
                                chunk.Spawns.Count);
                    }
                }

                if (!WorldGenerationDiagnostics
                        .ValidateDeterminism(
                            pipeline,
                            seed,
                            new ChunkCoordinate(0, 0),
                            0.0001f,
                            out string determinismMessage))
                {
                    report.AddError(
                        "Determinism failed for seed " +
                        seed +
                        ": " +
                        determinismMessage);
                }

                if (!WorldGenerationDiagnostics
                        .ValidateSpawnDeterminism(
                            pipeline,
                            seed,
                            new ChunkCoordinate(0, 0),
                            0.0001f,
                            out string spawnMessage))
                {
                    report.AddError(
                        "Spawn determinism failed for seed " +
                        seed +
                        ": " +
                        spawnMessage);
                }

                if (!WorldGenerationDiagnostics
                        .ValidateEastWestBorder(
                            pipeline,
                            seed,
                            new ChunkCoordinate(0, 0),
                            0.0001f,
                            out string eastWestMessage))
                {
                    report.AddError(
                        "East/west seam validation failed for seed " +
                        seed +
                        ": " +
                        eastWestMessage);
                }

                if (!WorldGenerationDiagnostics
                        .ValidateNorthSouthBorder(
                            pipeline,
                            seed,
                            new ChunkCoordinate(0, 0),
                            0.0001f,
                            out string northSouthMessage))
                {
                    report.AddError(
                        "North/south seam validation failed for seed " +
                        seed +
                        ": " +
                        northSouthMessage);
                }

                terrainProbe.Clear();
            }

            chunkTimes.Sort();

            double medianChunkMilliseconds =
                chunkTimes.Count > 0
                    ? chunkTimes[
                        chunkTimes.Count / 2]
                    : 0.0;

            double maximumChunkMilliseconds =
                chunkTimes.Count > 0
                    ? chunkTimes[
                        chunkTimes.Count - 1]
                    : 0.0;

            if (totalSpawns == 0)
            {
                report.AddError(
                    "Stress region produced zero generated spawns.");
            }

            if (maximumChunkSpawns > 10000)
            {
                report.AddError(
                    "A chunk produced " +
                    maximumChunkSpawns +
                    " spawns. This is too high for ordinary GameObject " +
                    "presentation and likely indicates a broken spacing rule.");
            }
            else if (maximumChunkSpawns > 3000)
            {
                report.AddWarning(
                    "A chunk produced " +
                    maximumChunkSpawns +
                    " spawns. Verify the intended presentation path before " +
                    "using production models.");
            }

            if (mountainBarrierCells == 0)
            {
                report.AddWarning(
                    "No MountainBarrier cells appeared in the stress sample. " +
                    "This is not a correctness failure, but mountain blocking " +
                    "should be visually checked on additional seeds.");
            }

            if (maximumChunkMilliseconds > 250.0)
            {
                report.AddWarning(
                    "Slowest synchronous stress chunk took " +
                    maximumChunkMilliseconds.ToString("0.00") +
                    " ms in the Editor. Runtime streaming is cooperative, " +
                    "but inspect the per-stage timings below.");
            }

            if (maximumMacroMilliseconds > 5000.0)
            {
                report.AddWarning(
                    "Macro planning exceeded 5 seconds on at least one stress " +
                    "seed. Inspect river/road planning before large-map tests.");
            }

            foreach (
                KeyValuePair<string, double> pair
                in stageMaximums)
            {
                double average =
                    stageTotals[pair.Key] /
                    Math.Max(
                        1,
                        stageCounts[pair.Key]);

                report.AddInfo(
                    "Stage '" +
                    pair.Key +
                    "': avg=" +
                    average.ToString("0.00") +
                    " ms, max=" +
                    pair.Value.ToString("0.00") +
                    " ms.");

                if (pair.Value > 50.0)
                {
                    report.AddWarning(
                        "Generation stage '" +
                        pair.Key +
                        "' exceeded 50 ms in the Editor (" +
                        pair.Value.ToString("0.00") +
                        " ms). Cooperative stage-level scheduling cannot split " +
                        "inside this stage; profile/split it if runtime hitches remain.");
                }
            }

            if (unresolvedArchetypes.Count > 0)
            {
                report.AddError(
                    "Unresolved generated archetypes: " +
                    string.Join(
                        ", ",
                        unresolvedArchetypes));
            }

            report.AddInfo(
                "Stress seeds=" +
                Seeds.Length +
                ", chunks=" +
                chunksGenerated +
                ", chunk median=" +
                medianChunkMilliseconds.ToString("0.00") +
                " ms, chunk max=" +
                maximumChunkMilliseconds.ToString("0.00") +
                " ms, macro max=" +
                maximumMacroMilliseconds.ToString("0.00") +
                " ms.");

            report.AddInfo(
                "Spawns=" +
                totalSpawns +
                ", max/chunk=" +
                maximumChunkSpawns +
                ", deposits=" +
                totalDeposits +
                ", mountain barrier cells=" +
                mountainBarrierCells +
                ".");

            report.AddInfo(
                "Macro totals: point features=" +
                macroPointFeatures +
                ", rivers=" +
                rivers +
                ", roads=" +
                roads +
                ", bridges=" +
                bridges +
                ".");

            foreach (
                KeyValuePair<SpawnCategory, int> pair
                in categoryCounts)
            {
                report.AddInfo(
                    "Spawn category " +
                    pair.Key +
                    ": " +
                    pair.Value +
                    ".");
            }

            return report;
        }

        private static WorldChunkData GenerateMeasuredChunk(
            WorldGenerationPipeline pipeline,
            int seed,
            ChunkCoordinate coordinate,
            Dictionary<string, double> stageMaximums,
            Dictionary<string, double> stageTotals,
            Dictionary<string, int> stageCounts,
            out double totalMilliseconds)
        {
            WorldChunkGenerationWork work =
                pipeline.BeginIncrementalGeneration(
                    seed,
                    coordinate);

            var total =
                Stopwatch.StartNew();

            while (!work.IsCompleted)
            {
                var stageWatch =
                    Stopwatch.StartNew();

                bool ran =
                    work.StepNextStage();

                stageWatch.Stop();

                if (!ran)
                    continue;

                string stageName =
                    string.IsNullOrWhiteSpace(
                        work.LastStageName)
                        ? "<unnamed>"
                        : work.LastStageName;

                double milliseconds =
                    stageWatch.Elapsed.TotalMilliseconds;

                if (!stageMaximums.TryGetValue(
                        stageName,
                        out double maximum) ||
                    milliseconds > maximum)
                {
                    stageMaximums[
                        stageName] =
                        milliseconds;
                }

                if (!stageTotals.ContainsKey(
                        stageName))
                {
                    stageTotals.Add(
                        stageName,
                        0.0);

                    stageCounts.Add(
                        stageName,
                        0);
                }

                stageTotals[
                    stageName] +=
                    milliseconds;

                stageCounts[
                    stageName]++;
            }

            total.Stop();

            totalMilliseconds =
                total.Elapsed.TotalMilliseconds;

            return work.Result;
        }

        private static void ValidateChunk(
            WorldChunkData chunk,
            WorldGenerationSettings generation,
            WorldSpawnCatalog catalog,
            HashSet<long> stableIds,
            HashSet<string> unresolvedArchetypes,
            Dictionary<SpawnCategory, int> categoryCounts,
            WorldGenerationStressReport report,
            ref int mountainBarrierCells)
        {
            float chunkSize =
                generation.ChunkWorldSize;

            Vector3 origin =
                chunk.Coordinate.GetWorldOrigin(
                    chunkSize);

            float maxX =
                origin.x +
                chunkSize;

            float maxZ =
                origin.z +
                chunkSize;

            var localIds =
                new HashSet<long>();

            for (int i = 0;
                 i < chunk.Spawns.Count;
                 i++)
            {
                WorldSpawnData spawn =
                    chunk.Spawns[i];

                if (!localIds.Add(
                        spawn.stableId))
                {
                    report.AddError(
                        "Duplicate spawn stableId " +
                        spawn.stableId +
                        " inside chunk " +
                        chunk.Coordinate +
                        ".");
                }

                if (!stableIds.Add(
                        spawn.stableId))
                {
                    report.AddError(
                        "Spawn stableId " +
                        spawn.stableId +
                        " appeared in more than one stress chunk for seed.");
                }

                if (string.IsNullOrWhiteSpace(
                        spawn.archetypeId))
                {
                    report.AddError(
                        "Spawn with stableId " +
                        spawn.stableId +
                        " has empty archetypeId.");
                }
                else if (
                    catalog == null ||
                    !catalog.ContainsArchetype(
                        spawn.archetypeId))
                {
                    unresolvedArchetypes.Add(
                        spawn.archetypeId ??
                        "<empty>");
                }

                if (!IsFinite(
                        spawn.worldPosition.x) ||
                    !IsFinite(
                        spawn.worldPosition.y) ||
                    !IsFinite(
                        spawn.worldPosition.z) ||
                    !IsFinite(
                        spawn.yawDegrees) ||
                    !IsFinite(
                        spawn.uniformScale) ||
                    spawn.uniformScale <= 0f)
                {
                    report.AddError(
                        "Spawn " +
                        spawn.stableId +
                        " has invalid transform values.");
                }

                if (spawn.worldPosition.x <
                        origin.x -
                        PositionEpsilon ||
                    spawn.worldPosition.x >=
                        maxX +
                        PositionEpsilon ||
                    spawn.worldPosition.z <
                        origin.z -
                        PositionEpsilon ||
                    spawn.worldPosition.z >=
                        maxZ +
                        PositionEpsilon)
                {
                    report.AddError(
                        "Spawn " +
                        spawn.stableId +
                        " lies outside owning chunk " +
                        chunk.Coordinate +
                        ".");
                }

                if (!categoryCounts.ContainsKey(
                        spawn.category))
                {
                    categoryCounts.Add(
                        spawn.category,
                        0);
                }

                categoryCounts[
                    spawn.category]++;
            }

            for (int z = 0;
                 z < chunk.CellsPerSide;
                 z++)
            {
                for (int x = 0;
                     x < chunk.CellsPerSide;
                     x++)
                {
                    if (chunk.GetTerrainClass(
                            x,
                            z) ==
                        TerrainClass.MountainBarrier)
                    {
                        mountainBarrierCells++;
                    }
                }
            }
        }

        private static void ValidateMacroPlan(
            int seed,
            MacroWorldPlan plan,
            WorldTerrainProbe terrainProbe,
            MacroWorldPlannerSettings settings,
            WorldGenerationStressReport report)
        {
            if (plan == null)
            {
                report.AddError(
                    "Macro plan is null for seed " +
                    seed +
                    ".");

                return;
            }

            for (int i = 0;
                 i < plan.Roads.Count;
                 i++)
            {
                WorldRoadData road =
                    plan.Roads[i];

                if (road == null)
                {
                    report.AddError(
                        "Null road in macro plan for seed " +
                        seed +
                        ".");

                    continue;
                }

                for (int p = 0;
                     p < road.centerline.Count;
                     p++)
                {
                    WorldTerrainSample sample =
                        terrainProbe.Sample(
                            road.centerline[p]);

                    if (sample.terrainClass ==
                        TerrainClass.MountainBarrier)
                    {
                        report.AddError(
                            "Road " +
                            road.stableId +
                            " crosses MountainBarrier at point " +
                            p +
                            " for seed " +
                            seed +
                            ".");
                    }
                }
            }

            BridgePlannerSettings bridges =
                settings != null
                    ? settings.Bridges
                    : null;

            if (bridges != null &&
                bridges.enabled &&
                bridges.standardizeCrossings)
            {
                float expectedSpan =
                    Mathf.Max(
                        bridges.standardRiverCrossingWidth,
                        bridges.standardBridgeSpan);

                for (int i = 0;
                     i < plan.BridgeSites.Count;
                     i++)
                {
                    WorldBridgeSiteData bridge =
                        plan.BridgeSites[i];

                    if (!Mathf.Approximately(
                            bridge.requiredSpan,
                            expectedSpan))
                    {
                        report.AddError(
                            "Bridge " +
                            bridge.stableId +
                            " requiredSpan=" +
                            bridge.requiredSpan +
                            " but standardized span is " +
                            expectedSpan +
                            ".");
                    }
                }
            }
        }

        private static long HashMacroPlan(
            MacroWorldPlan plan)
        {
            unchecked
            {
                long hash =
                    1469598103934665603L;

                for (int i = 0;
                     i < plan.PointFeatures.Count;
                     i++)
                {
                    WorldPointFeatureData feature =
                        plan.PointFeatures[i];

                    hash =
                        Mix(
                            hash,
                            feature.stableId);

                    hash =
                        Mix(
                            hash,
                            BitConverter.SingleToInt32Bits(
                                feature.worldPosition.x));

                    hash =
                        Mix(
                            hash,
                            BitConverter.SingleToInt32Bits(
                                feature.worldPosition.y));
                }

                for (int i = 0;
                     i < plan.Rivers.Count;
                     i++)
                {
                    WorldRiverData river =
                        plan.Rivers[i];

                    hash =
                        Mix(
                            hash,
                            river.stableId);

                    for (int p = 0;
                         p < river.centerline.Count;
                         p++)
                    {
                        hash =
                            Mix(
                                hash,
                                BitConverter.SingleToInt32Bits(
                                    river.centerline[p].x));

                        hash =
                            Mix(
                                hash,
                                BitConverter.SingleToInt32Bits(
                                    river.centerline[p].y));
                    }
                }

                for (int i = 0;
                     i < plan.Roads.Count;
                     i++)
                {
                    hash =
                        Mix(
                            hash,
                            plan.Roads[i].stableId);
                }

                for (int i = 0;
                     i < plan.BridgeSites.Count;
                     i++)
                {
                    hash =
                        Mix(
                            hash,
                            plan.BridgeSites[i].stableId);
                }

                return hash;
            }
        }

        private static long Mix(
            long hash,
            long value)
        {
            unchecked
            {
                return
                    (hash ^ value) *
                    1099511628211L;
            }
        }

        private static bool IsFinite(float value)
        {
            return
                !float.IsNaN(value) &&
                !float.IsInfinity(value);
        }
    }
}
