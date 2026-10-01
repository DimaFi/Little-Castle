namespace LittleCastle.World
{
    /// <summary>
    /// Host-facing amount of balancing applied when selecting a random world.
    ///
    /// Even strict modes should preserve procedural variation. This setting is
    /// about how aggressively poor/trapped starts are rejected or compensated.
    /// </summary>
    public enum WorldStartFairnessMode : byte
    {
        /// <summary>
        /// Maximum procedural chaos. Only fundamentally unusable/trapped starts
        /// are rejected. Resource poverty is allowed.
        /// </summary>
        WildRandom = 0,

        /// <summary>
        /// Default intended experience. Scarcity is allowed, but highly
        /// pressured starts prefer somewhat better nearby opportunities.
        /// </summary>
        Light = 1,

        /// <summary>
        /// More comparable starts while retaining asymmetric worlds.
        /// </summary>
        Balanced = 2,

        /// <summary>
        /// Stronger competitive filtering for players who want it.
        /// </summary>
        Competitive = 3
    }
}
