using System;
using System.IO;
using System.Text;
using LittleCastle.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;

namespace LittleCastle.Editor
{
    /// <summary>
    /// PlayMode metrics capture; does NOT claim a nographics GPU benchmark.
    /// Enable before entering Play Mode. Samples existing WorldStreamer public
    /// counters only; never mutates game state or profile budgets.
    /// </summary>
    [InitializeOnLoad]
    public static class ConceptWorldPerformanceAudit
    {
        private const string EnabledKey = "LittleCastle.ConceptPerf.Enabled";
        private const string FileKey = "LittleCastle.ConceptPerf.File";
        private const string CsvHeader =
            "utc,editorSeconds,playSeconds,frameDeltaMs,streamerFound," +
            "seed,chunkMeters,active,desired,missing,pendingLoads," +
            "pendingGeneration,urgentGeneration,backgroundGeneration," +
            "activeColliders,cachedChunks,totalLoads,totalUnloads," +
            "lastLoadMs,lastUnloadMs,lastMeshMs,lastSpawnMs,lastStageMs," +
            "worstStageMs,visibleReady,macroRoads,macroRivers,macroBridges," +
            "managedBytes,totalAllocatedMemoryBytes";

        private static readonly StringBuilder Buffered = new StringBuilder();
        private static double nextSample;

        static ConceptWorldPerformanceAudit()
        {
            EditorApplication.update -= Sample;
            EditorApplication.update += Sample;
            EditorApplication.playModeStateChanged -= OnPlayModeState;
            EditorApplication.playModeStateChanged += OnPlayModeState;
        }

        [MenuItem("Little Castle/World/Concept Performance/Start CSV Capture")]
        public static void StartCapture()
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string logs = Path.Combine(root, "Logs");
            Directory.CreateDirectory(logs);
            string path = Path.Combine(logs,
                "GPT6-ConceptPerf-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + ".csv");
            File.WriteAllText(path, CsvHeader + "\n", Encoding.UTF8);
            SessionState.SetString(FileKey, path);
            SessionState.SetBool(EnabledKey, true);
            Buffered.Length = 0;
            nextSample = 0;
            Debug.Log("[Concept Performance] Enabled. Capture during PlayMode: " + path +
                ". Output is NOT GPU timing or validated FPS.");
        }

        [MenuItem("Little Castle/World/Concept Performance/Stop CSV Capture")]
        public static void StopCapture()
        {
            Flush();
            SessionState.SetBool(EnabledKey, false);
            Debug.Log("[Concept Performance] Stopped: " + SessionState.GetString(FileKey, ""));
        }

        [MenuItem("Little Castle/World/Concept Performance/Validate Concept Units")]
        public static void ValidateConceptUnits()
        {
            var definition = AssetDatabase.LoadAssetAtPath<WorldDefinition>(
                "Assets/_Game/Settings/World/ConceptWorld_v001/ConceptWorldDefinition.asset");
            if (definition == null || definition.GenerationSettings == null ||
                definition.StreamingSettings == null)
                throw new InvalidOperationException("Concept WorldDefinition/profile unavailable.");

            WorldGenerationSettings world = definition.GenerationSettings;
            WorldStreamingSettings stream = definition.StreamingSettings;
            float size = world.ChunkWorldSize;
            float cell = world.CellWorldSize;
            float visibleRadius = stream.LoadRadiusChunks * size;
            float unloadRadius = stream.UnloadRadiusChunks * size;
            float prefetchRadius = stream.PrefetchRadiusChunks * size;
            string report =
                "[Concept Performance] profile: " + size + " m/chunk, " +
                world.CellsPerSide + " cells/chunk, " + cell + " m/cell; " +
                "loadRadius=" + visibleRadius + "m, unloadRadius=" +
                unloadRadius + "m, prefetchRadius=" + prefetchRadius +
                "m, cacheLimit=" + stream.MaxCachedChunks +
                ", colliderRadius=" + (stream.ColliderRadiusChunks * size) +
                "m, load/frame=" + stream.MaxChunkLoadsPerFrame +
                ", generationStages/frame=" + stream.UrgentGenerationStagesPerFrame;
            Debug.Log(report + ". These are configured budgets, NOT measured performance.");
        }

        private static void OnPlayModeState(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.ExitingPlayMode)
                Flush();
            nextSample = 0;
        }

        private static void Sample()
        {
            if (!SessionState.GetBool(EnabledKey, false) ||
                !EditorApplication.isPlaying)
                return;

            double editorTime = EditorApplication.timeSinceStartup;
            if (editorTime < nextSample)
                return;
            nextSample = editorTime + 0.5;

            WorldStreamer streamer = UnityEngine.Object.FindAnyObjectByType<WorldStreamer>();
            string noStreamer = string.Join(",", new string[24]);
            string rest;
            if (streamer == null)
            {
                rest = noStreamer;
            }
            else
            {
                MacroWorldPlan macro = streamer.MacroPlan;
                float chunk = streamer.Definition != null &&
                    streamer.Definition.GenerationSettings != null
                    ? streamer.Definition.GenerationSettings.ChunkWorldSize : 0f;
                rest = string.Join(",", new[]
                {
                    "1",
                    streamer.WorldSeed.ToString(),
                    chunk.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
                    streamer.ActiveChunkCount.ToString(),
                    streamer.DesiredChunkCount.ToString(),
                    streamer.MissingDesiredChunkCount.ToString(),
                    streamer.PendingLoadCount.ToString(),
                    streamer.PendingGenerationCount.ToString(),
                    streamer.PendingUrgentGenerationCount.ToString(),
                    streamer.PendingBackgroundGenerationCount.ToString(),
                    streamer.ActiveTerrainColliderCount.ToString(),
                    streamer.CachedChunkCount.ToString(),
                    streamer.TotalChunkLoads.ToString(),
                    streamer.TotalChunkUnloads.ToString(),
                    Num(streamer.LastChunkLoadMilliseconds),
                    Num(streamer.LastChunkUnloadMilliseconds),
                    Num(streamer.LastMeshBuildMilliseconds),
                    Num(streamer.LastSpawnPresentationMilliseconds),
                    Num(streamer.LastGenerationStageMilliseconds),
                    Num(streamer.WorstGenerationStageMilliseconds),
                    streamer.IsVisibleAreaReady ? "1" : "0",
                    (macro != null ? macro.Roads.Count : 0).ToString(),
                    (macro != null ? macro.Rivers.Count : 0).ToString(),
                    (macro != null ? macro.BridgeSites.Count : 0).ToString()
                });
            }

            Buffered.Append(DateTime.UtcNow.ToString("o")).Append(',')
                .Append(Num(editorTime)).Append(',')
                .Append(Num(Time.realtimeSinceStartup)).Append(',')
                .Append(Num(Time.unscaledDeltaTime * 1000f)).Append(',')
                .Append(rest).Append(',')
                .Append(GC.GetTotalMemory(false)).Append(',')
                .Append(Profiler.GetTotalAllocatedMemoryLong()).Append('\n');

            if (Buffered.Length > 8192)
                Flush();
        }

        private static string Num(double value)
        {
            return value.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
        }

        private static void Flush()
        {
            if (Buffered.Length == 0)
                return;
            string output = SessionState.GetString(FileKey, "");
            if (!string.IsNullOrEmpty(output))
            {
                File.AppendAllText(output, Buffered.ToString(), Encoding.UTF8);
                Buffered.Length = 0;
            }
        }
    }
}
