using System;
using System.Collections.Generic;
using UnityEngine;

namespace LittleCastle.World
{
    [Serializable]
    public struct ResourceDepositRuntimeDelta
    {
        public long stableId;
        public ChunkCoordinate chunkCoordinate;
        public int remainingCapacity;

        public ResourceDepositRuntimeDelta(
            long stableId,
            int remainingCapacity)
            : this(
                stableId,
                default,
                remainingCapacity)
        {
        }

        public ResourceDepositRuntimeDelta(
            long stableId,
            ChunkCoordinate chunkCoordinate,
            int remainingCapacity)
        {
            this.stableId = stableId;
            this.chunkCoordinate = chunkCoordinate;
            this.remainingCapacity = remainingCapacity;
        }
    }

    /// <summary>
    /// Authoritative runtime changes layered over deterministic generated data.
    ///
    /// The deterministic seed defines the base world.
    /// This state stores only what changed after session start:
    /// removed generated objects, depleted resources and player/runtime entities.
    ///
    /// Chunk revisions let networking/clients ask only for changed chunk state
    /// without retransmitting generated meshes or the whole world.
    /// </summary>
    [Serializable]
    public sealed class WorldRuntimeDeltaState
    {
        [SerializeField]
        private List<long> removedSpawnIds =
            new List<long>();

        [SerializeField]
        private List<WorldRemovedSpawnChunkRecord> removedSpawnChunks =
            new List<WorldRemovedSpawnChunkRecord>();

        [SerializeField]
        private List<ResourceDepositRuntimeDelta> resourceDeposits =
            new List<ResourceDepositRuntimeDelta>();

        [SerializeField]
        private List<WorldRuntimeEntityState> runtimeEntities =
            new List<WorldRuntimeEntityState>();

        [SerializeField]
        private List<WorldChunkRevisionRecord> chunkRevisions =
            new List<WorldChunkRevisionRecord>();

        [NonSerialized]
        private HashSet<long> removedSpawnIndex;

        [NonSerialized]
        private Dictionary<long, ChunkCoordinate> removedSpawnChunkIndex;

        [NonSerialized]
        private Dictionary<long, int> runtimeEntityIndex;

        [NonSerialized]
        private Dictionary<ChunkCoordinate, int> chunkRevisionIndex;

        public event Action<ChunkCoordinate, int> ChunkRevisionChanged;

        public IReadOnlyList<long> RemovedSpawnIds =>
            removedSpawnIds;

        public IReadOnlyList<ResourceDepositRuntimeDelta> ResourceDeposits =>
            resourceDeposits;

        public IReadOnlyList<WorldRuntimeEntityState> RuntimeEntities =>
            runtimeEntities;

        public IReadOnlyList<WorldChunkRevisionRecord> ChunkRevisions =>
            chunkRevisions;

        public bool IsSpawnRemoved(long stableId)
        {
            EnsureIndexes();

            return
                removedSpawnIndex.Contains(
                    stableId);
        }

        /// <summary>
        /// Legacy/global removal path. Prefer the chunk-aware overload for new
        /// multiplayer/server mutations so a chunk revision can be advanced.
        /// </summary>
        public bool MarkSpawnRemoved(long stableId)
        {
            EnsureIndexes();

            if (!removedSpawnIndex.Add(
                    stableId))
            {
                return false;
            }

            removedSpawnIds.Add(
                stableId);

            return true;
        }

        public bool MarkSpawnRemoved(
            long stableId,
            ChunkCoordinate chunkCoordinate)
        {
            EnsureIndexes();

            bool changed = false;

            if (removedSpawnIndex.Add(
                    stableId))
            {
                removedSpawnIds.Add(
                    stableId);

                changed = true;
            }

            if (!removedSpawnChunkIndex.TryGetValue(
                    stableId,
                    out ChunkCoordinate knownChunk) ||
                knownChunk != chunkCoordinate)
            {
                SetRemovedSpawnChunkRecord(
                    stableId,
                    chunkCoordinate);

                changed = true;
            }

            if (changed)
            {
                IncrementChunkRevision(
                    chunkCoordinate);
            }

            return changed;
        }

