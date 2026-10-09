using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace LittleCastle.World
{
    /// <summary>
    /// Versioned compact identity for one deterministic generated world.
    /// A saved identity MUST come from an approved canonical settings
    /// manifest. This standalone value does not change existing saves.
    /// </summary>
    public readonly struct WorldGenerationIdentity :
        IEquatable<WorldGenerationIdentity>
    {
        public const string FormatTag = "LCW1";
        public const int HashHexLength = 64;

        public readonly string worldId;
        public readonly string profileId;
        public readonly int generationVersion;
        public readonly int worldSeed;
        public readonly string settingsSha256;

        private WorldGenerationIdentity(
            string worldId,
            string profileId,
            int generationVersion,
            int worldSeed,
            string settingsSha256)
        {
            this.worldId = worldId;
            this.profileId = profileId;
            this.generationVersion = generationVersion;
            this.worldSeed = worldSeed;
            this.settingsSha256 = settingsSha256;
        }

        public bool IsValid =>
            ValidName(worldId) &&
            ValidName(profileId) &&
            generationVersion >= 1 &&
            ValidHash(settingsSha256);

        public static WorldGenerationIdentity Create(
            string worldId,
            string profileId,
            int generationVersion,
            int worldSeed,
            string approvedSettingsHash)
        {
            if (!ValidName(worldId) ||
                !ValidName(profileId) ||
                generationVersion < 1 ||
                !ValidHash(approvedSettingsHash))
                throw new ArgumentException(
                    "Invalid world/profile/version or 64-character " +
                    "lowercase canonical SHA-256 settings fingerprint.");

            return new WorldGenerationIdentity(
                worldId, profileId, generationVersion,
                worldSeed, approvedSettingsHash);
        }

        /// <summary>
        /// Deterministic, delimiter-safe compact format. World and profile
        /// identifiers must use ASCII letters/digits/underscore/dot/hyphen.
        /// No Unity object references, asset GUIDs, local file paths or
        /// machine-dependent ToString/hash-code values are embedded.
        /// </summary>
        public string ToCompactString()
        {
            if (!IsValid)
                throw new InvalidOperationException(
                    "Incomplete generation identity cannot be persisted.");

            return FormatTag + "|" + worldId + "|" + profileId + "|" +
                generationVersion.ToString(CultureInfo.InvariantCulture) +
                "|" + worldSeed.ToString(CultureInfo.InvariantCulture) +
                "|" + settingsSha256;
        }

        public static bool TryParse(
            string compact,
            out WorldGenerationIdentity identity)
        {
            identity = default;
            if (string.IsNullOrEmpty(compact))
                return false;

            string[] components = compact.Split('|');
            if (components.Length != 6 ||
                components[0] != FormatTag ||
                !int.TryParse(components[3], NumberStyles.None,
                    CultureInfo.InvariantCulture, out int version) ||
                !int.TryParse(components[4], NumberStyles.AllowLeadingSign,
                    CultureInfo.InvariantCulture, out int seed) ||
                !ValidName(components[1]) ||
                !ValidName(components[2]) ||
                version < 1 ||
                !ValidHash(components[5]))
                return false;

            identity = new WorldGenerationIdentity(
                components[1], components[2],
                version, seed, components[5]);
            return true;
        }

        public bool Equals(WorldGenerationIdentity other) =>
            worldId == other.worldId &&
            profileId == other.profileId &&
            generationVersion == other.generationVersion &&
            worldSeed == other.worldSeed &&
            settingsSha256 == other.settingsSha256;

        public override bool Equals(object obj) =>
            obj is WorldGenerationIdentity other && Equals(other);

        public override int GetHashCode()
        {
            // Hashing is only for in-memory collection placement. It is
            // NOT the persistent identity or a source for generation IDs.
            unchecked
            {
                int hash = 17;
                hash = hash * 31 +
                    DeterministicHash.String32(worldId);
                hash = hash * 31 +
                    DeterministicHash.String32(profileId);
                hash = hash * 31 + generationVersion;
                hash = hash * 31 + worldSeed;
                hash = hash * 31 +
                    DeterministicHash.String32(settingsSha256);
                return hash;
            }
        }

        public static bool operator ==(
            WorldGenerationIdentity left,
            WorldGenerationIdentity right) => left.Equals(right);

        public static bool operator !=(
            WorldGenerationIdentity left,
            WorldGenerationIdentity right) => !left.Equals(right);

        private static bool ValidName(string name)
        {
            if (string.IsNullOrEmpty(name) || name.Length > 128)
                return false;

            for (int i = 0; i < name.Length; i++)
            {
                char ch = name[i];
                if ((ch >= 'a' && ch <= 'z') ||
                    (ch >= 'A' && ch <= 'Z') ||
                    (ch >= '0' && ch <= '9') ||
                    ch == '_' || ch == '-' || ch == '.')
                    continue;
                return false;
            }
            return true;
        }

        private static bool ValidHash(string hex)
        {
            if (hex == null || hex.Length != HashHexLength)
                return false;
            for (int i = 0; i < hex.Length; i++)
            {
                char ch = hex[i];
                if ((ch >= '0' && ch <= '9') ||
                    (ch >= 'a' && ch <= 'f'))
                    continue;
                return false;
            }
            return true;
        }
    }

    /// <summary>
    /// SHA-256 of explicitly approved canonical settings bytes.
    /// This does NOT infer a gameplay-safe manifest from ToJson,
    /// ScriptableObject names, file timestamps, local GUIDs or textures.
    ///
    /// The root owner must define the complete, sorted and versioned
    /// descriptor of all gameplay-affecting generation parameters BEFORE
    /// using this method for persistent saves or multiplayer.
    /// </summary>
    public static class WorldGenerationSettingsFingerprint
    {
        private static readonly byte[] Domain =
            Encoding.UTF8.GetBytes(
                "LittleCastle/WorldGenerationSettings/ApprovedManifest/v1\n");

        public static string ComputeApprovedManifestSha256(
            byte[] approvedCanonicalManifest)
        {
            if (approvedCanonicalManifest == null ||
                approvedCanonicalManifest.Length == 0)
                throw new ArgumentException(
                    "A non-empty approved canonical settings manifest is required.",
                    nameof(approvedCanonicalManifest));

            // Domain separation prevents these bytes from being mistaken
            // for hashes of unrelated serialization protocols.
            byte[] input = new byte[
                Domain.Length + approvedCanonicalManifest.Length];
            Buffer.BlockCopy(Domain, 0, input, 0, Domain.Length);
            Buffer.BlockCopy(approvedCanonicalManifest, 0,
                input, Domain.Length, approvedCanonicalManifest.Length);

            using (SHA256 sha = SHA256.Create())
            {
                byte[] digest = sha.ComputeHash(input);
                const string alphabet = "0123456789abcdef";
                char[] chars = new char[digest.Length * 2];
                for (int i = 0; i < digest.Length; i++)
                {
                    chars[i * 2] = alphabet[digest[i] >> 4];
                    chars[i * 2 + 1] = alphabet[digest[i] & 15];
                }
                return new string(chars);
            }
        }
    }
}
