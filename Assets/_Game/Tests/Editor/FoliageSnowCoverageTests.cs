using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace LittleCastle.Tests
{
    // Draw the production forward pass directly: no scene, camera, importer or
    // render-pipeline asset is changed by these tests.
    public sealed class FoliageSnowCoverageTests
    {
        private const string ArtRoot = "Assets/_Game/Art/Studies/ValleyArt_Pass03/";

        [Test]
        public void ReverseWoundLeaf_SnowCoversVisibleTopButNotUndersideOrVerticalSide()
        {
            using (var gpu = new SnowRender())
            {
                var mesh = new Mesh { name = "Temporary reverse-wound leaf" };
                var material = gpu.MakeMaterial();
                try
                {
                    mesh.vertices = new[]
                    {
                        new Vector3(-1, 0, -1), new Vector3(1, 0, -1),
                        new Vector3(1, 0, 1), new Vector3(-1, 0, 1)
                    };
                    mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
                    mesh.normals = Enumerable.Repeat(Vector3.down, 4).ToArray();
                    mesh.tangents = Enumerable.Repeat(new Vector4(1, 0, 0, 1), 4).ToArray();
                    mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
                    mesh.RecalculateBounds();

                    var top = new Vector3(0, 4, 0);
                    var summer = gpu.Draw(mesh, 0, Matrix4x4.identity, material,
                        top, Vector3.zero, Vector3.forward, 1.4f, false);
                    var winter = gpu.Draw(mesh, 0, Matrix4x4.identity, material,
                        top, Vector3.zero, Vector3.forward, 1.4f, true);
                    AssertSnowGain(summer, winter, .75f, "reverse-wound leaf viewed from above");

                    AssertNoSnowGain(gpu, mesh, material, Matrix4x4.identity,
                        new Vector3(0, -4, 0), Vector3.forward, "leaf underside");
                    AssertNoSnowGain(gpu, mesh, material,
                        Matrix4x4.Rotate(Quaternion.Euler(90, 0, 0)),
                        new Vector3(0, 0, 4), Vector3.up, "vertical leaf");

                    material.SetFloat("_SnowAcceptance", 0);
                    var optedOut = gpu.Draw(mesh, 0, Matrix4x4.identity, material,
                        top, Vector3.zero, Vector3.forward, 1.4f, true);
                    AssertSamePixels(summer, optedOut, "material snow opt-out");
                    material.SetFloat("_SnowAcceptance", 1);
                    var summerAgain = gpu.Draw(mesh, 0, Matrix4x4.identity, material,
                        top, Vector3.zero, Vector3.forward, 1.4f, false);
                    AssertSamePixels(summer, summerAgain, "zero snow coverage after winter");
                }
                finally
                {
                    Object.DestroyImmediate(material);
                    Object.DestroyImmediate(mesh);
                }
            }
        }

        [TestCase("A", 0)] [TestCase("A", 1)] [TestCase("A", 2)]
        [TestCase("B", 0)] [TestCase("B", 1)] [TestCase("B", 2)]
        [TestCase("C", 0)] [TestCase("C", 1)] [TestCase("C", 2)]
        [TestCase("D", 0)] [TestCase("D", 1)] [TestCase("D", 2)]
        public void ProductionOak_FoliageReceivesSnowAtCloseAndFarFraming(string variant, int lod)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                ArtRoot + "Oak_" + variant + "_Study.prefab");
            Assert.That(prefab, Is.Not.Null);
            string meshName = "SM_Tree_Oak_" + variant + (lod == 0 ? "" : "_LOD" + lod);
            var filter = prefab.GetComponentsInChildren<MeshFilter>(true)
                .Single(f => f.sharedMesh != null && f.sharedMesh.name == meshName);
            var mesh = filter.sharedMesh;
            Assert.That(mesh.subMeshCount, Is.EqualTo(2), meshName);
            Assert.That(mesh.HasVertexAttribute(VertexAttribute.Normal), Is.True, meshName);
            Assert.That(mesh.HasVertexAttribute(VertexAttribute.Tangent), Is.True, meshName);
            Assert.That(mesh.HasVertexAttribute(VertexAttribute.TexCoord0), Is.True, meshName);
            var source = filter.GetComponent<Renderer>().sharedMaterials[1];
            Assert.That(source.shader.name, Is.EqualTo("Little Castle/Foliage/LC Foliage"));

            using (var gpu = new SnowRender())
            {
                var material = gpu.MakeMaterial(source);
                try
                {
                    var matrix = filter.transform.localToWorldMatrix;
                    Bounds bounds = TransformBounds(mesh.bounds, matrix);
                    var eye = bounds.center + Vector3.up * (bounds.size.magnitude + 5);
                    foreach (float framing in new[] { 1.15f, 2.3f })
                    {
                        float size = Mathf.Max(bounds.extents.x, bounds.extents.z) * framing;
                        var summer = gpu.Draw(mesh, 1, matrix, material,
                            eye, bounds.center, Vector3.forward, size, false);
                        var winter = gpu.Draw(mesh, 1, matrix, material,
                            eye, bounds.center, Vector3.forward, size, true);
                        AssertSnowGain(summer, winter, .08f, meshName + " framing=" + framing);
                    }
                }
                finally { Object.DestroyImmediate(material); }
            }
        }

        private static Bounds TransformBounds(Bounds bounds, Matrix4x4 matrix)
        {
            var result = new Bounds(matrix.MultiplyPoint3x4(bounds.center), Vector3.zero);
            for (int i = 0; i < 8; i++)
                result.Encapsulate(matrix.MultiplyPoint3x4(bounds.center + Vector3.Scale(
                    bounds.extents, new Vector3((i & 1) == 0 ? -1 : 1,
                        (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1))));
            return result;
        }

        private static void AssertNoSnowGain(SnowRender gpu, Mesh mesh, Material material,
            Matrix4x4 matrix, Vector3 eye, Vector3 up, string label)
        {
            var summer = gpu.Draw(mesh, 0, matrix, material, eye, Vector3.zero, up, 1.4f, false);
            var winter = gpu.Draw(mesh, 0, matrix, material, eye, Vector3.zero, up, 1.4f, true);
            Assert.That(summer.Count(p => p.a > .9f), Is.GreaterThan(100), label + " must render");
            AssertSamePixels(summer, winter, label);
        }

        private static void AssertSamePixels(Color[] expected, Color[] actual, string label)
        {
            float maxDelta = 0;
            for (int i = 0; i < expected.Length; i++)
                maxDelta = Mathf.Max(maxDelta, Mathf.Abs(expected[i].r - actual[i].r),
                    Mathf.Abs(expected[i].g - actual[i].g), Mathf.Abs(expected[i].b - actual[i].b));
            Assert.That(maxDelta, Is.LessThan(.012f), label);
        }

        private static void AssertSnowGain(Color[] summer, Color[] winter, float minimumFraction, string label)
        {
            int visible = 0, snowy = 0;
            float gain = 0;
            for (int i = 0; i < summer.Length; i++)
            {
                if (summer[i].a < .9f) continue;
                visible++;
                float delta = winter[i].grayscale - summer[i].grayscale;
                gain += delta;
                if (delta > .06f) snowy++;
            }
            Assert.That(visible, Is.GreaterThan(100), label + " must render foliage");
            float fraction = (float)snowy / visible;
            TestContext.WriteLine(label + ": visible=" + visible + ", snowy fraction="
                + fraction.ToString("F3") + ", mean luminance gain=" + (gain / visible).ToString("F3"));
            Assert.That(fraction, Is.GreaterThan(minimumFraction), label + " needs visible snow");
            Assert.That(gain / visible, Is.GreaterThan(.02f), label + " winter must brighten the crown");
        }

        private sealed class SnowRender : IDisposable
        {
            private readonly Dictionary<string, float> floats = new Dictionary<string, float>();
            private readonly Dictionary<string, Vector4> vectors = new Dictionary<string, Vector4>();
            private readonly Dictionary<string, Texture> textures = new Dictionary<string, Texture>();
            private readonly Dictionary<string, Matrix4x4> matrices = new Dictionary<string, Matrix4x4>();
            private readonly Dictionary<string, bool> keywords = new Dictionary<string, bool>();
            private readonly bool asyncCompilation;
            private readonly RenderTexture previousTarget;
            private readonly RenderTexture target;
            private readonly Texture2D readback;
            private readonly CommandBuffer commands;

            public SnowRender()
            {
                if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                    Assert.Ignore("Foliage snow regression requires a graphics device.");
                var shader = Shader.Find("Little Castle/Foliage/LC Foliage");
                Assert.That(shader, Is.Not.Null);
                Assert.That(shader.isSupported, Is.True);
                asyncCompilation = ShaderUtil.allowAsyncCompilation;
                ShaderUtil.allowAsyncCompilation = false;
                previousTarget = RenderTexture.active;
                target = new RenderTexture(256, 256, 24, RenderTextureFormat.ARGB32,
                    RenderTextureReadWrite.Linear) { hideFlags = HideFlags.HideAndDontSave };
                target.Create();
                readback = new Texture2D(256, 256, TextureFormat.RGBA32, false, true)
                    { hideFlags = HideFlags.HideAndDontSave };
                commands = new CommandBuffer { name = "Temporary foliage snow regression" };
                foreach (string keyword in new[] { "_SCREEN_SPACE_OCCLUSION", "_LC_SPATIAL_INDIRECT",
                    "_MAIN_LIGHT_SHADOWS", "_MAIN_LIGHT_SHADOWS_CASCADE", "_MAIN_LIGHT_SHADOWS_SCREEN",
                    "FOG_LINEAR", "FOG_EXP", "FOG_EXP2" })
                {
                    keywords[keyword] = Shader.IsKeywordEnabled(keyword);
                    Shader.DisableKeyword(keyword);
                }
                foreach (string name in new[] { "unity_MatrixV", "unity_MatrixInvV", "glstate_matrix_projection",
                    "unity_MatrixVP", "unity_MatrixInvVP", "unity_CameraProjection", "unity_CameraInvProjection" })
                    matrices[name] = Shader.GetGlobalMatrix(name);
                SetFloat("_LC_EnvironmentEnabled", 1);
                SetVector("_LC_EnvironmentBounds", new Vector4(-32, -32, 1f / 64, 1f / 64));
                SetVector("_LC_EnvironmentSnowColor", Color.white);
                SetTexture("_LC_BiomeTint", Texture2D.whiteTexture);
                SetTexture("_LC_EnvironmentSnow", Texture2D.blackTexture);
                SetVector("_LC_SunDirection", new Vector4(0, 1, 0, 0));
                SetVector("_LC_MoonDirection", new Vector4(0, 1, 0, 0));
                SetVector("_LC_SunColor", Vector4.zero);
                SetVector("_LC_MoonColor", Vector4.zero);
                SetVector("_LC_AmbientSkyColor", Color.white);
                SetVector("_LC_AmbientEquatorColor", Color.white);
                SetVector("_LC_AmbientGroundColor", Color.white);
                SetVector("_LC_ShadowTint", Color.white);
                SetVector("_LC_Wind", Vector4.zero);
                SetVector("_LC_GameTime", Vector4.zero);
                SetFloat("_LC_Twilight", 0);
                SetVector("_WorldSpaceCameraPos", Vector4.zero);
            }

            public Material MakeMaterial(Material source = null)
            {
                var material = source == null
                    ? new Material(Shader.Find("Little Castle/Foliage/LC Foliage"))
                    : new Material(source);
                material.hideFlags = HideFlags.HideAndDontSave;
                material.SetFloat("_WindAmplitude", 0);
                material.SetFloat("_TransmissionStrength", 0);
                material.SetFloat("_OcclusionStrength", 0);
                material.SetFloat("_ShadowTintStrength", 0);
                material.SetFloat("_AmbientStrength", .8f);
                material.SetFloat("_ColorVariation", 0);
                material.SetFloat("_SnowAcceptance", 1);
                if (source == null)
                {
                    material.SetColor("_Color", new Color(.08f, .18f, .04f, 1));
                    material.SetFloat("_BumpScale", 0);
                }
                return material;
            }

            public Color[] Draw(Mesh mesh, int submesh, Matrix4x4 matrix, Material material,
                Vector3 eye, Vector3 center, Vector3 up, float size, bool snow)
            {
                SetTexture("_LC_EnvironmentSnow", snow ? Texture2D.whiteTexture : Texture2D.blackTexture);
                SetVector("_WorldSpaceCameraPos", new Vector4(eye.x, eye.y, eye.z, 1));
                var view = Matrix4x4.Scale(new Vector3(1, 1, -1)) *
                    Matrix4x4.TRS(eye, Quaternion.LookRotation(center - eye, up), Vector3.one).inverse;
                var projection = GL.GetGPUProjectionMatrix(Matrix4x4.Ortho(-size, size,
                    -size, size, .1f, 100), true);
                commands.Clear();
                commands.SetRenderTarget(target);
                commands.ClearRenderTarget(true, true, Color.clear);
                commands.SetViewProjectionMatrices(view, projection);
                commands.DrawMesh(mesh, matrix, material, submesh, material.FindPass("FORWARD"));
                Graphics.ExecuteCommandBuffer(commands);
                RenderTexture.active = target;
                readback.ReadPixels(new Rect(0, 0, 256, 256), 0, 0);
                readback.Apply(false, false);
                RenderTexture.active = previousTarget;
                return readback.GetPixels();
            }

            private void SetFloat(string name, float value)
            {
                if (!floats.ContainsKey(name)) floats.Add(name, Shader.GetGlobalFloat(name));
                Shader.SetGlobalFloat(name, value);
            }

            private void SetVector(string name, Vector4 value)
            {
                if (!vectors.ContainsKey(name)) vectors.Add(name, Shader.GetGlobalVector(name));
                Shader.SetGlobalVector(name, value);
            }

            private void SetTexture(string name, Texture value)
            {
                if (!textures.ContainsKey(name)) textures.Add(name, Shader.GetGlobalTexture(name));
                Shader.SetGlobalTexture(name, value);
            }

            public void Dispose()
            {
                RenderTexture.active = previousTarget;
                commands.Dispose();
                foreach (var pair in floats) Shader.SetGlobalFloat(pair.Key, pair.Value);
                foreach (var pair in vectors) Shader.SetGlobalVector(pair.Key, pair.Value);
                foreach (var pair in textures) Shader.SetGlobalTexture(pair.Key, pair.Value);
                foreach (var pair in matrices) Shader.SetGlobalMatrix(pair.Key, pair.Value);
                foreach (var pair in keywords)
                    if (pair.Value) Shader.EnableKeyword(pair.Key); else Shader.DisableKeyword(pair.Key);
                ShaderUtil.allowAsyncCompilation = asyncCompilation;
                target.Release();
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(readback);
            }
        }
    }
}
