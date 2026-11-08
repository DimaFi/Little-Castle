using LittleCastle.World.Weather;
using NUnit.Framework;
using UnityEngine;

namespace LittleCastle.Tests
{
    public sealed class RainStormMathTests
    {
        [TestCase(640,480)]
        [TestCase(1280,720)]
        [TestCase(1920,1080)]
        public void WeatherPanelStaysRightOfWorldEditorAndInsideViewport(int width,int height)
        {
            var panel=RainStormController.DebugPanelBounds(width,height);
            Assert.That(panel.xMin,Is.GreaterThan(320*Mathf.Clamp(width/1600f,.85f,1.3f)));
            Assert.That(panel.xMax,Is.LessThanOrEqualTo(width));Assert.That(panel.yMax,Is.LessThanOrEqualTo(height));
        }
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
        public void BothRainShadersLoadFromResources()
        {
            Assert.IsNotNull(Resources.Load<Shader>("RainStorm/LC_RainStreak"));
            Assert.IsNotNull(Resources.Load<Shader>("RainStorm/LC_RainRipple"));
        }

        [Test]
        public void DistantLightningShaderIsAvailableForURPStorm()
        {
            Assert.IsNotNull(Resources.Load<Shader>("RainStorm/LC_StormLightning"),
                "The distant lightning shader must be included in Resources.");
        }

        [Test]
        public void LightningChannelRequiresAnActiveVisibleCloudBank()
        {
            var owner = new GameObject("Lightning placement test");
            var cameraObject = new GameObject("Lightning test camera");
            var bank = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var renderer = bank.GetComponent<Renderer>();
            try
            {
                var controller = owner.AddComponent<RainStormController>();
                var camera = cameraObject.AddComponent<Camera>();
                camera.farClipPlane = 960f;
                bank.transform.position = new Vector3(0f, 0f, 375f);
                bank.transform.localScale = new Vector3(310f, 96f, 150f);
                const System.Reflection.BindingFlags flags =
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                typeof(RainStormController).GetField("gameplayCamera", flags).SetValue(controller, camera);
                var select = typeof(RainStormController).GetMethod("TryGetCloudBolt", flags);
                var args = new object[] { 123u, Vector3.zero, Vector3.zero };
                RainStormController.RegisterLightningCloud(renderer);
                Assert.IsTrue((bool)select.Invoke(controller, args));
                Assert.IsTrue(renderer.bounds.Contains((Vector3)args[1]));
                Assert.IsTrue(renderer.bounds.Contains((Vector3)args[2]));

                camera.fieldOfView = 44f;
                camera.aspect = 16f / 9f;
                bank.transform.position = camera.ViewportToWorldPoint(new Vector3(.10f, .92f, 400f));
                Assert.IsTrue((bool)select.Invoke(controller, args),
                    "A bank cropped at the upper-left edge must allow a compact intracloud channel.");

                renderer.enabled = false;
                Assert.IsFalse((bool)select.Invoke(controller, args), "Hidden bank must produce sheet lightning only.");
                renderer.enabled = true;
                bank.transform.position = new Vector3(0f, 0f, -375f);
                Assert.IsFalse((bool)select.Invoke(controller, args), "Do not project a bolt from a bank behind the camera.");
                bank.transform.position = new Vector3(0f, 0f, 375f);
                RainStormController.UnregisterLightningCloud(renderer);
                Assert.IsFalse((bool)select.Invoke(controller, args), "No registered cloud must leave no floating channel.");
            }
            finally
            {
                RainStormController.UnregisterLightningCloud(renderer);
                Object.DestroyImmediate(bank);
                Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void WinterSnowfallCanBeDisabledWithoutDisablingStormSnow()
        {
            Assert.AreEqual(0f,
                LittleCastle.Rendering.MapSnowfallPresentation.EvaluateIntensity(
                    1f, 1f, false, 0f, 0f, 0f), .0001f);
            Assert.Greater(
                LittleCastle.Rendering.MapSnowfallPresentation.EvaluateIntensity(
                    1f, 1f, false, 0f, 1f, 0f), .99f);
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
