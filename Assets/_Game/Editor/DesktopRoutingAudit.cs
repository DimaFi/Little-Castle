using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using LittleCastle.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace LittleCastle.Editor
{
    /// <summary>
    /// One real, data-only concept macro plan per executeMethod invocation.
    /// No profile asset, scene, or detailed chunk presentation is changed.
    /// </summary>
    public static class DesktopRoutingAudit
    {
        private const string DefinitionPath =
            "Assets/_Game/Settings/World/ConceptWorld_v001/ConceptWorldDefinition.asset";

        [Serializable]
        private sealed class AuditRecord
        {
            public string utc;
            public string status;
            public string profile;
            public int seed;
            public int sizeMeters;
            public float chunkWorldSize;
            public int cellsPerSide;
            public float planningHalo;
            public int minimumFixedBridgeCount;
            public int maximumRouteAttempts;
            public int maximumGuidedAttempts;
            public bool connectedGraphRequired;
            public bool playerCountNotInMacroPlanner = true;
            public long elapsedMilliseconds;
            public long managedBytesBefore;
            public long managedBytesAfter;
            public long observedPeakManagedBytes;
            public long managedAllocatedBytesOnMainThread;
            public long profilerAllocatedBytesBefore;
            public long profilerAllocatedBytesAfter;
            public long profilerAllocatedBytesDelta;
            public int features;
            public int logicalConnections;
            public int roads;
            public int rivers;
            public int bridges;
            public int fixedBridges;
            public int components;
            public int missingRoads;
            public int orphanBridges;
            public int unbridgedCrossings;
            public bool routeSatisfied;
            public List<string> errors = new List<string>();
        }

        private sealed class ManagedPeakSampler : IDisposable
        {
            private readonly Thread thread;
            private volatile bool running = true;
            private long peak;

            public long Peak => Interlocked.Read(ref peak);

            public ManagedPeakSampler(long initial)
            {
                peak = initial;
                thread = new Thread(Sample)
                {
                    IsBackground = true,
                    Name = "DesktopRoutingAuditMemory"
                };
                thread.Start();
            }

            private void Sample()
            {
                while (running)
                {
                    long observed = GC.GetTotalMemory(false);
                    long current;
                    do
                    {
                        current = Interlocked.Read(ref peak);
                        if (observed <= current)
                            break;
                    }
                    while (Interlocked.CompareExchange(ref peak, observed, current) != current);
                    Thread.Sleep(25);
                }
            }

            public void Dispose()
            {
                running = false;
                thread.Join();
            }
        }

        [MenuItem("Little Castle/World/Concept Performance/Run Desktop Routing Audit")]
        public static void Run()
        {
            int seed = ReadInteger("LC_AUDIT_SEED", 12345);
            int size = ReadInteger("LC_AUDIT_SIZE", 1024);
            if (size < 64 || size > 6144)
                throw new ArgumentOutOfRangeException("LC_AUDIT_SIZE",
                    "Expected map width in meters between 64 and 6144.");

            var record = new AuditRecord
            {
                utc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                status = "failed",
                profile = DefinitionPath,
                seed = seed,
                sizeMeters = size,
                minimumFixedBridgeCount = 1,
                maximumRouteAttempts = 2,
                maximumGuidedAttempts = 2,
                connectedGraphRequired = true
            };

            MacroWorldPlannerSettings temporaryMacro = null;
            try
            {
                WorldDefinition definition = AssetDatabase.LoadAssetAtPath<WorldDefinition>(
                    DefinitionPath);
                if (definition == null || definition.GenerationSettings == null ||
                    definition.MacroPlannerSettings == null)
                    throw new InvalidOperationException("Concept profile is missing.");

                WorldGenerationSettings generation = definition.GenerationSettings;
                temporaryMacro = Object.Instantiate(definition.MacroPlannerSettings);
                temporaryMacro.hideFlags = HideFlags.HideAndDontSave;
                BridgePlannerSettings bridges = temporaryMacro.Bridges;
                bridges.enabled = true;
                bridges.useFixedStoneBridgeSites = true;
                bridges.enableBridgeAwareRouting = true;
                bridges.maxBridgeRoutingAttempts = 2;
                bridges.maxGuidedCrossingAttempts = 2;
                bridges.minimumFixedBridgeCount = 1;
                bridges.requireConnectedFeatureGraph = true;
                bridges.failOnRoutingError = false;

                record.chunkWorldSize = generation.ChunkWorldSize;
                record.cellsPerSide = generation.CellsPerSide;
                record.planningHalo = temporaryMacro.PlanningHalo;

                var pipeline = new WorldGenerationPipeline(generation, null);
                var probe = new WorldTerrainProbe(pipeline, generation, seed);
                var planner = new MacroWorldPlanner(temporaryMacro);
                var bounds = new Rect(-size * 0.5f, -size * 0.5f, size, size);

                record.managedBytesBefore = GC.GetTotalMemory(false);
                record.profilerAllocatedBytesBefore = Profiler.GetTotalAllocatedMemoryLong();
                long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
                var watch = Stopwatch.StartNew();
                MacroWorldPlan plan;
                using (var sampler = new ManagedPeakSampler(record.managedBytesBefore))
                {
                    try
                    {
                        plan = planner.GenerateForBounds(seed, bounds, probe);
                    }
                    finally
                    {
                        watch.Stop();
                        record.elapsedMilliseconds = watch.ElapsedMilliseconds;
                        record.managedBytesAfter = GC.GetTotalMemory(false);
                        record.observedPeakManagedBytes = Math.Max(
                            sampler.Peak, record.managedBytesAfter);
                        record.profilerAllocatedBytesAfter =
                            Profiler.GetTotalAllocatedMemoryLong();
                        record.managedAllocatedBytesOnMainThread =
                            GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
                    }
                }

                record.profilerAllocatedBytesDelta =
                    record.profilerAllocatedBytesAfter -
                    record.profilerAllocatedBytesBefore;
                record.features = plan.PointFeatures.Count;
                record.logicalConnections = plan.RoadConnections.Count;
                record.roads = plan.Roads.Count;
                record.rivers = plan.Rivers.Count;
                record.bridges = plan.BridgeSites.Count;
                record.routeSatisfied = plan.BridgeAwareRoutingSatisfied;

                WorldRouteConnectivityValidator.Report diagnostics = plan.RouteDiagnostics;
                if (diagnostics == null)
                    throw new InvalidOperationException("Bridge-aware route diagnostics are absent.");

                record.fixedBridges = diagnostics.fixedBridgeCount;
                record.components = diagnostics.components;
                record.missingRoads = diagnostics.missingRealizedConnections;
                record.orphanBridges = diagnostics.orphanBridgeSites;
                record.unbridgedCrossings = diagnostics.unbridgedCrossings;
                record.errors.AddRange(diagnostics.errors);
                record.status = diagnostics.IsValid ? "valid" : "invalid";
            }
            catch (Exception exception)
            {
                record.errors.Add(exception.ToString());
                Debug.LogWarning("[Desktop Routing Audit] " + exception);
            }
            finally
            {
                if (temporaryMacro != null)
                    Object.DestroyImmediate(temporaryMacro);
                WriteRecord(record);
            }
        }

        public static void RunDesktopMatrix()
        {
            string oldSeed = Environment.GetEnvironmentVariable("LC_AUDIT_SEED");
            string oldSize = Environment.GetEnvironmentVariable("LC_AUDIT_SIZE");
            try
            {
                Environment.SetEnvironmentVariable("LC_AUDIT_SIZE", "1024");
                foreach (int seed in new[] { 12345, 54321, -10101 })
                {
                    Environment.SetEnvironmentVariable("LC_AUDIT_SEED", seed.ToString(CultureInfo.InvariantCulture));
                    Run();
                }
                Environment.SetEnvironmentVariable("LC_AUDIT_SIZE", "3072");
                Environment.SetEnvironmentVariable("LC_AUDIT_SEED", "12345");
                Run();
            }
            finally
            {
                Environment.SetEnvironmentVariable("LC_AUDIT_SEED", oldSeed);
                Environment.SetEnvironmentVariable("LC_AUDIT_SIZE", oldSize);
            }
        }

        private static int ReadInteger(string name, int fallback)
        {
            string value = Environment.GetEnvironmentVariable(name);
            if (string.IsNullOrWhiteSpace(value))
                return fallback;
            if (int.TryParse(value, NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out int parsed))
                return parsed;
            throw new FormatException(name + " must be an integer: " + value);
        }

        private static void WriteRecord(AuditRecord record)
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string logs = Path.Combine(root, "Logs");
            Directory.CreateDirectory(logs);
            string output = Path.Combine(logs,
                "Desktop-Routing-" + record.seed + "-" + record.sizeMeters + ".json");
            File.WriteAllText(output, JsonUtility.ToJson(record, true) + "\n", Encoding.UTF8);
            Debug.Log("[Desktop Routing Audit] " + record.status +
                " seed=" + record.seed + " size=" + record.sizeMeters +
                " elapsedMs=" + record.elapsedMilliseconds +
                " roads=" + record.roads + " rivers=" + record.rivers +
                " fixedBridges=" + record.fixedBridges +
                " components=" + record.components +
                " issues=" + record.errors.Count + " output=" + output);
        }
    }
}
