using System.Collections.Generic;

namespace LittleCastle.World
{
    /// <summary>
    /// Client-side cache of authoritative chunk snapshots.
    ///
    /// This cache is deliberately renderer-independent. A client may know a
    /// chunk's state without having any GameObject or mesh loaded for it.
    /// </summary>
    public sealed class WorldClientKnowledgeCache
    {
        private readonly Dictionary<ChunkCoordinate, WorldChunkStateSnapshot>
            snapshots =
                new Dictionary<ChunkCoordinate, WorldChunkStateSnapshot>();

        public int Count => snapshots.Count;

        public bool TryGetSnapshot(
            ChunkCoordinate coordinate,
            out WorldChunkStateSnapshot snapshot)
        {
            return
                snapshots.TryGetValue(
                    coordinate,
                    out snapshot);
        }

        public int GetKnownRevision(
            ChunkCoordinate coordinate)
        {
            if (!snapshots.TryGetValue(
                    coordinate,
                    out WorldChunkStateSnapshot snapshot) ||
                snapshot == null)
            {
                return 0;
            }

            return snapshot.revision;
        }

        public bool ApplySnapshot(
            WorldChunkStateSnapshot snapshot)
        {
            if (snapshot == null)
                return false;

            if (snapshots.TryGetValue(
                    snapshot.chunkCoordinate,
                    out WorldChunkStateSnapshot current) &&
                current != null &&
                current.revision >
                    snapshot.revision)
            {
                return false;
            }

            snapshots[
                snapshot.chunkCoordinate] =
                CloneSnapshot(
                    snapshot);

            return true;
        }

        public void Clear()
        {
            snapshots.Clear();
        }

        public static bool ShouldReceiveDetailedSnapshot(
            WorldInformationMode mode,
            FogOfWarVisibility visibility)
        {
            if (mode ==
                WorldInformationMode.FullMapLive)
            {
                return true;
            }

            return
                visibility ==
                FogOfWarVisibility.Visible;
        }

        private static WorldChunkStateSnapshot CloneSnapshot(
            WorldChunkStateSnapshot source)
        {
            var clone =
                new WorldChunkStateSnapshot
                {
                    chunkCoordinate =
                        source.chunkCoordinate,
                    revision =
                        source.revision
                };

            if (source.removedGeneratedSpawnIds != null)
            {
                clone.removedGeneratedSpawnIds.AddRange(
                    source.removedGeneratedSpawnIds);
            }

            if (source.resourceDeposits != null)
            {
                clone.resourceDeposits.AddRange(
                    source.resourceDeposits);
            }

            if (source.runtimeEntities != null)
            {
                clone.runtimeEntities.AddRange(
                    source.runtimeEntities);
            }

            return clone;
        }
    }
}
