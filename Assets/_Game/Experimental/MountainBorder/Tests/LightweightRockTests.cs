using System;
using System.Collections.Generic;
using LittleCastle.Experimental.MountainBorder.Presentation;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace LittleCastle.Experimental.MountainBorder.Tests
{
    public sealed class LightweightRockTests
    {
        [Test]
        public void SeedsAreRepeatableDistinctAndDoNotUseUnityRandomState()
        {
            var state = UnityEngine.Random.state;
            Mesh a = null, repeat = null, different = null;
            try
            {
                a = LightweightRockBuilder.Build(1729, 0);
                repeat = LightweightRockBuilder.Build(1729, 0);
                different = LightweightRockBuilder.Build(1730, 0);
                CollectionAssert.AreEqual(a.vertices, repeat.vertices);
                CollectionAssert.AreEqual(a.normals, repeat.normals);
                CollectionAssert.AreEqual(a.triangles, repeat.triangles);
                Assert.That(a.vertices, Is.Not.EqualTo(different.vertices));
                Assert.That(UnityEngine.Random.state, Is.EqualTo(state));
                Assert.That(a, Is.Not.SameAs(repeat));
            }
            finally
            {
                if (a) Object.DestroyImmediate(a);
                if (repeat) Object.DestroyImmediate(repeat);
                if (different) Object.DestroyImmediate(different);
            }
        }

        [Test]
        public void AllLodsHaveClosedOrientedStoneShellsAndBoundedCostAcrossSeeds()
        {
            var seeds = new[] { 0, 1, -1, 1729, 65537, int.MinValue, int.MaxValue };
            var expected = new[] { 76, 44, 26 };
            foreach (int seed in seeds)
                for (int lod = 0; lod < 3; lod++)
                {
                    Mesh mesh = LightweightRockBuilder.Build(seed, lod);
                    try
                    {
                        var points = mesh.vertices; var normals = mesh.normals; var triangles = mesh.triangles;
                        Assert.That(triangles.Length / 3, Is.EqualTo(expected[lod]));
                        Assert.That(points.Length, Is.LessThanOrEqualTo(228));
                        Assert.That(mesh.subMeshCount, Is.EqualTo(1));
                        Assert.That(mesh.uv.Length, Is.EqualTo(points.Length));
                        Assert.That(mesh.colors32.Length, Is.EqualTo(points.Length));
                        Assert.That(mesh.bounds.min.y, Is.EqualTo(0).Within(.00001f));
                        Assert.That(mesh.bounds.max.y, Is.InRange(2.3f, 2.60001f));
                        var order = new Dictionary<Vector3, int>();
                        foreach (Vector3 point in points)
                        {
                            Assert.That(Finite(point.x) && Finite(point.y) && Finite(point.z), Is.True);
                            if (!order.ContainsKey(point)) order.Add(point, order.Count);
                        }
                        var edges = new Dictionary<Tuple<int, int>, int>();
                        var orientation = new Dictionary<Tuple<int, int>, int>();
                        double volume = 0;
                        for (int i = 0; i < triangles.Length; i += 3)
                        {
                            Vector3 a = points[triangles[i]], b = points[triangles[i + 1]], c = points[triangles[i + 2]];
                            Vector3 cross = Vector3.Cross(b - a, c - a);
                            Assert.That(cross.sqrMagnitude, Is.GreaterThan(1e-12f));
                            volume += Vector3.Dot(a, Vector3.Cross(b, c)) / 6d;
                            for (int j = 0; j < 3; j++)
                            {
                                Vector3 normal = normals[triangles[i + j]];
                                Assert.That(Vector3.Dot(normal, cross.normalized), Is.GreaterThan(.9999f));
                                Assert.That(normal.sqrMagnitude, Is.EqualTo(1).Within(.0001f));
                                int first = order[points[triangles[i + j]]], second = order[points[triangles[i + (j + 1) % 3]]];
                                var key = Tuple.Create(Math.Min(first, second), Math.Max(first, second));
                                edges.TryGetValue(key, out int count); edges[key] = count + 1;
                                orientation.TryGetValue(key, out int sum); orientation[key] = sum + (first < second ? 1 : -1);
                            }
                        }
                        foreach (var pair in edges)
                        {
                            Assert.That(pair.Value, Is.EqualTo(2), "Open/nonmanifold shell.");
                            Assert.That(orientation[pair.Key], Is.Zero, "Inconsistent face winding.");
                        }
                        Assert.That(volume, Is.GreaterThan(0), "Shell must face outward.");
                        foreach (Color32 color in mesh.colors32) Assert.That(color, Is.EqualTo(new Color32(0, 0, 0, 0)));
                    }
                    finally { Object.DestroyImmediate(mesh); }
                }
        }

        [Test]
        public void InvalidLodsAndNonfiniteOrUnboundedDimensionsAreRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => LightweightRockBuilder.Build(0, -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => LightweightRockBuilder.Build(0, 3));
            foreach (float value in new[] { float.NaN, float.PositiveInfinity, 0f, 13f })
                Assert.Throws<ArgumentException>(() => LightweightRockBuilder.Build(0, 0, new LightweightRockSettings { width = value }));
            Assert.Throws<ArgumentException>(() => LightweightRockBuilder.Build(0, 0, new LightweightRockSettings { height = float.NegativeInfinity }));
            Assert.Throws<ArgumentException>(() => LightweightRockBuilder.Build(0, 0, new LightweightRockSettings { length = -.5f }));
        }

        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
