using System.Collections.Generic;

namespace LittleCastle.World
{
    public sealed class MacroWorldPlan
    {
        private readonly List<WorldPointFeatureData> pointFeatures =
            new List<WorldPointFeatureData>();

        private readonly List<WorldRoadConnectionData> roadConnections =
            new List<WorldRoadConnectionData>();

        private readonly List<WorldRoadData> roads =
            new List<WorldRoadData>();

        private readonly List<WorldRiverData> rivers =
            new List<WorldRiverData>();

        private readonly List<WorldBridgeSiteData> bridgeSites =
            new List<WorldBridgeSiteData>();

        private readonly HashSet<long> pointFeatureIds =
            new HashSet<long>();

        private readonly HashSet<long> roadConnectionIds =
            new HashSet<long>();

        private readonly HashSet<long> roadIds =
            new HashSet<long>();

        private readonly HashSet<long> riverIds =
            new HashSet<long>();

        private readonly HashSet<long> bridgeIds =
            new HashSet<long>();

        public int WorldSeed { get; }

        public IReadOnlyList<WorldPointFeatureData> PointFeatures =>
            pointFeatures;

        public IReadOnlyList<WorldRoadConnectionData> RoadConnections =>
            roadConnections;

        public IReadOnlyList<WorldRoadData> Roads => roads;
        public IReadOnlyList<WorldRiverData> Rivers => rivers;
        public IReadOnlyList<WorldBridgeSiteData> BridgeSites => bridgeSites;

        public MacroWorldPlan(int worldSeed)
        {
            WorldSeed = worldSeed;
        }

        public bool AddPointFeature(WorldPointFeatureData feature)
        {
            if (!pointFeatureIds.Add(feature.stableId))
                return false;

            pointFeatures.Add(feature);
            return true;
        }

        public bool AddRoadConnection(
            WorldRoadConnectionData connection)
        {
            if (!roadConnectionIds.Add(connection.stableId))
                return false;

            roadConnections.Add(connection);
            return true;
        }

        public bool AddRoad(WorldRoadData road)
        {
            if (road == null ||
                !roadIds.Add(road.stableId))
            {
                return false;
            }

            roads.Add(road);
            return true;
        }

        public bool AddRiver(WorldRiverData river)
        {
            if (river == null ||
                !riverIds.Add(river.stableId))
            {
                return false;
            }

            rivers.Add(river);
            return true;
        }

        public bool AddBridgeSite(
            WorldBridgeSiteData bridgeSite)
        {
            if (!bridgeIds.Add(bridgeSite.stableId))
                return false;

            bridgeSites.Add(bridgeSite);
            return true;
        }

        public bool TryGetPointFeature(
            long stableId,
            out WorldPointFeatureData feature)
        {
            for (int i = 0; i < pointFeatures.Count; i++)
            {
                if (pointFeatures[i].stableId == stableId)
                {
                    feature = pointFeatures[i];
                    return true;
                }
            }

            feature = default(WorldPointFeatureData);
            return false;
        }

        public void MergeFrom(MacroWorldPlan other)
        {
            if (other == null ||
                other.WorldSeed != WorldSeed)
            {
                return;
            }

            for (int i = 0; i < other.PointFeatures.Count; i++)
                AddPointFeature(other.PointFeatures[i]);

            for (int i = 0; i < other.RoadConnections.Count; i++)
                AddRoadConnection(other.RoadConnections[i]);

            for (int i = 0; i < other.Roads.Count; i++)
                AddRoad(other.Roads[i]);

            for (int i = 0; i < other.Rivers.Count; i++)
                AddRiver(other.Rivers[i]);

            for (int i = 0; i < other.BridgeSites.Count; i++)
                AddBridgeSite(other.BridgeSites[i]);
        }
    }
}
