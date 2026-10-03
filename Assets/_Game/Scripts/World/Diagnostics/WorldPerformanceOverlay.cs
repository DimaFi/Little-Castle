using UnityEngine;

namespace LittleCastle.World
{
    /// <summary>
    /// Lightweight development overlay for world streaming diagnostics.
    ///
    /// It intentionally reads existing WorldStreamer counters and does not scan
    /// the whole scene every frame.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public sealed class WorldPerformanceOverlay : MonoBehaviour
    {
        [SerializeField]
        private WorldStreamer worldStreamer;

        [SerializeField]
        private LittleCastle.Rendering.NightLightBudgetManager
            nightLightBudgetManager;

        [SerializeField]
        private LittleCastle.Rendering.VisibilityBudgetManager
            visibilityBudgetManager;

        [SerializeField]
        private bool showOverlay = true;

        [SerializeField]
        private KeyCode toggleKey = KeyCode.F8;

        [SerializeField]
        [Min(0.05f)]
        private float sampleInterval = 0.25f;

        [SerializeField]
        private Rect screenRect =
            new Rect(
                12f,
                12f,
                380f,
                530f);

        private float nextSampleTime;
        private float frameTimeAccumulator;
        private int frameSampleCount;
        private float averageFrameMilliseconds;
        private float framesPerSecond;

        private GUIStyle labelStyle;
        private GUIStyle boxStyle;

        private void Awake()
        {
            ResolveReferences();
        }

        private void Update()
        {
            if (Input.GetKeyDown(toggleKey))
                showOverlay = !showOverlay;

            frameTimeAccumulator +=
                Time.unscaledDeltaTime;

            frameSampleCount++;

            if (Time.unscaledTime <
                nextSampleTime)
            {
                return;
            }

            nextSampleTime =
                Time.unscaledTime +
                sampleInterval;

            float averageDelta =
                frameTimeAccumulator /
                Mathf.Max(
                    1,
                    frameSampleCount);

            frameTimeAccumulator = 0f;
            frameSampleCount = 0;

            float measuredMilliseconds =
                Mathf.Max(
                    0.00001f,
                    averageDelta) *
                1000f;

            averageFrameMilliseconds =
                Mathf.Lerp(
                    averageFrameMilliseconds <= 0f
                        ? measuredMilliseconds
                        : averageFrameMilliseconds,
                    measuredMilliseconds,
                    0.45f);

            framesPerSecond =
                1000f /
                Mathf.Max(
                    0.01f,
                    averageFrameMilliseconds);

            ResolveReferences();
        }

        private void OnGUI()
        {
            if (!showOverlay ||
                worldStreamer == null)
            {
                return;
            }

            EnsureStyles();

            GUI.Box(
                screenRect,
                GUIContent.none,
                boxStyle);

            GUILayout.BeginArea(
                new Rect(
                    screenRect.x + 10f,
                    screenRect.y + 8f,
                    screenRect.width - 20f,
                    screenRect.height - 16f));

            GUILayout.Label(
                "LITTLE CASTLE — WORLD STREAMING",
                labelStyle);

            GUILayout.Label(
                "FPS: " +
                framesPerSecond.ToString("0.0") +
                "   Frame: " +
                averageFrameMilliseconds.ToString("0.00") +
                " ms",
                labelStyle);

            GUILayout.Space(4f);

            GUILayout.Label(
                "Resident chunks: " +
                worldStreamer.ActiveChunkCount +
                "   Visible target: " +
                worldStreamer.DesiredChunkCount,
                labelStyle);

            GUILayout.Label(
                "Visible missing: " +
                worldStreamer.MissingDesiredChunkCount +
                "   Ready: " +
                (worldStreamer.VisibleReadiness01 * 100f).ToString("0") +
                "%   Presentation queue: " +
                worldStreamer.PendingLoadCount,
                labelStyle);

            GUILayout.Label(
                "Cached data: " +
                worldStreamer.CachedChunkCount +
                "   Generation: " +
                worldStreamer.PendingGenerationCount +
                " (urgent " +
                worldStreamer.PendingUrgentGenerationCount +
                " / bg " +
                worldStreamer.PendingBackgroundGenerationCount +
                ")",
                labelStyle);

            GUILayout.Label(
                "Terrain colliders active: " +
                worldStreamer.ActiveTerrainColliderCount +
                "   Object colliders: " +
                worldStreamer.ActiveGeneratedObjectColliderCount,
                labelStyle);

            GUILayout.Space(4f);

            GUILayout.Label(
                "Last gen stage: " +
                (string.IsNullOrEmpty(
                    worldStreamer.LastGenerationStageName)
                    ? "-"
                    : worldStreamer.LastGenerationStageName) +
                "  " +
                worldStreamer.LastGenerationStageMilliseconds.ToString("0.00") +
                " ms",
                labelStyle);

            GUILayout.Label(
                "Worst gen stage: " +
                (string.IsNullOrEmpty(
                    worldStreamer.WorstGenerationStageName)
                    ? "-"
                    : worldStreamer.WorstGenerationStageName) +
                "  " +
                worldStreamer.WorstGenerationStageMilliseconds.ToString("0.00") +
                " ms",
                labelStyle);

            GUILayout.Label(
                "Prefetched chunks completed: " +
                worldStreamer.CompletedPrefetchChunks,
                labelStyle);

            GUILayout.Space(4f);

            GUILayout.Label(
                "Last chunk presentation: " +
                worldStreamer.LastChunkLoadMilliseconds.ToString("0.00") +
                " ms",
                labelStyle);

            GUILayout.Label(
                "  Mesh build: " +
                worldStreamer.LastMeshBuildMilliseconds.ToString("0.00") +
                " ms",
                labelStyle);

            GUILayout.Label(
                "  Spawn presentation: " +
                worldStreamer.LastSpawnPresentationMilliseconds.ToString("0.00") +
                " ms (" +
                worldStreamer.LastChunkSpawnCount +
                " objects)",
                labelStyle);

            GUILayout.Label(
                "Last unload: " +
                worldStreamer.LastChunkUnloadMilliseconds.ToString("0.00") +
                " ms",
                labelStyle);

            GUILayout.Label(
                "Total loads/unloads: " +
                worldStreamer.TotalChunkLoads +
                " / " +
                worldStreamer.TotalChunkUnloads,
                labelStyle);

            if (nightLightBudgetManager != null)
            {
                GUILayout.Space(4f);

                GUILayout.Label(
                    "Night lights: " +
                    nightLightBudgetManager.ActiveRealtimeLightCount +
                    " realtime / " +
                    nightLightBudgetManager.RegisteredEmitterCount +
                    " registered   pools " +
                    nightLightBudgetManager.VisiblePoolCount,
                    labelStyle);

                GUILayout.Label(
                    "  Candidates: " +
                    nightLightBudgetManager.CandidateCount +
                    "   fading: " +
                    nightLightBudgetManager.FadingRealtimeLightCount,
                    labelStyle);
            }

            if (visibilityBudgetManager != null)
            {
                GUILayout.Space(4f);

                GUILayout.Label(
                    "Visibility: " +
                    visibilityBudgetManager.RegisteredTargetCount +
                    " targets   camera " +
                    visibilityBudgetManager.ApproximateCameraVisibleCount +
                    "   culled-visible " +
                    visibilityBudgetManager.CullingVisibleCount,
                    labelStyle);

                GUILayout.Label(
                    "  T0 " +
                    visibilityBudgetManager.FullCount +
                    "   T1 " +
                    visibilityBudgetManager.BalancedCount +
                    "   T2 " +
                    visibilityBudgetManager.FarCount +
                    "   T3 " +
                    visibilityBudgetManager.HiddenCount,
                    labelStyle);
            }

            LittleCastle.Rendering.HlodRebuildScheduler
                hlodScheduler =
                    LittleCastle.Rendering.HlodRebuildScheduler.Instance;

            if (hlodScheduler != null)
            {
                GUILayout.Space(4f);

                GUILayout.Label(
                    "HLOD: " +
                    hlodScheduler.PendingCount +
                    " pending   rebuilds " +
                    hlodScheduler.TotalRebuildCount +
                    "   last " +
                    hlodScheduler.LastRebuildMilliseconds.ToString("0.00") +
                    " ms",
                    labelStyle);
            }

            GUILayout.Space(5f);

            GUILayout.Label(
                "F8 — hide/show diagnostics",
                labelStyle);

            GUILayout.EndArea();
        }

        private void ResolveReferences()
        {
            if (worldStreamer == null)
            {
                worldStreamer =
                    FindFirstObjectByType<
                        WorldStreamer>();
            }

            if (nightLightBudgetManager == null)
            {
                nightLightBudgetManager =
                    FindFirstObjectByType<
                        LittleCastle.Rendering.NightLightBudgetManager>();
            }

            if (visibilityBudgetManager == null)
            {
                visibilityBudgetManager =
                    FindFirstObjectByType<
                        LittleCastle.Rendering.VisibilityBudgetManager>();
            }
        }

        private void EnsureStyles()
        {
            if (labelStyle == null)
            {
                labelStyle =
                    new GUIStyle(
                        GUI.skin.label);

                labelStyle.fontSize = 13;
                labelStyle.normal.textColor =
                    Color.white;
            }

            if (boxStyle == null)
            {
                boxStyle =
                    new GUIStyle(
                        GUI.skin.box);
            }
        }

        private void OnValidate()
        {
            sampleInterval =
                Mathf.Max(
                    0.05f,
                    sampleInterval);
        }
    }
}
