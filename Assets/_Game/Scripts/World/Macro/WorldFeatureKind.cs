namespace LittleCastle.World
{
    /// <summary>
    /// Stable high-level feature categories used by macro planning and save data.
    /// Not every category is implemented yet.
    /// </summary>
    public enum WorldFeatureKind : byte
    {
        Unknown = 0,
        NeutralSettlement = 1,
        Ruin = 2,
        Signpost = 3,
        RoadNode = 4,
        BridgeSite = 5,
        OreDeposit = 6,
        StoneDeposit = 7,
        ForestRegion = 8,
        Landmark = 9
    }
}
