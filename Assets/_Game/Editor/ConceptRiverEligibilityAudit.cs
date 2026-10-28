using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using LittleCastle.World;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace LittleCastle.Editor
{
    /// <summary>Read-only source-filter audit, not a replacement river algorithm.</summary>
    public static class ConceptRiverEligibilityAudit
    {
        [Serializable]
        private sealed class Record
        {
            public int seed;
            public string experiment;
            public float sizeMeters = 1024f;
            public float halo;
            public float sourceSpacing;
            public float sourceChance;
            public float minSourceHeight;
            public int sourceTerrainMask;
            public int gridCandidates;
            public int chanceRejected;
            public int outsideBounds;
            public int terrainRejected;
            public int heightRejected;
            public int slopeRejected;
            public int eligible;
            public int actualRivers;
            public int eligibleNotRealized;
            public long eligibilityMilliseconds;
            public long buildAfterEligibilityMilliseconds;
            public bool buildUsesEligibilityWarmedProbe = true;
        }

        [Serializable]
        private sealed class Results
        {
            public string utc;
            public string profile;
            public string note = "In-memory experiments only; saved settings unchanged; no roads/bridges or FPS acceptance.";
            public List<Record> records = new List<Record>();
        }

        public static void Run()
        {
            const string path = "Assets/_Game/Settings/World/ConceptWorld_v001/ConceptWorldDefinition.asset";
            var definition = AssetDatabase.LoadAssetAtPath<WorldDefinition>(path);
            if (definition == null || definition.GenerationSettings == null || definition.MacroPlannerSettings == null)
                throw new InvalidOperationException("Concept definition missing.");
            var result = new Results { utc = DateTime.UtcNow.ToString("o"), profile = path };
            foreach (int seed in new[] { 12345, 54321 })
            {
                foreach (string experiment in new[] { "saved", "chance1", "chance1-spacing320", "saved-allowplains" })
                {
                    var macro = Object.Instantiate(definition.MacroPlannerSettings);
                    try
                    {
                        RiverPlannerSettings settings = macro.Rivers;
                        if (experiment == "chance1" || experiment == "chance1-spacing320")
                            settings.sourceChance = 1f;
                        if (experiment == "chance1-spacing320") settings.sourceSpacing = 320f;
                        if (experiment == "saved-allowplains") settings.sourceTerrain |= TerrainClassMask.Plains;
                        float half = 512f + macro.PlanningHalo;
                        var bounds = new Rect(-half, -half, half * 2f, half * 2f);
                        var probe = new WorldTerrainProbe(new WorldGenerationPipeline(definition.GenerationSettings),
                            definition.GenerationSettings, seed);
                        var record = new Record {
                            seed = seed, experiment = experiment, halo = macro.PlanningHalo,
                            sourceSpacing = settings.sourceSpacing, sourceChance = settings.sourceChance,
                            minSourceHeight = settings.minSourceHeight, sourceTerrainMask = (int)settings.sourceTerrain };
                        var watch = Stopwatch.StartNew();
                        CountSourceFilters(seed, bounds, probe, settings, record);
                        record.eligibilityMilliseconds = watch.ElapsedMilliseconds;
                        watch.Restart();
                        var plan = new MacroWorldPlan(seed);
                        RiverNetworkPlanner.BuildRivers(seed, bounds, probe, settings, plan);
                        record.buildAfterEligibilityMilliseconds = watch.ElapsedMilliseconds;
                        record.actualRivers = plan.Rivers.Count;
                        record.eligibleNotRealized = record.eligible - record.actualRivers;
                        probe.Clear();
                        result.records.Add(record);
                        UnityEngine.Debug.Log($"RIVER_ELIGIBILITY seed={seed} experiment={experiment} " +
                            $"eligible={record.eligible} rivers={record.actualRivers} classReject={record.terrainRejected} " +
                            $"heightReject={record.heightRejected} slopeReject={record.slopeRejected}");
                    }
                    finally { Object.DestroyImmediate(macro); }
                }
            }
            string output = Path.GetFullPath("docs/reports/concept-river-eligibility-2026-10-09.json");
            File.WriteAllText(output, JsonUtility.ToJson(result, true) + "\n");
        }

        // Mirrors only the source-grid/filter prelude of RiverNetworkPlanner.
        // Trace acceptance is measured by invoking the real planner above.
        // Future source-selection changes must update this diagnostic mirror.
        private static void CountSourceFilters(int seed, Rect bounds, WorldTerrainProbe probe,
            RiverPlannerSettings settings, Record record)
        {
            float spacing = Mathf.Max(100f, settings.sourceSpacing);
            int salt = DeterministicHash.String32("river_source");
            int minX = Mathf.FloorToInt(bounds.xMin / spacing) - 1;
            int maxX = Mathf.FloorToInt(bounds.xMax / spacing) + 1;
            int minZ = Mathf.FloorToInt(bounds.yMin / spacing) - 1;
            int maxZ = Mathf.FloorToInt(bounds.yMax / spacing) + 1;
            for (int z = minZ; z <= maxZ; z++)
                for (int x = minX; x <= maxX; x++)
                {
                    record.gridCandidates++;
                    if (DeterministicHash.Hash01(seed, x, z, salt ^ 0x201) > settings.sourceChance)
                    { record.chanceRejected++; continue; }
                    var source = new Vector2(
                        (x + DeterministicHash.Hash01(seed, x, z, salt ^ 0x202)) * spacing,
                        (z + DeterministicHash.Hash01(seed, x, z, salt ^ 0x203)) * spacing);
                    if (!bounds.Contains(source)) { record.outsideBounds++; continue; }
                    WorldTerrainSample sample = probe.Sample(source);
                    if (!settings.sourceTerrain.Contains(sample.terrainClass))
                    { record.terrainRejected++; continue; }
                    if (sample.height < settings.minSourceHeight) { record.heightRejected++; continue; }
                    if (sample.slope > settings.maxSourceSlope) { record.slopeRejected++; continue; }
                    record.eligible++;
                }
        }
    }
}
