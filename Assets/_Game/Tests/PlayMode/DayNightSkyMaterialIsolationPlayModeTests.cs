using System.Collections;
using System.Linq;
using System.Reflection;
using LittleCastle.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;

namespace LittleCastle.Tests
{
    public sealed class DayNightSkyMaterialIsolationPlayModeTests
    {
        static readonly string[] VectorProperties = { "_ZenithColor", "_HorizonColor", "_LowerSkyColor", "_SunColor", "_MoonColor", "_SunDirection", "_MoonDirection" };
        static readonly string[] FloatProperties = { "_SunVisibility", "_MoonVisibility", "_StarVisibility", "_TwilightStrength", "_Daylight" };

        [UnityTest]
        public IEnumerator ClockChanges_AnimateOwnedSkyAndPreserveSourceAcrossLifecycle()
        {
            var previousSky = RenderSettings.skybox;
            var previousSun = RenderSettings.sun;
            var previousAmbientMode = RenderSettings.ambientMode;
            var previousAmbientSky = RenderSettings.ambientSkyColor;
            var previousAmbientEquator = RenderSettings.ambientEquatorColor;
            var previousAmbientGround = RenderSettings.ambientGroundColor;
            bool previousFog = RenderSettings.fog;
            var previousFogMode = RenderSettings.fogMode;
            var previousFogColor = RenderSettings.fogColor;
            float previousFogStart = RenderSettings.fogStartDistance, previousFogEnd = RenderSettings.fogEndDistance;
            var root = new GameObject("Sky material isolation fixture");
            root.SetActive(false);
            Material source = null, external = null;
            WorldTimeSettings settings = null;
            try
            {
                var shader = Shader.Find("Little Castle/Sky/Stylized Day Night");
                Assert.That(shader, Is.Not.Null);
                source = new Material(shader);
                external = new Material(shader);
                source.SetFloat("_Daylight", .37f);
                source.SetColor("_ZenithColor", new Color(.13f, .27f, .41f));
                var sourceVectors = VectorProperties.ToDictionary(p => p, source.GetVector);
                var sourceFloats = FloatProperties.ToDictionary(p => p, source.GetFloat);
                settings = ScriptableObject.CreateInstance<WorldTimeSettings>();
                var clock = root.AddComponent<WorldTimeSystem>();
                SetField(clock, "timeSettingsOverride", settings);
                clock.RunAutomatically = false;
                var sun = new GameObject("Fixture sun").AddComponent<Light>(); sun.transform.SetParent(root.transform);
                var moon = new GameObject("Fixture moon").AddComponent<Light>(); moon.transform.SetParent(root.transform);
                var controller = root.AddComponent<DayNightLightingController>();
                SetField(controller, "timeSystem", clock);
                SetField(controller, "sun", sun);
                SetField(controller, "moon", moon);
                SetField(controller, "skyboxMaterial", source);
                RenderSettings.skybox = external;
                root.SetActive(true);
                Assert.That(clock.TryInitialize(), Is.True);
                clock.RestoreState(new WorldTimeState(1, 12 * 60));
                controller.ApplyLighting();
                var firstOwned = RenderSettings.skybox;
                Assert.That(firstOwned, Is.Not.Null);
                Assert.That(firstOwned, Is.Not.SameAs(source));
                Assert.That(firstOwned, Is.Not.SameAs(external));
                Assert.That(firstOwned.hideFlags, Is.EqualTo(HideFlags.HideAndDontSave));
                Assert.That(firstOwned.GetFloat("_Daylight"), Is.EqualTo(1f));
                var noonZenith = firstOwned.GetColor("_ZenithColor");
                Assert.That(GetField(controller, "skyboxMaterial"), Is.SameAs(source), "Serialized source reference must remain authored.");

                clock.RestoreState(new WorldTimeState(1, 60));
                controller.ApplyLighting();
                yield return null;
                Assert.That(RenderSettings.skybox, Is.SameAs(firstOwned), "Clock updates must reuse one material.");
                Assert.That(firstOwned.GetFloat("_Daylight"), Is.EqualTo(0f));
                Assert.That(firstOwned.GetColor("_ZenithColor"), Is.Not.EqualTo(noonZenith));
                foreach (var property in sourceVectors)
                    Assert.That(source.GetVector(property.Key), Is.EqualTo(property.Value), "Source mutated: " + property.Key);
                foreach (var property in sourceFloats)
                    Assert.That(source.GetFloat(property.Key), Is.EqualTo(property.Value), "Source mutated: " + property.Key);

                controller.enabled = false;
                Assert.That(RenderSettings.skybox, Is.SameAs(external), "Disable must restore the previous sky while owned.");
                yield return null;
                Assert.That(firstOwned == null, Is.True, "Disabled controller must release its material after the frame.");
                controller.enabled = true;
                var secondOwned = RenderSettings.skybox;
                Assert.That(secondOwned, Is.Not.Null);
                Assert.That(secondOwned, Is.Not.SameAs(source));
                Assert.That(secondOwned.GetFloat("_Daylight"), Is.EqualTo(0f), "Re-enable must reapply current clock state.");

                // Another presentation owner can replace the sky before cleanup.
                RenderSettings.skybox = source;
                controller.enabled = false;
                Assert.That(RenderSettings.skybox, Is.SameAs(source), "Cleanup must not overwrite a different sky owner.");
                yield return null;
                Assert.That(secondOwned == null, Is.True);
                controller.enabled = true;
                var thirdOwned = RenderSettings.skybox;
                Assert.That(thirdOwned, Is.Not.SameAs(source));
                Object.Destroy(controller);
                yield return null;
                yield return null;
                Assert.That(RenderSettings.skybox, Is.SameAs(source), "Destroy must restore the sky captured on re-enable.");
                Assert.That(thirdOwned == null, Is.True, "Destroy must release the owned material.");
                foreach (var property in sourceVectors)
                    Assert.That(source.GetVector(property.Key), Is.EqualTo(property.Value));
                foreach (var property in sourceFloats)
                    Assert.That(source.GetFloat(property.Key), Is.EqualTo(property.Value));
            }
            finally
            {
                // Disable immediately so a failed assertion cannot leave a late
                // sky restoration pending after the global state is restored.
                if (root != null) { root.SetActive(false); Object.Destroy(root); }
                RenderSettings.skybox = previousSky;
                RenderSettings.sun = previousSun;
                RenderSettings.ambientMode = previousAmbientMode;
                RenderSettings.ambientSkyColor = previousAmbientSky;
                RenderSettings.ambientEquatorColor = previousAmbientEquator;
                RenderSettings.ambientGroundColor = previousAmbientGround;
                RenderSettings.fog = previousFog;
                RenderSettings.fogMode = previousFogMode;
                RenderSettings.fogColor = previousFogColor;
                RenderSettings.fogStartDistance = previousFogStart;
                RenderSettings.fogEndDistance = previousFogEnd;
                if (source != null) Object.Destroy(source);
                if (external != null) Object.Destroy(external);
                if (settings != null) Object.Destroy(settings);
            }
        }

        static void SetField(object target, string name, object value) =>
            target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

        static object GetField(object target, string name) =>
            target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    }
}
