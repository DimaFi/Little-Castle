using System.Collections.Generic;
using LittleCastle.World;
using NUnit.Framework;
using UnityEngine;

namespace LittleCastle.Tests
{
    /// <summary>
    /// Exact legacy-mask parity and pure river-envelope corner cases.
    /// The independent LegacyExpected function deliberately retains the
    /// Q04-before-refactor expression order as a regression oracle.
    /// </summary>
    public sealed class RiverEnvelopeTests
    {
        private const float BankEdgeMeters = 1.8f;

        [Test]
        public void ProfileWidth_InterpolatesAtPointOfClosestApproach()
        {
            WorldRiverData river = River(
                7,
                new Vector2(-20f, -10f),
                new Vector2(20f, -10f));
            river.widths = new List<float> { 2f, 10f };

            Assert.That(RiverEnvelopeUtility.TryCreateSegment(
                river, 0, out var segment), Is.True);
            Assert.That(segment.halfStartWidth, Is.EqualTo(1f));
            Assert.That(segment.halfEndWidth, Is.EqualTo(5f));

            float a = RiverEnvelopeUtility.WetnessAt(
                new Vector2(-18f, -7f), segment, BankEdgeMeters);
            float b = RiverEnvelopeUtility.WetnessAt(
                new Vector2(18f, -7f), segment, BankEdgeMeters);
            Assert.That(b, Is.GreaterThan(a));
            Assert.That(a, Is.InRange(0f, 1f));
            Assert.That(b, Is.InRange(0f, 1f));
        }

        [Test]
        public void ZeroLengthAndRepeatedPoints_AreSkippedWithoutChangingIndices()
        {
            WorldRiverData river = River(
                5,
                new Vector2(-10f, -10f),
                new Vector2(0f, 0f),
                new Vector2(0f, 0f),
                new Vector2(10f, 10f));
            river.widths = new List<float> { 2f, 4f, 4f, 8f };
            Assert.That(RiverEnvelopeUtility.TryCreateSegment(
                river, 0, out _), Is.True);
            Assert.That(RiverEnvelopeUtility.TryCreateSegment(
                river, 1, out _), Is.False);
            Assert.That(RiverEnvelopeUtility.TryCreateSegment(
                river, 2, out var later), Is.True);
            Assert.That(later.halfStartWidth, Is.EqualTo(2f));
            Assert.That(later.halfEndWidth, Is.EqualTo(4f));
            Assert.That(RiverEnvelopeUtility.TryCreateSegment(
                river, -1, out _), Is.False);
            Assert.That(RiverEnvelopeUtility.TryCreateSegment(
                river, 3, out _), Is.False);
            Assert.That(RiverEnvelopeUtility.TryCreateSegment(
                null, 0, out _), Is.False);
        }

        [Test]
        public void NegativeChunkBroadPhase_IncludesTouchedBoundary()
        {
            var segment = new RiverEnvelopeUtility.Segment(
                new Vector2(-40f, -16f), new Vector2(-30f, -16f),
                0.1f, 0.1f);

            Assert.That(RiverEnvelopeUtility.OverlapsChunk(
                segment, 1.8f, -32f, -32f, 0f, 0f), Is.True);
            Assert.That(RiverEnvelopeUtility.OverlapsChunk(
                segment, 0f, 0f, -32f, 32f, 0f), Is.False);
            Assert.That(RiverEnvelopeUtility.OverlapsChunk(
                segment, 1.8f, -80f, -32f, -45f, 0f), Is.False);
            Assert.That(RiverEnvelopeUtility.OverlapsChunk(
                segment, 1.8f, -31.9f, -32f, 0f, 0f), Is.True);
        }

