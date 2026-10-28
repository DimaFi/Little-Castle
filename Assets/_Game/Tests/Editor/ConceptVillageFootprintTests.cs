using LittleCastle.World;
using NUnit.Framework;

namespace LittleCastle.Tests
{
    public sealed class ConceptVillageFootprintTests
    {
        [Test]
        public void IsSupportedFootprint_AcceptsFiveFiniteSamplesWithinSpread()
        {
            float[] heights = { 3f, 3.1f, 3.2f, 3.05f, 3.15f };

            Assert.That(
                ConceptVillageFootprintPresenter.IsSupportedFootprint(
                    heights,
                    0.35f),
                Is.True);
        }

        [Test]
        public void IsSupportedFootprint_RejectsNaNSample()
        {
            float[] heights = { 3f, 3.1f, float.NaN, 3.05f, 3.15f };

            Assert.That(
                ConceptVillageFootprintPresenter.IsSupportedFootprint(
                    heights,
                    0.35f),
                Is.False);
        }

        [Test]
        public void IsSupportedFootprint_RejectsInfiniteSample()
        {
            float[] heights =
                { 3f, 3.1f, float.PositiveInfinity, 3.05f, 3.15f };

            Assert.That(
                ConceptVillageFootprintPresenter.IsSupportedFootprint(
                    heights,
                    0.35f),
                Is.False);
        }

        [Test]
        public void IsSupportedFootprint_AcceptsInclusiveMaximumBoundary()
        {
            float[] heights = { 0f, 0.35f, 0.1f, 0.2f, 0.3f };

            Assert.That(
                ConceptVillageFootprintPresenter.IsSupportedFootprint(
                    heights,
                    0.35f),
                Is.True);
        }

        [Test]
        public void IsSupportedFootprint_RejectsSpreadAboveMaximum()
        {
            float[] heights = { 1f, 1.351f, 1.1f, 1.2f, 1.3f };

            Assert.That(
                ConceptVillageFootprintPresenter.IsSupportedFootprint(
                    heights,
                    0.35f),
                Is.False);
        }

        [Test]
        public void IsSupportedFootprint_RejectsUnsafeArguments()
        {
            Assert.That(
                ConceptVillageFootprintPresenter.IsSupportedFootprint(
                    null,
                    0.35f),
                Is.False);
            Assert.That(
                ConceptVillageFootprintPresenter.IsSupportedFootprint(
                    new[] { 1f, 1f, 1f, 1f },
                    0.35f),
                Is.False);
            Assert.That(
                ConceptVillageFootprintPresenter.IsSupportedFootprint(
                    new[] { 1f, 1f, 1f, 1f, 1f },
                    -0.01f),
                Is.False);
            Assert.That(
                ConceptVillageFootprintPresenter.IsSupportedFootprint(
                    new[] { 1f, 1f, 1f, 1f, 1f },
                    float.NaN),
                Is.False);
            Assert.That(
                ConceptVillageFootprintPresenter.IsSupportedFootprint(
                    new[] { 1f, 1f, 1f, 1f, 1f },
                    float.PositiveInfinity),
                Is.False);
        }
    }
}
