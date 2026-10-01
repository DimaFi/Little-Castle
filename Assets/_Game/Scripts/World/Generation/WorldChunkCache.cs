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

        private readonly WorldGenerationPipeline pipeline;
        private readonly int worldSeed;

        private int maxEntries;

        public int Count => chunks.Count;

        /// <summary>
        /// 0 means unlimited.
        /// </summary>
        public int MaxEntries => maxEntries;

        public WorldChunkCache(
            WorldGenerationPipeline pipeline,
            int worldSeed,
            int maxEntries = 0)
        {
            this.pipeline =
                pipeline ??
                throw new ArgumentNullException(nameof(pipeline));

            this.worldSeed = worldSeed;
            this.maxEntries = Math.Max(0, maxEntries);
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
                pipeline.GenerateChunk(
                    worldSeed,
                    coordinate);

            chunks.Add(
                coordinate,
                generated);

            AddUsageNode(
                coordinate);

            TrimToCapacity();

            return generated;
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

        public void SetCapacity(int capacity)
        {
            maxEntries =
                Math.Max(
                    0,
                    capacity);

            TrimToCapacity();
        }

        public bool Remove(
            ChunkCoordinate coordinate)
        {
            if (!chunks.Remove(coordinate))
                return false;

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
                ChunkCoordinate oldest =
                    usageOrder.First.Value;

                usageOrder.RemoveFirst();
                usageNodes.Remove(oldest);
                chunks.Remove(oldest);
            }
        }
    }
}
