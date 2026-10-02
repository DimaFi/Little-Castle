using System;
using System.Collections.Generic;
using UnityEngine;

namespace LittleCastle.World
{
    public enum WorldSimulationTier : byte
    {
        Active = 0,
        Warm = 1,
        Sleeping = 2
    }

    /// <summary>
    /// Server-side simulation interest policy.
    ///
    /// This does not control rendering. It answers how frequently gameplay
    /// systems should simulate a chunk based on distance to any player.
    /// </summary>
    [Serializable]
    public sealed class WorldSimulationLodPolicy
    {
        [Min(0)]
        public int activeRadiusChunks = 2;

        [Min(0)]
        public int warmRadiusChunks = 6;

        [Min(0.02f)]
        public float activeTickSeconds = 0.1f;

        [Min(0.1f)]
        public float warmTickSeconds = 1f;

        [Min(1f)]
        public float sleepingTickSeconds = 10f;

        public WorldSimulationTier Evaluate(
            ChunkCoordinate chunk,
            IReadOnlyCollection<ChunkCoordinate> playerChunks)
        {
            if (playerChunks == null ||
                playerChunks.Count == 0)
            {
                return
                    WorldSimulationTier.Sleeping;
            }

            int activeRadius =
                Mathf.Max(
                    0,
                    activeRadiusChunks);

            int warmRadius =
                Mathf.Max(
                    activeRadius,
                    warmRadiusChunks);

            int activeSqr =
                activeRadius *
                activeRadius;

            int warmSqr =
                warmRadius *
                warmRadius;

            foreach (
                ChunkCoordinate playerChunk
                in playerChunks)
            {
                int dx =
                    chunk.x -
                    playerChunk.x;

                int dz =
                    chunk.z -
                    playerChunk.z;

                int distanceSqr =
                    dx * dx +
                    dz * dz;

                if (distanceSqr <=
                    activeSqr)
                {
                    return
                        WorldSimulationTier.Active;
                }

                if (distanceSqr <=
                    warmSqr)
                {
                    return
                        WorldSimulationTier.Warm;
                }
            }

            return
                WorldSimulationTier.Sleeping;
        }

        public float GetTickIntervalSeconds(
            WorldSimulationTier tier)
        {
            switch (tier)
            {
                case WorldSimulationTier.Active:
                    return
                        Mathf.Max(
                            0.02f,
                            activeTickSeconds);

                case WorldSimulationTier.Warm:
                    return
                        Mathf.Max(
                            0.1f,
                            warmTickSeconds);

                default:
                    return
                        Mathf.Max(
                            1f,
                            sleepingTickSeconds);
            }
        }
    }

    /// <summary>
    /// Minimal transport-agnostic registry for server player interest.
    ///
    /// Dedicated-server gameplay systems can register each player's current
    /// chunk and evaluate simulation tier without loading any client visuals.
    /// </summary>
    public sealed class WorldServerInterestRegistry
    {
        private readonly Dictionary<int, ChunkCoordinate> playerChunks =
            new Dictionary<int, ChunkCoordinate>();

        private readonly List<ChunkCoordinate> scratch =
            new List<ChunkCoordinate>();

        public int PlayerCount =>
            playerChunks.Count;

        public void SetPlayerChunk(
            int playerId,
            ChunkCoordinate chunk)
        {
            playerChunks[
                playerId] =
                chunk;
        }

        public bool RemovePlayer(
            int playerId)
        {
            return
                playerChunks.Remove(
                    playerId);
        }

        public WorldSimulationTier EvaluateTier(
            ChunkCoordinate chunk,
            WorldSimulationLodPolicy policy)
        {
            if (policy == null)
                throw new ArgumentNullException(nameof(policy));

            scratch.Clear();

            foreach (
                KeyValuePair<int, ChunkCoordinate> pair
                in playerChunks)
            {
                scratch.Add(
                    pair.Value);
            }

            return
                policy.Evaluate(
                    chunk,
                    scratch);
        }
    }
}
