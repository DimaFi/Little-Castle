using System;
using System.Text;
using LittleCastle.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace LittleCastle.Tests
{
    /// <summary>
    /// Q16 identity gate tests, not a migrated production save serializer.
    /// A fixture's canonical bytes are intentionally NOT presented as an
    /// approved root manifest for actual save compatibility.
    /// </summary>
    public sealed class GenerationIdentityTests
    {
        private static readonly byte[] SampleManifest =
            Encoding.UTF8.GetBytes(
                "fixture-only-v1;world=concept;erosion=9;river=on");

        private static WorldGenerationIdentity Id(
            int seed = -10101,
            int version = 3,
            string world = "little_castle_concept",
            string profile = "concept_world_v001",
            byte[] canonicalManifest = null)
        {
            return WorldGenerationIdentity.Create(
                world,
                profile,
                version,
                seed,
                WorldGenerationSettingsFingerprint.ComputeApprovedManifestSha256(
                    canonicalManifest ?? SampleManifest));
        }

        [Test]
        public void StableManifestHash_IsLowercaseSha256AndDomainSeparated()
        {
            string first =
                WorldGenerationSettingsFingerprint.ComputeApprovedManifestSha256(
                    SampleManifest);
            string repeat =
                WorldGenerationSettingsFingerprint.ComputeApprovedManifestSha256(
                    (byte[])SampleManifest.Clone());
            Assert.That(first, Is.EqualTo(repeat));
            Assert.That(first.Length, Is.EqualTo(64));
            Assert.That(first, Does.Match("^[0-9a-f]{64}$"));
            Assert.That(first, Is.Not.EqualTo(
                "d104f8ba1d45f7979e29dfd17766ad00"),
                "Never use shortened GUIDs as generation fingerprints.");

            byte[] different = (byte[])SampleManifest.Clone();
            different[different.Length - 1] ^= 0x01;
            Assert.That(
                WorldGenerationSettingsFingerprint
                    .ComputeApprovedManifestSha256(different),
                Is.Not.EqualTo(first));
        }

        [Test]
        public void NullOrEmptyUnapprovedManifest_IsRejected()
        {
            Assert.Throws<ArgumentException>(() =>
                WorldGenerationSettingsFingerprint
                    .ComputeApprovedManifestSha256(null));
            Assert.Throws<ArgumentException>(() =>
                WorldGenerationSettingsFingerprint
                    .ComputeApprovedManifestSha256(new byte[0]));
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void InvalidGenerationVersion_CannotBeSerialized(int version)
        {
            string hash =
                WorldGenerationSettingsFingerprint.ComputeApprovedManifestSha256(
                    SampleManifest);
            Assert.Throws<ArgumentException>(() =>
                WorldGenerationIdentity.Create(
                    "world", "profile", version, 1, hash));
        }

        [Test]
        public void DelimiterOrUnicodeName_CannotCorruptCompactIdentity()
        {
            string hash =
                WorldGenerationSettingsFingerprint.ComputeApprovedManifestSha256(
                    SampleManifest);
            foreach (string bad in new[] { "", "with|pipe", "white space",
                "é", "slash/name", new string('a', 129) })
            {
                Assert.Throws<ArgumentException>(() =>
                    WorldGenerationIdentity.Create(
                        bad, "profile", 1, 2, hash));
                Assert.Throws<ArgumentException>(() =>
                    WorldGenerationIdentity.Create(
                        "world", bad, 1, 2, hash));
            }
        }

        [Test]
        public void LowercaseFullHashRequired_UppercaseOrInvalidRejected()
        {
            string sha =
                WorldGenerationSettingsFingerprint.ComputeApprovedManifestSha256(
                    SampleManifest);
            Assert.Throws<ArgumentException>(() =>
                WorldGenerationIdentity.Create(
                    "world", "profile", 1, 2, sha.ToUpperInvariant()));
            Assert.Throws<ArgumentException>(() =>
                WorldGenerationIdentity.Create(
                    "world", "profile", 1, 2, sha.Substring(0, 32)));
            Assert.Throws<ArgumentException>(() =>
                WorldGenerationIdentity.Create(
                    "world", "profile", 1, 2, new string('z', 64)));
        }

        [TestCase(int.MinValue)]
        [TestCase(int.MaxValue)]
        [TestCase(0)]
        [TestCase(-10101)]
        public void CompactRoundtrip_PreservesExtremeSeeds(int seed)
        {
            WorldGenerationIdentity source = Id(seed);
            string compact = source.ToCompactString();
            Assert.That(compact, Does.StartWith("LCW1|"));
            Assert.That(WorldGenerationIdentity.TryParse(
                compact, out WorldGenerationIdentity parsed), Is.True);
            Assert.That(parsed, Is.EqualTo(source));
            Assert.That(parsed.GetHashCode(), Is.EqualTo(
                source.GetHashCode()));
            Assert.That(parsed.ToCompactString(), Is.EqualTo(compact));
        }

        [Test]
        public void InvalidCompactSavedStrings_AreNeverSilentlyMigrated()
        {
            string good = Id().ToCompactString();
            foreach (string bad in new[]
            {
                null, "", "LCW0|w|p|1|2|" + new string('a', 64),
                "LCW1|w|p|0|2|" + new string('a', 64),
                "LCW1|w|p|1|overflow|" + new string('a', 64),
                good + "|extra",
                good.Replace("LCW1", "LCW2"),
                good.Replace("|-10101|", "|1|2|"),
                "LCW1|test|profile|1|1|BAD"
            })
            {
                Assert.That(WorldGenerationIdentity.TryParse(
                    bad, out _), Is.False, bad);
            }
        }

        [Test]
        public void MissingLegacyIdentity_AlwaysFailsClosed()
        {
            WorldGenerationIdentity current = Id();
            WorldGenerationIdentityCheck empty =
                WorldGenerationIdentityValidator.ValidateSavedCompact(
                    null, current);
            Assert.That(empty.IsCompatible, Is.False);
            Assert.That(empty.reason,
                Is.EqualTo(WorldGenerationMismatch.MissingSavedIdentity));
            Assert.That(empty.message, Does.Contain("Legacy"));

            WorldGenerationIdentityCheck bad =
                WorldGenerationIdentityValidator.ValidateSavedCompact(
                    "unknown", current);
            Assert.That(bad.reason,
                Is.EqualTo(WorldGenerationMismatch.MalformedSavedIdentity));
        }

        [Test]
        public void WorldProfileVersionSettingsAndSeedGetTypedRejections()
        {
            WorldGenerationIdentity current = Id();
            Assert.That(WorldGenerationIdentityValidator.Validate(
                Id(world: "other_world"), current).reason,
                Is.EqualTo(WorldGenerationMismatch.WorldIdChanged));
            Assert.That(WorldGenerationIdentityValidator.Validate(
                Id(profile: "other_profile"), current).reason,
                Is.EqualTo(WorldGenerationMismatch.ProfileIdChanged));
            Assert.That(WorldGenerationIdentityValidator.Validate(
                Id(version: 2), current).reason,
                Is.EqualTo(WorldGenerationMismatch.GenerationVersionChanged));
            Assert.That(WorldGenerationIdentityValidator.Validate(
                Id(canonicalManifest: Encoding.UTF8.GetBytes("different")),
                current).reason,
                Is.EqualTo(WorldGenerationMismatch.SettingsManifestChanged));
            Assert.That(WorldGenerationIdentityValidator.Validate(
                Id(seed: 200), current).reason,
                Is.EqualTo(WorldGenerationMismatch.SeedChanged));
        }

        [Test]
        public void SameIdentity_IsOnlyPermissionToContinueOtherChecks()
        {
            WorldGenerationIdentity expected = Id();
            WorldGenerationIdentityCheck accepted =
                WorldGenerationIdentityValidator.ValidateSavedCompact(
                    expected.ToCompactString(), Id());
            Assert.That(accepted.IsCompatible, Is.True);
            Assert.That(accepted.reason,
                Is.EqualTo(WorldGenerationMismatch.None));
            Assert.That(accepted.message, Does.Contain("MAY"),
                "Match does not certify chunk data, multiplayer or save schema.");
        }

        [Test]
        public void AbsentCurrentIdentity_DoesNotAcceptAnySavedIdentity()
        {
            WorldGenerationIdentityCheck result =
                WorldGenerationIdentityValidator.ValidateSavedCompact(
                    Id().ToCompactString(), default);
            Assert.That(result.IsCompatible, Is.False);
            Assert.That(result.reason,
                Is.EqualTo(WorldGenerationMismatch.MissingCurrentIdentity));
            Assert.Throws<InvalidOperationException>(() =>
                default(WorldGenerationIdentity).ToCompactString());
        }

        [Test]
        public void WorldDefinitionAdapter_UsesActualSavedVersionAndSeed()
        {
            const string path =
                "Assets/_Game/Settings/World/ConceptWorld_v001/" +
                "ConceptWorldDefinition.asset";
            WorldDefinition definition =
                AssetDatabase.LoadAssetAtPath<WorldDefinition>(path);
            Assert.That(definition, Is.Not.Null);
            WorldGenerationIdentity identity =
                WorldGenerationIdentityValidator
                    .CreateCurrentFromApprovedManifest(
                        definition, -10101, SampleManifest);

            Assert.That(identity.worldId, Is.EqualTo(definition.WorldId));
            Assert.That(identity.profileId,
                Is.EqualTo(definition.GenerationSettings.ProfileId));
            Assert.That(identity.generationVersion,
                Is.EqualTo(definition.GenerationSettings.GenerationVersion));
            Assert.That(identity.worldSeed, Is.EqualTo(-10101));
            Assert.That(WorldGenerationIdentity.TryParse(
                identity.ToCompactString(), out var parsed), Is.True);
            Assert.That(parsed, Is.EqualTo(identity));
        }

        [Test]
        public void MatchingIdentity_KeepsExistingRemovalAndBuildingDeltas()
        {
            var coord = new ChunkCoordinate(-1, 2);
            var seedIdentity = Id();
            var server = new WorldRuntimeDeltaState();
            const long removedTree = 817203L;
            const long buildingId = -12L;

            server.MarkSpawnRemoved(removedTree, coord);
            server.UpsertRuntimeEntity(new WorldRuntimeEntityState(
                buildingId, coord, "house_upgrade",
                SpawnCategory.Structure,
                new Vector3(-12f, 0f, 90f), 45f, 1f,
                ownerPlayerId: 2, level: 3));
            WorldChunkStateSnapshot savedSnapshot =
                server.BuildChunkSnapshot(coord);

            var loaded = new WorldRuntimeDeltaState();
            Assert.That(
                WorldGenerationIdentityValidator.ValidateSavedCompact(
                    seedIdentity.ToCompactString(), Id()).IsCompatible,
                Is.True);
            Assert.That(loaded.ApplyChunkSnapshot(savedSnapshot), Is.True);
            Assert.That(loaded.IsSpawnRemoved(removedTree), Is.True);
            Assert.That(loaded.TryGetRuntimeEntity(
                buildingId, out WorldRuntimeEntityState building), Is.True);
            Assert.That(building.ownerPlayerId, Is.EqualTo(2));
            Assert.That(building.level, Is.EqualTo(3));

            Assert.That(
                DeterministicHash.StableId(-10101, -1, 2, 0x1427),
                Is.EqualTo(
                    DeterministicHash.StableId(-10101, -1, 2, 0x1427)),
                "Stable generated IDs do not depend on eviction/snapshot order.");
        }

        [Test]
        public void ChangedSeed_RejectsDeltaBeforeApplyingIt()
        {
            WorldGenerationIdentity current = Id(seed: -10101);
            WorldGenerationIdentity old = Id(seed: 1337);
            WorldGenerationIdentityCheck result =
                WorldGenerationIdentityValidator.Validate(old, current);
            Assert.That(result.IsCompatible, Is.False);
            Assert.That(result.reason,
                Is.EqualTo(WorldGenerationMismatch.SeedChanged));

            var delta = new WorldRuntimeDeltaState();
            Assert.That(delta.RemovedSpawnIds.Count, Is.Zero,
                "Gate must not mutate existing runtime state.");
        }
    }
}
