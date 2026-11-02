using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace LittleCastle.Tests
{
    public sealed class EnvironmentSnowUniformityTests
    {
        [TestCase(1f, .002f)]
        [TestCase(.9f, .015f)]
        public void DeepSnow_DoesNotRevealRepeatedDarkGroundBands(float coverage, float allowedVariation)
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) Assert.Ignore("GPU required.");
            var shader = Shader.Find("Hidden/LittleCastle/Tests/Environment Snow Probe");
            Assert.That(shader, Is.Not.Null);
            float oldEnabled = Shader.GetGlobalFloat("_LC_EnvironmentEnabled");
            Vector4 oldBounds = Shader.GetGlobalVector("_LC_EnvironmentBounds");
            var oldSnow = Shader.GetGlobalTexture("_LC_EnvironmentSnow");
            var oldTarget = RenderTexture.active;
            bool oldAsync = ShaderUtil.allowAsyncCompilation;
            var material = new Material(shader);
            var snow = new Texture2D(1, 1, TextureFormat.RGBAFloat, false, true);
            var readback = new Texture2D(128, 128, TextureFormat.RGBAFloat, false, true);
            var target = new RenderTexture(128, 128, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            var commands = new CommandBuffer {name = "Deep snow uniformity regression"};
            try
            {
                ShaderUtil.allowAsyncCompilation = false;
                snow.SetPixel(0, 0, new Color(coverage, 0, 0)); snow.Apply();
                Shader.SetGlobalFloat("_LC_EnvironmentEnabled", 1);
                Shader.SetGlobalVector("_LC_EnvironmentBounds", new Vector4(-32, -32, 1f/64, 1f/64));
                Shader.SetGlobalTexture("_LC_EnvironmentSnow", snow);
                target.Create(); commands.SetRenderTarget(target);
                commands.ClearRenderTarget(false, true, Color.magenta);
                commands.DrawProcedural(Matrix4x4.identity, material, 0, MeshTopology.Triangles, 3);
                Graphics.ExecuteCommandBuffer(commands);
                RenderTexture.active = target;
                readback.ReadPixels(new Rect(0, 0, 128, 128), 0, 0); readback.Apply();
                var pixels = readback.GetPixels();
                Assert.That(pixels.Max(p => p.r) - pixels.Min(p => p.r), Is.LessThan(allowedVariation),
                    "A uniform 48m snow field must not acquire repeated soil stripes.");
                Assert.That(pixels.Average(p => p.r), Is.GreaterThan(coverage - .02f));
                Assert.That(pixels.All(p => Mathf.Abs(p.r-p.g) < .001f && p.a > .99f), Is.True,
                    "Probe must render, not return the clear color.");
            }
            finally
            {
                RenderTexture.active = oldTarget;
                Shader.SetGlobalFloat("_LC_EnvironmentEnabled", oldEnabled);
                Shader.SetGlobalVector("_LC_EnvironmentBounds", oldBounds);
                Shader.SetGlobalTexture("_LC_EnvironmentSnow", oldSnow);
                ShaderUtil.allowAsyncCompilation = oldAsync;
                commands.Release(); target.Release();
                foreach (var o in new Object[] {material, snow, readback, target}) Object.DestroyImmediate(o);
            }
        }
    }
}
