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
        public string archetypeId;
        public Vector2 worldPosition;
        public float yawDegrees;
        public float requiredSpan;

        /// <summary>
        /// Authored root elevation for a fixed site. Legacy bridge records leave
        /// this at zero and continue to sample terrain during chunk projection.
        /// </summary>
        public float baseElevation;

        /// <summary>
        /// Version of the fixed terrain/model contract. Empty for legacy sites.
        /// </summary>
        public string contractVersion;

        /// <summary>
        /// True only when the bridge owns a deterministic authored terrain site.
        /// </summary>
        public bool isFixedSite;

        public WorldBridgeSiteData(
            long stableId,
            long roadId,
            long riverId,
            string archetypeId,
            Vector2 worldPosition,
            float yawDegrees,
            float requiredSpan)
        {
            this.stableId = stableId;
            this.roadId = roadId;
            this.riverId = riverId;
            this.archetypeId = archetypeId;
            this.worldPosition = worldPosition;
            this.yawDegrees = yawDegrees;
            this.requiredSpan = requiredSpan;
            baseElevation = 0f;
            contractVersion = string.Empty;
            isFixedSite = false;
        }

        public WorldBridgeSiteData(
            long stableId,
            long roadId,
            long riverId,
            string archetypeId,
            Vector2 worldPosition,
            float yawDegrees,
            float requiredSpan,
            float baseElevation,
            string contractVersion,
            bool isFixedSite)
        {
            this.stableId = stableId;
            this.roadId = roadId;
            this.riverId = riverId;
            this.archetypeId = archetypeId;
            this.worldPosition = worldPosition;
            this.yawDegrees = yawDegrees;
            this.requiredSpan = requiredSpan;
            this.baseElevation = baseElevation;
            this.contractVersion = contractVersion;
            this.isFixedSite = isFixedSite;
        }
    }
}
