namespace LittleCastle.World
{
    /// <summary>
    /// Inputs shared by generation stages for one generation request.
    /// </summary>
    public sealed class GenerationContext
    {
        public int WorldSeed { get; }
        public WorldGenerationSettings Settings { get; }
        public MacroWorldPlan MacroPlan { get; }

        public GenerationContext(
            int worldSeed,
            WorldGenerationSettings settings,
            MacroWorldPlan macroPlan = null)
        {
            WorldSeed = worldSeed;
            Settings = settings;
            MacroPlan = macroPlan;
        }
    }
}