        public bool RestoreSpawn(long stableId)
        {
            EnsureIndexes();

            if (!removedSpawnIndex.Remove(
                    stableId))
            {
                return false;
            }

            removedSpawnIds.Remove(
                stableId);

            if (removedSpawnChunkIndex.TryGetValue(
                    stableId,
                    out ChunkCoordinate coordinate))
            {
                RemoveRemovedSpawnChunkRecord(
                    stableId);

                IncrementChunkRevision(
                    coordinate);
            }

            return true;
        }

        public bool RestoreSpawn(
            long stableId,
            ChunkCoordinate chunkCoordinate)
        {
            EnsureIndexes();

            bool changed =
                removedSpawnIndex.Remove(
                    stableId);

            if (changed)
            {
                removedSpawnIds.Remove(
                    stableId);
            }

            if (removedSpawnChunkIndex.ContainsKey(
                    stableId))
            {
                RemoveRemovedSpawnChunkRecord(
                    stableId);

                changed = true;
            }

            if (changed)
            {
                IncrementChunkRevision(
                    chunkCoordinate);
            }

            return changed;
        }

        public int GetRemainingCapacity(
            WorldResourceDepositData generatedDeposit)
        {
            for (int i = 0;
                 i < resourceDeposits.Count;
                 i++)
            {
                if (resourceDeposits[i].stableId ==
                    generatedDeposit.stableId)
                {
                    return
                        Mathf.Max(
                            0,
                            resourceDeposits[i].remainingCapacity);
                }
            }

            return
                Mathf.Max(
                    0,
                    generatedDeposit.capacity);
        }

        /// <summary>
        /// Legacy/global resource update. Prefer the chunk-aware overload for
        /// authoritative multiplayer mutations.
        /// </summary>
        public void SetRemainingCapacity(
            long stableId,
            int remainingCapacity)
        {
            SetRemainingCapacityInternal(
                stableId,
                default,
                remainingCapacity,
                false);
        }

        public void SetRemainingCapacity(
            long stableId,
            ChunkCoordinate chunkCoordinate,
            int remainingCapacity)
        {
            if (SetRemainingCapacityInternal(
                    stableId,
                    chunkCoordinate,
                    remainingCapacity,
                    true))
            {
                IncrementChunkRevision(
                    chunkCoordinate);
            }
        }

        public bool UpsertRuntimeEntity(
            WorldRuntimeEntityState state)
        {
            if (state.runtimeId == 0 ||
                string.IsNullOrWhiteSpace(
                    state.archetypeId))
            {
                return false;
            }

            EnsureIndexes();

            state.uniformScale =
                Mathf.Max(
                    0.01f,
                    state.uniformScale);

            state.level =
                Mathf.Max(
                    1,
                    state.level);

            if (runtimeEntityIndex.TryGetValue(
                    state.runtimeId,
                    out int existingIndex))
            {
                WorldRuntimeEntityState previous =
                    runtimeEntities[existingIndex];

                if (RuntimeEntityEquals(
                        previous,
                        state))
                {
                    return false;
                }

                runtimeEntities[existingIndex] =
                    state;

                if (previous.chunkCoordinate !=
                    state.chunkCoordinate)
                {
                    IncrementChunkRevision(
                        previous.chunkCoordinate);
                }

                IncrementChunkRevision(
                    state.chunkCoordinate);

                return true;
            }

            runtimeEntityIndex.Add(
                state.runtimeId,
                runtimeEntities.Count);

            runtimeEntities.Add(
                state);

            IncrementChunkRevision(
                state.chunkCoordinate);

            return true;
        }

        public bool RemoveRuntimeEntity(
            long runtimeId)
        {
            EnsureIndexes();

            if (!runtimeEntityIndex.TryGetValue(
                    runtimeId,
                    out int index))
            {
                return false;
            }

            WorldRuntimeEntityState removed =
                runtimeEntities[index];

            runtimeEntities.RemoveAt(
                index);

            RebuildRuntimeEntityIndex();

            IncrementChunkRevision(
                removed.chunkCoordinate);

            return true;
        }

