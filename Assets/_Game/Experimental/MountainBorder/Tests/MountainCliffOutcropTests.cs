using System;
using System.Collections.Generic;
using LittleCastle.Experimental.MountainBorder.Presentation;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace LittleCastle.Experimental.MountainBorder.Tests
{
    public sealed class MountainCliffOutcropTests
    {
        [Test]
        public void OutcropsAreDeterministicAndLeaveTheInputAndWaterCorridorUntouched()
        {
            Mesh source = MountainMassBlockoutV1.Build(1729), a = null, b = null;
            try
            {
                var vertices = source.vertices; var indices = source.triangles;
                a = MountainCliffOutcropBuilder.Build(source, 1729, 0);
                b = MountainCliffOutcropBuilder.Build(source, 1729, 0);
                CollectionAssert.AreEqual(a.vertices, b.vertices); CollectionAssert.AreEqual(a.normals, b.normals);
                CollectionAssert.AreEqual(a.triangles, b.triangles);
                CollectionAssert.AreEqual(vertices, source.vertices); CollectionAssert.AreEqual(indices, source.triangles);
                Assert.That(a, Is.Not.SameAs(source));
                foreach (Vector3 p in a.vertices)
                {
                    Assert.That((p.x >= -124f && p.x <= -77f) || (p.x >= -23f && p.x <= 0f), Is.True);
                    Assert.That(p.z, Is.InRange(12f, 28f));
                    Assert.That(float.IsNaN(p.y) || float.IsInfinity(p.y), Is.False);
                    Assert.That(p.y, Is.LessThan(35f), "Outcrops must remain beneath the original mountain/shelf envelope.");
                }
                foreach (Color32 mask in a.colors32) Assert.That(mask, Is.EqualTo(new Color32(0, 0, 0, 0)));
            }
            finally { if (a) Object.DestroyImmediate(a); if (b) Object.DestroyImmediate(b); Object.DestroyImmediate(source); }
        }

        [Test]
        public void AllOutcropLodsAreClosedFiniteNondegenerateAndWithinTheirSmallBudgets()
        {
            Mesh source = MountainMassBlockoutV1.Build(1729);
            var counts = new int[3];
            try
            {
                for (int lod = 0; lod < 3; lod++)
                {
                    Mesh output = MountainCliffOutcropBuilder.Build(source, 1729, lod);
                    try
                    {
                        var points = output.vertices; var triangles = output.triangles;
                        counts[lod] = triangles.Length / 3;
                        Assert.That(counts[lod], Is.InRange(1, lod == 0 ? 2000 : lod == 1 ? 600 : 130));
                        var edges = new Dictionary<Tuple<Vector3, Vector3>, int>();
                        var order = new Dictionary<Vector3, int>();
                        foreach (var p in points) if (!order.ContainsKey(p)) order.Add(p, order.Count);
                        double volume = 0;
                        for (int i = 0; i < triangles.Length; i += 3)
                        {
                            Vector3 a = points[triangles[i]], b = points[triangles[i + 1]], c = points[triangles[i + 2]];
                            Assert.That(Vector3.Cross(b - a, c - a).sqrMagnitude, Is.GreaterThan(1e-10f));
                            volume += Vector3.Dot(a, Vector3.Cross(b, c)) / 6d;
                            for (int j = 0; j < 3; j++)
                            {
                                Vector3 first = points[triangles[i + j]], second = points[triangles[i + (j + 1) % 3]];
                                var edge = order[first] < order[second] ? Tuple.Create(first, second) : Tuple.Create(second, first);
                                edges.TryGetValue(edge, out int count); edges[edge] = count + 1;
                            }
                        }
                        foreach (var pair in edges) Assert.That(pair.Value, Is.EqualTo(2), "Open or nonmanifold stone course.");
                        Assert.That(volume, Is.GreaterThan(0), "Stone shells must face outward.");
                        foreach (var normal in output.normals) Assert.That(normal.sqrMagnitude, Is.EqualTo(1).Within(.001f));
                    }
                    finally { Object.DestroyImmediate(output); }
                }
                Assert.That(counts[0], Is.GreaterThan(counts[1])); Assert.That(counts[1], Is.GreaterThan(counts[2]));
                TestContext.WriteLine("Cliff shoulder triangles LOD0/1/2: " + string.Join(" / ", counts));
            }
            finally { Object.DestroyImmediate(source); }
        }
    }
}
