using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace LittleCastle.Tests
{
    public sealed class StylizedRenderingContractTests
    {
        [TestCase(
            "Little Castle/Sky/Stylized Day Night",
            "_SunDirection",
            "_MoonDirection",
            "_Daylight")]
        [TestCase(
            "Little Castle/Surface/LC Stylized Lit",
            "_MainTex",
            "_BumpMap",
            "_EmissionNightStrength")]
        [TestCase(
            "Little Castle/Surface/LC Stylized Lit",
            "_RoughnessMap",
            "_LocalLightStrength",
            "_EmissionMap")]
        [TestCase(
            "Little Castle/Foliage/LC Foliage",
            "_CrownSway",
            "_VertexWave",
            "_LeafFlutter")]
        [TestCase(
            "Little Castle/Foliage/LC Foliage",
            "_OpacityMap",
            "_UseHeightWindMask",
            "_TransmissionStrength")]
        [TestCase(
            "Little Castle/Foliage/LC Grass",
            "_GustStrength",
            "_MicroFlutter",
            "_TransmissionStrength")]
        [TestCase(
            "Little Castle/Foliage/LC Grass",
            "_OpacityMap",
            "_GustStrength",
            "_MicroFlutter")]
        [TestCase(
            "Little Castle/Terrain/LC Terrain",
            "_GrassTex",
            "_MacroStrength",
            "_UseVertexMasks")]
        [TestCase(
            "Little Castle/Effects/LC Night Light Pool",
            "_Intensity",
            "_NightThreshold",
            "_FlickerStrength")]
        [TestCase(
            "Little Castle/Effects/LC Emissive Glow",
            "_MainTex",
            "_NightStrength",
            "_FlickerStrength")]
        [TestCase(
            "Little Castle/Distance/LC Distant Simple",
            "_MainTex",
            "_UseAlphaClip",
            "_TopLightStrength")]
        public void SharedShaders_ExistAndExposeRequiredProperties(
            string shaderName,
            string propertyA,
            string propertyB,
            string propertyC)
        {
            Shader shader =
                Shader.Find(
                    shaderName);

            Assert.That(
                shader,
                Is.Not.Null,
                "Missing shader: " +
                shaderName);

            var material =
                new Material(
                    shader);

            try
            {
                Assert.That(
                    material.HasProperty(
                        propertyA),
                    Is.True,
                    shaderName +
                    " missing " +
                    propertyA);

                Assert.That(
                    material.HasProperty(
                        propertyB),
                    Is.True,
                    shaderName +
                    " missing " +
                    propertyB);

                Assert.That(
                    material.HasProperty(
                        propertyC),
                    Is.True,
                    shaderName +
                    " missing " +
                    propertyC);
            }
            finally
            {
                Object.DestroyImmediate(
                    material);
            }
        }

        [TestCase(
            "Assets/_Game/Materials/StylizedDayNightSky.mat")]
        [TestCase(
            "Assets/_Game/Materials/Shared/LC_StylizedLit_Default.mat")]
        [TestCase(
            "Assets/_Game/Materials/Shared/LC_Foliage_Default.mat")]
        [TestCase(
            "Assets/_Game/Materials/Shared/LC_Grass_Default.mat")]
        [TestCase(
            "Assets/_Game/Materials/Material_terrain/LC_Terrain_Default.mat")]
        [TestCase(
            "Assets/_Game/Materials/Shared/LC_NightLightPool_Default.mat")]
        [TestCase(
            "Assets/_Game/Materials/Shared/LC_EmissiveGlow_Default.mat")]
        [TestCase(
            "Assets/_Game/Materials/Shared/LC_Wheat_Mature.mat")]
        [TestCase(
            "Assets/_Game/Materials/Shared/LC_DistantSimple_Default.mat")]
        public void DefaultRenderingMaterials_Exist(
            string assetPath)
        {
            Material material =
                AssetDatabase.LoadAssetAtPath<
                    Material>(assetPath);

            Assert.That(
                material,
                Is.Not.Null,
                "Missing default rendering material: " +
                assetPath);

            Assert.That(
                material.shader,
                Is.Not.Null,
                "Material has no shader: " +
                assetPath);
        }

        [Test]
        public void ColorSpace_IsExplicitlyVisibleToReview()
        {
            Assert.That(
                PlayerSettings.colorSpace == ColorSpace.Gamma ||
                PlayerSettings.colorSpace == ColorSpace.Linear,
                Is.True);
        }
    }
}
