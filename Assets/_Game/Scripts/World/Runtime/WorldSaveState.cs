using System;

namespace LittleCastle.World
{
    /// <summary>
    /// Minimal serializable root for future save files.
    /// Gameplay systems can extend/wrap this later.
    /// </summary>
    [Serializable]
    public sealed class WorldSaveState
    {
        public WorldSaveMetadata metadata;
        public WorldRuntimeDeltaState runtimeDelta =
            new WorldRuntimeDeltaState();

        public static WorldSaveState CreateNew(
            WorldDefinition definition,
            int worldSeed)
        {
            return new WorldSaveState
            {
                metadata =
                    WorldSaveMetadata.FromDefinition(
                        definition,
                        worldSeed),
                runtimeDelta =
                    new WorldRuntimeDeltaState()
            };
        }
    }
}
