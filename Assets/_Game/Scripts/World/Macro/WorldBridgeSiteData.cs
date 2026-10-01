using System;
using UnityEngine;

namespace LittleCastle.World
{
    [Serializable]
    public struct WorldBridgeSiteData
    {
        public long stableId;
        public long roadId;
        public long riverId;
        public Vector2 worldPosition;
        public float yawDegrees;
        public float requiredSpan;

        public WorldBridgeSiteData(
            long stableId,
            long roadId,
            long riverId,
            Vector2 worldPosition,
            float yawDegrees,
            float requiredSpan)
        {
            this.stableId = stableId;
            this.roadId = roadId;
            this.riverId = riverId;
            this.worldPosition = worldPosition;
            this.yawDegrees = yawDegrees;
            this.requiredSpan = requiredSpan;
        }
    }
}
