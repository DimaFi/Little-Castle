using LittleCastle.Rendering;
using NUnit.Framework;

namespace LittleCastle.Tests
{
    public sealed class AmbientSnowfallTests
    {
        [Test]
        public void WinterWithoutSimulatedWeatherStillHasGentleSnowfall()
        {
            Assert.AreEqual(.17f,MapSnowfallPresentation.EvaluateIntensity(1f,0f,false,0f,0f),.0001f);
            Assert.AreEqual(.085f,MapSnowfallPresentation.EvaluateIntensity(.5f,0f,false,0f,0f),.0001f);
        }

        [Test]
        public void DrySummerDoesNotSnow()
        {
            Assert.AreEqual(0f,MapSnowfallPresentation.EvaluateIntensity(0f,0f,true,1f,0f),.0001f);
        }

        [Test]
        public void SnowCoveredGroundReceivesGentleSnowfallEvenOutsideWinterTheme()
        {
            Assert.AreEqual(.17f,MapSnowfallPresentation.EvaluateIntensity(0f,1f,false,0f,0f),.0001f);
        }

        [Test]
        public void HeavyRainSuppressesSnowButZeroRainDoesNot()
        {
            float snow=MapSnowfallPresentation.EvaluateIntensity(1f,0f,true,.7f,0f);
            Assert.Greater(snow,.5f);
            Assert.AreEqual(0f,MapSnowfallPresentation.EvaluateIntensity(1f,0f,true,.7f,1f),.0001f);
        }
    }
}
