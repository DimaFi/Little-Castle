using System;
using System.Collections.Generic;
using UnityEngine;

namespace LittleCastle.World
{
    [Serializable]
    public sealed class WorldRiverData
    {
        public long stableId;
        public float nominalWidth;
        public float nominalDepth;
        public List<Vector2> centerline = new List<Vector2>();
    }
}
