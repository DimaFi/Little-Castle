using System;
using LittleCastle.Gameplay;

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

        public WorldTimeState worldTime =
            new WorldTimeState(
                1,
                8.0 * 60.0);

        public GameplaySessionState gameplay =
            new GameplaySessionState();

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
                    new WorldRuntimeDeltaState(),
                worldTime =
                    new WorldTimeState(
                        1,
                        definition != null &&
                        definition.TimeSettings != null
                            ? definition.TimeSettings.StartHour * 60.0
                            : 8.0 * 60.0),
                gameplay =
                    new GameplaySessionState
                    {
                        worldSeed = worldSeed
                    }
            };
        }
    }
}
