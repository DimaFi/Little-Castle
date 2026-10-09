using System;
using System.Collections.Generic;
using LittleCastle.World;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace LittleCastle.Tests
{
    /// <summary>
    /// Data-driven/actual-Mesh EditMode tests, NOT a gameplay-frame GPU test.
    /// Synthetic heightfield explicitly has varying x and z derivatives,
    /// so independent Mesh.RecalculateNormals would reveal border strips.
    /// </summary>
    public sealed class ChunkTerrainNormalSeamTests
    {
        private sealed class Fixture
        {
            public readonly Dictionary<ChunkCoordinate, StreamedChunkView>
                views = new Dictionary<ChunkCoordinate, StreamedChunkView>();
            public readonly Dictionary<ChunkCoordinate, WorldChunkData>
                data = new Dictionary<ChunkCoordinate, WorldChunkData>();

            public WorldChunkData Lookup(ChunkCoordinate coordinate)
            {
                return data.TryGetValue(coordinate, out WorldChunkData value)
                    ? value : null;
            }

            public StreamedChunkView Add(
                int cx, int cz, bool playable = true,
                float heightOffset = 0f)
            {
                var coordinate = new ChunkCoordinate(cx, cz);
                var chunk = new WorldChunkData(coordinate, 8);
                const float chunkSize = 8f;
                for (int z = 0; z <= 8; z++)
                {
                    for (int x = 0; x <= 8; x++)
                    {
                        float wx = cx * chunkSize + x;
                        float wz = cz * chunkSize + z;
                        // Exactly shared edge heights, unequal local normals.
                        float height =
                            1f + .2f * wx + .1f * wz +
                            .02f * wx * wx + .01f * wz * wz +
                            heightOffset;
                        chunk.SetHeight(x, z, height);
                    }
                }

                Mesh mesh = ChunkMeshBuilder.Build(chunk, chunkSize);
                var colors = new Color32[mesh.vertexCount];
                for (int i = 0; i < colors.Length; i++)
                    colors[i] = new Color32(200, 35, 0, 255);
                mesh.colors32 = colors;

                var go = new GameObject("NormalSeam_" + cx + "_" + cz);
                go.transform.position = coordinate.GetWorldOrigin(chunkSize);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var view = go.AddComponent<StreamedChunkView>();
                view.Initialize(coordinate, mesh, playable);
                views.Add(coordinate, view);
                data.Add(coordinate, chunk);
                return view;
            }

            public Mesh Mesh(int x, int z)
            {
                return views[new ChunkCoordinate(x, z)]
                    .GetComponent<MeshFilter>().sharedMesh;
            }

            public void Clear()
            {
                foreach (StreamedChunkView view in views.Values)
                {
                    if (view == null) continue;
                    view.ReleaseOwnedResources();
                    Object.DestroyImmediate(view.gameObject);
                }

                views.Clear();
                data.Clear();
            }
        }

        private readonly List<Fixture> fixtures = new List<Fixture>();

        [TearDown]
        public void TearDown()
        {
            foreach (Fixture fixture in fixtures)
                fixture.Clear();
            fixtures.Clear();
        }

        private Fixture NewFixture()
        {
            var fixture = new Fixture();
            fixtures.Add(fixture);
            return fixture;
        }

        [Test]
        public void WestEastBoundary_UsesIdenticalCenteredNormalsOnNegativeSeam()
        {
            Fixture fixture = NewFixture();
            fixture.Add(-1, -1);
            fixture.Add(0, -1);
            int changed = ChunkTerrainNormalSeams.RefreshAround(
                new ChunkCoordinate(0, -1), fixture.views, 8f, fixture.Lookup);

            Assert.That(changed, Is.EqualTo(2));
            Vector3[] west = fixture.Mesh(-1, -1).normals;
            Vector3[] east = fixture.Mesh(0, -1).normals;

            for (int z = 1; z < 8; z++)
            {
                Vector3 w = west[z * 9 + 8];
                Vector3 e = east[z * 9];
                Assert.That(Vector3.Distance(w, e), Is.LessThan(0.00001f));
                float worldZ = -8 + z;
                Vector3 expected = new Vector3(
                    -.2f, 1f, -(.1f + .02f * worldZ)).normalized;
                Assert.That(Vector3.Distance(w, expected),
                    Is.LessThan(0.00001f));
            }
        }

        [Test]
        public void FourWayNegativeCorner_IsIdenticalAfterAnyChunkLoadOrder()
        {
            Fixture first = NewFixture();
            Fixture reverse = NewFixture();
            var forward = new[]
            {
                new ChunkCoordinate(-1, -1),
                new ChunkCoordinate(0, -1),
                new ChunkCoordinate(-1, 0),
                new ChunkCoordinate(0, 0)
            };

            foreach (ChunkCoordinate coordinate in forward)
            {
                first.Add(coordinate.x, coordinate.z);
                ChunkTerrainNormalSeams.RefreshAround(
                    coordinate, first.views, 8f, first.Lookup);
            }

            for (int i = forward.Length - 1; i >= 0; i--)
            {
                ChunkCoordinate coordinate = forward[i];
                reverse.Add(coordinate.x, coordinate.z);
                ChunkTerrainNormalSeams.RefreshAround(
                    coordinate, reverse.views, 8f, reverse.Lookup);
            }

            var corners = new[]
            {
                new { x = -1, z = -1, vertex = 80 },
                new { x = 0, z = -1, vertex = 72 },
                new { x = -1, z = 0, vertex = 8 },
                new { x = 0, z = 0, vertex = 0 }
            };

            Vector3 expected = new Vector3(-.2f, 1f, -.1f).normalized;
            foreach (var corner in corners)
            {
                Vector3 a = first.Mesh(corner.x, corner.z)
                    .normals[corner.vertex];
                Vector3 b = reverse.Mesh(corner.x, corner.z)
                    .normals[corner.vertex];
                Assert.That(Vector3.Distance(a, expected),
                    Is.LessThan(0.00001f));
                Assert.That(Vector3.Distance(a, b),
                    Is.LessThan(0.00001f));
            }
        }

        [Test]
        public void NormalStitch_NeverChangesMeshHeightsTopologyOrColorMasks()
        {
            Fixture fixture = NewFixture();
            fixture.Add(-1, -1);
            fixture.Add(0, -1);

            Mesh west = fixture.Mesh(-1, -1);
            Vector3[] vertices = west.vertices;
            int[] triangles = west.triangles;
            Color32[] colors = west.colors32;
            Vector2[] uv = west.uv;
            Bounds bounds = west.bounds;

            ChunkTerrainNormalSeams.RefreshAround(
                new ChunkCoordinate(0, -1), fixture.views, 8f, fixture.Lookup);

            Assert.That(west.vertices, Is.EqualTo(vertices));
            Assert.That(west.triangles, Is.EqualTo(triangles));
            Assert.That(west.colors32, Is.EqualTo(colors));
            Assert.That(west.uv, Is.EqualTo(uv));
            Assert.That(west.bounds, Is.EqualTo(bounds));
        }

        [Test]
        public void ActualHeightCrack_CannotBeHiddenByVisualNormalWeld()
        {
            Fixture fixture = NewFixture();
            fixture.Add(-1, -1);
            fixture.Add(0, -1, heightOffset: 3f);

            Mesh west = fixture.Mesh(-1, -1);
            Mesh east = fixture.Mesh(0, -1);
            Vector3[] westNormals = west.normals;
            Vector3[] eastNormals = east.normals;

            int changed = ChunkTerrainNormalSeams.RefreshAround(
                new ChunkCoordinate(0, -1), fixture.views, 8f, fixture.Lookup);

            Assert.That(changed, Is.Zero,
                "A real 3m height discontinuity is a generation failure, " +
                "not a lighting artifact that can be welded.");
            Assert.That(west.normals, Is.EqualTo(westNormals));
            Assert.That(east.normals, Is.EqualTo(eastNormals));
        }

        [Test]
        public void PartialOrDifferentProfileNeighbour_DoesNotFakeMatchingSurface()
        {
            Fixture alone = NewFixture();
            var a = alone.Add(-1, 0);
            Vector3[] before = alone.Mesh(-1, 0).normals;
            Assert.That(ChunkTerrainNormalSeams.RefreshAround(
                a.Coordinate, alone.views, 8f, alone.Lookup), Is.Zero);
            Assert.That(alone.Mesh(-1, 0).normals, Is.EqualTo(before));

            Fixture mixed = NewFixture();
            mixed.Add(-1, 0, playable: true);
            mixed.Add(0, 0, playable: false);
            Vector3[] untouched = mixed.Mesh(0, 0).normals;
            Assert.That(ChunkTerrainNormalSeams.RefreshAround(
                new ChunkCoordinate(0, 0),
                mixed.views, 8f, mixed.Lookup), Is.Zero);
            Assert.That(mixed.Mesh(0, 0).normals, Is.EqualTo(untouched));
        }
    }
}
