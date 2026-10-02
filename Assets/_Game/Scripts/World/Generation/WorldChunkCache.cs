using System;
using System.Collections.Generic;

namespace LittleCastle.World
{
    /// <summary>
    /// In-memory LRU cache for deterministic generated base chunk data.
    ///
    /// The cache owns data only. Streamed GameObjects/meshes are managed by
    /// presentation code. Evicting a chunk is safe because the same seed and
    /// generation version can regenerate the same base chunk.
    ///
    /// A cache can optionally stop generation at an earlier pipeline phase.
    /// This is used by finite-map visual padding to create terrain without
    /// gameplay resources/spawns outside playable bounds.
    /// </summary>
    public sealed class WorldChunkCache
    {
        private readonly Dictionary<ChunkCoordinate, WorldChunkData> chunks =
            new Dictionary<ChunkCoordinate, WorldChunkData>();

        private readonly LinkedList<ChunkCoordinate> usageOrder =
            new LinkedList<ChunkCoordinate>();

        private readonly Dictionary<ChunkCoordinate, LinkedListNode<ChunkCoordinate>>
            usageNodes =
                new Dictionary<ChunkCoordinate, LinkedListNode<ChunkCoordinate>>();

        private readonly HashSet<ChunkCoordinate> pinned =
            new HashSet<ChunkCoordinate>();

        private readonly WorldGenerationPipeline pipeline;
        private readonly int worldSeed;
        private readonly WorldGenerationStagePhase maximumPhase;

        private int maxEntries;

        public int Count => chunks.Count;

        /// <summary>
        /// 0 means unlimited.
        /// </summary>
        public int MaxEntries => maxEntries;

        public WorldGenerationStagePhase MaximumPhase =>
            maximumPhase;

        public WorldChunkCache(
            WorldGenerationPipeline pipeline,
            int worldSeed,
            int maxEntries = 0,
            WorldGenerationStagePhase maximumPhase =
                WorldGenerationStagePhase.PostProcess)
        {
            this.pipeline =
                pipeline ??
                throw new ArgumentNullException(nameof(pipeline));

            this.worldSeed = worldSeed;
            this.maxEntries = Math.Max(0, maxEntries);
            this.maximumPhase = maximumPhase;
        }

        public WorldChunkData GetOrGenerate(
            ChunkCoordinate coordinate)
        {
            if (chunks.TryGetValue(
                coordinate,
                out WorldChunkData existing))
            {
                Touch(coordinate);
                return existing;
            }

            WorldChunkData generated =
                Generate(
                    coordinate);

            chunks.Add(
                coordinate,
                generated);

            AddUsageNode(
                coordinate);

            TrimToCapacity();

            return generated;
        }

        /// <summary>
        /// Gets/generates a chunk and pins it before capacity trimming.
        /// Use this when the caller is about to make the chunk actively visible.
        /// </summary>
        public WorldChunkData GetOrGeneratePinned(
            ChunkCoordinate coordinate)
        {
            if (chunks.TryGetValue(
                coordinate,
                out WorldChunkData existing))
            {
                pinned.Add(coordinate);
                Touch(coordinate);
                return existing;
            }

            WorldChunkData generated =
                Generate(
                    coordinate);

            chunks.Add(
                coordinate,
                generated);

            AddUsageNode(
                coordinate);

            pinned.Add(
                coordinate);

            TrimToCapacity();

            return generated;
        }

        public bool Contains(
            ChunkCoordinate coordinate)
        {
            return chunks.ContainsKey(
                coordinate);
        }

        public bool TryGet(
            ChunkCoordinate coordinate,
            out WorldChunkData chunk)
        {
            if (chunks.TryGetValue(
                coordinate,
                out chunk))
            {
                Touch(coordinate);
                return true;
            }

            return false;
        }

        public void StoreGenerated(
            WorldChunkData chunk)
        {
            if (chunk == null)
                throw new ArgumentNullException(nameof(chunk));

            ChunkCoordinate coordinate =
                chunk.Coordinate;

            if (chunks.ContainsKey(
                    coordinate))
            {
                Touch(
                    coordinate);

                return;
            }

            chunks.Add(
                coordinate,
                chunk);

            AddUsageNode(
                coordinate);

            TrimToCapacity();
        }

        public void SetCapacity(int capacity)
        {
            maxEntries =
                Math.Max(
                    0,
                    capacity);

            TrimToCapacity();
        }

        public void Pin(ChunkCoordinate coordinate)
        {
            if (!chunks.ContainsKey(coordinate))
                return;

            pinned.Add(coordinate);
            Touch(coordinate);
        }

        public void Unpin(ChunkCoordinate coordinate)
        {
            if (!pinned.Remove(coordinate))
                return;

            TrimToCapacity();
        }

        public bool IsPinned(ChunkCoordinate coordinate)
        {
            return pinned.Contains(coordinate);
        }

        public bool Remove(
            ChunkCoordinate coordinate)
        {
            if (!chunks.Remove(coordinate))
                return false;

            pinned.Remove(coordinate);

            if (usageNodes.TryGetValue(
                coordinate,
                out LinkedListNode<ChunkCoordinate> node))
            {
                usageOrder.Remove(node);
                usageNodes.Remove(coordinate);
            }

            return true;
        }

        public void Clear()
        {
            chunks.Clear();
            usageOrder.Clear();
            usageNodes.Clear();
            pinned.Clear();
        }

        private WorldChunkData Generate(
            ChunkCoordinate coordinate)
        {
            return
                pipeline.GenerateChunkThroughPhase(
                    worldSeed,
                    coordinate,
                    maximumPhase);
        }

        private void Touch(
            ChunkCoordinate coordinate)
        {
            if (!usageNodes.TryGetValue(
                coordinate,
                out LinkedListNode<ChunkCoordinate> node))
            {
                AddUsageNode(coordinate);
                return;
            }

            usageOrder.Remove(node);
            usageOrder.AddLast(node);
        }

        private void AddUsageNode(
            ChunkCoordinate coordinate)
        {
            var node =
                usageOrder.AddLast(
                    coordinate);

            usageNodes[coordinate] =
                node;
        }

        private void TrimToCapacity()
        {
            if (maxEntries <= 0)
                return;

            while (chunks.Count > maxEntries &&
                   usageOrder.First != null)
            {
                LinkedListNode<ChunkCoordinate> candidate =
                    usageOrder.First;

                while (candidate != null &&
                       pinned.Contains(candidate.Value))
                {
                    candidate = candidate.Next;
                }

                if (candidate == null)
                {
                    // All remaining cached chunks are actively pinned.
                    // Capacity becomes a soft limit until some are unpinned.
                    break;
                }

                ChunkCoordinate oldest =
                    candidate.Value;

                usageOrder.Remove(candidate);
                usageNodes.Remove(oldest);
                chunks.Remove(oldest);
            }
        }
    }
}
