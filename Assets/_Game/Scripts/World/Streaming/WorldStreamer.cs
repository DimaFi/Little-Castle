using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;

namespace LittleCastle.World
{
    /// <summary>
    /// Budgeted runtime chunk streamer around one focus transform.
    ///
    /// Preferred production mode is a finite session map selected by the host.
    /// In that mode one MacroWorldPlan is generated for the entire playable map
    /// at session start and never changes while the match is running.
    ///
    /// Legacy radius-based macro streaming is retained as a fallback for the
    /// current Unity test setup until WorldMapRules assets are configured.
    /// </summary>
    public sealed class WorldStreamer : MonoBehaviour
    {
        private static readonly ProfilerMarker UpdateMarker =
            new ProfilerMarker("World.Streaming.Update");

        private static readonly ProfilerMarker LoadMarker =
            new ProfilerMarker("World.Streaming.LoadChunk");

        private static readonly ProfilerMarker UnloadMarker =
            new ProfilerMarker("World.Streaming.UnloadChunk");

        private static readonly ProfilerMarker MeshBuildMarker =
            new ProfilerMarker("World.MeshBuild");

        private static readonly ProfilerMarker SpawnPresentationMarker =
            new ProfilerMarker("World.SpawnPresentation");

        private static readonly ProfilerMarker GenerationStageMarker =
            new ProfilerMarker("World.Generation.Stage");

        [Header("World")]
        [SerializeField] private int worldSeed = 12345;
        [SerializeField] private WorldDefinition worldDefinition;

        [Header("Finite session map")]
        [SerializeField] private bool useFiniteSessionMap = false;

        [Min(1)]
        [SerializeField] private int sessionPlayerCount = 1;

        [SerializeField] private string mapSizePresetId = "medium";

        [SerializeField]
        private WorldSessionStartOptions sessionStartOptions =
            new WorldSessionStartOptions();

        [Header("Streaming focus")]
        [SerializeField] private Transform focus;
        [SerializeField] private bool streamOnStart = true;

        [Header("Presentation")]
        [Tooltip(
            "Optional stationary scene root for streamed chunk GameObjects. " +
            "Leave null to place chunks at the scene root. Do not use a root " +
            "that follows the player.")]
        [SerializeField] private Transform chunkPresentationRoot;

        [SerializeField] private Material terrainMaterial;

        [Header("Runtime state")]
        [SerializeField]
        private WorldRuntimeDeltaState runtimeDelta =
            new WorldRuntimeDeltaState();

        private readonly Dictionary<ChunkCoordinate, StreamedChunkView>
            activeChunks =
                new Dictionary<ChunkCoordinate, StreamedChunkView>();

        private readonly HashSet<ChunkCoordinate> desiredChunks =
            new HashSet<ChunkCoordinate>();

        private readonly Queue<ChunkCoordinate> loadQueue =
            new Queue<ChunkCoordinate>();

        private readonly HashSet<ChunkCoordinate> queuedLoads =
            new HashSet<ChunkCoordinate>();

        private readonly Queue<ChunkCoordinate> urgentGenerationQueue =
            new Queue<ChunkCoordinate>();

        private readonly Queue<ChunkCoordinate> backgroundGenerationQueue =
            new Queue<ChunkCoordinate>();

        private readonly HashSet<ChunkCoordinate> queuedGeneration =
            new HashSet<ChunkCoordinate>();

        private readonly List<ChunkCoordinate> scratchCoordinates =
            new List<ChunkCoordinate>();

        private WorldGenerationPipeline pipeline;
        private WorldChunkGenerationWork activeGenerationWork;
        private WorldChunkCache activeGenerationCache;
        private ChunkCoordinate activeGenerationCoordinate;
        private bool activeGenerationUrgent;

        private WorldChunkCache chunkCache;
        private WorldChunkCache visualChunkCache;
        private MacroWorldPlan macroPlan;
        private WorldSessionMap sessionMap;

        private ChunkCoordinate currentFocusChunk;
        private ChunkCoordinate prefetchSortCenter;
        private ChunkCoordinate sessionMacroCenterChunk;

        private bool initialized;
        private bool hasFocusChunk;
        private bool macroEdgeWarningIssued;

        private double lastChunkLoadMilliseconds;
        private double lastChunkUnloadMilliseconds;
        private double lastMeshBuildMilliseconds;
        private double lastSpawnPresentationMilliseconds;
        private double lastGenerationStageMilliseconds;
        private string lastGenerationStageName = string.Empty;
        private double worstGenerationStageMilliseconds;
        private string worstGenerationStageName = string.Empty;
        private int completedPrefetchChunks;
        private int lastChunkSpawnCount;
        private int totalChunkLoads;
        private int totalChunkUnloads;
        private int activeTerrainColliderCount;
        private int activeGeneratedObjectColliderCount;
        private int priorityPreparationRadiusChunks = -1;

        public int WorldSeed => worldSeed;
        public WorldDefinition Definition => worldDefinition;
        public int ActiveChunkCount => activeChunks.Count;
        public int PendingLoadCount => loadQueue.Count;

        public int PendingGenerationCount =>
            urgentGenerationQueue.Count +
            backgroundGenerationQueue.Count +
            (activeGenerationWork != null ? 1 : 0);

        public int PendingUrgentGenerationCount =>
            urgentGenerationQueue.Count +
            (activeGenerationWork != null &&
             activeGenerationUrgent
                ? 1
                : 0);

        public int PendingBackgroundGenerationCount =>
            backgroundGenerationQueue.Count +
            (activeGenerationWork != null &&
             !activeGenerationUrgent
                ? 1
                : 0);

        public int DesiredChunkCount => desiredChunks.Count;

        public int MissingDesiredChunkCount
        {
            get
            {
                int missing = 0;

                foreach (
                    ChunkCoordinate coordinate
                    in desiredChunks)
                {
                    if (!activeChunks.ContainsKey(
                            coordinate))
                    {
                        missing++;
                    }
                }

                return missing;
            }
        }

        public bool IsVisibleAreaReady =>
            initialized &&
            MissingDesiredChunkCount == 0;

        public float VisibleReadiness01 =>
            DesiredChunkCount <= 0
                ? (initialized ? 1f : 0f)
                : 1f -
                  (float)MissingDesiredChunkCount /
                  DesiredChunkCount;

        public int ActiveTerrainColliderCount => activeTerrainColliderCount;
        public int ActiveGeneratedObjectColliderCount =>
            activeGeneratedObjectColliderCount;
        public Transform Focus => focus;

