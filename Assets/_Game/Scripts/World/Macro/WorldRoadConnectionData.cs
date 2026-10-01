using System;

namespace LittleCastle.World
{
    /// <summary>
    /// Logical desired connection between two macro features.
    /// It is not yet a solved geometric road path.
    /// </summary>
    [Serializable]
    public struct WorldRoadConnectionData
    {
        public long stableId;
        public long fromFeatureId;
        public long toFeatureId;
        public RoadKind roadKind;

        public WorldRoadConnectionData(
            long stableId,
            long fromFeatureId,
            long toFeatureId,
            RoadKind roadKind)
        {
            this.stableId = stableId;
            this.fromFeatureId = fromFeatureId;
            this.toFeatureId = toFeatureId;
            this.roadKind = roadKind;
        }
    }
}
