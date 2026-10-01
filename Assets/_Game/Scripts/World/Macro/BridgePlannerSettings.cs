using System;

namespace LittleCastle.World
{
    [Serializable]
    public sealed class BridgePlannerSettings
    {
        public bool enabled = true;
        public string archetypeId = "bridge_wood_small";
        public float extraSpan = 2f;
    }
}
