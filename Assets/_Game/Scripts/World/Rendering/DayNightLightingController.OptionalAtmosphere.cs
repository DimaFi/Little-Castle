namespace LittleCastle.World
{
    // Opt-in rendering contract: no effect until the optional bridge enables it.
    public sealed partial class DayNightLightingController
    {
        public bool ExternalSkyPresenterActive { get; set; }
        public float RequestedStormStrength => stormStrength;
        public float RequestedCloudiness => overcast;
        public float RequestedPrecipitation => precipitation;
    }
}