        public bool TryGetRuntimeEntity(
            long runtimeId,
            out WorldRuntimeEntityState state)
        {
            EnsureIndexes();

            if (runtimeEntityIndex.TryGetValue(
                    runtimeId,
                    out int index))
            {
                state =
                    runtimeEntities[index];

                return true;
            }

            state = default;
            return false;
        }

        public void GetRuntimeEntitiesForChunk(
            ChunkCoordinate chunkCoordinate,
            List<WorldRuntimeEntityState> output)
        {
            if (output == null)
                throw new ArgumentNullException(nameof(output));

            output.Clear();

            for (int i = 0;
                 i < runtimeEntities.Count;
                 i++)
            {
                WorldRuntimeEntityState state =
                    runtimeEntities[i];

                if (state.chunkCoordinate ==
                    chunkCoordinate)
                {
                    output.Add(
                        state);
                }
            }
        }

        public int GetChunkRevision(
            ChunkCoordinate chunkCoordinate)
        {
            EnsureIndexes();

            if (!chunkRevisionIndex.TryGetValue(
                    chunkCoordinate,
                    out int index))
            {
                return 0;
            }

            return
                Mathf.Max(
                    0,
                    chunkRevisions[index].revision);
        }

        public WorldChunkStateSnapshot BuildChunkSnapshot(
            ChunkCoordinate chunkCoordinate)
        {
            EnsureIndexes();

            var snapshot =
                new WorldChunkStateSnapshot
                {
                    chunkCoordinate =
                        chunkCoordinate,
                    revision =
                        GetChunkRevision(
                            chunkCoordinate)
                };

            for (int i = 0;
                 i < removedSpawnChunks.Count;
                 i++)
            {
                WorldRemovedSpawnChunkRecord record =
                    removedSpawnChunks[i];

                if (record.chunkCoordinate ==
                    chunkCoordinate)
                {
                    snapshot.removedGeneratedSpawnIds.Add(
                        record.stableId);
                }
            }

            for (int i = 0;
                 i < resourceDeposits.Count;
                 i++)
            {
                ResourceDepositRuntimeDelta delta =
                    resourceDeposits[i];

                if (delta.chunkCoordinate ==
                    chunkCoordinate)
                {
                    snapshot.resourceDeposits.Add(
                        delta);
                }
            }

            for (int i = 0;
                 i < runtimeEntities.Count;
                 i++)
            {
                WorldRuntimeEntityState state =
                    runtimeEntities[i];

                if (state.chunkCoordinate ==
                    chunkCoordinate)
                {
                    snapshot.runtimeEntities.Add(
                        state);
                }
            }

            return snapshot;
        }

