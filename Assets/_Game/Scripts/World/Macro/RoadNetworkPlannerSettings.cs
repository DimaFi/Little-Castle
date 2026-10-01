using System;

namespace LittleCastle.World
{
    [Serializable]
    public sealed class RoadNetworkPlannerSettings
    {
        public bool enabled = true;
        public RoadKind roadKind = RoadKind.DirtRoad;

        public int nearestConnectionsPerSettlement = 2;

        public float maxConnectionDistance = 2400f;
    }
}
