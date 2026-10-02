using System;
using System.Collections.Generic;
using LittleCastle.Building;
using LittleCastle.Gameplay;

namespace LittleCastle.World
{
    /// <summary>
    /// Transport-agnostic authoritative world-state facade.
    ///
    /// A dedicated server/host owns one instance. Clients should receive
    /// revisions/snapshots; they must not treat rendered GameObjects as truth.
    /// </summary>
    public sealed class WorldStateAuthority
    {
        private readonly WorldRuntimeDeltaState state;
        private readonly WallRuntimeRegistry walls =
            new WallRuntimeRegistry();

        private readonly GameplaySimulationAuthority gameplay;

        public WorldRuntimeDeltaState State => state;
        public WallRuntimeRegistry Walls => walls;
        public GameplaySimulationAuthority Gameplay => gameplay;

        public WorldStateAuthority(
            WorldRuntimeDeltaState state = null,
            GameplaySessionState gameplayState = null,
            GameplayRules gameplayRules = null)
        {
            this.state =
                state ??
                new WorldRuntimeDeltaState();

            this.state.RebuildIndexes();

            gameplay =
                new GameplaySimulationAuthority(
                    gameplayState ??
                    new GameplaySessionState(),
                    gameplayRules);
        }

        public int GetChunkRevision(
            ChunkCoordinate coordinate)
        {
            return
                state.GetChunkRevision(
                    coordinate);
        }

        public WorldChunkStateSnapshot BuildChunkSnapshot(
            ChunkCoordinate coordinate)
        {
            return
                state.BuildChunkSnapshot(
                    coordinate);
        }

        public void BuildRevisionManifest(
            List<WorldChunkRevisionRecord> output)
        {
            if (output == null)
                throw new ArgumentNullException(nameof(output));

            output.Clear();

            IReadOnlyList<WorldChunkRevisionRecord> revisions =
                state.ChunkRevisions;

            for (int i = 0;
                 i < revisions.Count;
                 i++)
            {
                output.Add(
                    revisions[i]);
            }
        }

        public bool RemoveGeneratedSpawn(
            ChunkCoordinate coordinate,
            long stableId)
        {
            return
                state.MarkSpawnRemoved(
                    stableId,
                    coordinate);
        }

        public bool RestoreGeneratedSpawn(
            ChunkCoordinate coordinate,
            long stableId)
        {
            return
                state.RestoreSpawn(
                    stableId,
                    coordinate);
        }

        public void SetResourceCapacity(
            ChunkCoordinate coordinate,
            long stableId,
            int remainingCapacity)
        {
            state.SetRemainingCapacity(
                stableId,
                coordinate,
                remainingCapacity);
        }

        public bool UpsertRuntimeEntity(
            WorldRuntimeEntityState entity)
        {
            return
                state.UpsertRuntimeEntity(
                    entity);
        }

        public bool RemoveRuntimeEntity(
            long runtimeId)
        {
            return
                state.RemoveRuntimeEntity(
                    runtimeId);
        }

        public bool UpsertWall(
            WallRuntimeState wall)
        {
            return
                walls.Upsert(
                    wall);
        }

        public bool RemoveWall(
            long wallId)
        {
            return
                walls.Remove(
                    wallId);
        }
    }
}
