using LittleCastle.World;
using NUnit.Framework;
using UnityEngine;

namespace LittleCastle.Tests
{
    /// <summary>
    /// Independent source tests for the diagnostics input; PNG/visual
    /// acceptance must still be performed in a real Unity Editor.
    /// </summary>
    public sealed class ConceptLandscapeDiagnosticsTests
    {
        [TestCase(12345, 2048f)]
        [TestCase(54321, 4096f)]
        [TestCase(-10101, 6144f)]
        public void StylizedSampler_MultipleWorldSpansRemainFiniteAndDeterministic(
            int seed, float span)
        {
            var settings = new StylizedLandformSettings();
            bool seedDifferenceObserved = false;

            for (int z = 0; z < 13; z++)
            {
                for (int x = 0; x < 13; x++)
                {
                    float worldX = -span * 0.5f + span * x / 12f;
                    float worldZ = -span * 0.5f + span * z / 12f;

                    StylizedLandformSample a =
                        StylizedLandformSampler.Sample(
                            seed, worldX, worldZ, settings);
                    StylizedLandformSample b =
                        StylizedLandformSampler.Sample(
                            seed, worldX, worldZ, settings);
                    StylizedLandformSample changed =
                        StylizedLandformSampler.Sample(
                            seed + 1, worldX, worldZ, settings);

                    Assert.That(a.Height, Is.EqualTo(b.Height));
                    Assert.That(float.IsNaN(a.Height) ||
                        float.IsInfinity(a.Height), Is.False);
                    Assert.That(a.PlainMask, Is.InRange(0f, 1f));
                    Assert.That(a.HighlandMask, Is.InRange(0f, 1f));
                    Assert.That(a.HillMask, Is.InRange(0f, 1f));
                    Assert.That(a.ScarpMask, Is.InRange(0f, 1f));

                    if (Mathf.Abs(a.Height - changed.Height) > 0.001f)
                        seedDifferenceObserved = true;
                }
            }

            Assert.That(seedDifferenceObserved, Is.True,
                "This particular sample grid must respond to seed changes.");
        }

        [Test]
        public void NegativeGridBorder_GivesIdenticalAbsoluteWorldSamples()
        {
            var settings = new StylizedLandformSettings();
            const int seed = 12345;
            const float chunkMeters = 32f;

            // (-2,-3) and (-1,-3) share x=-32m; the shared
            // sample must not depend on which chunk visits it.
            for (int z = 0; z <= 64; z++)
            {
                float worldZ = -96f + z * 0.5f;
                float fromLeft =
                    StylizedLandformSampler.SampleHeight(
                        seed, -2f * chunkMeters + 64 * 0.5f,
                        worldZ, settings);
                float fromRight =
                    StylizedLandformSampler.SampleHeight(
                        seed, -1f * chunkMeters, worldZ, settings);
                Assert.That(fromLeft, Is.EqualTo(fromRight));
            }
        }
    }
}