        public float GetPreparedDataReadiness01(
            int radiusChunks)
        {
            if (!initialized ||
                StreamingSettings == null)
            {
                return 0f;
            }

            int radius =
                Mathf.Max(
                    0,
                    radiusChunks);

            int expected = 0;
            int ready = 0;

            for (int z = -radius;
                 z <= radius;
                 z++)
            {
                for (int x = -radius;
                     x <= radius;
                     x++)
                {
                    if (StreamingSettings.CircularLoading &&
                        x * x + z * z >
                        radius * radius)
                    {
                        continue;
                    }

                    var coordinate =
                        new ChunkCoordinate(
                            currentFocusChunk.x + x,
                            currentFocusChunk.z + z);

                    if (!IsInsideStreamableArea(
                            coordinate))
                    {
                        continue;
                    }

                    expected++;

                    if (IsChunkDataCached(
                            coordinate))
                    {
                        ready++;
                    }
                }
            }

            return
                expected <= 0
                    ? 1f
                    : (float)ready /
                      expected;
        }

        public int GetPreparedDataCount(
            int radiusChunks)
        {
            if (!initialized ||
                StreamingSettings == null)
            {
                return 0;
            }

            int radius =
                Mathf.Max(
                    0,
                    radiusChunks);

            int ready = 0;

            for (int z = -radius;
                 z <= radius;
                 z++)
            {
                for (int x = -radius;
                     x <= radius;
                     x++)
                {
                    if (StreamingSettings.CircularLoading &&
                        x * x + z * z >
                        radius * radius)
                    {
                        continue;
                    }

                    var coordinate =
                        new ChunkCoordinate(
                            currentFocusChunk.x + x,
                            currentFocusChunk.z + z);

                    if (IsInsideStreamableArea(
                            coordinate) &&
                        IsChunkDataCached(
                            coordinate))
                    {
                        ready++;
                    }
                }
            }

            return ready;
        }

        public double LastChunkLoadMilliseconds =>
            lastChunkLoadMilliseconds;

        public double LastChunkUnloadMilliseconds =>
            lastChunkUnloadMilliseconds;

        public double LastMeshBuildMilliseconds =>
            lastMeshBuildMilliseconds;

        public double LastSpawnPresentationMilliseconds =>
            lastSpawnPresentationMilliseconds;

        public double LastGenerationStageMilliseconds =>
            lastGenerationStageMilliseconds;

        public string LastGenerationStageName =>
            lastGenerationStageName;

        public double WorstGenerationStageMilliseconds =>
            worstGenerationStageMilliseconds;

        public string WorstGenerationStageName =>
            worstGenerationStageName;

        public int CompletedPrefetchChunks =>
            completedPrefetchChunks;

        public int LastChunkSpawnCount =>
            lastChunkSpawnCount;

        public int TotalChunkLoads =>
            totalChunkLoads;

        public int TotalChunkUnloads =>
            totalChunkUnloads;

        public int CachedChunkCount =>
            (chunkCache != null ? chunkCache.Count : 0) +
            (visualChunkCache != null ? visualChunkCache.Count : 0);

        public MacroWorldPlan MacroPlan => macroPlan;
        public WorldRuntimeDeltaState RuntimeDelta => runtimeDelta;
        public WorldSessionMap SessionMap => sessionMap;
        public bool UsesFiniteSessionMap => sessionMap != null;

        private WorldGenerationSettings GenerationSettings =>
            worldDefinition != null
                ? worldDefinition.GenerationSettings
                : null;

        private MacroWorldPlannerSettings MacroSettings =>
            worldDefinition != null
                ? worldDefinition.MacroPlannerSettings
                : null;

        private WorldMapRules MapRules =>
            worldDefinition != null
                ? worldDefinition.MapRules
                : null;

        private WorldStreamingSettings StreamingSettings =>
            worldDefinition != null
                ? worldDefinition.StreamingSettings
                : null;

        private WorldSpawnCatalog SpawnCatalog =>
            worldDefinition != null
                ? worldDefinition.SpawnCatalog
                : null;

        private void Start()
        {
            if (streamOnStart)
                InitializeStreaming();
        }

        private void Update()
        {
            using (UpdateMarker.Auto())
            {
                if (!initialized)
                    return;

                ChunkCoordinate focusChunk =
                    GetFocusChunk();

                if (!hasFocusChunk ||
                    focusChunk != currentFocusChunk)
                {
                    ChunkCoordinate previousFocus =
                        currentFocusChunk;

                    bool hadPreviousFocus =
                        hasFocusChunk;

                    currentFocusChunk = focusChunk;
                    hasFocusChunk = true;

                    UpdatePrefetchSortCenter(
                        hadPreviousFocus
                            ? previousFocus
                            : currentFocusChunk,
                        currentFocusChunk);

                    RefreshDesiredChunks();
                    UpdateMacroEdgeWarning();
                    RefreshChunkColliderStates();
                }

                ProcessUnloads();
                ProcessLoads();
                ProcessGeneration();
            }
        }

        private void OnDestroy()
        {
            ShutdownStreaming();
        }

        /// <summary>
        /// Configures the finite map chosen by a multiplayer host.
        ///
        /// Returns false when the requested map preset is not allowed for the
        /// supplied player count.
        /// </summary>
        public bool ConfigureFiniteSession(
            int seed,
            int playerCount,
            string presetId,
            WorldSessionStartOptions startOptions = null)
        {
            if (worldDefinition == null ||
                MapRules == null)
            {
                Debug.LogError(
                    "Finite session configuration requires WorldMapRules " +
                    "on WorldDefinition.",
                    this);

                return false;
            }

            if (!MapRules.TryResolvePreset(
                    presetId,
                    playerCount,
                    out WorldMapSizePreset preset,
                    out string error))
            {
                Debug.LogError(
                    error,
                    this);

                return false;
            }

            worldSeed = seed;
            sessionPlayerCount =
                Mathf.Max(
                    1,
                    playerCount);

            mapSizePresetId =
                presetId;

            sessionStartOptions =
                startOptions != null
                    ? startOptions.Clone()
                    : new WorldSessionStartOptions();

            useFiniteSessionMap = true;

            sessionMap =
                WorldSessionMapFactory.Create(
                    worldSeed,
                    sessionPlayerCount,
                    preset,
                    new ChunkCoordinate(0, 0),
                    sessionStartOptions);

            if (initialized)
                InitializeStreaming();

            return true;
        }

        /// <summary>
        /// Clamps a camera/unit target to gameplay bounds.
        ///
        /// The caller remains responsible for movement/pathfinding behavior.
        /// Visual padding outside playable bounds is intentionally excluded.
        /// </summary>
        public bool TryClampToPlayableBounds(
            ref Vector3 worldPosition,
            float inset = 0f)
        {
            if (sessionMap == null ||
                GenerationSettings == null)
            {
                return false;
            }

            worldPosition =
                sessionMap.playableChunks.ClampWorldPosition(
                    worldPosition,
                    GenerationSettings.ChunkWorldSize,
                    inset);

            return true;
        }

