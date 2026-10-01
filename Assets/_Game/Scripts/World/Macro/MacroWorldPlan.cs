using System.Collections.Generic;

namespace LittleCastle.World
{
    /// <summary>
    /// World-scale generated plan shared by many chunks.
    /// It stores stable macro feature descriptors, not scene objects.
    /// </summary>
    public sealed class MacroWorldPlan
    {
        private readonly List<WorldPointFeatureData> pointFeatures =
            new List<WorldPointFeatureData>();

        private readonly List<WorldRoadData> roads =
            new List<WorldRoadData>();

        private readonly List<WorldRiverData> rivers =
            new List<WorldRiverData>();

        private readonly List<WorldBridgeSiteData> bridgeSites =
            new List<WorldBridgeSiteData>();

        public int WorldSeed { get; }

        public IReadOnlyList<WorldPointFeatureData> PointFeatures =>
            pointFeatures;

        public IReadOnlyList<WorldRoadData> Roads => roads;
        public IReadOnlyList<WorldRiverData> Rivers => rivers;
        public IReadOnlyList<WorldBridgeSiteData> BridgeSites => bridgeSites;

        public MacroWorldPlan(int worldSeed)
        {
            WorldSeed = worldSeed;
        }

        public void AddPointFeature(WorldPointFeatureData feature) =>
            pointFeatures.Add(feature);

        public void AddRoad(WorldRoadData road)
        {
            if (road != null)
                roads.Add(road);
        }

        public void AddRiver(WorldRiverData river)
        {
            if (river != null)
                rivers.Add(river);
        }

        public void AddBridgeSite(WorldBridgeSiteData bridgeSite) =>
            bridgeSites.Add(bridgeSite);
    }
}
