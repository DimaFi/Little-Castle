using LittleCastle.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace LittleCastle.Tests
{
    public sealed class LightingPaletteTests
    {
        [Test]
        public void Palette_CanBePreviewedAndRemovedWithoutChangingClockOrAuthoredData()
        {
            var previousSky = RenderSettings.ambientSkyColor;
            var previousEquator = RenderSettings.ambientEquatorColor;
            var previousGround = RenderSettings.ambientGroundColor;
            var previousMode = RenderSettings.ambientMode;
            var previousSkybox = RenderSettings.skybox;
            var root = new GameObject("Lighting palette isolation test");
            root.SetActive(false);
            var settings = ScriptableObject.CreateInstance<WorldTimeSettings>();
            var palette = ScriptableObject.CreateInstance<LightingPaletteProfile>();
            try
            {
                var clock = root.AddComponent<WorldTimeSystem>();
                var serializedClock = new SerializedObject(clock);
                serializedClock.FindProperty("timeSettingsOverride").objectReferenceValue = settings;
                serializedClock.ApplyModifiedPropertiesWithoutUndo();
                Assert.That(clock.TryInitialize(), Is.True);
                clock.RunAutomatically = false;
                var atmosphere = root.AddComponent<DayNightLightingController>();
                var sun = new GameObject("Test sun").AddComponent<Light>();
                sun.transform.SetParent(root.transform);
                sun.type = LightType.Directional;
                var moon = new GameObject("Test moon").AddComponent<Light>();
                moon.transform.SetParent(root.transform);
                moon.type = LightType.Directional;
                var serializedAtmosphere = new SerializedObject(atmosphere);
                serializedAtmosphere.FindProperty("timeSystem").objectReferenceValue = clock;
                serializedAtmosphere.FindProperty("sun").objectReferenceValue = sun;
                serializedAtmosphere.FindProperty("moon").objectReferenceValue = moon;
                serializedAtmosphere.FindProperty("controlFog").boolValue = false;
                serializedAtmosphere.ApplyModifiedPropertiesWithoutUndo();
                string authoredProfile = JsonUtility.ToJson(palette);
                clock.RestoreState(new WorldTimeState(4, 60));
                var clockBefore = JsonUtility.ToJson(clock.CaptureState());
                atmosphere.Palette = palette;
                atmosphere.ApplyLighting();
                Assert.That(RenderSettings.ambientSkyColor, Is.EqualTo(palette.nightSky));
                Assert.That(RenderSettings.ambientEquatorColor, Is.EqualTo(palette.nightEquator));
                atmosphere.Palette = null;
                atmosphere.ApplyLighting();
                Assert.That(RenderSettings.ambientSkyColor, Is.EqualTo(new Color(.25f, .31f, .46f)));
                Assert.That(JsonUtility.ToJson(clock.CaptureState()), Is.EqualTo(clockBefore), "Lighting must not advance authoritative time.");
                Assert.That(JsonUtility.ToJson(palette), Is.EqualTo(authoredProfile), "Preview must not write into its palette asset.");
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(settings);
                Object.DestroyImmediate(palette);
                RenderSettings.ambientSkyColor = previousSky;
                RenderSettings.ambientEquatorColor = previousEquator;
                RenderSettings.ambientGroundColor = previousGround;
                RenderSettings.ambientMode = previousMode;
                RenderSettings.skybox = previousSkybox;
            }
        }
    }
}