        public bool IsInsidePlayableBounds(
            Vector3 worldPosition)
        {
            if (sessionMap == null ||
                GenerationSettings == null)
            {
                return true;
            }

            Rect bounds =
                sessionMap.playableChunks.ToWorldRect(
                    GenerationSettings.ChunkWorldSize);

            return
                worldPosition.x >= bounds.xMin &&
                worldPosition.x < bounds.xMax &&
                worldPosition.z >= bounds.yMin &&
                worldPosition.z < bounds.yMax;
        }

        [ContextMenu("Initialize Streaming")]
        public void InitializeStreaming()
        {
            ShutdownStreaming();

            if (!ValidateRequiredConfiguration())
                return;

            if (!TryBuildConfiguredFiniteSessionMap())
                return;

            runtimeDelta =
                runtimeDelta ??
                new WorldRuntimeDeltaState();

            runtimeDelta.RebuildIndexes();
            runtimeDelta.ChunkRevisionChanged -=
                OnChunkRuntimeRevisionChanged;

            runtimeDelta.ChunkRevisionChanged +=
                OnChunkRuntimeRevisionChanged;

            currentFocusChunk =
                GetFocusChunk();

            prefetchSortCenter =
                currentFocusChunk;

            hasFocusChunk = true;
            sessionMacroCenterChunk =
                currentFocusChunk;

            macroPlan =
                BuildSessionMacroPlan(
                    sessionMacroCenterChunk);

            pipeline =
                new WorldGenerationPipeline(
                    GenerationSettings,
                    macroPlan);

            chunkCache =
                new WorldChunkCache(
                    pipeline,
                    worldSeed,
                    StreamingSettings.MaxCachedChunks);

            if (sessionMap != null)
            {
                visualChunkCache =
                    new WorldChunkCache(
                        pipeline,
                        worldSeed,
                        StreamingSettings.MaxCachedChunks,
                        WorldGenerationStagePhase.TerrainAnalysis);
            }

            initialized = true;

            RefreshDesiredChunks();
            UpdateMacroEdgeWarning();
        }

        [ContextMenu("Shutdown Streaming")]
        public void ShutdownStreaming()
        {
            if (runtimeDelta != null)
            {
                runtimeDelta.ChunkRevisionChanged -=
                    OnChunkRuntimeRevisionChanged;
            }

            initialized = false;
            hasFocusChunk = false;
            macroEdgeWarningIssued = false;

            loadQueue.Clear();
            queuedLoads.Clear();
            desiredChunks.Clear();

            urgentGenerationQueue.Clear();
            backgroundGenerationQueue.Clear();
            queuedGeneration.Clear();

            activeGenerationWork = null;
            activeGenerationCache = null;
            activeGenerationUrgent = false;

            ReleaseAllViews();

            if (chunkCache != null)
                chunkCache.Clear();

            if (visualChunkCache != null)
                visualChunkCache.Clear();

            chunkCache = null;
            visualChunkCache = null;
            pipeline = null;
            macroPlan = null;

            lastChunkLoadMilliseconds = 0.0;
            lastChunkUnloadMilliseconds = 0.0;
            lastMeshBuildMilliseconds = 0.0;
            lastSpawnPresentationMilliseconds = 0.0;
            lastGenerationStageMilliseconds = 0.0;
            lastGenerationStageName = string.Empty;
            worstGenerationStageMilliseconds = 0.0;
            worstGenerationStageName = string.Empty;
            completedPrefetchChunks = 0;
            lastChunkSpawnCount = 0;
            totalChunkLoads = 0;
            totalChunkUnloads = 0;
            priorityPreparationRadiusChunks = -1;
            activeTerrainColliderCount = 0;
            activeGeneratedObjectColliderCount = 0;
        }

        [ContextMenu("Refresh Streaming Now")]
        public void RefreshStreamingNow()
        {
            if (!initialized)
            {
                InitializeStreaming();
                return;
            }

            ChunkCoordinate previousFocus =
                currentFocusChunk;

            bool hadPreviousFocus =
                hasFocusChunk;

            currentFocusChunk =
                GetFocusChunk();

            hasFocusChunk = true;

            UpdatePrefetchSortCenter(
                hadPreviousFocus
                    ? previousFocus
                    : currentFocusChunk,
                currentFocusChunk);

            RefreshDesiredChunks();
            UpdateMacroEdgeWarning();
        }

        public void SetPriorityPreparationRadius(
            int radiusChunks)
        {
            priorityPreparationRadiusChunks =
                Mathf.Max(
                    0,
                    radiusChunks);

            if (initialized)
                RefreshGenerationRequests();
        }

        public void ClearPriorityPreparationRadius()
        {
            priorityPreparationRadiusChunks = -1;

            if (initialized)
                RefreshGenerationRequests();
        }

        public void SetRuntimeDelta(
            WorldRuntimeDeltaState value)
        {
            if (runtimeDelta != null)
            {
                runtimeDelta.ChunkRevisionChanged -=
                    OnChunkRuntimeRevisionChanged;
            }

            runtimeDelta =
                value ??
                new WorldRuntimeDeltaState();

            runtimeDelta.RebuildIndexes();

            if (initialized)
            {
                runtimeDelta.ChunkRevisionChanged +=
                    OnChunkRuntimeRevisionChanged;

                RefreshAllActiveSpawnPresentation();
            }
        }

        private bool ValidateRequiredConfiguration()
        {
            if (worldDefinition == null)
            {
                Debug.LogError(
                    "WorldStreamer requires a WorldDefinition.",
                    this);

                return false;
            }

            if (GenerationSettings == null)
            {
                Debug.LogError(
                    "WorldDefinition requires WorldGenerationSettings.",
                    this);

                return false;
            }

            if (StreamingSettings == null)
            {
                Debug.LogError(
                    "WorldDefinition requires WorldStreamingSettings for runtime streaming.",
                    this);

                return false;
            }

            if (focus == null)
            {
                Debug.LogError(
                    "WorldStreamer requires an explicit focus Transform.",
                    this);

                return false;
            }

            if (useFiniteSessionMap &&
                MapRules == null)
            {
                Debug.LogError(
                    "Finite session map is enabled, but WorldDefinition has no WorldMapRules.",
                    this);

                return false;
            }

            WorldConfigurationValidationReport report =
                WorldGenerationConfigurationValidator.Validate(
                    worldDefinition);

            if (!report.IsValid)
            {
                Debug.LogError(
                    report.ToMultilineString(),
                    this);

                return false;
            }

            if (report.Warnings.Count > 0)
            {
                Debug.LogWarning(
                    report.ToMultilineString(),
                    this);
            }

            return true;
        }

