using System.Collections.Generic;
using UnityEngine;

namespace LittleCastle.World
{
    /// <summary>
    /// Budgeted runtime chunk streamer around one focus transform.
    ///
    /// Current macro generation is bounded/on-demand, so this streamer creates
    /// one fixed macro plan for the session. It deliberately refuses to silently
    /// rebuild macro roads/rivers while the player moves because that could
    /// mutate already visited world structure.
    ///
    /// Future fixed macro tiles can replace the session-plan source without
    /// changing the chunk/presentation lifecycle implemented here.
    /// </summary>
    public sealed class WorldStreamer : MonoBehaviour
    {
        [Header("World")]
        [SerializeField] private int worldSeed = 12345;
        [SerializeField] private WorldDefinition worldDefinition;

        [Header("Streaming focus")]
        [SerializeField] private Transform focus;
        [SerializeField] private bool streamOnStart = true;

        [Header("Presentation")]
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

        private readonly List<ChunkCoordinate> scratchCoordinates =
            new List<ChunkCoordinate>();

        private WorldGenerationPipeline pipeline;
        private WorldChunkCache chunkCache;
        private MacroWorldPlan macroPlan;

        private ChunkCoordinate currentFocusChunk;
        private ChunkCoordinate sessionMacroCenterChunk;

        private bool initialized;
        private bool hasFocusChunk;
        private bool macroEdgeWarningIssued;

        public int WorldSeed => worldSeed;
        public WorldDefinition Definition => worldDefinition;
        public int ActiveChunkCount => activeChunks.Count;
        public int CachedChunkCount => chunkCache != null ? chunkCache.Count : 0;
        public MacroWorldPlan MacroPlan => macroPlan;
        public WorldRuntimeDeltaState RuntimeDelta => runtimeDelta;

        private WorldGenerationSettings GenerationSettings =>
            worldDefinition != null
                ? worldDefinition.GenerationSettings
                : null;

        private MacroWorldPlannerSettings MacroSettings =>
            worldDefinition != null
                ? worldDefinition.MacroPlannerSettings
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
            if (!initialized)
                return;

            ChunkCoordinate focusChunk =
                GetFocusChunk();

            if (!hasFocusChunk ||
                focusChunk != currentFocusChunk)
            {
                currentFocusChunk = focusChunk;
                hasFocusChunk = true;

                RefreshDesiredChunks();
                UpdateMacroEdgeWarning();
            }

            ProcessUnloads();
            ProcessLoads();
        }

        private void OnDestroy()
        {
            ReleaseAllViews();

            if (chunkCache != null)
                chunkCache.Clear();
        }

        [ContextMenu("Initialize Streaming")]
        public void InitializeStreaming()
        {
            ShutdownStreaming();

            if (!ValidateRequiredConfiguration())
                return;

            runtimeDelta =
                runtimeDelta ??
                new WorldRuntimeDeltaState();

            currentFocusChunk =
                GetFocusChunk();

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

            initialized = true;

            RefreshDesiredChunks();
            UpdateMacroEdgeWarning();
        }

        [ContextMenu("Shutdown Streaming")]
        public void ShutdownStreaming()
        {
            initialized = false;
            hasFocusChunk = false;
            macroEdgeWarningIssued = false;

            loadQueue.Clear();
            queuedLoads.Clear();
            desiredChunks.Clear();

            ReleaseAllViews();

            if (chunkCache != null)
                chunkCache.Clear();

            chunkCache = null;
            pipeline = null;
            macroPlan = null;
        }

        [ContextMenu("Refresh Streaming Now")]
        public void RefreshStreamingNow()
        {
            if (!initialized)
            {
                InitializeStreaming();
                return;
            }

            currentFocusChunk =
                GetFocusChunk();

            hasFocusChunk = true;

            RefreshDesiredChunks();
            UpdateMacroEdgeWarning();
        }

        public void SetRuntimeDelta(
            WorldRuntimeDeltaState value)
        {
            runtimeDelta =
                value ??
                new WorldRuntimeDeltaState();

            runtimeDelta.RebuildIndexes();
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

        private MacroWorldPlan BuildSessionMacroPlan(
            ChunkCoordinate center)
        {
            MacroWorldPlannerSettings macroSettings =
                MacroSettings;

            if (macroSettings == null)
                return null;

            WorldGenerationSettings settings =
                GenerationSettings;

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

            var requestedBounds =
                new Rect(
                    minX,
                    minZ,
                    size,
                    size);

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

            for (int z = -radius; z <= radius; z++)
            {
                for (int x = -radius; x <= radius; x++)
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

                    if (!IsInsideSessionMacroArea(
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

            while (processed < budget &&
                   loadQueue.Count > 0)
            {
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

                if (!IsInsideSessionMacroArea(
                        coordinate))
                {
                    continue;
                }

                CreateChunkView(
                    coordinate);

                processed++;
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

            for (int i = 0; i < count; i++)
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

        private void CreateChunkView(
            ChunkCoordinate coordinate)
        {
            if (chunkCache == null ||
                pipeline == null)
            {
                return;
            }

            WorldChunkData chunkData =
                chunkCache.GetOrGeneratePinned(
                    coordinate);

            float chunkSize =
                GenerationSettings.ChunkWorldSize;

            Vector3 worldOrigin =
                coordinate.GetWorldOrigin(
                    chunkSize);

            Mesh mesh =
                ChunkMeshBuilder.Build(
                    chunkData,
                    chunkSize);

            var chunkObject =
                new GameObject(
                    "StreamedChunk_" +
                    coordinate.x +
                    "_" +
                    coordinate.z);

            chunkObject.transform.SetParent(
                transform,
                false);

            chunkObject.transform.position =
                worldOrigin;

            var view =
                chunkObject.AddComponent<
                    StreamedChunkView>();

            view.Initialize(
                coordinate,
                mesh);

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

            if (StreamingSettings.AddMeshCollider)
            {
                var collider =
                    chunkObject.AddComponent<
                        MeshCollider>();

                collider.sharedMesh =
                    mesh;
            }

            WorldSpawnCatalog catalog =
                SpawnCatalog;

            if (StreamingSettings.RenderGeneratedSpawns &&
                catalog != null)
            {
                ChunkSpawnPresenter.Populate(
                    chunkObject.transform,
                    chunkData,
                    catalog,
                    worldOrigin,
                    runtimeDelta);
            }

            activeChunks.Add(
                coordinate,
                view);
        }

        private void UnloadChunk(
            ChunkCoordinate coordinate)
        {
            if (!activeChunks.TryGetValue(
                    coordinate,
                    out StreamedChunkView view))
            {
                return;
            }

            activeChunks.Remove(
                coordinate);

            if (chunkCache != null)
                chunkCache.Unpin(coordinate);

            if (view == null)
                return;

            GameObject chunkObject =
                view.gameObject;

            view.ReleaseOwnedResources();

            if (Application.isPlaying)
                Destroy(chunkObject);
            else
                DestroyImmediate(chunkObject);
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

        private bool IsInsideSessionMacroArea(
            ChunkCoordinate coordinate)
        {
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
                        "WorldStreamer focus is approaching the edge of the " +
                        "fixed session MacroWorldPlan. Remaining macro chunks: " +
                        remaining +
                        ". Current implementation will not silently rebuild " +
                        "roads/rivers while moving. Increase macroPlanRadiusChunks " +
                        "or migrate to fixed macro tiles before production.",
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
