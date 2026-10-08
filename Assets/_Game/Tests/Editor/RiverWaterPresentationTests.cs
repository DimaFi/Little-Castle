using System.Collections.Generic;
using System.Diagnostics;
using LittleCastle.World;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace LittleCastle.Tests
{
    public sealed class RiverWaterPresentationTests
    {
        private const float ChunkSize = 64f;
        private const int Cells = 32;
        private readonly List<Object> created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            for (int i = created.Count - 1; i >= 0; i--)
            {
                if (created[i] != null)
                    Object.DestroyImmediate(created[i]);
            }
            created.Clear();
        }

        [Test]
        public void NegativeChunkSeam_UsesIdenticalWorldWaterVertices()
        {
            var plan = new MacroWorldPlan(7);
            plan.AddRiver(MakeRiver(101,
                new Vector2(-110f, -24.25f),
                new Vector2(-18f, -24.25f), 5f, 2f));
            Mesh left = RiverWaterMeshBuilder.Build(
                MakeChunk(-2, -1), ChunkSize, plan);
            Mesh right = RiverWaterMeshBuilder.Build(
                MakeChunk(-1, -1), ChunkSize, plan);
            Track(left);
            Track(right);

            Assert.That(left, Is.Not.Null);
            Assert.That(right, Is.Not.Null);
            var leftEdge = GetSeamVertices(left, -128f, -64f);
            var rightEdge = GetSeamVertices(right, -64f, -64f);
            Assert.That(leftEdge.Count, Is.GreaterThan(0));
            Assert.That(leftEdge.Count, Is.EqualTo(rightEdge.Count));
            foreach (Vector2 point in leftEdge)
            {
                bool matched = false;
                foreach (Vector2 other in rightEdge)
                {
                    if ((point - other).sqrMagnitude < 0.000001f)
                    {
                        matched = true;
                        break;
                    }
                }
                Assert.That(matched, Is.True,
                    $"Missing matching seam point {point}");
            }
        }

        [TestCase(0f)]
        [TestCase(37f)]
        [TestCase(90f)]
        [TestCase(180f)]
        public void FixedBridgeCore_UsesContractWaterHeightAtYaw(float yaw)
        {
            Vector2 origin = new Vector2(-20f, -20f);
            Vector2 start = FixedBridgeSiteProfile.LocalToWorld(
                new Vector2(-25f, 0f), origin, yaw);
            Vector2 end = FixedBridgeSiteProfile.LocalToWorld(
                new Vector2(25f, 0f), origin, yaw);
            var plan = new MacroWorldPlan(8);
            plan.AddRiver(MakeRiver(202, start, end, 5f, 2f));
            plan.AddBridgeSite(new WorldBridgeSiteData(
                303, 404, 202, FixedBridgeSiteProfile.AssetId,
                origin, yaw, 5f, 12f,
                FixedBridgeSiteProfile.ContractVersion, true));

            ChunkCoordinate coordinate =
                WorldChunkCoordinateUtility.FromWorldPosition(
                    origin.x, origin.y, ChunkSize);
            Mesh mesh = RiverWaterMeshBuilder.Build(
                MakeChunk(coordinate.x, coordinate.z), ChunkSize, plan);
            Track(mesh);
            Assert.That(mesh, Is.Not.Null);

            int coreVertices = 0;
            Vector3 chunkOrigin = coordinate.GetWorldOrigin(ChunkSize);
            foreach (Vector3 vertex in mesh.vertices)
            {
                Vector2 local = FixedBridgeSiteProfile.WorldToLocal(
                    new Vector2(vertex.x + chunkOrigin.x,
                        vertex.z + chunkOrigin.z), origin, yaw);
                if (Mathf.Abs(local.x) >= 4f ||
                    Mathf.Abs(local.y) >= 2f)
                    continue;
                coreVertices++;
                Assert.That(vertex.y,
                    Is.EqualTo(11.15f).Within(0.0001f));
            }
            Assert.That(coreVertices, Is.GreaterThan(0));
        }

        [Test]
        public void FixedBridgeOnNegativeSeam_KeepsCanonicalWaterHeight()
        {
            Vector2 origin = new Vector2(-64f, -32f);
            const float yaw = 37f;
            var plan = new MacroWorldPlan(12);
            plan.AddRiver(MakeRiver(801,
                FixedBridgeSiteProfile.LocalToWorld(
                    new Vector2(-25f, 0f), origin, yaw),
                FixedBridgeSiteProfile.LocalToWorld(
                    new Vector2(25f, 0f), origin, yaw),
                5f, 2f));
            plan.AddBridgeSite(new WorldBridgeSiteData(
                802, 803, 801, FixedBridgeSiteProfile.AssetId,
                origin, yaw, 5f, 12f,
                FixedBridgeSiteProfile.ContractVersion, true));

            Mesh left = RiverWaterMeshBuilder.Build(
                MakeChunk(-2, -1), ChunkSize, plan);
            Mesh right = RiverWaterMeshBuilder.Build(
                MakeChunk(-1, -1), ChunkSize, plan);
            Track(left);
            Track(right);
            Assert.That(left, Is.Not.Null);
            Assert.That(right, Is.Not.Null);

            var leftEdge = GetSeamVertices(left, -128f, -64f);
            var rightEdge = GetSeamVertices(right, -64f, -64f);
            int fixedMatches = 0;
            foreach (Vector2 vertex in leftEdge)
            {
                if (Mathf.Abs(vertex.x + 32f) > 1f)
                    continue;
                Assert.That(vertex.y, Is.EqualTo(11.15f).Within(0.0001f));
                bool matched = false;
                foreach (Vector2 other in rightEdge)
                {
                    if ((vertex - other).sqrMagnitude < 0.000001f)
                    {
                        matched = true;
                        break;
                    }
                }
                Assert.That(matched, Is.True);
                fixedMatches++;
            }
            Assert.That(fixedMatches, Is.GreaterThan(0));
        }

        [Test]
        public void Mesh_IsFiniteWithinChunkAndHasValidTriangles()
        {
            var plan = new MacroWorldPlan(9);
            plan.AddRiver(MakeRiver(505,
                new Vector2(-70f, -10f),
                new Vector2(-2f, -50f), 4f, 2f));
            Mesh mesh = RiverWaterMeshBuilder.Build(
                MakeChunk(-1, -1), ChunkSize, plan);
            Track(mesh);
            Assert.That(mesh, Is.Not.Null);
            Assert.That(mesh.vertexCount, Is.GreaterThan(0));
            Assert.That(mesh.triangles.Length % 3, Is.Zero);
            foreach (Vector3 vertex in mesh.vertices)
            {
                Assert.That(float.IsNaN(vertex.x) ||
                    float.IsInfinity(vertex.x) ||
                    float.IsNaN(vertex.y) ||
                    float.IsInfinity(vertex.y) ||
                    float.IsNaN(vertex.z) ||
                    float.IsInfinity(vertex.z), Is.False);
                Assert.That(vertex.x, Is.InRange(-0.0001f, 64.0001f));
                Assert.That(vertex.z, Is.InRange(-0.0001f, 64.0001f));
            }
            foreach (int index in mesh.triangles)
                Assert.That(index, Is.InRange(0, mesh.vertexCount - 1));
        }

        [Test]
        public void ConceptChunk_ReportsMeshSizeAndBuildTime()
        {
            var plan = new MacroWorldPlan(13);
            plan.AddRiver(MakeRiver(901,
                new Vector2(-70f, -25f),
                new Vector2(2f, -25f), 5f, 2f));

            var timer = Stopwatch.StartNew();
            Mesh mesh = RiverWaterMeshBuilder.Build(
                MakeChunk(-1, -1), ChunkSize, plan);
            timer.Stop();
            Track(mesh);
            Assert.That(mesh, Is.Not.Null);
            TestContext.WriteLine(
                $"River water concept 64m/32-cell chunk: " +
                $"{mesh.vertexCount} vertices, " +
                $"{mesh.triangles.Length / 3} triangles, " +
                $"{timer.Elapsed.TotalMilliseconds:F3} ms build");
        }

        [Test]
        public void Presenter_CombinesOverlappingRiversAndReleasesOwnedMesh()
        {
            var plan = new MacroWorldPlan(10);
            plan.AddRiver(MakeRiver(601,
                new Vector2(-60f, -30f),
                new Vector2(-4f, -30f), 4f, 2f));
            plan.AddRiver(MakeRiver(602,
                new Vector2(-60f, -30f),
                new Vector2(-4f, -30f), 4f, 2f));

            var root = new GameObject("water-test-chunk");
            Track(root);
            Shader shader = Shader.Find("Standard");
            Assert.That(shader, Is.Not.Null);
            var material = new Material(shader);
            Track(material);
            Assert.That(RiverWaterPresenter.Populate(
                root.transform, MakeChunk(-1, -1),
                ChunkSize, plan, null), Is.Null);
            Assert.That(root.transform.childCount, Is.Zero);

            RiverWaterPresenter presenter =
                RiverWaterPresenter.Populate(root.transform,
                    MakeChunk(-1, -1), ChunkSize, plan, material);
            Assert.That(presenter, Is.Not.Null);
            Assert.That(RiverWaterPresenter.Populate(root.transform,
                MakeChunk(-1, -1), ChunkSize, plan, material),
                Is.SameAs(presenter));
            Assert.That(root.GetComponentsInChildren<MeshFilter>().Length,
                Is.EqualTo(1));
            Mesh owned = presenter.OwnedMesh;
            Assert.That(owned, Is.Not.Null);
            presenter.ReleaseOwnedResources();
            Assert.That(presenter.OwnedMesh, Is.Null);
            Assert.That(presenter.GetComponent<MeshFilter>().sharedMesh,
                Is.Null);
            Assert.That(presenter.GetComponent<MeshRenderer>().enabled,
                Is.False);
            Assert.That(owned == null, Is.True);
            presenter.ReleaseOwnedResources();

            RiverWaterPresenter rebuilt =
                RiverWaterPresenter.Populate(root.transform,
                    MakeChunk(-1, -1), ChunkSize, plan, material);
            Assert.That(rebuilt, Is.SameAs(presenter));
            Assert.That(root.transform.childCount, Is.EqualTo(1));
            Assert.That(root.GetComponentsInChildren<MeshFilter>().Length,
                Is.EqualTo(1));
            Assert.That(rebuilt.OwnedMesh, Is.Not.Null);
            Assert.That(rebuilt.GetComponent<MeshRenderer>().enabled,
                Is.True);
        }

        [Test]
        public void FixedSiteWithoutMatchingRiver_DoesNotInventWater()
        {
            var plan = new MacroWorldPlan(11);
            plan.AddBridgeSite(new WorldBridgeSiteData(
                701, 702, 703, FixedBridgeSiteProfile.AssetId,
                new Vector2(-20f, -20f), 0f, 5f, 10f,
                FixedBridgeSiteProfile.ContractVersion, true));
            Assert.That(RiverWaterMeshBuilder.Build(
                MakeChunk(-1, -1), ChunkSize, plan), Is.Null);
        }

        private static WorldChunkData MakeChunk(int x, int z)
        {
            var chunk = new WorldChunkData(
                new ChunkCoordinate(x, z), Cells);
            for (int sz = 0; sz <= Cells; sz++)
            {
                for (int sx = 0; sx <= Cells; sx++)
                {
                    float worldX = x * ChunkSize + sx * 2f;
                    float worldZ = z * ChunkSize + sz * 2f;
                    chunk.SetHeight(sx, sz,
                        4f + worldX * 0.002f + worldZ * 0.001f);
                }
            }
            return chunk;
        }

        private static WorldRiverData MakeRiver(
            long id, Vector2 start, Vector2 end,
            float width, float depth)
        {
            var river = new WorldRiverData
            {
                stableId = id,
                nominalWidth = width,
                nominalDepth = depth
            };
            river.centerline.Add(start);
            river.centerline.Add(end);
            river.widths.Add(width);
            river.widths.Add(width);
            river.depths.Add(depth);
            river.depths.Add(depth);
            return river;
        }

        private static List<Vector2> GetSeamVertices(
            Mesh mesh, float originX, float seamX)
        {
            var unique = new List<Vector2>();
            foreach (Vector3 vertex in mesh.vertices)
            {
                if (Mathf.Abs(vertex.x + originX - seamX) > 0.0001f)
                    continue;
                var point = new Vector2(vertex.z - 64f, vertex.y);
                bool exists = false;
                foreach (Vector2 prior in unique)
                {
                    if ((prior - point).sqrMagnitude < 0.000001f)
                    {
                        exists = true;
                        break;
                    }
                }
                if (!exists)
                    unique.Add(point);
            }
            return unique;
        }

        private void Track(Object value)
        {
            if (value != null)
                created.Add(value);
        }
    }
}