        [Test]
        public void SharpBend_MaximumEnvelopeIncludesWideNeighborSegment()
        {
            WorldRiverData river = River(
                8,
                new Vector2(-10f, 0f),
                new Vector2(0f, 0f),
                new Vector2(0f, 10f));
            river.widths = new List<float> { 12f, 12f, 1f };
            Assert.That(RiverEnvelopeUtility.TryCreateSegment(
                river, 0, out var broad), Is.True);
            Assert.That(RiverEnvelopeUtility.TryCreateSegment(
                river, 1, out var narrow), Is.True);

            Vector2 point = new Vector2(4f, 3f);
            float broadValue = RiverEnvelopeUtility.WetnessAt(
                point, broad, BankEdgeMeters);
            float narrowValue = RiverEnvelopeUtility.WetnessAt(
                point, narrow, BankEdgeMeters);
            Assert.That(broadValue, Is.GreaterThan(0f));
            Assert.That(broadValue, Is.GreaterThan(narrowValue),
                "Nearest-only segment selection would lose the wider bend.");
        }

        [Test]
        public void ContinuousWorldPosition_AgreesAcrossNegativePositiveSeam()
        {
            var plan = PlanWithBendsAndConfluence();
            var left = new WorldChunkData(
                new ChunkCoordinate(-1, -1), 32);
            var right = new WorldChunkData(
                new ChunkCoordinate(0, -1), 32);
            Color32[] a = ChunkTerrainVertexMasks.Build(left, 32f, plan);
            Color32[] b = ChunkTerrainVertexMasks.Build(right, 32f, plan);

            for (int z = 0; z <= 32; z++)
            {
                Color32 fromLeft = a[z * 33 + 32];
                Color32 fromRight = b[z * 33];
                Assert.That(fromLeft.g, Is.EqualTo(fromRight.g),
                    "World x=0 has one deterministic wetness value.");
                Assert.That(fromLeft.a, Is.EqualTo(255));
            }
        }

        [Test]
        public void RefactorMatchesOriginalWetnessMasks_ForBendsAndConfluences()
        {
            MacroWorldPlan plan = PlanWithBendsAndConfluence();

            foreach (ChunkCoordinate coord in new[]
            {
                new ChunkCoordinate(-2, -1),
                new ChunkCoordinate(-1, -1),
                new ChunkCoordinate(0, -1),
                new ChunkCoordinate(1, -1),
                new ChunkCoordinate(-1, 0),
                new ChunkCoordinate(0, 0)
            })
            {
                var chunk = new WorldChunkData(coord, 32);
                Color32[] actual = ChunkTerrainVertexMasks.Build(
                    chunk, 32f, plan);

                for (int z = 0; z <= 32; z++)
                {
                    for (int x = 0; x <= 32; x++)
                    {
                        Vector2 point = new Vector2(
                            coord.x * 32f + x,
                            coord.z * 32f + z);
                        byte expected = LegacyWetnessByte(
                            point, plan, coord, 32f);

                        Assert.That(actual[z * 33 + x].g,
                            Is.EqualTo(expected),
                            "Legacy mask byte changed at chunk=(" +
                            coord.x + "," + coord.z + ") local=(" +
                            x + "," + z + ").");
                        Assert.That(actual[z * 33 + x].r, Is.Zero);
                        Assert.That(actual[z * 33 + x].b, Is.Zero);
                        Assert.That(actual[z * 33 + x].a, Is.EqualTo(255));
                    }
                }
            }
        }

        [Test]
        public void WetnessLegacyParity_UniformWidthAndVerySmallHalfWidth()
        {
            MacroWorldPlan plan = new MacroWorldPlan(4);
            WorldRiverData river = River(11,
                new Vector2(-64f, -20f),
                new Vector2(64f, -20f));
            river.nominalWidth = 0.1f;
            plan.AddRiver(river);

            var chunk = new WorldChunkData(new ChunkCoordinate(-1, -1), 32);
            Color32[] colors = ChunkTerrainVertexMasks.Build(
                chunk, 32f, plan);
            for (int z = 0; z <= 32; z++)
                for (int x = 0; x <= 32; x++)
                {
                    var point = new Vector2(x - 32f, z - 32f);
                    Assert.That(colors[z * 33 + x].g,
                        Is.EqualTo(LegacyWetnessByte(
                            point, plan, chunk.Coordinate, 32f)));
                }
        }

