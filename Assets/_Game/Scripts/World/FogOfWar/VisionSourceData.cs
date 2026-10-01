using System;
using UnityEngine;

namespace LittleCastle.World
{
    /// <summary>
    /// Plain data describing one source of map vision.
    ///
    /// Units, buildings, towers and owned neutral settlements can all expose
    /// themselves to fog-of-war as vision sources without fog code depending
    /// on their concrete gameplay class.
    /// </summary>
    [Serializable]
    public struct VisionSourceData
    {
        public long stableId;
        public int viewerId;
        public Vector2 worldPosition;
        public float radius;

        public VisionSourceData(
            long stableId,
            int viewerId,
            Vector2 worldPosition,
            float radius)
        {
            this.stableId = stableId;
            this.viewerId = viewerId;
            this.worldPosition = worldPosition;
            this.radius = radius;
        }
    }
}
