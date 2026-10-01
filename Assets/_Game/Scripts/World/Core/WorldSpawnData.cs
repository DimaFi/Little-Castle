using System;
using UnityEngine;

namespace LittleCastle.World
{
    /// <summary>
    /// Authoritative placement record for a generated world object.
    ///
    /// archetypeId is resolved to a prefab only by the presentation layer.
    /// stableId is used by saves/runtime deltas (for example: a chopped tree).
    /// </summary>
    [Serializable]
    public struct WorldSpawnData
    {
        public long stableId;
        public string archetypeId;
        public SpawnCategory category;
        public Vector3 worldPosition;
        public float yawDegrees;
        public float uniformScale;

        public WorldSpawnData(
            long stableId,
            string archetypeId,
            SpawnCategory category,
            Vector3 worldPosition,
            float yawDegrees,
            float uniformScale)
        {
            this.stableId = stableId;
            this.archetypeId = archetypeId;
            this.category = category;
            this.worldPosition = worldPosition;
            this.yawDegrees = yawDegrees;
            this.uniformScale = uniformScale;
        }
    }
}
