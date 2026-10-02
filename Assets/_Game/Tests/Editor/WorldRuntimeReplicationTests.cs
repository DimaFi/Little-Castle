using System.Collections.Generic;
using LittleCastle.World;
using NUnit.Framework;
using UnityEngine;

namespace LittleCastle.Tests
{
    public sealed class WorldRuntimeReplicationTests
    {
        [Test]
        public void ChunkSnapshot_RoundTripsAuthoritativeChanges()
        {
            var server =
                new WorldRuntimeDeltaState();

            var coordinate =
                new ChunkCoordinate(
                    4,
                    -3);

            Assert.That(
                server.MarkSpawnRemoved(
                    1001,
                    coordinate),
                Is.True);

            server.SetRemainingCapacity(
                2002,
                coordinate,
                17);

            var runtimeEntity =
                new WorldRuntimeEntityState(
                    -1,
                    coordinate,
                    "test_structure",
                    SpawnCategory.Structure,
                    new Vector3(
                        270f,
                        2f,
                        -180f),
                    45f,
                    1f,
                    ownerPlayerId: 2,
                    level: 3);

            Assert.That(
                server.UpsertRuntimeEntity(
                    runtimeEntity),
                Is.True);

            WorldChunkStateSnapshot snapshot =
                server.BuildChunkSnapshot(
                    coordinate);

            Assert.That(
                snapshot.revision,
                Is.GreaterThan(0));

            Assert.That(
                snapshot.removedGeneratedSpawnIds,
                Does.Contain(1001));

            Assert.That(
                snapshot.runtimeEntities.Count,
                Is.EqualTo(1));

            var client =
                new WorldRuntimeDeltaState();

            Assert.That(
                client.ApplyChunkSnapshot(
                    snapshot),
                Is.True);

            Assert.That(
                client.GetChunkRevision(
                    coordinate),
                Is.EqualTo(
                    snapshot.revision));

            Assert.That(
                client.IsSpawnRemoved(
                    1001),
                Is.True);

            Assert.That(
                client.TryGetRuntimeEntity(
                    -1,
                    out WorldRuntimeEntityState restored),
                Is.True);

            Assert.That(
                restored.level,
                Is.EqualTo(3));

            Assert.That(
                restored.ownerPlayerId,
                Is.EqualTo(2));
        }

        [Test]
        public void OlderChunkSnapshot_DoesNotOverwriteNewerClientState()
        {
            var coordinate =
                new ChunkCoordinate(
                    1,
                    1);

            var client =
                new WorldRuntimeDeltaState();

            var newer =
                new WorldChunkStateSnapshot
                {
                    chunkCoordinate =
                        coordinate,
                    revision = 5
                };

            newer.runtimeEntities.Add(
                new WorldRuntimeEntityState(
                    -5,
                    coordinate,
                    "newer",
                    SpawnCategory.Structure,
                    Vector3.zero,
                    0f,
                    1f));

            Assert.That(
                client.ApplyChunkSnapshot(
                    newer),
                Is.True);

            var older =
                new WorldChunkStateSnapshot
                {
                    chunkCoordinate =
                        coordinate,
                    revision = 4
                };

            Assert.That(
                client.ApplyChunkSnapshot(
                    older),
                Is.False);

            Assert.That(
                client.TryGetRuntimeEntity(
                    -5,
                    out WorldRuntimeEntityState ignored),
                Is.True);
        }

        [Test]
        public void InformationMode_FullMapReceivesLiveState_FogNeedsVisibility()
        {
            Assert.That(
                WorldClientKnowledgeCache.ShouldReceiveDetailedSnapshot(
                    WorldInformationMode.FullMapLive,
                    FogOfWarVisibility.Hidden),
                Is.True);

            Assert.That(
                WorldClientKnowledgeCache.ShouldReceiveDetailedSnapshot(
                    WorldInformationMode.FogOfWarLastKnown,
                    FogOfWarVisibility.Hidden),
                Is.False);

            Assert.That(
                WorldClientKnowledgeCache.ShouldReceiveDetailedSnapshot(
                    WorldInformationMode.FogOfWarLastKnown,
                    FogOfWarVisibility.Explored),
                Is.False);

            Assert.That(
                WorldClientKnowledgeCache.ShouldReceiveDetailedSnapshot(
                    WorldInformationMode.FogOfWarLastKnown,
                    FogOfWarVisibility.Visible),
                Is.True);
        }

        [Test]
        public void SimulationLod_UsesNearestPlayerInterest()
        {
            var policy =
                new WorldSimulationLodPolicy
                {
                    activeRadiusChunks = 2,
                    warmRadiusChunks = 6
                };

            var players =
                new List<ChunkCoordinate>
                {
                    new ChunkCoordinate(
                        0,
                        0),
                    new ChunkCoordinate(
                        20,
                        20)
                };

            Assert.That(
                policy.Evaluate(
                    new ChunkCoordinate(
                        1,
                        1),
                    players),
                Is.EqualTo(
                    WorldSimulationTier.Active));

            Assert.That(
                policy.Evaluate(
                    new ChunkCoordinate(
                        5,
                        0),
                    players),
                Is.EqualTo(
                    WorldSimulationTier.Warm));

            Assert.That(
                policy.Evaluate(
                    new ChunkCoordinate(
                        10,
                        10),
                    players),
                Is.EqualTo(
                    WorldSimulationTier.Sleeping));
        }
    }
}
