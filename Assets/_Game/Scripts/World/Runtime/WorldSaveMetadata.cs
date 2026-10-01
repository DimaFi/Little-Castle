using System;

namespace LittleCastle.World
{
    [Serializable]
    public struct WorldSaveMetadata
    {
        public string worldDefinitionId;
        public string generationProfileId;
        public int generationVersion;
        public int worldSeed;

        public WorldSaveMetadata(
            string worldDefinitionId,
            string generationProfileId,
            int generationVersion,
            int worldSeed)
        {
            this.worldDefinitionId = worldDefinitionId;
            this.generationProfileId = generationProfileId;
            this.generationVersion = generationVersion;
            this.worldSeed = worldSeed;
        }

        public static WorldSaveMetadata FromDefinition(
            WorldDefinition definition,
            int worldSeed)
        {
            if (definition == null ||
                definition.GenerationSettings == null)
            {
                return new WorldSaveMetadata(
                    string.Empty,
                    string.Empty,
                    0,
                    worldSeed);
            }

            return new WorldSaveMetadata(
                definition.WorldId,
                definition.GenerationSettings.ProfileId,
                definition.GenerationSettings.GenerationVersion,
                worldSeed);
        }

        public bool IsCompatibleWith(
            WorldDefinition definition,
            out string reason)
        {
            if (definition == null)
            {
                reason =
                    "WorldDefinition is missing.";
                return false;
            }

            WorldGenerationSettings settings =
                definition.GenerationSettings;

            if (settings == null)
            {
                reason =
                    "WorldGenerationSettings is missing.";
                return false;
            }

            if (worldDefinitionId !=
                definition.WorldId)
            {
                reason =
                    "World definition ID mismatch. Save='" +
                    worldDefinitionId +
                    "', current='" +
                    definition.WorldId +
                    "'.";

                return false;
            }

            if (generationProfileId !=
                settings.ProfileId)
            {
                reason =
                    "Generation profile mismatch. Save='" +
                    generationProfileId +
                    "', current='" +
                    settings.ProfileId +
                    "'.";

                return false;
            }

            if (generationVersion !=
                settings.GenerationVersion)
            {
                reason =
                    "Generation version mismatch. Save=" +
                    generationVersion +
                    ", current=" +
                    settings.GenerationVersion +
                    ".";

                return false;
            }

            reason = "Compatible.";
            return true;
        }
    }
}
