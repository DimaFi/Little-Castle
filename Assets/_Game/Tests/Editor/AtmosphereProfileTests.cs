using LittleCastle.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace LittleCastle.Tests
{
    public sealed class AtmosphereProfileTests
    {
        const string Root = "Assets/_Game/Settings/Rendering/Atmosphere/";

        [Test]
        public void Twilight_StaysInUnitRangeAcrossMidnightAndHasFullDawnAndDusk()
        {
            for (float hour = 0; hour < 24; hour += .025f)
                Assert.That(AtmosphereProfile.TwilightWeight(hour, 6, 20, 1.25f), Is.InRange(0, 1));
            Assert.That(AtmosphereProfile.TwilightWeight(6, 6, 20, 1.25f), Is.EqualTo(1));
            Assert.That(AtmosphereProfile.TwilightWeight(20, 6, 20, 1.25f), Is.EqualTo(1));
            Assert.That(AtmosphereProfile.TwilightWeight(13, 6, 20, 1.25f), Is.EqualTo(0));
        }

        [Test]
        public void AuthoredCycle_HasFourDistinctLooksAndValidFog()
        {
            var cycle = AssetDatabase.LoadAssetAtPath<AtmosphereProfile>(Root + "LittleCastleAtmosphere.asset");
            Assert.That(cycle, Is.Not.Null);
            Assert.That(cycle.IsComplete, Is.True);
            foreach (var look in new[] { cycle.sunnyDay, cycle.goldenHour, cycle.blueNight, cycle.snowyMoonlight })
            {
                Assert.That(look.look.fogEnd, Is.GreaterThan(look.look.fogStart));
                Assert.That(look.look.ambientSky.maxColorComponent, Is.GreaterThan(.2f), "Night must stay readable.");
            }
            Assert.That(cycle.goldenHour.look.direct.b, Is.LessThan(cycle.sunnyDay.look.direct.b));
            Assert.That(cycle.snowyMoonlight.look.ambientGround.b, Is.GreaterThan(cycle.blueNight.look.ambientGround.b));
        }

        [Test]
        public void Snow_ChangesNightBounceWithoutChangingSunnyDay()
        {
            var cycle = AssetDatabase.LoadAssetAtPath<AtmosphereProfile>(Root + "LittleCastleAtmosphere.asset");
            Assert.That(cycle.Evaluate(1, 0, 60, 0).zenith, Is.EqualTo(cycle.Evaluate(1, 0, 60, 1).zenith));
            Assert.That(cycle.Evaluate(0, 0, -60, 0).ambientGround, Is.EqualTo(cycle.blueNight.look.ambientGround));
            Assert.That(cycle.Evaluate(0, 0, -60, 1).ambientGround, Is.EqualTo(cycle.snowyMoonlight.look.ambientGround));
        }

        [Test]
        public void LowSun_StaysGoldenWhenDirectIntensityFades()
        {
            var cycle = AssetDatabase.LoadAssetAtPath<AtmosphereProfile>(Root + "LittleCastleAtmosphere.asset");
            Assert.That(cycle.Evaluate(.01f, .5f, 1, 0).direct, Is.EqualTo(cycle.goldenHour.look.direct));
        }

        [Test]
        public void Blend_IsContinuousAtDaylightAndGoldenBoundaries()
        {
            var cycle = AssetDatabase.LoadAssetAtPath<AtmosphereProfile>(Root + "LittleCastleAtmosphere.asset");
            foreach (float elevation in new[] { 10f, 40f })
            {
                Color a = cycle.Evaluate(.7f, .1f, elevation - .001f, .4f).horizon;
                Color b = cycle.Evaluate(.7f, .1f, elevation + .001f, .4f).horizon;
                Assert.That(Vector4.Distance(a, b), Is.LessThan(.001f));
            }
            Assert.That(Vector4.Distance(cycle.Evaluate(0, 1, 0, 0).zenith,
                cycle.Evaluate(.00001f, 1, 0, 0).zenith), Is.LessThan(.001f));
        }

        [Test]
        public void SurfaceArtControls_InterpolateAndRetainLegacyZeroDefaults()
        {
            var legacy = default(AtmosphereLook);
            Assert.That(legacy.groundBounceStrength, Is.Zero);
            Assert.That(legacy.toneMapping, Is.Zero);
            var authored = new AtmosphereLook {groundBounce = Color.red, groundBounceStrength = .4f,
                toneMapping = .8f, exposure = 1};
            var middle = AtmosphereLook.Lerp(legacy, authored, .5f);
            Assert.That(middle.groundBounceStrength, Is.EqualTo(.2f).Within(.0001f));
            Assert.That(middle.groundBounce.r, Is.EqualTo(.5f).Within(.0001f));
            Assert.That(middle.toneMapping, Is.EqualTo(.4f).Within(.0001f));
            Assert.That(middle.exposure, Is.EqualTo(.5f).Within(.0001f));
        }

        [TestCase(float.NaN, 0)]
        [TestCase(float.PositiveInfinity, 0)]
        [TestCase(-1, 0)]
        [TestCase(2, 1)]
        public void ExternalWeather_ClampsInvalidInput(float input, float expected)
        {
            Assert.That(AtmosphereProfile.SanitizeUnit(input), Is.EqualTo(expected));
        }
    }
}
