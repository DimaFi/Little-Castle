namespace LittleCastle.World
{
    /// <summary>
    /// Ordered generation phases.
    /// Stage lists must be non-decreasing by phase.
    /// </summary>
    public enum WorldGenerationStagePhase
    {
        TerrainBase = 100,
        TerrainModification = 150,
        TerrainAnalysis = 200,
        MacroProjection = 300,
        Climate = 350,
        Biome = 375,
        Surface = 390,
        EnvironmentFields = 400,
        GroundCover = 425,
        Resources = 500,
        LocalSpawns = 600,
        PostProcess = 700
    }
}
