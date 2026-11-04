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
        public void WinterPrecipitationGrowsDuringRainAndStorm()
        {
            float quiet = MapSnowfallPresentation.EvaluateIntensity(1f,0f,false,0f,0f);
            float rain = MapSnowfallPresentation.EvaluateIntensity(1f,0f,false,0f,.48f);
            float storm = MapSnowfallPresentation.EvaluateIntensity(1f,0f,false,0f,1f);
            Assert.AreEqual(.17f, quiet, .0001f);
            Assert.Greater(rain, quiet);
            Assert.AreEqual(1f, storm, .0001f);
            Assert.AreEqual(0f, MapSnowfallPresentation.EvaluateIntensity(0f,0f,true,1f,1f), .0001f);
            Assert.AreEqual(0f, MapSnowfallPresentation.EvaluateIntensity(1f,1f,false,0f,0f,0f), .0001f);
        }
    }
}
