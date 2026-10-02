using UnityEngine;

namespace LittleCastle.World
{
    /// <summary>
    /// Runtime chunk-streaming policy.
    ///
    /// This asset controls presentation/loading behavior only.
    /// It does not change deterministic generation rules.
    /// </summary>
    [CreateAssetMenu(
        fileName = "WorldStreamingSettings",
        menuName = "Little Castle/World/World Streaming Settings")]
    public sealed class WorldStreamingSettings : ScriptableObject
    {
        [Header("Chunk visibility")]
        [Min(0)]
        [SerializeField] private int loadRadiusChunks = 3;

        [Min(0)]
        [SerializeField] private int unloadPaddingChunks = 1;

        [Tooltip(
            "If enabled, chunks are requested using a circular radius. " +
            "If disabled, the requested area is square.")]
        [SerializeField] private bool circularLoading = true;

        [Header("Per-frame budget")]
        [Min(1)]
        [SerializeField] private int maxChunkLoadsPerFrame = 1;

        [Min(1)]
        [SerializeField] private int maxChunkUnloadsPerFrame = 2;

        [Header("Cooperative generation / prefetch")]
        [Tooltip(
            "Extra data-only ring generated outside the visible radius. " +
            "These chunks are cached but not presented until needed.")]
        [Min(0)]
        [SerializeField] private int prefetchPaddingChunks = 2;

        [Tooltip(
            "Maximum generation stages executed per frame while visible chunks " +
            "are waiting for data. Stages still run on the main thread.")]
        [Min(1)]
        [SerializeField] private int urgentGenerationStagesPerFrame = 2;

        [Tooltip(
            "Maximum generation stages executed per frame for data-only " +
            "background prefetch after urgent visible work is satisfied.")]
        [Min(1)]
        [SerializeField] private int backgroundGenerationStagesPerFrame = 1;

        [Tooltip(
            "Background prefetch pauses when the previous frame exceeded this " +
            "time. Urgent visible generation is not blocked by this limit.")]
        [Min(1f)]
        [SerializeField] private float backgroundPrefetchFrameLimitMs = 18f;

        [Header("Generated-data cache")]
        [Tooltip(
            "Maximum generated chunk-data entries kept in memory. " +
            "0 means unlimited. Eviction is deterministic-safe because " +
            "evicted chunks regenerate from the same seed.")]
        [Min(0)]
        [SerializeField] private int maxCachedChunks = 128;

        [Header("Session macro plan")]
        [Tooltip(
            "Current macro generation is bounded. The streamer therefore builds " +
            "one fixed macro plan around the initial focus for the session. " +
            "This radius must be comfortably larger than loadRadiusChunks.")]
        [Min(4)]
        [SerializeField] private int macroPlanRadiusChunks = 48;

        [Tooltip(
            "How many chunks before the fixed macro-plan edge should trigger " +
            "a warning. The current streamer deliberately does not silently " +
            "replan macro roads/rivers while the player is moving.")]
        [Min(0)]
        [SerializeField] private int macroEdgeWarningChunks = 6;

        [Header("Presentation")]
        [SerializeField] private bool renderGeneratedSpawns = true;

        [Tooltip(
            "MeshColliders are useful for camera ground following and nearby " +
            "gameplay, but expensive across the whole visible map.")]
        [SerializeField] private bool addMeshCollider = false;

        [Tooltip(
            "Only playable chunks inside this radius around the streaming focus " +
            "keep an active terrain MeshCollider. Visible distant chunks remain " +
            "render-only. 0 means only the focus chunk.")]
        [Min(0)]
        [SerializeField] private int colliderRadiusChunks = 1;

        public int LoadRadiusChunks =>
            Mathf.Max(0, loadRadiusChunks);

        public int UnloadRadiusChunks =>
            LoadRadiusChunks +
            Mathf.Max(0, unloadPaddingChunks);

        public bool CircularLoading => circularLoading;

        public int MaxChunkLoadsPerFrame =>
            Mathf.Max(1, maxChunkLoadsPerFrame);

        public int MaxChunkUnloadsPerFrame =>
            Mathf.Max(1, maxChunkUnloadsPerFrame);

        public int PrefetchPaddingChunks =>
            Mathf.Max(
                0,
                prefetchPaddingChunks);

        public int PrefetchRadiusChunks =>
            LoadRadiusChunks +
            PrefetchPaddingChunks;

        public int UrgentGenerationStagesPerFrame =>
            Mathf.Max(
                1,
                urgentGenerationStagesPerFrame);

        public int BackgroundGenerationStagesPerFrame =>
            Mathf.Max(
                1,
                backgroundGenerationStagesPerFrame);

        public float BackgroundPrefetchFrameLimitMs =>
            Mathf.Max(
                1f,
                backgroundPrefetchFrameLimitMs);

        public int MaxCachedChunks =>
            Mathf.Max(0, maxCachedChunks);

        public int MacroPlanRadiusChunks =>
            Mathf.Max(
                PrefetchRadiusChunks + 2,
                macroPlanRadiusChunks);

        public int MacroEdgeWarningChunks =>
            Mathf.Max(0, macroEdgeWarningChunks);

        public bool RenderGeneratedSpawns =>
            renderGeneratedSpawns;

        public bool AddMeshCollider =>
            addMeshCollider;

        public int ColliderRadiusChunks =>
            Mathf.Clamp(
                colliderRadiusChunks,
                0,
                LoadRadiusChunks);
    }
}
