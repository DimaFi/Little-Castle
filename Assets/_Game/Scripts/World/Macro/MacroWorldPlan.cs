using System.Collections.Generic;

namespace LittleCastle.World
{
    /// <summary>
    /// World-scale generated plan shared by many chunks.
    ///
    /// This intentionally starts small. Cross-chunk systems such as neutral
    /// settlements, ruins and landmarks belong here. Road and river network
    /// collections will be added as dedicated data contracts when implemented.
    /// </summary>
    public sealed class MacroWorldPlan
    {
        private readonly List<WorldPointFeatureData> pointFeatures =
            new List<WorldPointFeatureData>();

        public int WorldSeed { get; }
        public IReadOnlyList<WorldPointFeatureData> PointFeatures => pointFeatures;

        public MacroWorldPlan(int worldSeed)
        {
            WorldSeed = worldSeed;
        }

        public void AddPointFeature(WorldPointFeatureData feature)
        {
            pointFeatures.Add(feature);
        }
    }
}
