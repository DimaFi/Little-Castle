using LittleCastle.Rendering;
using LittleCastle.World;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace LittleCastle.Tests
{
    /// <summary>Source-level regression guards; real Game visual QA is still required.</summary>
    public sealed class WeatherVisualPolishTests
    {
        [Test]
        public void PlanarReflectionsNeverUsePartialCoverageWithCoherenceEnabled()
        {
            Assert.IsTrue(MapRiverReflections.CanCaptureCoherently(1, 1, true));
            Assert.IsTrue(MapRiverReflections.CanCaptureCoherently(2, 2, true));
            Assert.IsFalse(MapRiverReflections.CanCaptureCoherently(2, 1, true));
            Assert.IsFalse(MapRiverReflections.CanCaptureCoherently(3, 2, true));
            Assert.IsFalse(MapRiverReflections.CanCaptureCoherently(0, 2, true));
            Assert.IsTrue(MapRiverReflections.CanCaptureCoherently(3, 2, false));
            Assert.IsFalse(MapRiverReflections.CanCaptureCoherently(1, 0, false));
        }

        [Test]
        public void MeadowScatterSafetyRejectsMissingAndOversizedLodsWithoutTouchingMap()
        {
            Assert.IsNotNull(MeadowScatterRenderer.VisualSafetyIssue(null));
            var missing = new MeadowScatterLibrary.Prototype
            {
                stableId = "missing-card",
                lodMeshes = new Mesh[4],
                materials = new Material[1]
            };
            Assert.That(MeadowScatterRenderer.VisualSafetyIssue(missing),
                Does.Contain("Missing mesh"));

            var huge = new Mesh();
            try
            {
                huge.vertices = new[]
                {
                    Vector3.zero, new Vector3(50, 0, 0), new Vector3(0, 0, 50)
                };
                huge.triangles = new[] {0, 1, 2};
                var invalid = new MeadowScatterLibrary.Prototype
                {
                    stableId = "oversized-card",
                    localBounds = new Bounds(Vector3.zero, Vector3.one),
                    lodMeshes = new[] {huge, huge, huge, huge},
                    materials = new Material[1]
                };
                Assert.That(MeadowScatterRenderer.VisualSafetyIssue(invalid),
                    Does.Contain("oversized"));
            }
            finally { Object.DestroyImmediate(huge); }
        }

        [Test]
        public void StormSkyShaderKeepsOptionalBankOffUntilWeatherRequestsIt()
        {
            Shader shader = Shader.Find("Little Castle/Sky/Stylized Day Night");
            Assert.IsNotNull(shader);
            Assert.IsTrue(shader.isSupported, "Storm sky shader is unsupported or failed compilation.");
            var material = new Material(shader);
            try
            {
                Assert.IsTrue(material.HasProperty("_StormCloudMass"));
                Assert.IsTrue(material.HasProperty("_StormCloudBase"));
                Assert.IsTrue(material.HasProperty("_StormCloudReach"));
                Assert.IsTrue(material.HasProperty("_StormCloudDensity"));
                Assert.IsTrue(material.HasProperty("_StormPeakCloudMass"));
                Assert.IsTrue(material.HasProperty("_StormPeakCloudLower"));
                Assert.IsTrue(material.HasProperty("_StormPeakCloudUpper"));
                Assert.IsTrue(material.HasProperty("_StormPeakCloudDensity"));
                Assert.IsTrue(material.HasProperty("_StormPeakCloudSoftness"));
                Assert.IsNotNull(Shader.Find("Little Castle/Sky/Stylized Day Night"));
                // Three-dimensional storm cloud field is the dominant cloud
                // appearance in Storm mode; clear weather retains legacy cloud style.
                Assert.That(material.GetFloat("_StormPeakCloudMass"), Is.EqualTo(0f).Within(.0001f));
                Assert.That(material.GetFloat("_StormCloudMass"), Is.EqualTo(0f).Within(.0001f));
            }
            finally { Object.DestroyImmediate(material); }
        }

        [Test]
        public void FarSnowfallMaterialSupportsContinuousHeightAndRadialFades()
        {
            var material = Resources.Load<Material>("MapAuthoring/LocalSnowfall");
            Assert.That(material, Is.Not.Null);
            Assert.That(material.HasProperty("_FallHeight"), Is.True);
            Assert.That(material.HasProperty("_BaseHeight"), Is.True);
            Assert.That(material.HasProperty("_FieldRadius"), Is.True);
            Assert.That(material.HasProperty("_MinDistance"), Is.True);
            Assert.That(material.HasProperty("_FlakeScale"), Is.True);
            Assert.That(material.HasProperty("_LayerOpacity"), Is.True);
        }

        [Test]
        public void NewAtmosphereProfilesProvideCameraSafeCloudBankDefaults()
        {
            var profile = ScriptableObject.CreateInstance<AtmosphereProfile>();
            try
            {
                Assert.That(profile.stormBankLower, Is.GreaterThan(0f));
                Assert.That(profile.stormBankReach, Is.GreaterThan(profile.stormBankLower));
                Assert.That(profile.stormBankDensity, Is.InRange(0f, 1f));
                Assert.That(profile.stormPeakCloudMass, Is.InRange(0f, 1f));
                Assert.That(profile.stormPeakCloudUpper, Is.GreaterThan(profile.stormPeakCloudLower));
            }
            finally { Object.DestroyImmediate(profile); }
        }
    }
}
