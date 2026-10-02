namespace LittleCastle.World
{
    /// <summary>
    /// Controls how much current world-state information a client is allowed
    /// to know independently of what it is currently rendering.
    /// </summary>
    public enum WorldInformationMode : byte
    {
        /// <summary>
        /// Clients may receive current state/revision information for the whole
        /// finite map. Rendering remains local/interest-based.
        /// </summary>
        FullMapLive = 0,

        /// <summary>
        /// Current dynamic state is received only for currently visible areas.
        /// Explored areas keep their last known snapshot until observed again.
        /// </summary>
        FogOfWarLastKnown = 1
    }
}
