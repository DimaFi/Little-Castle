namespace LittleCastle.World
{
    /// <summary>
    /// Host-selectable high-level arrangement of player starting areas.
    /// The final world positions still adapt to generated terrain/resources.
    /// </summary>
    public enum WorldStartPlacementMode : byte
    {
        RandomScattered = 0,

        /// <summary>
        /// Starts prefer the outer part of the playable map.
        /// Useful when players should expand inward.
        /// </summary>
        MapPerimeter = 1,

        /// <summary>
        /// Starts prefer evenly spaced positions around an ellipse/ring.
        /// With three players this naturally forms a triangle.
        /// </summary>
        PolygonRing = 2,

        /// <summary>
        /// Alternates outer and inner radial targets.
        /// Creates star-like pressure where some starts are more central.
        /// </summary>
        RadialStar = 3
    }
}
