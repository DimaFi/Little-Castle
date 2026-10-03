using System;

namespace LittleCastle.World
{
    [Serializable]
    public sealed class BridgePlannerSettings
    {
        public bool enabled = true;
        public string archetypeId = "bridge_wood_small";

        public bool standardizeCrossings = true;
        public float standardRiverCrossingWidth = 8f;
        public float standardBridgeSpan = 12f;
        public int crossingWidthBlendPointRadius = 2;

        // Retained for backwards compatibility when standardizeCrossings=false.
        public float extraSpan = 2f;
    }
}
