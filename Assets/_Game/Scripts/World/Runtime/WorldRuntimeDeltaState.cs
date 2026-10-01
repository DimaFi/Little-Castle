using System;
using System.Collections.Generic;
using UnityEngine;

namespace LittleCastle.World
{
    [Serializable]
    public struct ResourceDepositRuntimeDelta
    {
        public long stableId;
        public int remainingCapacity;

        public ResourceDepositRuntimeDelta(
            long stableId,
            int remainingCapacity)
        {
            this.stableId = stableId;
            this.remainingCapacity = remainingCapacity;
        }
    }

    /// <summary>
    /// Runtime changes layered over deterministic generated world data.
    ///
    /// This is intentionally small and serialization-friendly. A full save
    /// system can wrap/version this structure later.
    /// </summary>
    [Serializable]
    public sealed class WorldRuntimeDeltaState
    {
        [SerializeField]
        private List<long> removedSpawnIds =
            new List<long>();

        [SerializeField]
        private List<ResourceDepositRuntimeDelta> resourceDeposits =
            new List<ResourceDepositRuntimeDelta>();

        [NonSerialized]
        private HashSet<long> removedSpawnIndex;

        public IReadOnlyList<long> RemovedSpawnIds =>
            removedSpawnIds;

        public IReadOnlyList<ResourceDepositRuntimeDelta> ResourceDeposits =>
            resourceDeposits;

        public bool IsSpawnRemoved(long stableId)
        {
            EnsureIndexes();
            return removedSpawnIndex.Contains(stableId);
        }

        public bool MarkSpawnRemoved(long stableId)
        {
            EnsureIndexes();

            if (!removedSpawnIndex.Add(stableId))
                return false;

            removedSpawnIds.Add(stableId);
            return true;
        }

        public bool RestoreSpawn(long stableId)
        {
            EnsureIndexes();

            if (!removedSpawnIndex.Remove(stableId))
                return false;

            removedSpawnIds.Remove(stableId);
            return true;
        }

        public int GetRemainingCapacity(
            WorldResourceDepositData generatedDeposit)
        {
            for (int i = 0; i < resourceDeposits.Count; i++)
            {
                if (resourceDeposits[i].stableId ==
                    generatedDeposit.stableId)
                {
                    return Mathf.Max(
                        0,
                        resourceDeposits[i].remainingCapacity);
                }
            }

            return Mathf.Max(
                0,
                generatedDeposit.capacity);
        }

        public void SetRemainingCapacity(
            long stableId,
            int remainingCapacity)
        {
            int safeCapacity =
                Mathf.Max(0, remainingCapacity);

            for (int i = 0; i < resourceDeposits.Count; i++)
            {
                if (resourceDeposits[i].stableId != stableId)
                    continue;

                resourceDeposits[i] =
                    new ResourceDepositRuntimeDelta(
                        stableId,
                        safeCapacity);

                return;
            }

            resourceDeposits.Add(
                new ResourceDepositRuntimeDelta(
                    stableId,
                    safeCapacity));
        }

        public void RebuildIndexes()
        {
            removedSpawnIndex =
                new HashSet<long>(
                    removedSpawnIds);
        }

        private void EnsureIndexes()
        {
            if (removedSpawnIndex == null)
                RebuildIndexes();
        }
    }
}
