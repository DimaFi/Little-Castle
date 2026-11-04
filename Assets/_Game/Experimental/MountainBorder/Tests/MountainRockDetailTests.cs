using System;
using System.Collections.Generic;
using LittleCastle.Experimental.MountainBorder.Presentation;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace LittleCastle.Experimental.MountainBorder.Tests
{
    public sealed class MountainRockDetailTests
    {
        [Test]
        public void DetailedFixtureIsDeterministicOwnedAndDoesNotChangeItsSource()
        {
            Mesh source = MountainMassBlockoutV1.Build(1729), a = null, b = null;
            try
            {
                var originalVertices = source.vertices; var originalTriangles = source.triangles; var originalNormals = source.normals;
                a = MountainRockDetailBuilder.Build(source, 1729, 0);
                b = MountainRockDetailBuilder.Build(source, 1729, 0);
                Assert.That(a, Is.Not.SameAs(source)); Assert.That(a, Is.Not.SameAs(b));
                CollectionAssert.AreEqual(a.vertices, b.vertices); CollectionAssert.AreEqual(a.triangles, b.triangles);
                CollectionAssert.AreEqual(a.normals, b.normals);
                CollectionAssert.AreEqual(originalVertices, source.vertices); CollectionAssert.AreEqual(originalTriangles, source.triangles);
                CollectionAssert.AreEqual(originalNormals, source.normals);
            }
            finally { if (a != null) Object.DestroyImmediate(a); if (b != null) Object.DestroyImmediate(b); Object.DestroyImmediate(source); }
        }

        [Test]
        public void AllLodsAreUpwardBoundedAndKeepControlsWaterAndAClosedSharedSurface()
        {
            Mesh source = MountainMassBlockoutV1.Build(1729);
            var counts = new int[3];
            try
            {
                for (int lod = 0; lod < 3; lod++)
                {
                    Mesh result = MountainRockDetailBuilder.Build(source, 1729, lod);
                    try
                    {
                        Vector3[] vertices = result.vertices, normals = result.normals; int[] triangles = result.triangles;
                        counts[lod] = triangles.Length / 3;
                        Assert.That(counts[lod], Is.InRange(source.triangles.Length / 3, 120000));
                        var positions = new HashSet<Vector3>(vertices);
                        foreach (Vector3 original in source.vertices)
                            if (original.z >= 72f || InsideWater(original) || IsOuter(original))
                                Assert.That(positions.Contains(original), Is.True, "Moved original control or protected sample: " + original);
                        for (int i = 0; i < vertices.Length; i++)
                        {
                            Vector3 p = vertices[i];
                            Assert.That(float.IsNaN(p.y) || float.IsInfinity(p.y), Is.False);
                            Assert.That(normals[i].sqrMagnitude, Is.EqualTo(1f).Within(.001f));
                            Assert.That(normals[i].y, Is.GreaterThan(0f));
                            if (InsideWater(p)) Assert.That(p.y, Is.EqualTo(MountainMassBlockoutV1.Height(1729, new Vector2(p.x, p.z))).Within(.002f));
                        }
                        AssertNoInteriorBoundary(vertices, triangles);
                        for (int i = 0; i < triangles.Length; i += 3)
                            Assert.That(Vector3.Cross(vertices[triangles[i + 1]] - vertices[triangles[i]], vertices[triangles[i + 2]] - vertices[triangles[i]]).y,
                                Is.GreaterThan(0f), "Folded triangle at " + i);
                    }
                    finally { Object.DestroyImmediate(result); }
                }
                Assert.That(counts[0], Is.GreaterThan(counts[1])); Assert.That(counts[1], Is.GreaterThan(counts[2]));
                TestContext.WriteLine("Structural detail triangles LOD0/1/2: " + string.Join(" / ", counts));
            }
            finally { Object.DestroyImmediate(source); }
        }

        [Test]
        public void BroadDrySlopeGetsVisibleStructuralCutsInEveryLodWithoutMovingControls()
        {
            // An affine 45-degree dry face makes displacement measurable
            // independently of the fixture's authored macro creases/textures.
            var source = new Mesh();
            var controlPoints = new Vector3[25];
            var indices = new List<int>();
            for (int z = 0; z < 5; z++) for (int x = 0; x < 5; x++)
                controlPoints[z * 5 + x] = new Vector3(-32f + x * 16f, 75f + z * 16f, 90f + z * 16f);
            for (int z = 0; z < 4; z++) for (int x = 0; x < 4; x++)
            {
                int a = z * 5 + x, b = a + 1, c = a + 5, d = c + 1;
                indices.AddRange(new[] { a, c, b, b, c, d });
            }
            source.vertices = controlPoints; source.triangles = indices.ToArray();
            var settings = new MountainRockDetailSettings();
            try
            {
                for (int lod = 0; lod < 3; lod++)
                {
                    Mesh result = MountainRockDetailBuilder.Build(source, 1729, lod, settings);
                    try
                    {
                        var positions = new HashSet<Vector3>(result.vertices);
                        foreach (var control in controlPoints) Assert.That(positions.Contains(control), Is.True);
                        float maximumCut = 0; int significantCuts = 0;
                        foreach (var point in positions)
                        {
                            float cut = 75f + point.z - 90f - point.y;
                            maximumCut = Mathf.Max(maximumCut, cut);
                            if (cut > settings.relief * .5f) significantCuts++;
                            Assert.That(cut, Is.GreaterThanOrEqualTo(-.0001f), "Alpine structure must not raise new peaks.");
                            Assert.That(cut, Is.LessThan(settings.relief * 2f), "Structural excavation is bounded.");
                        }
                        Assert.That(maximumCut, Is.GreaterThan(settings.relief * .75f), "Far LOD must retain metre-scale structure.");
                        Assert.That(significantCuts, Is.GreaterThan(8), "One isolated dent is not structural slope relief.");
                        Assert.That(result.triangles.Length / 3, Is.LessThanOrEqualTo(settings.maximumTriangles));
                    }
                    finally { Object.DestroyImmediate(result); }
                }
                CollectionAssert.AreEqual(controlPoints, source.vertices);
            }
            finally { Object.DestroyImmediate(source); }
        }

        [Test]
        public void RefinementStitchesAnExistingCollinearTJunction()
        {
            var source = new Mesh(); Mesh result = null;
            source.vertices = new[] { new Vector3(0, 30, 80), new Vector3(10, 35, 80), new Vector3(0, 30, 90),
                new Vector3(10, 35, 90), new Vector3(5, 32.5f, 85) };
            // Two small faces meet one coarse face on the shared b--c diagonal.
            source.triangles = new[] { 0, 2, 1, 1, 4, 3, 4, 2, 3 };
            try
            {
                result = MountainRockDetailBuilder.Build(source, 7, 0, new MountainRockDetailSettings { relief = 0 });
                Assert.That(result.triangles.Length, Is.GreaterThan(source.triangles.Length));
                var points = result.vertices; var indices = result.triangles;
                var edges = PositionEdges(points, indices);
                foreach (var edge in edges)
                    if (edge.Value == 1)
                    {
                        Vector3 a = edge.Key.Item1, b = edge.Key.Item2;
                        Assert.That((Mathf.Abs(a.x) < .001f && Mathf.Abs(b.x) < .001f) ||
                                    (Mathf.Abs(a.x - 10) < .001f && Mathf.Abs(b.x - 10) < .001f) ||
                                    (Mathf.Abs(a.z - 80) < .001f && Mathf.Abs(b.z - 80) < .001f) ||
                                    (Mathf.Abs(a.z - 90) < .001f && Mathf.Abs(b.z - 90) < .001f), Is.True,
                            "Unstitched interior edge: " + a + " / " + b);
                    }
            }
            finally { if (result != null) Object.DestroyImmediate(result); Object.DestroyImmediate(source); }
        }

        static bool InsideWater(Vector3 p) => p.x >= -74f && p.x <= -24f && p.z >= -58f && p.z <= 72f;
        static bool IsOuter(Vector3 p) => Mathf.Abs(p.x + 150f) < .001f || Mathf.Abs(p.x - 150f) < .001f ||
            Mathf.Abs(p.z + 70f) < .001f || Mathf.Abs(p.z - 180f) < .001f;

        static void AssertNoInteriorBoundary(Vector3[] positions, int[] indices)
        {
            foreach (var edge in PositionEdges(positions, indices))
            {
                Assert.That(edge.Value, Is.InRange(1, 2), "Nonmanifold geometric edge");
                if (edge.Value == 1) Assert.That(IsOuter(edge.Key.Item1) && IsOuter(edge.Key.Item2), Is.True,
                    "Open interior edge: " + edge.Key.Item1 + " / " + edge.Key.Item2);
            }
        }

        static Dictionary<Tuple<Vector3, Vector3>, int> PositionEdges(Vector3[] positions, int[] indices)
        {
            var order = new Dictionary<Vector3, int>();
            foreach (Vector3 p in positions) if (!order.ContainsKey(p)) order.Add(p, order.Count);
            var edges = new Dictionary<Tuple<Vector3, Vector3>, int>();
            for (int i = 0; i < indices.Length; i += 3) for (int j = 0; j < 3; j++)
            {
                Vector3 a = positions[indices[i + j]], b = positions[indices[i + (j + 1) % 3]];
                var key = order[a] < order[b] ? Tuple.Create(a, b) : Tuple.Create(b, a);
                edges.TryGetValue(key, out int count); edges[key] = count + 1;
            }
            return edges;
        }
    }
}
