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
        // Optional handmade-map delta; old generated-world saves leave this null.
        public MapEditSave mapEdits;
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