        private bool TryBuildConfiguredFiniteSessionMap()
        {
            if (!useFiniteSessionMap)
                return true;

            if (sessionMap != null)
                return true;

            if (!MapRules.TryResolvePreset(
                    mapSizePresetId,
                    sessionPlayerCount,
                    out WorldMapSizePreset preset,
                    out string error))
            {
                Debug.LogError(
                    error,
                    this);

                return false;
            }

            sessionMap =
                WorldSessionMapFactory.Create(
                    worldSeed,
                    sessionPlayerCount,
                    preset,
                    new ChunkCoordinate(0, 0),
                    sessionStartOptions);

            return true;
        }

        private MacroWorldPlan BuildSessionMacroPlan(
            ChunkCoordinate center)
        {
            MacroWorldPlannerSettings macroSettings =
                MacroSettings;

            if (macroSettings == null)
                return null;

            WorldGenerationSettings settings =
                GenerationSettings;

            Rect requestedBounds;

            if (sessionMap != null)
            {
                requestedBounds =
                    sessionMap.playableChunks.ToWorldRect(
                        settings.ChunkWorldSize);
            }
            else
            {
                int radius =
                    StreamingSettings.MacroPlanRadiusChunks;

                float chunkSize =
                    settings.ChunkWorldSize;

                float minX =
                    (center.x - radius) *
                    chunkSize;

                float minZ =
                    (center.z - radius) *
                    chunkSize;

                float size =
                    (radius * 2 + 1) *
                    chunkSize;

                requestedBounds =
                    new Rect(
                        minX,
                        minZ,
                        size,
                        size);
            }

            var terrainOnlyPipeline =
                new WorldGenerationPipeline(
                    settings,
                    null);

            var terrainProbe =
                new WorldTerrainProbe(
                    terrainOnlyPipeline,
                    settings,
                    worldSeed);

            var planner =
                new MacroWorldPlanner(
                    macroSettings);

            return planner.GenerateForBounds(
                worldSeed,
                requestedBounds,
                terrainProbe);
        }

        private void RefreshDesiredChunks()
        {
            desiredChunks.Clear();
            loadQueue.Clear();
            queuedLoads.Clear();
            scratchCoordinates.Clear();

            int radius =
                StreamingSettings.LoadRadiusChunks;

            for (int z = -radius;
                 z <= radius;
                 z++)
            {
                for (int x = -radius;
                     x <= radius;
                     x++)
                {
                    if (StreamingSettings.CircularLoading &&
                        x * x + z * z >
                        radius * radius)
                    {
                        continue;
                    }

                    var coordinate =
                        new ChunkCoordinate(
                            currentFocusChunk.x + x,
                            currentFocusChunk.z + z);

                    if (!IsInsideStreamableArea(
                            coordinate))
                    {
                        continue;
                    }

                    desiredChunks.Add(
                        coordinate);

                    if (!activeChunks.ContainsKey(
                            coordinate))
                    {
                        scratchCoordinates.Add(
                            coordinate);
                    }
                }
            }

            scratchCoordinates.Sort(
                CompareDistanceToFocus);

            for (int i = 0;
                 i < scratchCoordinates.Count;
                 i++)
            {
                ChunkCoordinate coordinate =
                    scratchCoordinates[i];

                if (queuedLoads.Add(
                    coordinate))
                {
                    loadQueue.Enqueue(
                        coordinate);
                }
            }

            RefreshGenerationRequests();
        }

        private void RefreshGenerationRequests()
        {
            urgentGenerationQueue.Clear();
            backgroundGenerationQueue.Clear();
            queuedGeneration.Clear();

            if (pipeline == null ||
                chunkCache == null)
            {
                return;
            }

            if (activeGenerationWork != null)
            {
                if (desiredChunks.Contains(
                        activeGenerationCoordinate))
                {
                    activeGenerationUrgent = true;
                }
                else if (!IsWithinRadius(
                             activeGenerationCoordinate,
                             currentFocusChunk,
                             StreamingSettings.PrefetchRadiusChunks,
                             StreamingSettings.CircularLoading))
                {
                    activeGenerationWork = null;
                    activeGenerationCache = null;
                    activeGenerationUrgent = false;
                }
            }

            scratchCoordinates.Clear();

            foreach (
                ChunkCoordinate coordinate
                in desiredChunks)
            {
                if (!IsChunkDataCached(
                        coordinate) &&
                    !IsActiveGenerationCoordinate(
                        coordinate))
                {
                    scratchCoordinates.Add(
                        coordinate);
                }
            }

            scratchCoordinates.Sort(
                CompareDistanceToFocus);

            for (int i = 0;
                 i < scratchCoordinates.Count;
                 i++)
            {
                EnqueueGeneration(
                    scratchCoordinates[i],
                    true);
            }

            scratchCoordinates.Clear();

            if (priorityPreparationRadiusChunks >= 0)
            {
                int priorityRadius =
                    Mathf.Min(
                        priorityPreparationRadiusChunks,
                        StreamingSettings.PrefetchRadiusChunks);

                for (int z = -priorityRadius;
                     z <= priorityRadius;
                     z++)
                {
                    for (int x = -priorityRadius;
                         x <= priorityRadius;
                         x++)
                    {
                        if (StreamingSettings.CircularLoading &&
                            x * x + z * z >
                            priorityRadius * priorityRadius)
                        {
                            continue;
                        }

                        var coordinate =
                            new ChunkCoordinate(
                                currentFocusChunk.x + x,
                                currentFocusChunk.z + z);

                        if (desiredChunks.Contains(
                                coordinate) ||
                            !IsInsideStreamableArea(
                                coordinate) ||
                            IsChunkDataCached(
                                coordinate) ||
                            IsActiveGenerationCoordinate(
                                coordinate))
                        {
                            continue;
                        }

                        scratchCoordinates.Add(
                            coordinate);
                    }
                }

                scratchCoordinates.Sort(
                    CompareDistanceToFocus);

                for (int i = 0;
                     i < scratchCoordinates.Count;
                     i++)
                {
                    EnqueueGeneration(
                        scratchCoordinates[i],
                        true);
                }
            }

            scratchCoordinates.Clear();

            int prefetchRadius =
                StreamingSettings.PrefetchRadiusChunks;

            for (int z = -prefetchRadius;
                 z <= prefetchRadius;
                 z++)
            {
                for (int x = -prefetchRadius;
                     x <= prefetchRadius;
                     x++)
                {
                    if (StreamingSettings.CircularLoading &&
                        x * x + z * z >
                        prefetchRadius * prefetchRadius)
                    {
                        continue;
                    }

                    var coordinate =
                        new ChunkCoordinate(
                            currentFocusChunk.x + x,
                            currentFocusChunk.z + z);

                    if (desiredChunks.Contains(
                            coordinate) ||
                        !IsInsideStreamableArea(
                            coordinate) ||
                        IsChunkDataCached(
                            coordinate) ||
                        IsActiveGenerationCoordinate(
                            coordinate))
                    {
                        continue;
                    }

                    scratchCoordinates.Add(
                        coordinate);
                }
            }

            scratchCoordinates.Sort(
                CompareDistanceToPrefetchCenter);

            for (int i = 0;
                 i < scratchCoordinates.Count;
                 i++)
            {
                EnqueueGeneration(
                    scratchCoordinates[i],
                    false);
            }
        }