        public bool ApplyChunkSnapshot(
            WorldChunkStateSnapshot snapshot)
        {
            if (snapshot == null)
                return false;

            EnsureIndexes();

            ChunkCoordinate coordinate =
                snapshot.chunkCoordinate;

            int currentRevision =
                GetChunkRevision(
                    coordinate);

            if (snapshot.revision <
                currentRevision)
            {
                return false;
            }

            bool changed =
                snapshot.revision !=
                currentRevision;

            for (int i = removedSpawnChunks.Count - 1;
                 i >= 0;
                 i--)
            {
                if (removedSpawnChunks[i].chunkCoordinate !=
                    coordinate)
                {
                    continue;
                }

                long stableId =
                    removedSpawnChunks[i].stableId;

                removedSpawnChunks.RemoveAt(
                    i);

                removedSpawnIds.Remove(
                    stableId);

                changed = true;
            }

            for (int i = resourceDeposits.Count - 1;
                 i >= 0;
                 i--)
            {
                if (resourceDeposits[i].chunkCoordinate ==
                    coordinate)
                {
                    resourceDeposits.RemoveAt(
                        i);

                    changed = true;
                }
            }

            for (int i = runtimeEntities.Count - 1;
                 i >= 0;
                 i--)
            {
                if (runtimeEntities[i].chunkCoordinate ==
                    coordinate)
                {
                    runtimeEntities.RemoveAt(
                        i);

                    changed = true;
                }
            }

            if (snapshot.removedGeneratedSpawnIds != null)
            {
                for (int i = 0;
                     i < snapshot.removedGeneratedSpawnIds.Count;
                     i++)
                {
                    long stableId =
                        snapshot.removedGeneratedSpawnIds[i];

                    if (!removedSpawnIds.Contains(
                            stableId))
                    {
                        removedSpawnIds.Add(
                            stableId);
                    }

                    removedSpawnChunks.Add(
                        new WorldRemovedSpawnChunkRecord(
                            stableId,
                            coordinate));
                }
            }

            if (snapshot.resourceDeposits != null)
            {
                for (int i = 0;
                     i < snapshot.resourceDeposits.Count;
                     i++)
                {
                    ResourceDepositRuntimeDelta delta =
                        snapshot.resourceDeposits[i];

                    delta.chunkCoordinate =
                        coordinate;

                    resourceDeposits.Add(
                        delta);
                }
            }

            if (snapshot.runtimeEntities != null)
            {
                for (int i = 0;
                     i < snapshot.runtimeEntities.Count;
                     i++)
                {
                    WorldRuntimeEntityState state =
                        snapshot.runtimeEntities[i];

                    state.chunkCoordinate =
                        coordinate;

                    runtimeEntities.Add(
                        state);
                }
            }

            SetChunkRevisionExact(
                coordinate,
                snapshot.revision);

            RebuildIndexes();

            if (changed)
            {
                ChunkRevisionChanged?.Invoke(
                    coordinate,
                    Mathf.Max(
                        0,
                        snapshot.revision));
            }

            return changed;
        }

        public void RebuildIndexes()
        {
            removedSpawnIndex =
                new HashSet<long>(
                    removedSpawnIds);

            removedSpawnChunkIndex =
                new Dictionary<long, ChunkCoordinate>();

            for (int i = 0;
                 i < removedSpawnChunks.Count;
                 i++)
            {
                WorldRemovedSpawnChunkRecord record =
                    removedSpawnChunks[i];

                removedSpawnChunkIndex[
                    record.stableId] =
                    record.chunkCoordinate;
            }

            RebuildRuntimeEntityIndex();

            chunkRevisionIndex =
                new Dictionary<ChunkCoordinate, int>();

            for (int i = 0;
                 i < chunkRevisions.Count;
                 i++)
            {
                chunkRevisionIndex[
                    chunkRevisions[i].chunkCoordinate] =
                    i;
            }
        }

        private bool SetRemainingCapacityInternal(
            long stableId,
            ChunkCoordinate chunkCoordinate,
            int remainingCapacity,
            bool useChunkCoordinate)
        {
            int safeCapacity =
                Mathf.Max(
                    0,
                    remainingCapacity);

            for (int i = 0;
                 i < resourceDeposits.Count;
                 i++)
            {
                if (resourceDeposits[i].stableId !=
                    stableId)
                {
                    continue;
                }

                ResourceDepositRuntimeDelta previous =
                    resourceDeposits[i];

                ChunkCoordinate storedChunk =
                    useChunkCoordinate
                        ? chunkCoordinate
                        : previous.chunkCoordinate;

                if (previous.remainingCapacity ==
                        safeCapacity &&
                    previous.chunkCoordinate ==
                        storedChunk)
                {
                    return false;
                }

                resourceDeposits[i] =
                    new ResourceDepositRuntimeDelta(
                        stableId,
                        storedChunk,
                        safeCapacity);

                return true;
            }

            resourceDeposits.Add(
                new ResourceDepositRuntimeDelta(
                    stableId,
                    useChunkCoordinate
                        ? chunkCoordinate
                        : default,
                    safeCapacity));

            return true;
        }

