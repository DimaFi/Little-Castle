namespace LittleCastle.World
{
    /// <summary>
    /// Coarse terrain classification for gameplay and later placement rules.
    /// This is not a biome. Biomes will use climate/region data later.
    /// </summary>
    public enum TerrainClass : byte
    {
        Unknown = 0,
        Plains = 1,
        RollingHills = 2,
        Steep = 3,
        Highlands = 4,
        MountainBarrier = 5
    }
}
