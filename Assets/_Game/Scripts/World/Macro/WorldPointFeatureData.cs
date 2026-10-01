using System;
using UnityEngine;

namespace LittleCastle.World
{
    /// <summary>
    /// Serializable descriptor for a point-like macro world feature.
    /// No prefab or scene references belong here.
    /// </summary>
    [Serializable]
    public struct WorldPointFeatureData
    {
        public long stableId;
        public WorldFeatureKind kind;
        public string archetypeId;
        public Vector2 worldPosition;
        public float influenceRadius;

        public WorldPointFeatureData(
            long stableId,
            WorldFeatureKind kind,
            string archetypeId,
            Vector2 worldPosition,
            float influenceRadius)
        {
            this.stableId = stableId;
            this.kind = kind;
            this.archetypeId = archetypeId;
            this.worldPosition = worldPosition;
            this.influenceRadius = influenceRadius;
        }
    }
}
