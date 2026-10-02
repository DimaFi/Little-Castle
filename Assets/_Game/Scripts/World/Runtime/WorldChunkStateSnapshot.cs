using System;
using System.Collections.Generic;
using UnityEngine;

namespace LittleCastle.World
{
    [Serializable]
    public struct WorldRemovedSpawnChunkRecord
    {
        public long stableId;
        public ChunkCoordinate chunkCoordinate;

        public WorldRemovedSpawnChunkRecord(
            long stableId,
            ChunkCoordinate chunkCoordinate)
        {
            this.stableId = stableId;
            this.chunkCoordinate = chunkCoordinate;
        }
    }

    [Serializable]
    public struct WorldChunkRevisionRecord
    {
        public ChunkCoordinate chunkCoordinate;
        public int revision;

        public WorldChunkRevisionRecord(
            ChunkCoordinate chunkCoordinate,
            int revision)
        {
            this.chunkCoordinate = chunkCoordinate;
            this.revision = Mathf.Max(0, revision);
        }
    }

    [Serializable]
    public struct WorldRuntimeEntityState
    {
        public long runtimeId;
        public ChunkCoordinate chunkCoordinate;
        public string archetypeId;
        public SpawnCategory category;
        public Vector3 worldPosition;
        public float yawDegrees;
        public float uniformScale;
        public int ownerPlayerId;
        public int level;
        public int stateFlags;

        public WorldRuntimeEntityState(
            long runtimeId,
            ChunkCoordinate chunkCoordinate,
            string archetypeId,
            SpawnCategory category,
            Vector3 worldPosition,
            float yawDegrees,
            float uniformScale,
            int ownerPlayerId = 0,
            int level = 1,
            int stateFlags = 0)
        {
            this.runtimeId = runtimeId;
            this.chunkCoordinate = chunkCoordinate;
            this.archetypeId = archetypeId;
            this.category = category;
            this.worldPosition = worldPosition;
            this.yawDegrees = yawDegrees;
            this.uniformScale = Mathf.Max(0.01f, uniformScale);
            this.ownerPlayerId = ownerPlayerId;
            this.level = Mathf.Max(1, level);
            this.stateFlags = stateFlags;
        }

        public WorldSpawnData ToSpawnData()
        {
            return
                new WorldSpawnData(
                    runtimeId,
                    archetypeId,
                    category,
                    worldPosition,
                    yawDegrees,
                    Mathf.Max(
                        0.01f,
                        uniformScale));
        }
    }

    /// <summary>
    /// Compact authoritative delta snapshot for one chunk.
    ///
    /// Networking can serialize/transport this object without sending meshes,
    /// GameObjects or the deterministic base generation again.
    /// </summary>
    [Serializable]
    public sealed class WorldChunkStateSnapshot
    {
        public ChunkCoordinate chunkCoordinate;
        public int revision;

        public List<long> removedGeneratedSpawnIds =
            new List<long>();

        public List<ResourceDepositRuntimeDelta> resourceDeposits =
            new List<ResourceDepositRuntimeDelta>();

        public List<WorldRuntimeEntityState> runtimeEntities =
            new List<WorldRuntimeEntityState>();
    }
}
