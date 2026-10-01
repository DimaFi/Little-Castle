using System;

namespace LittleCastle.World
{
    [Flags]
    public enum BiomeMask
    {
        None = 0,
        Unknown = 1 << 0,
        TemperateGrassland = 1 << 1,
        TemperateWoodland = 1 << 2,
        WetLowland = 1 << 3,
        HighlandMeadow = 1 << 4,
        RockyHighland = 1 << 5,
        All =
            Unknown |
            TemperateGrassland |
            TemperateWoodland |
            WetLowland |
            HighlandMeadow |
            RockyHighland
    }

    public static class BiomeMaskExtensions
    {
        public static bool Contains(
            this BiomeMask mask,
            BiomeKind biome)
        {
            BiomeMask value;

            switch (biome)
            {
                case BiomeKind.TemperateGrassland:
                    value = BiomeMask.TemperateGrassland;
                    break;
                case BiomeKind.TemperateWoodland:
                    value = BiomeMask.TemperateWoodland;
                    break;
                case BiomeKind.WetLowland:
                    value = BiomeMask.WetLowland;
                    break;
                case BiomeKind.HighlandMeadow:
                    value = BiomeMask.HighlandMeadow;
                    break;
                case BiomeKind.RockyHighland:
                    value = BiomeMask.RockyHighland;
                    break;
                default:
                    value = BiomeMask.Unknown;
                    break;
            }

            return (mask & value) != 0;
        }
    }
}
