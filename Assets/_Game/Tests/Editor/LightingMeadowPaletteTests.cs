using LittleCastle.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace LittleCastle.Tests
{
    public sealed class LightingMeadowPaletteTests
    {
        [Test]
        public void ElevationPaletteWarmsLowSunBeforeDuskIntensityFade()
        {
            var settings=ScriptableObject.CreateInstance<WorldTimeSettings>();
            var palette=ScriptableObject.CreateInstance<LightingPaletteProfile>();
            var root=new GameObject("Elevation palette test");root.SetActive(false);
            var previousSky=RenderSettings.skybox;var previousSun=RenderSettings.sun;
            var sky=RenderSettings.ambientSkyColor;var equator=RenderSettings.ambientEquatorColor;var ground=RenderSettings.ambientGroundColor;var mode=RenderSettings.ambientMode;
            try
            {
                var clock=root.AddComponent<WorldTimeSystem>();var c=new SerializedObject(clock);c.FindProperty("timeSettingsOverride").objectReferenceValue=settings;c.ApplyModifiedPropertiesWithoutUndo();clock.TryInitialize();clock.RunAutomatically=false;
                var sun=new GameObject("Sun").AddComponent<Light>();sun.transform.SetParent(root.transform);sun.type=LightType.Directional;
                var atmosphere=root.AddComponent<DayNightLightingController>();var a=new SerializedObject(atmosphere);a.FindProperty("timeSystem").objectReferenceValue=clock;a.FindProperty("sun").objectReferenceValue=sun;a.FindProperty("controlFog").boolValue=false;a.ApplyModifiedPropertiesWithoutUndo();
                atmosphere.Palette=palette;clock.RestoreState(new WorldTimeState(1,18.5*60));atmosphere.ApplyLighting();Color original=sun.color;float originalIntensity=sun.intensity;
                palette.sunColorFromElevation=true;atmosphere.ApplyLighting();
                Assert.That(sun.color.b,Is.LessThan(original.b-.1f),"At 18:30 the actual low sun should warm while daylight is still strong.");
                Assert.That(sun.intensity,Is.EqualTo(originalIntensity).Within(.0001f),"Color tuning cannot change the clock-driven daylight intensity.");
                clock.RestoreState(new WorldTimeState(1,12*60));atmosphere.ApplyLighting();Assert.That(sun.color,Is.EqualTo(palette.sunDay));
                Assert.That(clock.CurrentHour,Is.EqualTo(12).Within(.0001f));
            }
            finally{Object.DestroyImmediate(root);Object.DestroyImmediate(settings);Object.DestroyImmediate(palette);RenderSettings.skybox=previousSky;RenderSettings.sun=previousSun;RenderSettings.ambientSkyColor=sky;RenderSettings.ambientEquatorColor=equator;RenderSettings.ambientGroundColor=ground;RenderSettings.ambientMode=mode;}
        }
    }
}
