using System;

namespace LittleCastle.World
{
    /// <summary>
    /// Immutable-ish runtime description of one finite multiplayer session map.
    ///
    /// The host chooses player count, map preset and seed before the match.
    /// </summary>
    [Serializable]
    public sealed class WorldSessionMap
    {
        public int worldSeed;
        public int playerCount;
        public string mapSizePresetId;

        public WorldSessionStartOptions startOptions =
            new WorldSessionStartOptions();

        public WorldChunkBounds playableChunks;
        public WorldChunkBounds visualChunks;

        public bool IsPlayableChunk(
            ChunkCoordinate coordinate)
        {
            return
                playableChunks.Contains(
                    coordinate);
        }

        public bool IsVisualChunk(
            ChunkCoordinate coordinate)
        {
            return
                visualChunks.Contains(
                    coordinate);
        }
    }

    public static class WorldSessionMapFactory
    {
        public static WorldSessionMap Create(
            int worldSeed,
            int playerCount,
            WorldMapSizePreset preset,
            ChunkCoordinate mapCenter,
            WorldSessionStartOptions startOptions = null)
        {
            if (preset == null)
                throw new ArgumentNullException(nameof(preset));

            if (!preset.AllowsPlayerCount(
                    playerCount))
            {
                throw new ArgumentException(
                    "Selected map preset does not allow this player count.",
                    nameof(playerCount));
            }

            int width =
                Math.Max(
                    4,
                    preset.widthChunks);

            int height =
                Math.Max(
                    4,
                    preset.heightChunks);

            int minX =
                mapCenter.x -
                width / 2;

            int minZ =
                mapCenter.z -
                height / 2;

            var playable =
                new WorldChunkBounds(
                    minX,
                    minZ,
                    width,
                    height);

            return new WorldSessionMap
            {
                worldSeed = worldSeed,
                playerCount = Math.Max(1, playerCount),
                mapSizePresetId = preset.presetId,
                startOptions =
                    startOptions != null
                        ? startOptions.Clone()
                        : new WorldSessionStartOptions(),
                playableChunks = playable,
                visualChunks =
                    playable.Expand(
                        Math.Max(
                            0,
                            preset.visualPaddingChunks))
            };
        }
    }
}
