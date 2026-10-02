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
        MountainBarrier = 1 << 4,
        All = Plains | RollingHills | Steep | Highlands | MountainBarrier
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
                case TerrainClass.MountainBarrier:
                    value = TerrainClassMask.MountainBarrier;
                    break;
                default:
                    return false;
            }

            return (mask & value) != 0;
        }
    }
}
