using System.Collections.Generic;

namespace LittleCastle.World
{
    /// <summary>
    /// In-memory cache for generated base chunk data.
    ///
    /// This is intentionally independent from GameObjects. A future
    /// WorldStreamer can use this cache while managing visual chunk pooling.
    /// </summary>
    public sealed class WorldChunkCache
    {
        private readonly Dictionary<ChunkCoordinate, WorldChunkData> chunks =
            new Dictionary<ChunkCoordinate, WorldChunkData>();

        private readonly WorldGenerationPipeline pipeline;
        private readonly int worldSeed;

        public int Count => chunks.Count;

        public WorldChunkCache(
            WorldGenerationPipeline pipeline,
            int worldSeed)
        {
            this.pipeline = pipeline;
            this.worldSeed = worldSeed;
        }

        public WorldChunkData GetOrGenerate(
            ChunkCoordinate coordinate)
        {
            if (chunks.TryGetValue(
                coordinate,
                out WorldChunkData existing))
            {
                return existing;
            }

            WorldChunkData generated =
                pipeline.GenerateChunk(
                    worldSeed,
                    coordinate);

            chunks.Add(coordinate, generated);
            return generated;
        }

        public bool TryGet(
            ChunkCoordinate coordinate,
            out WorldChunkData chunk)
        {
            return chunks.TryGetValue(
                coordinate,
                out chunk);
        }

        public bool Remove(ChunkCoordinate coordinate)
        {
            return chunks.Remove(coordinate);
        }

        public void Clear()
        {
            chunks.Clear();
        }
    }
}
