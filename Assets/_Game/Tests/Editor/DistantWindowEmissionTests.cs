using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace LittleCastle.Tests
{
    // Exercise the production distant forward pass without opening a scene or
    // changing a renderer, pipeline asset, authored material or light registry.
    public sealed class DistantWindowEmissionTests
    {
        [TestCase(false)]
        [TestCase(true)]
        public void NightEmission_BrightensOnlyMaskedWindows(bool instanced)
        {
            using (var gpu = new EmissionRender(instanced))
            {
                gpu.Material.SetTexture("_EmissionMap", gpu.WindowMask);
                gpu.Material.SetColor("_EmissionColor", new Color(.8f, .35f, .08f, 1));
                gpu.Material.SetFloat("_EmissionDayStrength", 0);
                gpu.Material.SetFloat("_EmissionNightStrength", 0);
                var baseline = gpu.Draw(1);

                gpu.Material.SetFloat("_EmissionNightStrength", .8f);
                var glowing = gpu.Draw(1);
                Color beforeWindow = RegionMean(baseline, true);
                Color afterWindow = RegionMean(glowing, true);
                Color wall = RegionMean(glowing, false);

                Assert.That(afterWindow.r - beforeWindow.r, Is.GreaterThan(.3f),
                    "A masked distant window must receive visible nighttime emission.");
                Assert.That(afterWindow.r - afterWindow.g, Is.GreaterThan(.15f),
                    "Emission retains its warm hue after the wall's full night desaturation.");
                Assert.That(wall.maxColorComponent, Is.InRange(.001f, .2f),
                    "The opaque wall must actually render under the controlled dim ambient light.");
                AssertRegionUnchanged(baseline, glowing, false,
                    "Black emission-mask texels must not brighten the unmarked wall.");
            }
        }

        [Test]
        public void DefaultEmissionIsNeutral_AndNightOnlyWindowsStayDarkByDay()
        {
            using (var gpu = new EmissionRender(false))
            {
                Assert.That(gpu.Material.GetFloat("_EmissionDayStrength"), Is.Zero);
                Assert.That(gpu.Material.GetFloat("_EmissionNightStrength"), Is.Zero);
                Assert.That(gpu.Material.GetColor("_EmissionColor").maxColorComponent, Is.Zero);
                var defaults = gpu.Draw(1);
                RegionMean(defaults, true);

                gpu.Material.SetTexture("_EmissionMap", gpu.WindowMask);
                gpu.Material.SetColor("_EmissionColor", new Color(.8f, .35f, .08f, 1));
                var explicitlyOff = gpu.Draw(1);
                AssertAllUnchanged(defaults, explicitlyOff,
                    "Opt-in emission defaults must preserve the existing distant appearance.");

                var dayBaseline = gpu.Draw(0);
                gpu.Material.SetFloat("_EmissionNightStrength", .8f);
                var dayWindows = gpu.Draw(0);
                AssertAllUnchanged(dayBaseline, dayWindows,
                    "A night-only window map must contribute no emission during the day.");
                RegionMean(dayWindows, true);
                RegionMean(dayWindows, false);
            }
        }

        const int Size = 128;

        static Color RegionMean(Color[] pixels, bool window)
        {
            Color sum = Color.clear;
            int count = 0;
            // Stay well inside the quad and away from the two-texel mask edge.
            for (int y = 24; y < 104; y++)
            for (int x = window ? 20 : 84; x < (window ? 44 : 108); x++)
            {
                Color pixel = pixels[y * Size + x];
                Assert.That(pixel.a, Is.GreaterThan(.99f), "The sampled opaque quad must render.");
                sum += pixel;
                count++;
            }
            return sum / count;
        }

        static float Difference(Color a, Color b) =>
            Mathf.Max(Mathf.Abs(a.r - b.r), Mathf.Abs(a.g - b.g), Mathf.Abs(a.b - b.b));

        static void AssertRegionUnchanged(Color[] before, Color[] after, bool window, string message)
        {
            float maximum = 0;
            for (int y = 24; y < 104; y++)
            for (int x = window ? 20 : 84; x < (window ? 44 : 108); x++)
                maximum = Mathf.Max(maximum, Difference(before[y * Size + x], after[y * Size + x]));
            Assert.That(maximum, Is.LessThan(.012f), message);
        }

        static void AssertAllUnchanged(Color[] before, Color[] after, string message)
        {
            float maximum = 0;
            for (int i = 0; i < before.Length; i++)
                maximum = Mathf.Max(maximum, Difference(before[i], after[i]));
            Assert.That(maximum, Is.LessThan(.012f), message);
        }

        sealed class EmissionRender : IDisposable
        {
            readonly Dictionary<string, float> floats = new Dictionary<string, float>();
            readonly Dictionary<string, Vector4> vectors = new Dictionary<string, Vector4>();
            readonly Dictionary<string, Matrix4x4> matrices = new Dictionary<string, Matrix4x4>();
            readonly Dictionary<string, bool> keywords = new Dictionary<string, bool>();
            readonly bool asyncCompilation;
            readonly bool instanced;
            readonly RenderTexture previousTarget;
            readonly RenderTexture target;
            readonly Texture2D readback;
            readonly Texture2D baseTexture;
            readonly Mesh quad;
            readonly CommandBuffer commands;
            public Material Material { get; }
            public Texture2D WindowMask { get; }

            public EmissionRender(bool useInstancing)
            {
                if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                    Assert.Ignore("Distant window regression requires a graphics device.");
                if (useInstancing && !SystemInfo.supportsInstancing)
                    Assert.Ignore("GPU instancing is unavailable on this graphics device.");
                var shader = Shader.Find("Little Castle/Distance/LC Distant Simple");
                Assert.That(shader, Is.Not.Null);
                Assert.That(shader.isSupported, Is.True);
                instanced = useInstancing;
                asyncCompilation = ShaderUtil.allowAsyncCompilation;
                previousTarget = RenderTexture.active;
                try
                {
                    ShaderUtil.allowAsyncCompilation = false;
                    Material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave, enableInstancing = instanced };
                    foreach (string property in new[] { "_EmissionMap", "_EmissionColor", "_EmissionDayStrength", "_EmissionNightStrength" })
                        Assert.That(Material.HasProperty(property), Is.True, "Missing distant emission contract: " + property);
                    Material.SetColor("_Color", new Color(.22f, .32f, .42f, 1));
                    Material.SetFloat("_NightDesaturate", 1);
                    Material.SetFloat("_TopLightStrength", 0);
                    Material.SetFloat("_Cull", (float)CullMode.Off);
                    Material.SetFloat("_SnowAcceptance", 0);

                    // Production deliberately shares the BaseColor sampler for
                    // emission. Set that sampler as well as the mask's import state.
                    baseTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false, true)
                    { hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Point,
                        wrapMode = TextureWrapMode.Clamp };
                    baseTexture.SetPixel(0, 0, Color.white);
                    baseTexture.Apply(false, false);
                    Material.SetTexture("_MainTex", baseTexture);

                    WindowMask = new Texture2D(2, 1, TextureFormat.RGBA32, false, true)
                    { name = "Owned two-region window mask", hideFlags = HideFlags.HideAndDontSave,
                        filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
                    WindowMask.SetPixels(new[] { Color.white, Color.black });
                    WindowMask.Apply(false, false);
                    quad = new Mesh { name = "Owned distant window regression quad", hideFlags = HideFlags.HideAndDontSave };
                    quad.vertices = new[] { new Vector3(-1, -1, 0), new Vector3(1, -1, 0),
                        new Vector3(1, 1, 0), new Vector3(-1, 1, 0) };
                    quad.normals = new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back };
                    quad.uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
                    quad.triangles = new[] { 0, 1, 2, 0, 2, 3 };
                    quad.RecalculateBounds();
                    target = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear)
                        { hideFlags = HideFlags.HideAndDontSave };
                    Assert.That(target.Create(), Is.True);
                    readback = new Texture2D(Size, Size, TextureFormat.RGBA32, false, true)
                        { hideFlags = HideFlags.HideAndDontSave };
                    commands = new CommandBuffer { name = "Distant window emission regression" };
                    foreach (string keyword in new[] { "FOG_LINEAR", "FOG_EXP", "FOG_EXP2" })
                    {
                        keywords[keyword] = Shader.IsKeywordEnabled(keyword);
                        Shader.DisableKeyword(keyword);
                    }
                    foreach (string name in new[] { "unity_MatrixV", "unity_MatrixInvV", "glstate_matrix_projection",
                        "unity_MatrixVP", "unity_MatrixInvVP", "unity_CameraProjection", "unity_CameraInvProjection" })
                        matrices[name] = Shader.GetGlobalMatrix(name);
                    SetFloat("_LC_EnvironmentEnabled", 0);
                    SetVector("_LC_SunDirection", new Vector4(0, 1, 0, 0));
                    SetVector("_LC_SunColor", Vector4.zero);
                    foreach (string name in new[] { "_LC_AmbientSkyColor", "_LC_AmbientEquatorColor", "_LC_AmbientGroundColor" })
                        SetVector(name, new Vector4(.12f, .12f, .12f, 1));
                }
                catch { Dispose(); throw; }
            }

            public Color[] Draw(float nightAmount)
            {
                SetFloat("_LC_NightAmount", nightAmount);
                var eye = new Vector3(0, 0, -4);
                var view = Matrix4x4.Scale(new Vector3(1, 1, -1)) *
                    Matrix4x4.TRS(eye, Quaternion.LookRotation(-eye, Vector3.up), Vector3.one).inverse;
                // CommandBuffer performs the GPU conversion; do not apply
                // GL.GetGPUProjectionMatrix a second time (D3D12 regression).
                var projection = Matrix4x4.Ortho(-1.25f, 1.25f, -1.25f, 1.25f, .1f, 20);
                int pass = Material.FindPass("FORWARD");
                Assert.That(pass, Is.GreaterThanOrEqualTo(0));
                commands.Clear();
                commands.SetRenderTarget(target);
                commands.ClearRenderTarget(true, true, Color.clear);
                commands.SetViewProjectionMatrices(view, projection);
                if (instanced)
                    commands.DrawMeshInstanced(quad, 0, Material, pass, new[] { Matrix4x4.identity }, 1);
                else
                    commands.DrawMesh(quad, Matrix4x4.identity, Material, 0, pass);
                Graphics.ExecuteCommandBuffer(commands);
                try
                {
                    RenderTexture.active = target;
                    readback.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
                    readback.Apply(false, false);
                    return readback.GetPixels();
                }
                finally { RenderTexture.active = previousTarget; }
            }

            void SetFloat(string name, float value)
            {
                if (!floats.ContainsKey(name)) floats.Add(name, Shader.GetGlobalFloat(name));
                Shader.SetGlobalFloat(name, value);
            }

            void SetVector(string name, Vector4 value)
            {
                if (!vectors.ContainsKey(name)) vectors.Add(name, Shader.GetGlobalVector(name));
                Shader.SetGlobalVector(name, value);
            }

            public void Dispose()
            {
                RenderTexture.active = previousTarget;
                commands?.Dispose();
                foreach (var pair in floats) Shader.SetGlobalFloat(pair.Key, pair.Value);
                foreach (var pair in vectors) Shader.SetGlobalVector(pair.Key, pair.Value);
                foreach (var pair in matrices) Shader.SetGlobalMatrix(pair.Key, pair.Value);
                foreach (var pair in keywords)
                    if (pair.Value) Shader.EnableKeyword(pair.Key); else Shader.DisableKeyword(pair.Key);
                ShaderUtil.allowAsyncCompilation = asyncCompilation;
                if (target != null) target.Release();
                foreach (var owned in new Object[] { Material, WindowMask, baseTexture, quad, readback, target })
                    if (owned != null) Object.DestroyImmediate(owned);
            }
        }
    }
}