        private void SetChunkRevisionExact(
            ChunkCoordinate chunkCoordinate,
            int revision)
        {
            EnsureIndexes();

            int safeRevision =
                Mathf.Max(
                    0,
                    revision);

            if (chunkRevisionIndex.TryGetValue(
                    chunkCoordinate,
                    out int index))
            {
                chunkRevisions[index] =
                    new WorldChunkRevisionRecord(
                        chunkCoordinate,
                        safeRevision);

                return;
            }

            chunkRevisionIndex.Add(
                chunkCoordinate,
                chunkRevisions.Count);

            chunkRevisions.Add(
                new WorldChunkRevisionRecord(
                    chunkCoordinate,
                    safeRevision));
        }

        private int IncrementChunkRevision(
            ChunkCoordinate chunkCoordinate)
        {
            EnsureIndexes();

            int revision;

            if (chunkRevisionIndex.TryGetValue(
                    chunkCoordinate,
                    out int index))
            {
                WorldChunkRevisionRecord record =
                    chunkRevisions[index];

                revision =
                    Mathf.Max(
                        0,
                        record.revision) +
                    1;

                chunkRevisions[index] =
                    new WorldChunkRevisionRecord(
                        chunkCoordinate,
                        revision);
            }
            else
            {
                revision = 1;

                chunkRevisionIndex.Add(
                    chunkCoordinate,
                    chunkRevisions.Count);

                chunkRevisions.Add(
                    new WorldChunkRevisionRecord(
                        chunkCoordinate,
                        revision));
            }

            ChunkRevisionChanged?.Invoke(
                chunkCoordinate,
                revision);

            return revision;
        }

        private void SetRemovedSpawnChunkRecord(
            long stableId,
            ChunkCoordinate chunkCoordinate)
        {
            for (int i = 0;
                 i < removedSpawnChunks.Count;
                 i++)
            {
                if (removedSpawnChunks[i].stableId !=
                    stableId)
                {
                    continue;
                }

                removedSpawnChunks[i] =
                    new WorldRemovedSpawnChunkRecord(
                        stableId,
                        chunkCoordinate);

                removedSpawnChunkIndex[
                    stableId] =
                    chunkCoordinate;

                return;
            }

            removedSpawnChunks.Add(
                new WorldRemovedSpawnChunkRecord(
                    stableId,
                    chunkCoordinate));

            removedSpawnChunkIndex[
                stableId] =
                chunkCoordinate;
        }

        private void RemoveRemovedSpawnChunkRecord(
            long stableId)
        {
            for (int i = 0;
                 i < removedSpawnChunks.Count;
                 i++)
            {
                if (removedSpawnChunks[i].stableId !=
                    stableId)
                {
                    continue;
                }

                removedSpawnChunks.RemoveAt(
                    i);

                break;
            }

            removedSpawnChunkIndex.Remove(
                stableId);
        }

        private void RebuildRuntimeEntityIndex()
        {
            runtimeEntityIndex =
                new Dictionary<long, int>();

            for (int i = 0;
                 i < runtimeEntities.Count;
                 i++)
            {
                runtimeEntityIndex[
                    runtimeEntities[i].runtimeId] =
                    i;
            }
        }

        private void EnsureIndexes()
        {
            if (removedSpawnIndex == null ||
                removedSpawnChunkIndex == null ||
                runtimeEntityIndex == null ||
                chunkRevisionIndex == null)
            {
                RebuildIndexes();
            }
        }

        private static bool RuntimeEntityEquals(
            WorldRuntimeEntityState a,
            WorldRuntimeEntityState b)
        {
            return
                a.runtimeId ==
                    b.runtimeId &&
                a.chunkCoordinate ==
                    b.chunkCoordinate &&
                a.archetypeId ==
                    b.archetypeId &&
                a.category ==
                    b.category &&
                a.worldPosition ==
                    b.worldPosition &&
                Mathf.Approximately(
                    a.yawDegrees,
                    b.yawDegrees) &&
                Mathf.Approximately(
                    a.uniformScale,
                    b.uniformScale) &&
                a.ownerPlayerId ==
                    b.ownerPlayerId &&
                a.level ==
                    b.level &&
                a.stateFlags ==
                    b.stateFlags;
        }
    }
}
