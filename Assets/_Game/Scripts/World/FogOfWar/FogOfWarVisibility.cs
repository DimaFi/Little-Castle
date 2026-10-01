namespace LittleCastle.World
{
    /// <summary>
    /// Logical visibility state for one viewer/player/team.
    ///
    /// Hidden   = never explored / maximum fog.
    /// Explored = seen before but not currently observed.
    /// Visible  = currently observed by at least one vision source.
    /// </summary>
    public enum FogOfWarVisibility : byte
    {
        Hidden = 0,
        Explored = 1,
        Visible = 2
    }
}
