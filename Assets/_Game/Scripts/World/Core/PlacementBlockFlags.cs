using System;

namespace LittleCastle.World
{
    [Flags]
    public enum PlacementBlockFlags
    {
        None = 0,
        Trees = 1 << 0,
        LargeObjects = 1 << 1,
        Resources = 1 << 2,
        Decoration = 1 << 3,
        Grass = 1 << 4,
        All = Trees | LargeObjects | Resources | Decoration | Grass
    }

    public static class PlacementBlockFlagsExtensions
    {
        public static PlacementBlockFlags ForCategory(SpawnCategory category)
        {
            switch (category)
            {
                case SpawnCategory.Tree:
                    return PlacementBlockFlags.Trees;
                case SpawnCategory.Grass:
                    return PlacementBlockFlags.Grass;
                case SpawnCategory.Rock:
                case SpawnCategory.RuinProp:
                    return PlacementBlockFlags.LargeObjects;
                case SpawnCategory.Bush:
                case SpawnCategory.Decoration:
                case SpawnCategory.Sign:
                    return PlacementBlockFlags.Decoration;
                case SpawnCategory.ResourceVisual:
                    return PlacementBlockFlags.Resources;
                default:
                    return PlacementBlockFlags.None;
            }
        }
    }
}
