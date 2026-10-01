using System;

namespace LittleCastle.World
{
    [Serializable]
    public sealed class TerrainRoadPathPlannerSettings
    {
        public bool enabled = true;

        public float gridStep = 24f;
        public float searchPadding = 160f;

        public float maxSlope = 32f;
        public float slopeCostMultiplier = 5f;
        public float highlandCostMultiplier = 1.5f;

        public int maxExpandedNodes = 12000;

        public float trailWidth = 2f;
        public float dirtRoadWidth = 5f;
        public float improvedRoadWidth = 7f;
        public float settlementStreetWidth = 8f;
        public float fortifiedRouteWidth = 9f;

        public float GetWidth(RoadKind kind)
        {
            switch (kind)
            {
                case RoadKind.Trail:
                    return trailWidth;
                case RoadKind.ImprovedRoad:
                    return improvedRoadWidth;
                case RoadKind.SettlementStreet:
                    return settlementStreetWidth;
                case RoadKind.FortifiedRoute:
                    return fortifiedRouteWidth;
                default:
                    return dirtRoadWidth;
            }
        }
    }
}
