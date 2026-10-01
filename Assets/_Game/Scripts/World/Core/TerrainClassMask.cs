using System;

namespace LittleCastle.World
{
    [Flags]
    public enum TerrainClassMask
    {
        None = 0,
        Plains = 1 << 0,
        RollingHills = 1 << 1,
        Steep = 1 << 2,
        Highlands = 1 << 3,
        All = Plains | RollingHills | Steep | Highlands
    }

    public static class TerrainClassMaskExtensions
    {
        public static bool Contains(this TerrainClassMask mask, TerrainClass terrainClass)
        {
            TerrainClassMask value;

            switch (terrainClass)
            {
                case TerrainClass.Plains:
                    value = TerrainClassMask.Plains;
                    break;
                case TerrainClass.RollingHills:
                    value = TerrainClassMask.RollingHills;
                    break;
                case TerrainClass.Steep:
                    value = TerrainClassMask.Steep;
                    break;
                case TerrainClass.Highlands:
                    value = TerrainClassMask.Highlands;
                    break;
                default:
                    return false;
            }

            return (mask & value) != 0;
        }
    }
}
