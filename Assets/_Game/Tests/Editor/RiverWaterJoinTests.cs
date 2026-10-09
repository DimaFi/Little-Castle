using System;
using System.Collections.Generic;
using LittleCastle.World;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace LittleCastle.Tests
{
    /// <summary>
    /// GPU-free topology/height tests. Water appearance, transparent
    /// overdraw, material and Player acceptance still require desktop.
    /// </summary>
    public sealed class RiverWaterJoinTests
    {
        private const float ChunkWorldSize = 64f;
        private const int TerrainCells = 32;
        private readonly List<Object> created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            for (int i = created.Count - 1; i >= 0; i--)
                if (created[i] != null)
                    Object.DestroyImmediate(created[i]);
            created.Clear();
        }

        [Test]
        public void ClippedWater_UsesSharedVerticesInsteadOfTripledCopies()
        {
            var plan = new MacroWorldPlan(15);
            plan.AddRiver(River(100,
                new Vector2(-30f, -35f),
                new Vector2(45f, -35f), 11f, 1.8f));
            Mesh mesh = Build(0, -1, plan);
            Assert.That(mesh, Is.Not.Null);
            Assert.That(mesh.triangles.Length / 3, Is.GreaterThan(50));
            Assert.That(mesh.vertexCount, Is.LessThan(mesh.triangles.Length),
                "The old triangle fan duplicated all three vertices " +
                "on every triangle; unchanged-position welding must reuse them.");

            var points = new HashSet<Vector3>();
            foreach (Vector3 vertex in mesh.vertices)
                Assert.That(points.Add(vertex), Is.True,
                    "Exact same local XYZ must not create another index.");

            for (int i = 0; i < mesh.vertexCount; i++)
            {
                Vector3 p = mesh.vertices[i];
                Vector2 uv = mesh.uv[i];
                Assert.That(uv.x,
                    Is.EqualTo(p.x * 0.1f).Within(0.0001f));
                Assert.That(uv.y,
                    Is.EqualTo((-64f + p.z) * 0.1f).Within(0.0001f));
            }
        }

        [Test]
        public void NegativeChunkBorder_WithConfluenceHasMatchingWaterContours()
        {
            var plan = new MacroWorldPlan(-10101);
            plan.AddRiver(River(11,
                new Vector2(-155f, -24.25f),
                new Vector2(15f, -24.25f), 9f, 2f));
            plan.AddRiver(River(12,
                new Vector2(-78f, -55f),
                new Vector2(-64f, -24.25f), 7f, 2.5f));

            Mesh a = Build(-2, -1, plan);
            Mesh b = Build(-1, -1, plan);
            Assert.That(a, Is.Not.Null);
            Assert.That(b, Is.Not.Null);
            var left = Edge(a, -128f, -64f);
            var right = Edge(b, -64f, -64f);
            Assert.That(left.Count, Is.GreaterThan(0));
            AssertMatching(left, right);
        }

        [Test]
        public void PositiveChunkBorder_WithCurvedProfileHasMatchingWaterContours()
        {
            var plan = new MacroWorldPlan(-10101);
            var bend = River(22,
                new Vector2(20f, 26.2f),
                new Vector2(64f, 21.5f), 8f, 2f);
            bend.centerline.Add(new Vector2(90f, 38f));
            bend.widths.Add(12f);
            bend.depths.Add(2.5f);
            plan.AddRiver(bend);
            Mesh a = Build(0, 0, plan);
            Mesh b = Build(1, 0, plan);
            Assert.That(a, Is.Not.Null);
            Assert.That(b, Is.Not.Null);
            List<Vector2> aEdge = Edge(a, 0f, 64f);
            List<Vector2> bEdge = Edge(b, 64f, 64f);
            Assert.That(aEdge.Count, Is.GreaterThan(0),
                "A dry-dry border cannot falsely pass seam parity.");
            AssertMatching(aEdge, bEdge);
        }

        [Test]
        public void FixedBridgeAtConfluence_AlwaysUsesLinkedRiverWaterline()
        {
            var plan = new MacroWorldPlan(77);
            Vector2 site = new Vector2(-30f, -30f);
            plan.AddRiver(River(100,
                new Vector2(-55f, -30f),
                new Vector2(5f, -30f), 5f, 2f));
            // Deliberately dominates max-score selection at the fixed
            // bridge center. The former implementation skipped the stamp
            // because its winner's river ID did not match bridge.riverId.
            plan.AddRiver(River(900,
                new Vector2(-55f, -30f),
                new Vector2(5f, -30f), 18f, 3f));
            plan.AddBridgeSite(Bridge(500, 100, site, 12f, 0f));

            Mesh mesh = Build(-1, -1, plan);
            Assert.That(mesh, Is.Not.Null);
            int coreCount = 0;
            foreach (Vector3 v in mesh.vertices)
            {
                Vector2 world = new Vector2(v.x - 64f, v.z - 64f);
                Vector2 local = FixedBridgeSiteProfile.WorldToLocal(
                    world, site, 0f);
                if (Mathf.Abs(local.x) >= 3f ||
                    Mathf.Abs(local.y) >= 1f)
                    continue;
                coreCount++;
                Assert.That(v.y,
                    Is.EqualTo(12f + FixedBridgeSiteProfile.WaterHeightOffset)
                        .Within(0.0001f));
            }
            Assert.That(coreCount, Is.GreaterThan(0));
        }

        [TestCase(0f)]
        [TestCase(37f)]
        [TestCase(90f)]
        public void FixedBridgeAtNegativeBorder_KeepsUnscaledWaterline(float yaw)
        {
            Vector2 site = new Vector2(-64f, -32f);
            var plan = new MacroWorldPlan(11);
            Vector2 left = FixedBridgeSiteProfile.LocalToWorld(
                new Vector2(-30f, 0f), site, yaw);
            Vector2 right = FixedBridgeSiteProfile.LocalToWorld(
                new Vector2(30f, 0f), site, yaw);
            plan.AddRiver(River(100, left, right, 6f, 2f));
            plan.AddBridgeSite(Bridge(501, 100, site, 12f, yaw));

            Mesh a = Build(-2, -1, plan);
            Mesh b = Build(-1, -1, plan);
            Assert.That(a, Is.Not.Null);
            Assert.That(b, Is.Not.Null);
            List<Vector2> borderA = Edge(a, -128f, -64f);
            List<Vector2> borderB = Edge(b, -64f, -64f);
            Assert.That(borderA.Count, Is.GreaterThan(0));
            AssertMatching(borderA, borderB);

            int coreCount = 0;
            foreach (Vector3 v in a.vertices)
            {
                Vector2 world = new Vector2(v.x - 128f, v.z - 64f);
                Vector2 local = FixedBridgeSiteProfile.WorldToLocal(
                    world, site, yaw);
                if (Mathf.Abs(local.x) > 3f ||
                    Mathf.Abs(local.y) > 1f)
                    continue;
                coreCount++;
                Assert.That(v.y,
                    Is.EqualTo(11.15f).Within(0.0001f));
            }
            Assert.That(coreCount, Is.GreaterThan(0));
        }

        [Test]
        public void OrphanBridgeId_CannotInventWaterOrForceHeight()
        {
            var plan = new MacroWorldPlan(6);
            Vector2 center = new Vector2(-30f, -30f);
            plan.AddRiver(River(10,
                new Vector2(-63f, -30f),
                new Vector2(-2f, -30f), 6f, 2f));
            plan.AddBridgeSite(Bridge(11, 999L, center, 50f, 0f));

            var clean = new MacroWorldPlan(6);
            clean.AddRiver(River(10,
                new Vector2(-63f, -30f),
                new Vector2(-2f, -30f), 6f, 2f));

            Mesh withOrphan = Build(-1, -1, plan);
            Mesh withoutSite = Build(-1, -1, clean);
            Assert.That(withOrphan, Is.Not.Null);
            Assert.That(withoutSite, Is.Not.Null);
            AssertSameMesh(withOrphan, withoutSite);
        }

        [Test]
        public void DisplacedBridgeOutsideLinkedChannel_DoesNotStampWater()
        {
            var plan = new MacroWorldPlan(7);
            var bare = new MacroWorldPlan(7);
            WorldRiverData river = River(20,
                new Vector2(-64f, -40f),
                new Vector2(0f, -40f), 5f, 2f);
            plan.AddRiver(river);
            bare.AddRiver(river);
            // Near the river chunk but outside its actual wetted ribbon.
            plan.AddBridgeSite(Bridge(
                21, 20, new Vector2(-20f, -5f), 80f, 0f));
            Mesh stamped = Build(-1, -1, plan);
            Mesh plain = Build(-1, -1, bare);
            Assert.That(stamped, Is.Not.Null);
            Assert.That(plain, Is.Not.Null);
            AssertSameMesh(plain, stamped);
        }

        [Test]
        public void ExactWeldingPreservesTriangleWindingAndFiniteData()
        {
            var plan = new MacroWorldPlan(8);
            plan.AddRiver(River(42,
                new Vector2(-100f, -19f),
                new Vector2(20f, -17f), 7f, 2f));
            Mesh mesh = Build(-1, -1, plan);
            Assert.That(mesh, Is.Not.Null);
            var vertices = mesh.vertices;
            var triangleIndices = mesh.triangles;
            Assert.That(triangleIndices.Length % 3, Is.Zero);
            for (int i = 0; i < triangleIndices.Length; i += 3)
            {
                Vector3 a = vertices[triangleIndices[i]];
                Vector3 b = vertices[triangleIndices[i + 1]];
                Vector3 c = vertices[triangleIndices[i + 2]];
                float area = (b.z - a.z) * (c.x - a.x) -
                    (b.x - a.x) * (c.z - a.z);
                Assert.That(area, Is.GreaterThan(0f));
                foreach (Vector3 v in new[] { a, b, c })
                {
                    Assert.That(float.IsNaN(v.y) ||
                        float.IsInfinity(v.y), Is.False);
                    Assert.That(v.x, Is.InRange(-0.001f, 64.001f));
                    Assert.That(v.z, Is.InRange(-0.001f, 64.001f));
                }
            }
        }

        [Test]
        public void ReusingInactiveWaterPresenter_AvoidsDuplicateObjects()
        {
            var plan = new MacroWorldPlan(99);
            plan.AddRiver(River(990,
                new Vector2(-60f, -29f),
                new Vector2(-1f, -29f), 8f, 2f));
            var root = new GameObject("Q05ParkedChunkRoot");
            created.Add(root);
            Shader shader = Shader.Find("Standard");
            Assert.That(shader, Is.Not.Null);
            var material = new Material(shader);
            created.Add(material);

            WorldChunkData data = Chunk(-1, -1);
            RiverWaterPresenter first = RiverWaterPresenter.Populate(
                root.transform, data, ChunkWorldSize, plan, material);
            Assert.That(first, Is.Not.Null);
            Mesh mesh = first.OwnedMesh;
            Assert.That(mesh, Is.Not.Null);
            Assert.That(root.transform.childCount, Is.EqualTo(1));

            first.gameObject.SetActive(false);
            RiverWaterPresenter again = RiverWaterPresenter.Populate(
                root.transform, data, ChunkWorldSize, plan, material);
            Assert.That(again, Is.SameAs(first));
            Assert.That(first.OwnedMesh, Is.SameAs(mesh));
            Assert.That(root.transform.childCount, Is.EqualTo(1),
                "A parked water GameObject must not be duplicated.");
        }

        private Mesh Build(int cx, int cz, MacroWorldPlan plan)
        {
            Mesh mesh = RiverWaterMeshBuilder.Build(
                Chunk(cx, cz), ChunkWorldSize, plan);
            if (mesh != null)
                created.Add(mesh);
            return mesh;
        }

        private static WorldChunkData Chunk(int cx, int cz)
        {
            var chunk = new WorldChunkData(
                new ChunkCoordinate(cx, cz), TerrainCells);
            for (int z = 0; z <= TerrainCells; z++)
                for (int x = 0; x <= TerrainCells; x++)
                {
                    float wx = cx * ChunkWorldSize + x * 2f;
                    float wz = cz * ChunkWorldSize + z * 2f;
                    chunk.SetHeight(x, z,
                        4f + wx * 0.002f + wz * 0.001f);
                }
            return chunk;
        }

        private static WorldRiverData River(
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

        private static WorldBridgeSiteData Bridge(
            long stableId, long riverId, Vector2 at,
            float elevation, float yaw)
        {
            return new WorldBridgeSiteData(
                stableId, 800, riverId, FixedBridgeSiteProfile.AssetId,
                at, yaw, 5f, elevation,
                FixedBridgeSiteProfile.ContractVersion, true);
        }

        private static List<Vector2> Edge(
            Mesh mesh, float originX, float boundaryX)
        {
            var result = new List<Vector2>();
            Vector3[] vertices = mesh.vertices;
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 v = vertices[i];
                if (Mathf.Abs(originX + v.x - boundaryX) > 0.0001f)
                    continue;
                var wzAndHeight = new Vector2(v.z, v.y);
                bool exists = false;
                for (int j = 0; j < result.Count; j++)
                {
                    if ((result[j] - wzAndHeight).sqrMagnitude < 0.0000001f)
                    {
                        exists = true;
                        break;
                    }
                }
                if (!exists)
                    result.Add(wzAndHeight);
            }
            return result;
        }

        private static void AssertMatching(
            List<Vector2> left,
            List<Vector2> right)
        {
            Assert.That(left.Count, Is.EqualTo(right.Count));
            for (int i = 0; i < left.Count; i++)
            {
                bool found = false;
                foreach (Vector2 point in right)
                    if ((left[i] - point).sqrMagnitude < 0.000001f)
                        found = true;
                Assert.That(found, Is.True,
                    "Missing identical border point " + left[i]);
            }
        }

        private static void AssertSameMesh(Mesh expected, Mesh actual)
        {
            Assert.That(actual.vertexCount, Is.EqualTo(expected.vertexCount));
            CollectionAssert.AreEqual(expected.triangles, actual.triangles);
            CollectionAssert.AreEqual(expected.vertices, actual.vertices);
            CollectionAssert.AreEqual(expected.uv, actual.uv);
        }
    }
}
