using System;

namespace LittleCastle.World
{
    public enum WorldGenerationMismatch : byte
    {
        None = 0,
        MissingSavedIdentity = 1,
        MalformedSavedIdentity = 2,
        MissingCurrentIdentity = 3,
        WorldIdChanged = 4,
        ProfileIdChanged = 5,
        GenerationVersionChanged = 6,
        SettingsManifestChanged = 7,
        SeedChanged = 8
    }

    public readonly struct WorldGenerationIdentityCheck
    {
        public readonly WorldGenerationMismatch reason;
        public readonly string message;

        public bool IsCompatible =>
            reason == WorldGenerationMismatch.None;

        public WorldGenerationIdentityCheck(
            WorldGenerationMismatch reason,
            string message)
        {
            this.reason = reason;
            this.message = message;
        }
    }

    /// <summary>
    /// Fail-closed source-only compatibility gate. Existing
    /// WorldRuntimeDeltaState/save snapshot schema is unchanged.
    /// Do not attach to production load until the root approves the
    /// canonical generation-settings manifest and a legacy migration.
    /// </summary>
    public static class WorldGenerationIdentityValidator
    {
        public static WorldGenerationIdentityCheck Validate(
            WorldGenerationIdentity saved,
            WorldGenerationIdentity current)
        {
            if (!current.IsValid)
                return Reject(
                    WorldGenerationMismatch.MissingCurrentIdentity,
                    "Current world does not have an approved settings identity.");

            if (!saved.IsValid)
                return Reject(
                    WorldGenerationMismatch.MalformedSavedIdentity,
                    "Saved world identity is incomplete or malformed.");

            if (saved.worldId != current.worldId)
                return Reject(
                    WorldGenerationMismatch.WorldIdChanged,
                    "World ID changed; cannot apply this save to another world.");

            if (saved.profileId != current.profileId)
                return Reject(
                    WorldGenerationMismatch.ProfileIdChanged,
                    "Generation profile changed; existing generated IDs " +
                    "and chunk geometry are not assumed compatible.");

            if (saved.generationVersion != current.generationVersion)
                return Reject(
                    WorldGenerationMismatch.GenerationVersionChanged,
                    "Generation version changed from " +
                    saved.generationVersion + " to " +
                    current.generationVersion +
                    "; explicit migration is required.");

            if (saved.settingsSha256 != current.settingsSha256)
                return Reject(
                    WorldGenerationMismatch.SettingsManifestChanged,
                    "Canonical generation settings fingerprint changed. " +
                    "Saved chunk/runtime deltas must not be silently reused.");

            if (saved.worldSeed != current.worldSeed)
                return Reject(
                    WorldGenerationMismatch.SeedChanged,
                    "World seed changed; generated content and stable IDs " +
                    "must not inherit a different seed's runtime removals.");

            return new WorldGenerationIdentityCheck(
                WorldGenerationMismatch.None,
                "Generation identity matches: save deltas MAY be processed " +
                "after separate schema, chunk and authority validation.");
        }

        public static WorldGenerationIdentityCheck ValidateSavedCompact(
            string savedCompact,
            WorldGenerationIdentity current)
        {
            if (!current.IsValid)
                return Reject(
                    WorldGenerationMismatch.MissingCurrentIdentity,
                    "Current world identity is invalid; refuse save loading.");

            if (string.IsNullOrEmpty(savedCompact))
                return Reject(
                    WorldGenerationMismatch.MissingSavedIdentity,
                    "Legacy save contains no generation identity; root must " +
                    "approve an explicit migration or reject loading.");

            if (!WorldGenerationIdentity.TryParse(
                    savedCompact, out WorldGenerationIdentity saved))
                return Reject(
                    WorldGenerationMismatch.MalformedSavedIdentity,
                    "Saved generation identity has an unknown format or " +
                    "invalid fields; do not guess a generation version.");

            return Validate(saved, current);
        }

        public static WorldGenerationIdentity CreateCurrentFromApprovedManifest(
            WorldDefinition definition,
            int worldSeed,
            byte[] approvedCanonicalManifest)
        {
            if (definition == null ||
                definition.GenerationSettings == null)
                throw new ArgumentException(
                    "A complete world definition is required.",
                    nameof(definition));

            string fingerprint =
                WorldGenerationSettingsFingerprint.ComputeApprovedManifestSha256(
                    approvedCanonicalManifest);

            return WorldGenerationIdentity.Create(
                definition.WorldId,
                definition.GenerationSettings.ProfileId,
                definition.GenerationSettings.GenerationVersion,
                worldSeed,
                fingerprint);
        }

        private static WorldGenerationIdentityCheck Reject(
            WorldGenerationMismatch reason,
            string message) =>
            new WorldGenerationIdentityCheck(reason, message);
    }
}