        private void EnqueueGeneration(
            ChunkCoordinate coordinate,
            bool urgent)
        {
            if (IsChunkDataCached(
                    coordinate) ||
                IsActiveGenerationCoordinate(
                    coordinate) ||
                !queuedGeneration.Add(
                    coordinate))
            {
                return;
            }

            if (urgent)
            {
                urgentGenerationQueue.Enqueue(
                    coordinate);
            }
            else
            {
                backgroundGenerationQueue.Enqueue(
                    coordinate);
            }
        }

        private bool IsChunkDataCached(
            ChunkCoordinate coordinate)
        {
            WorldChunkCache cache =
                GetSourceCache(
                    coordinate);

            return
                cache != null &&
                cache.Contains(
                    coordinate);
        }

        private bool IsActiveGenerationCoordinate(
            ChunkCoordinate coordinate)
        {
            return
                activeGenerationWork != null &&
                activeGenerationCoordinate ==
                coordinate;
        }

        private WorldChunkCache GetSourceCache(
            ChunkCoordinate coordinate)
        {
            bool isPlayableChunk =
                sessionMap == null ||
                sessionMap.IsPlayableChunk(
                    coordinate);

            return
                isPlayableChunk ||
                visualChunkCache == null
                    ? chunkCache
                    : visualChunkCache;
        }

        private void ProcessGeneration()
        {
            if (pipeline == null ||
                StreamingSettings == null)
            {
                return;
            }

            if (activeGenerationWork != null &&
                !activeGenerationUrgent &&
                urgentGenerationQueue.Count > 0)
            {
                ChunkCoordinate interrupted =
                    activeGenerationCoordinate;

                activeGenerationWork = null;
                activeGenerationCache = null;
                activeGenerationUrgent = false;

                EnqueueGeneration(
                    interrupted,
                    false);
            }

            if (activeGenerationWork != null)
            {
                if (desiredChunks.Contains(
                        activeGenerationCoordinate))
                {
                    activeGenerationUrgent = true;
                }
                else if (!IsWithinRadius(
                             activeGenerationCoordinate,
                             currentFocusChunk,
                             StreamingSettings.PrefetchRadiusChunks,
                             StreamingSettings.CircularLoading))
                {
                    activeGenerationWork = null;
                    activeGenerationCache = null;
                    activeGenerationUrgent = false;
                }
            }

            bool hasUrgentWork =
                activeGenerationWork != null
                    ? activeGenerationUrgent
                    : urgentGenerationQueue.Count > 0;

            int stageBudget =
                hasUrgentWork
                    ? StreamingSettings.UrgentGenerationStagesPerFrame
                    : StreamingSettings.BackgroundGenerationStagesPerFrame;

            if (!hasUrgentWork &&
                !CanRunBackgroundPrefetch())
            {
                return;
            }

            int stagesProcessed = 0;

            while (stagesProcessed < stageBudget)
            {
                if (activeGenerationWork == null &&
                    !TryBeginNextGenerationWork())
                {
                    break;
                }

                if (activeGenerationWork == null)
                    break;

                if (!activeGenerationUrgent &&
                    !CanRunBackgroundPrefetch())
                {
                    break;
                }

                if (activeGenerationWork.IsCompleted)
                {
                    CompleteActiveGeneration();
                    continue;
                }

                double startedAt =
                    Time.realtimeSinceStartupAsDouble;

                bool executedStage;

                using (GenerationStageMarker.Auto())
                {
                    executedStage =
                        activeGenerationWork.StepNextStage();
                }

                lastGenerationStageMilliseconds =
                    (Time.realtimeSinceStartupAsDouble -
                     startedAt) *
                    1000.0;

                lastGenerationStageName =
                    activeGenerationWork.LastStageName;

                if (lastGenerationStageMilliseconds >
                    worstGenerationStageMilliseconds)
                {
                    worstGenerationStageMilliseconds =
                        lastGenerationStageMilliseconds;

                    worstGenerationStageName =
                        lastGenerationStageName;
                }

                if (executedStage)
                    stagesProcessed++;

                if (activeGenerationWork.IsCompleted)
                {
                    CompleteActiveGeneration();
                }
            }
        }

        private bool TryBeginNextGenerationWork()
        {
            if (TryDequeueGenerationCoordinate(
                    urgentGenerationQueue,
                    true,
                    out ChunkCoordinate urgent))
            {
                BeginGenerationWork(
                    urgent,
                    true);

                return true;
            }

            if (!CanRunBackgroundPrefetch())
                return false;

            if (TryDequeueGenerationCoordinate(
                    backgroundGenerationQueue,
                    false,
                    out ChunkCoordinate background))
            {
                BeginGenerationWork(
                    background,
                    false);

                return true;
            }

            return false;
        }

        private bool TryDequeueGenerationCoordinate(
            Queue<ChunkCoordinate> queue,
            bool urgent,
            out ChunkCoordinate coordinate)
        {
            while (queue.Count > 0)
            {
                coordinate =
                    queue.Dequeue();

                queuedGeneration.Remove(
                    coordinate);

                if (IsChunkDataCached(
                        coordinate) ||
                    !IsInsideStreamableArea(
                        coordinate))
                {
                    continue;
                }

                if (urgent)
                {
                    bool visible =
                        desiredChunks.Contains(
                            coordinate);

                    bool startupPriority =
                        priorityPreparationRadiusChunks >= 0 &&
                        IsWithinRadius(
                            coordinate,
                            currentFocusChunk,
                            Mathf.Min(
                                priorityPreparationRadiusChunks,
                                StreamingSettings.PrefetchRadiusChunks),
                            StreamingSettings.CircularLoading);

                    if (!visible &&
                        !startupPriority)
                    {
                        continue;
                    }
                }
                else if (!IsWithinRadius(
                             coordinate,
                             currentFocusChunk,
                             StreamingSettings.PrefetchRadiusChunks,
                             StreamingSettings.CircularLoading))
                {
                    continue;
                }

                return true;
            }

            coordinate = default;
            return false;
        }

