using System;
using System.Collections.Generic;
using UnityEngine;

namespace LittleCastle.World
{
    [Serializable]
    public sealed class WorldRoadData
    {
        public long stableId;
        public RoadKind roadKind;
        public float width;
        public List<Vector2> centerline = new List<Vector2>();
    }
}
