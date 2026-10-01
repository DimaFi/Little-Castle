using System;
using UnityEngine;

namespace LittleCastle.World
{
    /// <summary>
    /// Serializable descriptor for a point-like world feature.
    /// This is data only: it does not contain prefab or scene references.
    ///
    /// Roads and rivers will use dedicated line/network data rather than this type.
    /// </summary>
    [Serializable]
    public struct WorldPointFeatureData
    {
        public int id;
        public WorldFeatureKind kind;
        public Vector2 worldPosition;
        public float influenceRadius;

        public WorldPointFeatureData(
            int id,
            WorldFeatureKind kind,
            Vector2 worldPosition,
            float influenceRadius)
        {
            this.id = id;
            this.kind = kind;
            this.worldPosition = worldPosition;
            this.influenceRadius = influenceRadius;
        }
    }
}