        private void BeginGenerationWork(
            ChunkCoordinate coordinate,
            bool urgent)
        {
            WorldChunkCache cache =
                GetSourceCache(
                    coordinate);

            if (cache == null)
                return;

            activeGenerationCoordinate =
                coordinate;

            activeGenerationCache =
                cache;

            activeGenerationUrgent =
                urgent ||
                desiredChunks.Contains(
                    coordinate);

            activeGenerationWork =
                pipeline.BeginIncrementalGeneration(
                    worldSeed,
                    coordinate,
                    cache.MaximumPhase);
        }

        private void CompleteActiveGeneration()
        {
            if (activeGenerationWork == null)
                return;

            bool wasUrgent =
                activeGenerationUrgent;

            if (activeGenerationCache != null)
            {
                activeGenerationCache.StoreGenerated(
                    activeGenerationWork.Result);
            }

            if (!wasUrgent)
                completedPrefetchChunks++;

            activeGenerationWork = null;
            activeGenerationCache = null;
            activeGenerationUrgent = false;
        }

        private bool CanRunBackgroundPrefetch()
        {
            float previousFrameMilliseconds =
                Time.unscaledDeltaTime *
                1000f;

            return
                previousFrameMilliseconds <=
                StreamingSettings.BackgroundPrefetchFrameLimitMs;
        }

        private void UpdatePrefetchSortCenter(
            ChunkCoordinate previous,
            ChunkCoordinate current)
        {
            int dx =
                Mathf.Clamp(
                    current.x -
                    previous.x,
                    -1,
                    1);

            int dz =
                Mathf.Clamp(
                    current.z -
                    previous.z,
                    -1,
                    1);

            int lead =
                StreamingSettings != null
                    ? StreamingSettings.PrefetchLeadChunks
                    : 0;

            prefetchSortCenter =
                new ChunkCoordinate(
                    current.x +
                    dx * lead,
                    current.z +
                    dz * lead);
        }

        private int CompareDistanceToPrefetchCenter(
            ChunkCoordinate a,
            ChunkCoordinate b)
        {
            int adx =
                a.x -
                prefetchSortCenter.x;

            int adz =
                a.z -
                prefetchSortCenter.z;

            int bdx =
                b.x -
                prefetchSortCenter.x;

            int bdz =
                b.z -
                prefetchSortCenter.z;

            int aDistance =
                adx * adx +
                adz * adz;

            int bDistance =
                bdx * bdx +
                bdz * bdz;

            if (aDistance != bDistance)
                return aDistance.CompareTo(bDistance);

            return
                CompareDistanceToFocus(
                    a,
                    b);
        }

        private int CompareDistanceToFocus(
            ChunkCoordinate a,
            ChunkCoordinate b)
        {
            int adx =
                a.x -
                currentFocusChunk.x;

            int adz =
                a.z -
                currentFocusChunk.z;

            int bdx =
                b.x -
                currentFocusChunk.x;

            int bdz =
                b.z -
                currentFocusChunk.z;

            int aDistance =
                adx * adx +
                adz * adz;

            int bDistance =
                bdx * bdx +
                bdz * bdz;

            if (aDistance != bDistance)
                return aDistance.CompareTo(bDistance);

            if (a.x != b.x)
                return a.x.CompareTo(b.x);

            return a.z.CompareTo(b.z);
        }

        private void ProcessLoads()
        {
            int budget =
                StreamingSettings.MaxChunkLoadsPerFrame;

            int processed = 0;
            int inspected = 0;
            int inspectionLimit =
                loadQueue.Count;

            while (processed < budget &&
                   loadQueue.Count > 0 &&
                   inspected < inspectionLimit)
            {
                inspected++;

                ChunkCoordinate coordinate =
                    loadQueue.Dequeue();

                queuedLoads.Remove(
                    coordinate);

                if (!desiredChunks.Contains(
                        coordinate) ||
                    activeChunks.ContainsKey(
                        coordinate))
                {
                    continue;
                }

                if (!IsInsideStreamableArea(
                        coordinate))
                {
                    continue;
                }

                if (CreateChunkView(
                        coordinate))
                {
                    processed++;
                    continue;
                }

                EnqueueGeneration(
                    coordinate,
                    true);

                if (queuedLoads.Add(
                        coordinate))
                {
                    loadQueue.Enqueue(
                        coordinate);
                }
            }
        }

        private void ProcessUnloads()
        {
            int radius =
                StreamingSettings.UnloadRadiusChunks;

            int budget =
                StreamingSettings.MaxChunkUnloadsPerFrame;

            scratchCoordinates.Clear();

            foreach (
                KeyValuePair<ChunkCoordinate, StreamedChunkView> pair
                in activeChunks)
            {
                if (!IsWithinRadius(
                        pair.Key,
                        currentFocusChunk,
                        radius,
                        StreamingSettings.CircularLoading))
                {
                    scratchCoordinates.Add(
                        pair.Key);
                }
            }

            scratchCoordinates.Sort(
                CompareFarthestFirst);

            int count =
                Mathf.Min(
                    budget,
                    scratchCoordinates.Count);

            for (int i = 0;
                 i < count;
                 i++)
            {
                UnloadChunk(
                    scratchCoordinates[i]);
            }
        }

        private int CompareFarthestFirst(
            ChunkCoordinate a,
            ChunkCoordinate b)
        {
            int adx =
                a.x -
                currentFocusChunk.x;

            int adz =
                a.z -
                currentFocusChunk.z;

            int bdx =
                b.x -
                currentFocusChunk.x;

            int bdz =
                b.z -
                currentFocusChunk.z;

            int aDistance =
                adx * adx +
                adz * adz;

            int bDistance =
                bdx * bdx +
                bdz * bdz;

            int comparison =
                bDistance.CompareTo(
                    aDistance);

            if (comparison != 0)
                return comparison;

            if (a.x != b.x)
                return a.x.CompareTo(b.x);

            return a.z.CompareTo(b.z);
        }

