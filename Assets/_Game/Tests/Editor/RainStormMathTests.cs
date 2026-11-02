using LittleCastle.World.Weather;
using NUnit.Framework;

namespace LittleCastle.Tests
{
    public sealed class RainStormMathTests
    {
        [Test]
        public void IntensityPresetsAreMonotonic()
        {
            Assert.AreEqual(0f, RainStormMath.Intensity(RainMode.Off));
            Assert.Less(RainStormMath.Intensity(RainMode.Drizzle), RainStormMath.Intensity(RainMode.Rain));
            Assert.Less(RainStormMath.Intensity(RainMode.Rain), RainStormMath.Intensity(RainMode.Downpour));
            Assert.Less(RainStormMath.Intensity(RainMode.Downpour), RainStormMath.Intensity(RainMode.Storm));
        }

        [Test]
        public void HashAndWorldCellAreDeterministicAcrossNegativeCoordinates()
        {
            Assert.AreEqual(RainStormMath.Hash(12345), RainStormMath.Hash(12345));
            Assert.AreEqual(-1, RainStormMath.Cell(-0.01f, 7));
            Assert.AreEqual(-2, RainStormMath.Cell(-7.01f, 7));
            Assert.AreEqual(1, RainStormMath.Cell(7.01f, 7));
        }

        [Test]
        public void WetnessGrowsAndThenDriesWithoutGoingNegative()
        {
            float wet = 0f;
            for (int i = 0; i < 120; i++) wet = RainStormMath.Wetness(wet, 1f, .1f, 2f, 5f);
            Assert.Greater(wet, .9f);
            float previous = wet;
            for (int i = 0; i < 60; i++) wet = RainStormMath.Wetness(wet, 0f, .1f, 2f, 5f);
            Assert.Less(wet, previous);
            Assert.GreaterOrEqual(wet, 0f);
        }

        [Test]
        public void WeatherQualityCapsCustomBudgets()
        {
            var custom = new RainBudget { nearParticles = 99999, distantParticles = -3,
                probeCount = 1000, puddleCount = 1000, splashCount = 1000 };
            var b = RainBudget.For(RainQuality.Custom, custom);
            Assert.AreEqual(4000, b.nearParticles);
            Assert.AreEqual(0, b.distantParticles);
            Assert.AreEqual(24, b.probeCount);
            Assert.AreEqual(64, b.puddleCount);
            Assert.AreEqual(40, b.splashCount);
            Assert.AreEqual(0, RainBudget.For(RainQuality.Off, custom).nearParticles);
        }

        [Test]
        public void NoTimeNoBlendAndNoOvershoot()
        {
            Assert.AreEqual(.3f, RainStormMath.Approach(.3f, 1, 4, 0));
            Assert.AreEqual(1f, RainStormMath.Approach(.3f, 1, 0, .1f));
            Assert.LessOrEqual(RainStormMath.Approach(.3f, 1, 4, .1f), 1f);
        }
    }
}
