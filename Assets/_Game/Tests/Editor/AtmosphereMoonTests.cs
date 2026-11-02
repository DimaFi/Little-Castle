using LittleCastle.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace LittleCastle.Tests
{
    public sealed class AtmosphereMoonTests
    {
        [Test]
        public void NewProfile_KeepsLegacyOrbitAndMaterialDiameterByDefault()
        {
            var profile = ScriptableObject.CreateInstance<AtmosphereProfile>();
            try
            {
                Assert.That(profile.artDirectedMoonOrbit, Is.False);
                Assert.That(profile.MoonAngularDiameter, Is.Zero);
                Assert.That(profile.MoonPhaseRotation, Is.Zero);
            }
            finally { Object.DestroyImmediate(profile); }
        }

        [Test]
        public void BoundedOrbit_CrossesHorizonAndPlacesMidnightMoonInAuthoredSkyBand()
        {
            var profile = ScriptableObject.CreateInstance<AtmosphereProfile>();
            try
            {
                profile.moonElevationRange = new Vector2(-12, 6);
                profile.moonAzimuthOffset = 41;
                // With 06:00/20:00 clock boundaries, solar angles are 180 at dusk,
                // 252 at midnight, 360 at dawn and 90 at the midpoint of the day.
                Assert.That(MoonDirection(profile, 180).y, Is.EqualTo(0).Within(.00001f));
                Assert.That(MoonDirection(profile, 360).y, Is.EqualTo(0).Within(.00001f));
                Vector3 midnight = MoonDirection(profile, 252);
                Assert.That(Mathf.Asin(midnight.y) * Mathf.Rad2Deg,
                    Is.EqualTo(5.70634f).Within(.001f));
                Assert.That(Mathf.Atan2(midnight.x, midnight.z) * Mathf.Rad2Deg,
                    Is.EqualTo(-15).Within(.001f));
                Assert.That(Mathf.Asin(MoonDirection(profile, 90).y) * Mathf.Rad2Deg,
                    Is.EqualTo(-12).Within(.001f));
            }
            finally { Object.DestroyImmediate(profile); }
        }

        [TestCase(0)]
        [TestCase(90)]
        [TestCase(180)]
        [TestCase(252)]
        [TestCase(270)]
        public void BoundedOrbit_IsContinuousAcrossClockAndHorizonBoundaries(float solarAngle)
        {
            var profile = ScriptableObject.CreateInstance<AtmosphereProfile>();
            try
            {
                float before = Mathf.Repeat(solarAngle - .001f, 360);
                float after = Mathf.Repeat(solarAngle + .001f, 360);
                Assert.That(Vector3.Distance(MoonDirection(profile, before), MoonDirection(profile, after)),
                    Is.LessThan(.0001f));
            }
            finally { Object.DestroyImmediate(profile); }
        }

        [Test]
        public void MoonControls_KeepInvalidValuesFiniteAndClampAuthoringBounds()
        {
            var profile = ScriptableObject.CreateInstance<AtmosphereProfile>();
            try
            {
                profile.moonElevationRange = new Vector2(float.NaN, float.PositiveInfinity);
                profile.moonAzimuthOffset = float.NegativeInfinity;
                profile.moonAngularDiameter = float.NaN;
                profile.moonPhase = float.PositiveInfinity;
                profile.moonPhaseRotation = float.NaN;
                Vector3 direction = -(profile.EvaluateMoonRotation(float.NaN, float.PositiveInfinity) * Vector3.forward);
                Assert.That(direction.sqrMagnitude, Is.EqualTo(1).Within(.0001f));
                Assert.That(profile.MoonAngularDiameter, Is.Zero);
                Assert.That(profile.SanitizedMoonPhase, Is.Zero);
                Assert.That(profile.MoonPhaseRotation, Is.Zero);
                profile.moonAngularDiameter = .001f;
                Assert.That(profile.MoonAngularDiameter, Is.EqualTo(.1f));
                profile.moonAngularDiameter = 100;
                profile.moonPhase = -1;
                profile.moonPhaseRotation = 1000;
                profile.moonElevationRange = new Vector2(-1000, 1000);
                Assert.That(profile.MoonAngularDiameter, Is.EqualTo(6));
                Assert.That(profile.SanitizedMoonPhase, Is.Zero);
                Assert.That(profile.MoonPhaseRotation, Is.EqualTo(180));
                Assert.That(Mathf.Asin(MoonDirection(profile, 270).y) * Mathf.Rad2Deg,
                    Is.EqualTo(89).Within(.001f));
            }
            finally { Object.DestroyImmediate(profile); }
        }

        [Test]
        public void OrbitOverride_SynchronizesLightAndOwnedSkyAndRestoresLegacyWithoutAdvancingClock()
        {
            var previousSkybox = RenderSettings.skybox;
            var previousSky = RenderSettings.ambientSkyColor;
            var previousEquator = RenderSettings.ambientEquatorColor;
            var previousGround = RenderSettings.ambientGroundColor;
            var previousMode = RenderSettings.ambientMode;
            string[] vectorGlobals = { "_LC_AtmosphereGroundBounce", "_LC_AtmosphereGrade", "_LC_CloudShadow", "_LC_CloudShadowDrift" };
            var previousGlobals = new Vector4[vectorGlobals.Length];
            for (int i = 0; i < vectorGlobals.Length; i++) previousGlobals[i] = Shader.GetGlobalVector(vectorGlobals[i]);
            float previousTransmission = Shader.GetGlobalFloat("_LC_AtmosphereTransmission");
            var root = new GameObject("Moon orbit isolation test");
            root.SetActive(false);
            var settings = ScriptableObject.CreateInstance<WorldTimeSettings>();
            var profile = ScriptableObject.CreateInstance<AtmosphereProfile>();
            var look = ScriptableObject.CreateInstance<AtmosphereLookProfile>();
            var source = new Material(Shader.Find("Little Castle/Sky/Stylized Day Night"));
            DayNightLightingController controller = null;
            try
            {
                profile.sunnyDay = profile.goldenHour = profile.blueNight = profile.snowyMoonlight = look;
                profile.moonElevationRange = new Vector2(-12, 6);
                profile.moonAzimuthOffset = 41;
                profile.moonAngularDiameter = 2;
                profile.moonPhase = .2f;
                profile.moonPhaseRotation = -25;
                var clock = root.AddComponent<WorldTimeSystem>();
                var serializedClock = new SerializedObject(clock);
                serializedClock.FindProperty("timeSettingsOverride").objectReferenceValue = settings;
                serializedClock.ApplyModifiedPropertiesWithoutUndo();
                Assert.That(clock.TryInitialize(), Is.True);
                clock.RunAutomatically = false;
                clock.RestoreState(new WorldTimeState(4, 0));
                string beforeClock = JsonUtility.ToJson(clock.CaptureState());
                controller = root.AddComponent<DayNightLightingController>();
                var moon = new GameObject("Test moon").AddComponent<Light>();
                moon.transform.SetParent(root.transform);
                moon.type = LightType.Directional;
                var serializedController = new SerializedObject(controller);
                serializedController.FindProperty("timeSystem").objectReferenceValue = clock;
                serializedController.FindProperty("moon").objectReferenceValue = moon;
                serializedController.FindProperty("sunAzimuth").floatValue = 142;
                serializedController.FindProperty("skyboxMaterial").objectReferenceValue = source;
                serializedController.FindProperty("controlFog").boolValue = false;
                serializedController.ApplyModifiedPropertiesWithoutUndo();
                controller.UnifiedProfile = profile;
                string beforeSource = EditorJsonUtility.ToJson(source);
                controller.ApplyLighting();
                Quaternion legacy = moon.transform.rotation;
                profile.artDirectedMoonOrbit = true;
                controller.ApplyLighting();
                Assert.That(Quaternion.Angle(moon.transform.rotation, profile.EvaluateMoonRotation(252, 142)), Is.LessThan(.01f));
                Assert.That(RenderSettings.skybox, Is.Not.SameAs(source));
                Vector3 skyDirection = RenderSettings.skybox.GetVector("_MoonDirection");
                Assert.That(Vector3.Distance(skyDirection, -moon.transform.forward), Is.LessThan(.00001f));
                Assert.That(RenderSettings.skybox.GetFloat("_MoonAngularDiameter"), Is.EqualTo(2));
                Assert.That(RenderSettings.skybox.GetFloat("_MoonPhaseRotation"), Is.EqualTo(-25));
                profile.artDirectedMoonOrbit = false;
                controller.ApplyLighting();
                Assert.That(Quaternion.Angle(moon.transform.rotation, legacy), Is.LessThan(.01f));
                profile.artDirectedMoonOrbit = true;
                controller.ApplyLighting();
                profile.blueNight = null;
                controller.ApplyLighting();
                Assert.That(Quaternion.Angle(moon.transform.rotation, legacy), Is.LessThan(.01f));
                Assert.That(RenderSettings.skybox.GetFloat("_MoonAngularDiameter"), Is.Zero);
                Assert.That(EditorJsonUtility.ToJson(source), Is.EqualTo(beforeSource));
                profile.blueNight = look;
                controller.ApplyLighting();
                controller.UnifiedProfile = null;
                controller.ApplyLighting();
                Assert.That(Quaternion.Angle(moon.transform.rotation, legacy), Is.LessThan(.01f));
                Assert.That(RenderSettings.skybox.GetFloat("_MoonAngularDiameter"), Is.Zero);
                Assert.That(JsonUtility.ToJson(clock.CaptureState()), Is.EqualTo(beforeClock));
            }
            finally
            {
                if (controller)
                    typeof(DayNightLightingController).GetMethod("ReleaseRuntimeSkybox",
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(controller, null);
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(settings);
                Object.DestroyImmediate(profile);
                Object.DestroyImmediate(look);
                Object.DestroyImmediate(source);
                RenderSettings.skybox = previousSkybox;
                RenderSettings.ambientSkyColor = previousSky;
                RenderSettings.ambientEquatorColor = previousEquator;
                RenderSettings.ambientGroundColor = previousGround;
                RenderSettings.ambientMode = previousMode;
                Shader.SetGlobalFloat("_LC_AtmosphereTransmission", previousTransmission);
                for (int i = 0; i < vectorGlobals.Length; i++) Shader.SetGlobalVector(vectorGlobals[i], previousGlobals[i]);
            }
        }

        private static Vector3 MoonDirection(AtmosphereProfile profile, float solarAngle) =>
            -(profile.EvaluateMoonRotation(solarAngle, 142) * Vector3.forward);
    }
}