        private bool CreateChunkView(
            ChunkCoordinate coordinate)
        {
            using (LoadMarker.Auto())
            {
                double loadStartedAt =
                    Time.realtimeSinceStartupAsDouble;

                if (chunkCache == null ||
                    pipeline == null)
                {
                    return false;
                }

                bool isPlayableChunk =
                    sessionMap == null ||
                    sessionMap.IsPlayableChunk(
                        coordinate);

                WorldChunkCache sourceCache =
                    GetSourceCache(
                        coordinate);

                if (sourceCache == null ||
                    !sourceCache.TryGet(
                        coordinate,
                        out WorldChunkData chunkData))
                {
                    return false;
                }

                sourceCache.Pin(
                    coordinate);

                float chunkSize =
                    GenerationSettings.ChunkWorldSize;

                Vector3 worldOrigin =
                    coordinate.GetWorldOrigin(
                        chunkSize);

                Mesh mesh;

                double meshStartedAt =
                    Time.realtimeSinceStartupAsDouble;

                using (MeshBuildMarker.Auto())
                {
                    mesh =
                        ChunkMeshBuilder.Build(
                            chunkData,
                            chunkSize);
                }

                lastMeshBuildMilliseconds =
                    (Time.realtimeSinceStartupAsDouble -
                     meshStartedAt) *
                    1000.0;

                var chunkObject =
                    new GameObject(
                        "StreamedChunk_" +
                        coordinate.x +
                        "_" +
                        coordinate.z);

                chunkObject.transform.SetParent(
                    chunkPresentationRoot,
                    true);

                chunkObject.transform.position =
                    worldOrigin;

                var view =
                    chunkObject.AddComponent<
                        StreamedChunkView>();

                view.Initialize(
                    coordinate,
                    mesh,
                    isPlayableChunk);

                var meshFilter =
                    chunkObject.AddComponent<
                        MeshFilter>();

                meshFilter.sharedMesh =
                    mesh;

                var meshRenderer =
                    chunkObject.AddComponent<
                        MeshRenderer>();

                meshRenderer.sharedMaterial =
                    terrainMaterial;

                bool colliderEnabled =
                    isPlayableChunk &&
                    StreamingSettings.AddMeshCollider &&
                    IsWithinRadius(
                        coordinate,
                        currentFocusChunk,
                        StreamingSettings.ColliderRadiusChunks,
                        StreamingSettings.CircularLoading);

                view.SetTerrainColliderEnabled(
                    colliderEnabled);

                if (view.TerrainColliderEnabled)
                    activeTerrainColliderCount++;

                WorldSpawnCatalog catalog =
                    SpawnCatalog;

                lastChunkSpawnCount = 0;
                lastSpawnPresentationMilliseconds = 0.0;

                if (isPlayableChunk &&
                    StreamingSettings.RenderGeneratedSpawns &&
                    catalog != null)
                {
                    double spawnStartedAt =
                        Time.realtimeSinceStartupAsDouble;

                    using (SpawnPresentationMarker.Auto())
                    {
                        lastChunkSpawnCount =
                            ChunkSpawnPresenter.Populate(
                                chunkObject.transform,
                                chunkData,
                                catalog,
                                worldOrigin,
                                runtimeDelta);
                    }

                    lastSpawnPresentationMilliseconds =
                        (Time.realtimeSinceStartupAsDouble -
                         spawnStartedAt) *
                        1000.0;
                }

                view.InvalidateGeneratedObjectColliderCache();

                bool objectCollidersEnabled =
                    isPlayableChunk &&
                    IsWithinRadius(
                        coordinate,
                        currentFocusChunk,
                        StreamingSettings.GeneratedObjectColliderRadiusChunks,
                        StreamingSettings.CircularLoading);

                view.SetGeneratedObjectCollidersEnabled(
                    objectCollidersEnabled);

                activeGeneratedObjectColliderCount +=
                    view.ActiveGeneratedObjectColliderCount;

                activeChunks.Add(
                    coordinate,
                    view);

                totalChunkLoads++;

                lastChunkLoadMilliseconds =
                    (Time.realtimeSinceStartupAsDouble -
                     loadStartedAt) *
                    1000.0;

                return true;
            }
        }

        private void OnChunkRuntimeRevisionChanged(
            ChunkCoordinate coordinate,
            int revision)
        {
            if (!initialized)
                return;

            RefreshChunkSpawnPresentation(
                coordinate);
        }

        private void RefreshAllActiveSpawnPresentation()
        {
            scratchCoordinates.Clear();

            foreach (
                KeyValuePair<ChunkCoordinate, StreamedChunkView> pair
                in activeChunks)
            {
                scratchCoordinates.Add(
                    pair.Key);
            }

            for (int i = 0;
                 i < scratchCoordinates.Count;
                 i++)
            {
                RefreshChunkSpawnPresentation(
                    scratchCoordinates[i]);
            }
        }

        private void RefreshChunkSpawnPresentation(
            ChunkCoordinate coordinate)
        {
            if (!activeChunks.TryGetValue(
                    coordinate,
                    out StreamedChunkView view) ||
                view == null ||
                !view.IsPlayableChunk ||
                !StreamingSettings.RenderGeneratedSpawns ||
                SpawnCatalog == null)
            {
                return;
            }

            WorldChunkCache sourceCache =
                GetSourceCache(
                    coordinate);

            if (sourceCache == null ||
                !sourceCache.TryGet(
                    coordinate,
                    out WorldChunkData chunkData))
            {
                return;
            }

            Transform root =
                view.transform;

            for (int i = root.childCount - 1;
                 i >= 0;
                 i--)
            {
                Transform child =
                    root.GetChild(i);

                if (child == null)
                    continue;

                child.gameObject.SetActive(
                    false);

                if (Application.isPlaying)
                {
                    Destroy(
                        child.gameObject);
                }
                else
                {
                    DestroyImmediate(
                        child.gameObject);
                }
            }

            float chunkSize =
                GenerationSettings.ChunkWorldSize;

            Vector3 worldOrigin =
                coordinate.GetWorldOrigin(
                    chunkSize);

            double spawnStartedAt =
                Time.realtimeSinceStartupAsDouble;

            view.InvalidateGeneratedObjectColliderCache();

            using (SpawnPresentationMarker.Auto())
            {
                lastChunkSpawnCount =
                    ChunkSpawnPresenter.Populate(
                        root,
                        chunkData,
                        SpawnCatalog,
                        worldOrigin,
                        runtimeDelta);
            }

            bool objectCollidersEnabled =
                IsWithinRadius(
                    coordinate,
                    currentFocusChunk,
                    StreamingSettings.GeneratedObjectColliderRadiusChunks,
                    StreamingSettings.CircularLoading);

            view.SetGeneratedObjectCollidersEnabled(
                objectCollidersEnabled);

            RefreshGeneratedObjectColliderCount();

            lastSpawnPresentationMilliseconds =
                (Time.realtimeSinceStartupAsDouble -
                 spawnStartedAt) *
                1000.0;
        }

