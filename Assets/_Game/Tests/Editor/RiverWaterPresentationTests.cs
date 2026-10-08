using System.Collections.Generic;
using LittleCastle.World;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace LittleCastle.Tests
{
    public sealed class RiverWaterPresentationTests
    {
        private readonly List<Object> owned = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (Object o in owned)
                if (o != null)
                    Object.DestroyImmediate(o);
            owned.Clear();
        }

        [Test]
        public void WaterRibbon_ClipsAcrossNegativeChunkBorderWithIdenticalSeamHeight()
        {
            var plan = new MacroWorldPlan(12345);
            var river = new WorldRiverData
            {
                stableId = 201,
                nominalWidth = 4f,
                centerline = new List<Vector2>
                {
                    new Vector2(-48f, 8f),
                    new Vector2(48f, 8f)
                }
            };
            plan.AddRiver(river);
            plan.AddBridgeSite(new WorldBridgeSiteData(
                301, 101, 201, FixedBridgeSiteProfile.AssetId,
                new Vector2(0f, 8f), 0f, FixedBridgeSiteProfile.BridgeLength,
                3f, FixedBridgeSiteProfile.ContractVersion, true));

            Mesh negative = RiverWaterMeshBuilder.Build(
                plan, new ChunkCoordinate(-1, 0), 32f,
                null, 0.55f, 20f);
            Mesh positive = RiverWaterMeshBuilder.Build(
                plan, new ChunkCoordinate(0, 0), 32f,
                null, 0.55f, 20f);
            Assert.That(negative, Is.Not.Null);
            Assert.That(positive, Is.Not.Null);
            owned.Add(negative);
            owned.Add(positive);

            AssertChunkBounds(negative, 32f);
            AssertChunkBounds(positive, 32f);

            var left = BorderVertices(negative, 32f);
            var right = BorderVertices(positive, 0f);
            Assert.That(left.Count, Is.GreaterThanOrEqualTo(2));
            Assert.That(left.Count, Is.EqualTo(right.Count));
            for (int i = 0; i < left.Count; i++)
            {
                Assert.That(left[i].z, Is.EqualTo(right[i].z).Within(0.0001f));
                Assert.That(left[i].y, Is.EqualTo(right[i].y).Within(0.0001f));
                Assert.That(left[i].y, Is.EqualTo(2.15f).Within(0.0001f),
                    "Bridge water must equal baseElevation - 0.85.");
            }
        }

        [Test]
        public void WaterRibbon_UsesCarvedTerrainWithoutRuntimeChunkMutation()
        {
            var plan = new MacroWorldPlan(5);
            plan.AddRiver(new WorldRiverData
            {
                stableId = 3,
                nominalWidth = 3f,
                centerline = new List<Vector2>
                {
                    new Vector2(4f, 8f),
                    new Vector2(28f, 8f)
                }
            });

            // 2x2 cells, 3x3 mesh vertices at constant -2m.
            var bed = new Mesh();
            owned.Add(bed);
            var positions = new Vector3[9];
            for (int z = 0; z < 3; z++)
                for (int x = 0; x < 3; x++)
                    positions[z * 3 + x] = new Vector3(x * 16f, -2f, z * 16f);
            bed.vertices = positions;

            Mesh surface = RiverWaterMeshBuilder.Build(
                plan, new ChunkCoordinate(0, 0), 32f,
                bed, 0.55f, 10f);
            Assert.That(surface, Is.Not.Null);
            owned.Add(surface);
            foreach (Vector3 point in surface.vertices)
                Assert.That(point.y, Is.EqualTo(-1.45f).Within(0.0001f));
            foreach (Vector3 point in bed.vertices)
                Assert.That(point.y, Is.EqualTo(-2f).Within(0.0001f));
        }

        [Test]
        public void WaterRibbon_SkipsUnrelatedOrEmptyChunks()
        {
            var empty = new MacroWorldPlan(1);
            Assert.That(RiverWaterMeshBuilder.Build(
                empty, new ChunkCoordinate(0, 0), 32f, null, 0.5f, 12f),
                Is.Null);

            empty.AddRiver(new WorldRiverData
            {
                stableId = 21,
                nominalWidth = 5f,
                centerline = new List<Vector2>
                {
                    new Vector2(512f, 512f),
                    new Vector2(544f, 512f)
                }
            });
            Assert.That(RiverWaterMeshBuilder.Build(
                empty, new ChunkCoordinate(0, 0), 32f, null, 0.5f, 12f),
                Is.Null);
        }

        private static void AssertChunkBounds(Mesh mesh, float size)
        {
            Assert.That(mesh.triangles.Length, Is.GreaterThan(0));
            foreach (Vector3 p in mesh.vertices)
            {
                Assert.That(p.x, Is.InRange(-0.0001f, size + 0.0001f));
                Assert.That(p.z, Is.InRange(-0.0001f, size + 0.0001f));
            }
        }

        private static List<Vector3> BorderVertices(Mesh mesh, float x)
        {
            var result = new List<Vector3>();
            foreach (Vector3 p in mesh.vertices)
                if (Mathf.Abs(p.x - x) < 0.0001f)
                    result.Add(p);
            result.Sort((a, b) => a.z.CompareTo(b.z));
            return result;
        }
    }
}
