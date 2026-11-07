using NUnit.Framework;
using UnityEngine;
using LittleCastle.Rendering;
using Object = UnityEngine.Object;

namespace LittleCastle.Tests
{
    public sealed class LittleCastleOptionalAtmosphereTests
    {
        [Test]
        public void FogFollowsWeatherWithinClampedLimits()
        {
            float clear = LittleCastleOptionalAtmosphereStack.EvaluateWeatherFogDensity(
                .1f, 0f, .55f, 1.25f);
            float storm = LittleCastleOptionalAtmosphereStack.EvaluateWeatherFogDensity(
                .1f, 1f, .55f, 1.25f);
            Assert.That(clear, Is.EqualTo(.055f).Within(.00001f));
            Assert.That(storm, Is.EqualTo(.125f).Within(.00001f));
            Assert.That(LittleCastleOptionalAtmosphereStack.EvaluateWeatherFogDensity(
                1f, 5f, .55f, 2f), Is.EqualTo(1f));
        }

        [Test]
        public void NewBridgeIsOptedOutByDefault()
        {
            var owner = new GameObject("optional-atmosphere-test");
            try
            {
                var bridge = owner.AddComponent<LittleCastleOptionalAtmosphereStack>();
                Assert.IsFalse(bridge.IsIntegrationRequested);
                Assert.IsFalse(bridge.IsPhysicalSkyActive);
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }
    }
}