        private void UnloadChunk(
            ChunkCoordinate coordinate)
        {
            using (UnloadMarker.Auto())
            {
                double unloadStartedAt =
                    Time.realtimeSinceStartupAsDouble;

                if (!activeChunks.TryGetValue(
                        coordinate,
                        out StreamedChunkView view))
                {
                    return;
                }

                activeChunks.Remove(
                    coordinate);

                if (view != null &&
                    view.TerrainColliderEnabled)
                {
                    activeTerrainColliderCount =
                        Mathf.Max(
                            0,
                            activeTerrainColliderCount - 1);
                }

                if (view != null)
                {
                    activeGeneratedObjectColliderCount =
                        Mathf.Max(
                            0,
                            activeGeneratedObjectColliderCount -
                            view.ActiveGeneratedObjectColliderCount);
                }

                if (view != null &&
                    !view.IsPlayableChunk &&
                    visualChunkCache != null)
                {
                    visualChunkCache.Unpin(
                        coordinate);
                }
                else if (chunkCache != null)
                {
                    chunkCache.Unpin(
                        coordinate);
                }

                if (view == null)
                    return;

                GameObject chunkObject =
                    view.gameObject;

                view.ReleaseOwnedResources();

                if (Application.isPlaying)
                    Destroy(chunkObject);
                else
                    DestroyImmediate(chunkObject);

                totalChunkUnloads++;

                lastChunkUnloadMilliseconds =
                    (Time.realtimeSinceStartupAsDouble -
                     unloadStartedAt) *
                    1000.0;
            }
        }

        private void ReleaseAllViews()
        {
            scratchCoordinates.Clear();

            foreach (
                KeyValuePair<ChunkCoordinate, StreamedChunkView> pair
                in activeChunks)
            {
                scratchCoordinates.Add(
                    pair.Key);
            }

            for (int i = 0;
                 i < scratchCoordinates.Count;
                 i++)
            {
                UnloadChunk(
                    scratchCoordinates[i]);
            }

            activeChunks.Clear();
        }

        private void RefreshChunkColliderStates()
        {
            activeTerrainColliderCount = 0;
            activeGeneratedObjectColliderCount = 0;

            if (StreamingSettings == null)
                return;

            foreach (
                KeyValuePair<ChunkCoordinate, StreamedChunkView> pair
                in activeChunks)
            {
                StreamedChunkView view =
                    pair.Value;

                if (view == null)
                    continue;

                bool terrainEnabled =
                    view.IsPlayableChunk &&
                    StreamingSettings.AddMeshCollider &&
                    IsWithinRadius(
                        pair.Key,
                        currentFocusChunk,
                        StreamingSettings.ColliderRadiusChunks,
                        StreamingSettings.CircularLoading);

                view.SetTerrainColliderEnabled(
                    terrainEnabled);

                if (view.TerrainColliderEnabled)
                    activeTerrainColliderCount++;

                bool objectCollidersEnabled =
                    view.IsPlayableChunk &&
                    IsWithinRadius(
                        pair.Key,
                        currentFocusChunk,
                        StreamingSettings.GeneratedObjectColliderRadiusChunks,
                        StreamingSettings.CircularLoading);

                view.SetGeneratedObjectCollidersEnabled(
                    objectCollidersEnabled);

                activeGeneratedObjectColliderCount +=
                    view.ActiveGeneratedObjectColliderCount;
            }
        }

        private void RefreshGeneratedObjectColliderCount()
        {
            activeGeneratedObjectColliderCount = 0;

            foreach (
                KeyValuePair<ChunkCoordinate, StreamedChunkView> pair
                in activeChunks)
            {
                StreamedChunkView view =
                    pair.Value;

                if (view != null)
                {
                    activeGeneratedObjectColliderCount +=
                        view.ActiveGeneratedObjectColliderCount;
                }
            }
        }

        private ChunkCoordinate GetFocusChunk()
        {
            Vector3 position =
                focus.position;

            return
                WorldChunkCoordinateUtility.FromWorldPosition(
                    position.x,
                    position.z,
                    GenerationSettings.ChunkWorldSize);
        }

        private bool IsInsideStreamableArea(
            ChunkCoordinate coordinate)
        {
            if (sessionMap != null)
            {
                return
                    sessionMap.IsVisualChunk(
                        coordinate);
            }

            if (MacroSettings == null)
                return true;

            int radius =
                StreamingSettings.MacroPlanRadiusChunks;

            int dx =
                Mathf.Abs(
                    coordinate.x -
                    sessionMacroCenterChunk.x);

            int dz =
                Mathf.Abs(
                    coordinate.z -
                    sessionMacroCenterChunk.z);

            return
                dx <= radius &&
                dz <= radius;
        }

        private void UpdateMacroEdgeWarning()
        {
            if (sessionMap != null)
            {
                // Reaching the edge is intentional for a finite session map.
                macroEdgeWarningIssued = false;
                return;
            }

            if (MacroSettings == null)
            {
                macroEdgeWarningIssued = false;
                return;
            }

            int radius =
                StreamingSettings.MacroPlanRadiusChunks;

            int dx =
                Mathf.Abs(
                    currentFocusChunk.x -
                    sessionMacroCenterChunk.x);

            int dz =
                Mathf.Abs(
                    currentFocusChunk.z -
                    sessionMacroCenterChunk.z);

            int used =
                Mathf.Max(
                    dx,
                    dz);

            int remaining =
                radius -
                used;

            if (remaining <=
                StreamingSettings.MacroEdgeWarningChunks)
            {
                if (!macroEdgeWarningIssued)
                {
                    macroEdgeWarningIssued = true;

                    Debug.LogWarning(
                        "Legacy radius-based WorldStreamer focus is approaching " +
                        "the edge of its fixed MacroWorldPlan. Remaining macro " +
                        "chunks: " +
                        remaining +
                        ". Configure WorldMapRules and a finite session map for " +
                        "production matches.",
                        this);
                }
            }
            else
            {
                macroEdgeWarningIssued = false;
            }
        }

        private static bool IsWithinRadius(
            ChunkCoordinate coordinate,
            ChunkCoordinate center,
            int radius,
            bool circular)
        {
            int dx =
                coordinate.x -
                center.x;

            int dz =
                coordinate.z -
                center.z;

            if (circular)
            {
                return
                    dx * dx +
                    dz * dz <=
                    radius * radius;
            }

            return
                Mathf.Abs(dx) <= radius &&
                Mathf.Abs(dz) <= radius;
        }
    }
}
