using System;
using System.Collections.Generic;
using UnityEngine;

namespace LittleCastle.World
{
    [Serializable]
    public struct StartResourceMetric
    {
        public ResourceKind resourceKind;
        public int effectiveCapacity;
        public float normalizedScore;

        public StartResourceMetric(
            ResourceKind resourceKind,
            int effectiveCapacity,
            float normalizedScore)
        {
            this.resourceKind = resourceKind;
            this.effectiveCapacity = effectiveCapacity;
            this.normalizedScore = normalizedScore;
        }
    }

    [Serializable]
    public sealed class WorldStartAreaMetrics
    {
        public float centerSlope;
        public float maximumSampledSlope;
        public float sampledHeightRange;
        public float averageForestDensity;

        public float terrainScore;
        public float forestScore;
        public float resourceScore;
        public float overallQualityScore;

        public List<StartResourceMetric> resources =
            new List<StartResourceMetric>();
    }

    [Serializable]
    public struct WorldPlayerStartData
    {
        public int playerIndex;
        public long stableId;
        public Vector3 worldPosition;
        public float normalizedScore;
        public WorldStartAreaMetrics metrics;

        public WorldPlayerStartData(
            int playerIndex,
            long stableId,
            Vector3 worldPosition,
            float normalizedScore,
            WorldStartAreaMetrics metrics)
        {
            this.playerIndex = playerIndex;
            this.stableId = stableId;
            this.worldPosition = worldPosition;
            this.normalizedScore = normalizedScore;
            this.metrics = metrics;
        }
    }
}