        [Test]
        public void PureHelperPreservesOriginalDistanceAndSmoothCorridor()
        {
            var p = new Vector2(-3f, 1.25f);
            var a = new Vector2(-8f, -7f);
            var b = new Vector2(9f, -1f);
            float actual = RiverEnvelopeUtility.DistanceToSegment(
                p, a, b, out float t);
            float previous = LegacyDistance(p, a, b, out float oldT);
            Assert.That(actual, Is.EqualTo(previous));
            Assert.That(t, Is.EqualTo(oldT));

            foreach (float distance in new[] { 0f, 0.5f, 3f, 3.5f, 5f, 100f })
            {
                Assert.That(RiverEnvelopeUtility.SoftCorridor(
                        distance, 3f, BankEdgeMeters),
                    Is.EqualTo(LegacySoftCorridor(
                        distance, 3f, BankEdgeMeters)));
            }
        }

        private static byte LegacyWetnessByte(
            Vector2 point,
            MacroWorldPlan plan,
            ChunkCoordinate coord,
            float chunkSize)
        {
            float minX = coord.x * chunkSize;
            float minZ = coord.z * chunkSize;
            float maxX = minX + chunkSize;
            float maxZ = minZ + chunkSize;
            float riverbank = 0f;

            foreach (WorldRiverData river in plan.Rivers)
            {
                if (river == null || river.centerline == null ||
                    river.centerline.Count < 2)
                    continue;

                for (int s = 0; s < river.centerline.Count - 1; s++)
                {
                    Vector2 a = river.centerline[s];
                    Vector2 b = river.centerline[s + 1];
                    if ((b - a).sqrMagnitude <= 0.000001f)
                        continue;

                    float halfA = Mathf.Max(0.1f,
                        river.GetWidthAtPoint(s) * 0.5f);
                    float halfB = Mathf.Max(0.1f,
                        river.GetWidthAtPoint(s + 1) * 0.5f);
                    float padding =
                        Mathf.Max(halfA, halfB) + BankEdgeMeters;
                    if (Mathf.Max(a.x, b.x) + padding < minX ||
                        Mathf.Min(a.x, b.x) - padding > maxX ||
                        Mathf.Max(a.y, b.y) + padding < minZ ||
                        Mathf.Min(a.y, b.y) - padding > maxZ)
                        continue;

                    float distance = LegacyDistance(
                        point, a, b, out float t);
                    float halfWidth = Mathf.Lerp(halfA, halfB, t);
                    riverbank = Mathf.Max(riverbank,
                        LegacySoftCorridor(distance,
                            halfWidth, BankEdgeMeters));
                }
            }

            return (byte)Mathf.RoundToInt(
                Mathf.Clamp01(riverbank) * 255f);
        }

        private static float LegacyDistance(
            Vector2 point,
            Vector2 a,
            Vector2 b,
            out float t)
        {
            Vector2 ab = b - a;
            float lengthSquared = ab.sqrMagnitude;
            t = lengthSquared > 0.000001f
                ? Mathf.Clamp01(Vector2.Dot(point - a, ab) / lengthSquared)
                : 0f;
            return (point - (a + ab * t)).magnitude;
        }

        private static float LegacySoftCorridor(
            float distance,
            float innerRadius,
            float falloff)
        {
            if (distance <= innerRadius)
                return 1f;
            float fraction = Mathf.Clamp01(
                (distance - innerRadius) / Mathf.Max(0.01f, falloff));
            return 1f - Mathf.SmoothStep(0f, 1f, fraction);
        }

        private static MacroWorldPlan PlanWithBendsAndConfluence()
        {
            var plan = new MacroWorldPlan(123);
            var a = River(100,
                new Vector2(-45f, -22f),
                new Vector2(-8f, -16f),
                new Vector2(-8f, -16f),
                new Vector2(15f, -6f),
                new Vector2(48f, -10f));
            a.widths = new List<float> { 2f, 10f, 10f, 3f, 15f };
            plan.AddRiver(a);

            var b = River(200,
                new Vector2(-12f, 35f),
                new Vector2(0f, -12f),
                new Vector2(18f, -25f));
            b.widths = new List<float> { 3f, 5f, 9f };
            plan.AddRiver(b);
            return plan;
        }

        private static WorldRiverData River(
            int stableId, params Vector2[] points)
        {
            return new WorldRiverData
            {
                stableId = stableId,
                nominalWidth = 5f,
                centerline = new List<Vector2>(points)
            };
        }
    }
}
