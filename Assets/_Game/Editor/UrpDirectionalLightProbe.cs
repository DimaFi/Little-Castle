using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using LittleCastle.Rendering;
using LittleCastle.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace LittleCastle.Editor
{
    /// <summary>Rendered diagnostic for production directionals competing with compact local lights.</summary>
    public static class UrpDirectionalLightProbe
    {
        const int Size = 512;
        const int ProbeLayer = 30;
        static readonly string[] VectorGlobals = { "_LC_SunDirection", "_LC_MoonDirection", "_LC_SunColor", "_LC_MoonColor", "_LC_AmbientColor", "_LC_AmbientSkyColor", "_LC_AmbientEquatorColor", "_LC_AmbientGroundColor", "_LC_ShadowTint", "_LC_Wind", "_LC_GameTime" };
        static readonly string[] FloatGlobals = { "_LC_Daylight", "_LC_Twilight", "_LC_NightAmount" };

        [MenuItem("Little Castle/Rendering/URP/Probe Production Directional Overlap")]
        public static void Run()
        {
            string output = Path.Combine(UrpAdoptionSetup.EvidenceRoot, "DirectionalOverlap");
            Directory.CreateDirectory(output);
            var failures = new List<string>();
            var temporary = new List<Object>();
            var report = new StringBuilder("Production ConceptBridgeTest clock + isolated LC Stylized Lit sphere.\n");
            var metrics = new StringBuilder("quality\thour\tlocal_type\tcontrol\tmean_abs_rgb_0_255\tchanged_pixels\tmax_rgb_delta\tpass\n");
            var states = new StringBuilder("quality\thour\tpipeline\tadditional_per_object\tsun_enabled\tsun_intensity\tmoon_enabled\tmoon_intensity\trender_settings_sun\tdaylight\tnight_amount\n");
            var setup = EditorSceneManager.GetSceneManagerSetup();
            int previousLevel = QualitySettings.GetQualityLevel();
            var previousPipeline = QualitySettings.renderPipeline;
            var vectors = VectorGlobals.ToDictionary(n => n, Shader.GetGlobalVector);
            var floats = FloatGlobals.ToDictionary(n => n, Shader.GetGlobalFloat);
            bool changedScenes = false;
            void Log(string message, string stack, LogType type)
            {
                if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) failures.Add(message);
            }
            Application.logMessageReceived += Log;
            try
            {
                Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Run outside Play Mode.");
                for (int i = 0; i < SceneManager.sceneCount; i++)
                    Require(!SceneManager.GetSceneAt(i).isDirty, "Save open scene changes before the probe.");
                Require(SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null, "A graphics device is required.");
                changedScenes = true;
                EditorSceneManager.OpenScene("Assets/_Game/Scenes/ConceptBridgeTest.unity", OpenSceneMode.Single);
                var clock = Object.FindAnyObjectByType<WorldTimeSystem>();
                var atmosphere = Object.FindAnyObjectByType<DayNightLightingController>();
                var globals = Object.FindAnyObjectByType<StylizedLightingGlobals>();
                Require(clock != null && atmosphere != null && globals != null, "Production clock/atmosphere/globals missing.");
                Require(clock.TryInitialize(), "Production clock initialization failed.");
                clock.RunAutomatically = false;
                var sun = atmosphere.Sun;
                var moon = atmosphere.Moon;
                Require(sun != null && moon != null, "Production sun/moon references missing.");

                // ApplyLighting writes sky material properties: use a disposable copy.
                var serialized = new SerializedObject(atmosphere);
                var sky = serialized.FindProperty("skyboxMaterial");
                Require(sky != null, "Production skybox field missing.");
                if (sky.objectReferenceValue is Material sourceSky)
                {
                    var copy = new Material(sourceSky) { hideFlags = HideFlags.HideAndDontSave };
                    temporary.Add(copy); sky.objectReferenceValue = copy; serialized.ApplyModifiedPropertiesWithoutUndo();
                }
                foreach (var renderer in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include)) renderer.enabled = false;
                foreach (var light in Object.FindObjectsByType<Light>(FindObjectsInactive.Include))
                    if (light != sun && light != moon) light.enabled = false;
                foreach (var sourceCamera in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include)) sourceCamera.enabled = false;

                var shader = Shader.Find("Little Castle/Surface/LC Stylized Lit");
                Require(shader != null && shader.isSupported, "LC Stylized Lit unavailable.");
                var material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
                material.SetColor("_Color", new Color(.24f, .24f, .24f));
                material.SetFloat("_LocalLightStrength", 1); temporary.Add(material);
                var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sphere.name = "Directional overlap probe solid"; sphere.layer = ProbeLayer;
                sphere.transform.position = new Vector3(0, 1, 0); sphere.transform.localScale = Vector3.one * 2;
                sphere.GetComponent<Renderer>().sharedMaterial = material;
                sphere.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                Object.DestroyImmediate(sphere.GetComponent<Collider>()); temporary.Add(sphere);
                var camera = new GameObject("Directional overlap dedicated camera").AddComponent<Camera>();
                temporary.Add(camera.gameObject); camera.enabled = false; camera.cullingMask = 1 << ProbeLayer;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.015f, .02f, .03f);
                camera.nearClipPlane = .05f; camera.farClipPlane = 30; camera.orthographic = true; camera.orthographicSize = 1.55f;
                camera.transform.position = new Vector3(4, 3, -6); camera.transform.LookAt(sphere.transform.position);
                var local = new GameObject("Directional overlap compact local").AddComponent<Light>();
                temporary.Add(local.gameObject); local.enabled = false; local.cullingMask = 1 << ProbeLayer;
                local.transform.position = new Vector3(1.3f, 2.3f, -1.5f); local.transform.LookAt(sphere.transform.position);
                local.color = new Color(1, .48f, .16f); local.range = 7; local.intensity = 6;
                local.spotAngle = 80; local.innerSpotAngle = 55; local.shadows = LightShadows.None;
                report.AppendLine("UTC=" + DateTime.UtcNow.ToString("O") + "\nUnity=" + Application.unityVersion + "\nGPU=" + SystemInfo.graphicsDeviceName);
                report.AppendLine("Source sun cullingMask=" + sun.cullingMask + ", moon cullingMask=" + moon.cullingMask + "; probe layer=" + ProbeLayer);

                foreach (string tier in new[] { "Low", "Medium" })
                {
                    var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(UrpQualityProfileSetup.SettingsPath + "/LittleCastle_URP_" + tier + ".asset");
                    Require(pipeline != null, "Missing owned " + tier + " profile.");
                    QualitySettings.renderPipeline = pipeline;
                    foreach (float hour in new[] { 12f, 19.9f, 1f })
                    {
                        clock.RestoreState(new WorldTimeState(1, hour * 60));
                        atmosphere.ApplyLighting(); globals.ApplyGlobals();
                        states.AppendLine(tier + "\t" + F(hour) + "\t" + pipeline.name + "\t" + pipeline.maxAdditionalLightsCount + "\t" + sun.enabled + "\t" + F(sun.intensity) + "\t" + moon.enabled + "\t" + F(moon.intensity) + "\t" + (RenderSettings.sun ? RenderSettings.sun.name : "NULL") + "\t" + F(globals.Daylight) + "\t" + F(globals.NightAmount));
                        foreach (var type in new[] { LightType.Point, LightType.Spot })
                        {
                            local.type = type;
                            string prefix = tier + "_" + F(hour).Replace('.', '_') + "_" + type;
                            local.enabled = false;
                            var off = Capture(camera, pipeline.msaaSampleCount, output, prefix + "_off");
                            local.enabled = true;
                            var on = Capture(camera, pipeline.msaaSampleCount, output, prefix + "_on");
                            bool pass = Compare(off, on, metrics, tier, hour, type, "production_directionals");
                            if (!pass) failures.Add("No local-light contribution with production directionals: " + prefix);

                            // Keep the already-applied LC globals unchanged: remove only
                            // the non-main directional from culling to discriminate slots.
                            var other = RenderSettings.sun == moon ? sun : moon;
                            bool oldEnabled = other.enabled;
                            try
                            {
                                other.enabled = false; local.enabled = false;
                                var diagnosticOff = Capture(camera, pipeline.msaaSampleCount, output, prefix + "_other_directional_disabled_off");
                                local.enabled = true;
                                var diagnosticOn = Capture(camera, pipeline.msaaSampleCount, output, prefix + "_other_directional_disabled_on");
                                Compare(diagnosticOff, diagnosticOn, metrics, tier, hour, type, "other_directional_disabled_diagnostic");
                            }
                            finally { other.enabled = oldEnabled; local.enabled = false; }
                        }
                    }
                }
                report.AppendLine("Required: Point and Spot must each change >=20 pixels by >3 channel values and mean RGB by >=0.002 under Low/Medium at all three actual clock states.");
                report.AppendLine("Diagnostic controls disable only the directional other than RenderSettings.sun, retaining cached LC direct/ambient globals; they are not the required production result. No pipeline budgets, foliage/grass contract or persistent assets changed.");
            }
            catch (Exception exception) { failures.Add(exception.ToString()); }
            finally
            {
                Application.logMessageReceived -= Log;
                QualitySettings.SetQualityLevel(previousLevel, false);
                QualitySettings.renderPipeline = previousPipeline;
                foreach (var item in temporary.AsEnumerable().Reverse()) if (item != null) Object.DestroyImmediate(item);
                if (changedScenes && setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup);
                foreach (var pair in vectors) Shader.SetGlobalVector(pair.Key, pair.Value);
                foreach (var pair in floats) Shader.SetGlobalFloat(pair.Key, pair.Value);
                File.WriteAllText(Path.Combine(output, "states.tsv"), states.ToString());
                File.WriteAllText(Path.Combine(output, "metrics.tsv"), metrics.ToString());
                report.AppendLine("RESULT=" + (failures.Count == 0 ? "PASS" : "FAIL") + "\nFailures=" + failures.Count + "\n" + string.Join("\n", failures));
                File.WriteAllText(Path.Combine(output, "result.txt"), report.ToString());
                Debug.Log("LC_URP_DIRECTIONAL_PROBE result=" + (failures.Count == 0 ? "PASS" : "FAIL") + " output=" + output);
                if (Application.isBatchMode) UrpBatchExit.WhenIdle(failures.Count == 0 ? 0 : 2);
            }
        }

        static Color32[] Capture(Camera camera, int samples, string output, string name)
        {
            var target = RenderTexture.GetTemporary(Size, Size, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default, samples);
            var previous = RenderTexture.active; var previousTarget = camera.targetTexture;
            var image = new Texture2D(Size, Size, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target;
                CameraCaptureCompatibility.Render(camera, target); CameraCaptureCompatibility.Render(camera, target);
                RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, Size, Size), 0, 0); image.Apply();
                File.WriteAllBytes(Path.Combine(output, name + ".png"), image.EncodeToPNG());
                var pixels = image.GetPixels32();
                Require(!pixels.Any(p => p.r > 235 && p.b > 235 && p.g < 35), "Error-shader magenta: " + name);
                return pixels;
            }
            finally { camera.targetTexture = previousTarget; RenderTexture.active = previous; RenderTexture.ReleaseTemporary(target); Object.DestroyImmediate(image); }
        }

        static bool Compare(Color32[] off, Color32[] on, StringBuilder metrics, string tier, float hour, LightType type, string control)
        {
            long total = 0; int changed = 0, maximum = 0;
            for (int i = 0; i < off.Length; i++)
            {
                int r = Math.Abs(off[i].r - on[i].r), g = Math.Abs(off[i].g - on[i].g), b = Math.Abs(off[i].b - on[i].b);
                int peak = Math.Max(r, Math.Max(g, b)); total += r + g + b;
                if (peak > 3) changed++; maximum = Math.Max(maximum, peak);
            }
            double mean = total / (off.Length * 3.0);
            bool pass = changed >= 20 && mean >= .002;
            metrics.AppendLine(tier + "\t" + F(hour) + "\t" + type + "\t" + control + "\t" + mean.ToString("F6", CultureInfo.InvariantCulture) + "\t" + changed + "\t" + maximum + "\t" + pass);
            return pass;
        }

        static string F(float value) => value.ToString("0.####", CultureInfo.InvariantCulture);
        static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    }
}
