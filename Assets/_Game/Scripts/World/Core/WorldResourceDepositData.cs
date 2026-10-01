using System;
using UnityEngine;

namespace LittleCastle.World
{
    /// <summary>
    /// Authoritative generated resource deposit.
    /// Visual rocks/ore meshes are presentation and may be changed independently.
    /// </summary>
    [Serializable]
    public struct WorldResourceDepositData
    {
        public long stableId;
        public ResourceKind resourceKind;
        public string visualArchetypeId;
        public Vector3 worldPosition;
        public float radius;
        public float richness;
        public int capacity;

        public WorldResourceDepositData(
            long stableId,
            ResourceKind resourceKind,
            string visualArchetypeId,
            Vector3 worldPosition,
            float radius,
            float richness,
            int capacity)
        {
            this.stableId = stableId;
            this.resourceKind = resourceKind;
            this.visualArchetypeId = visualArchetypeId;
            this.worldPosition = worldPosition;
            this.radius = radius;
            this.richness = richness;
            this.capacity = capacity;
        }
    }
}
