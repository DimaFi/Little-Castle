using System;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace LittleCastle.Tests
{
    /// <summary>
    /// Q06 import/source/material contract. NOT a GPU screenshot or
    /// proof of attractive day/night water in the target Player.
    /// </summary>
    public sealed class WaterMaterialContractTests
    {
        private const string ShaderPath =
            "Assets/_Game/Shaders/Terrain/LC_RiverWater.shader";
        private const string MaterialPath =
            "Assets/_Game/Settings/World/ConceptWorld_v001/" +
            "Concept_RiverWater.mat";
        private const string BridgeMaterialPath =
            "Assets/_Game/Models/Concept/BridgeValidation/Water.mat";

        private static Shader ShaderAsset()
        {
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            Assert.That(shader, Is.Not.Null);
            return shader;
        }

        private static Material MaterialAsset()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(
                MaterialPath);
            Assert.That(material, Is.Not.Null);
            return material;
        }

        private static string Source()
        {
            string root = Path.GetFullPath(Path.Combine(
                Application.dataPath, ".."));
            string file = Path.Combine(root, ShaderPath);
            Assert.That(File.Exists(file), Is.True, file);
            return File.ReadAllText(file);
        }

        [Test]
        public void IsolatedShaderAndMaterial_ResolveWithoutChangingBridgeMaterial()
        {
            Shader shader = ShaderAsset();
            Material material = MaterialAsset();
            Assert.That(shader.name,
                Is.EqualTo("Little Castle/Terrain/LC River Water"));
            Assert.That(material.shader, Is.SameAs(shader));

            Material bridge =
                AssetDatabase.LoadAssetAtPath<Material>(BridgeMaterialPath);
            Assert.That(bridge, Is.Not.Null);
            Assert.That(bridge, Is.Not.SameAs(material));
            Assert.That(bridge.shader, Is.Not.SameAs(shader));
            Assert.That(AssetDatabase.GetAssetPath(bridge),
                Is.EqualTo(BridgeMaterialPath));
        }

        [Test]
        public void ShaderIsOneTransparentPass_NoDepthRefractionOrCompute()
        {
            string source = Source();
            Assert.That(Regex.Matches(source, @"\bPass\s*\{").Count,
                Is.EqualTo(1));
            Assert.That(source, Does.Contain("Blend SrcAlpha OneMinusSrcAlpha"));
            Assert.That(source, Does.Contain("ZWrite Off"));
            Assert.That(source, Does.Contain("Cull Back"));
            Assert.That(source, Does.Contain("Transparent-10"));
            Assert.That(source, Does.Not.Contain("GrabPass"));
            Assert.That(source, Does.Not.Contain("_CameraDepthTexture"));
            Assert.That(source, Does.Not.Contain("_CameraOpaqueTexture"));
            Assert.That(source, Does.Not.Contain("sampler2D"));
            Assert.That(source, Does.Not.Contain("tex2D("));
            Assert.That(source, Does.Not.Contain("ComputeShader"));
            Assert.That(source, Does.Not.Contain("Tessellation"));
        }

        [Test]
        public void RenderDistanceAndFixedBridgeGeometry_AreNotShaderResponsibilities()
        {
            string source = Source();
            Assert.That(source, Does.Not.Contain("FixedBridgeSiteProfile"));
            Assert.That(source, Does.Not.Contain("WaterHeightOffset"));
            Assert.That(source, Does.Not.Contain("vertex.y +"));
            Assert.That(source, Does.Not.Contain("SV_Depth"));
            Assert.That(source, Does.Contain(
                "UnityObjectToClipPos(input.vertex)"));
            Assert.That(source, Does.Contain(
                "output.worldPosition ="));
            Assert.That(source, Does.Contain(
                "input.worldPosition.xz"));
        }

        [Test]
        public void SavedMaterialHasSoftPresetNotWhiteOpaque()
        {
            Material material = MaterialAsset();
            Assert.That(material.renderQueue, Is.EqualTo(2990));
            Assert.That(material.HasProperty("_DayColor"), Is.True);
            Assert.That(material.HasProperty("_NightColor"), Is.True);
            Color day = material.GetColor("_DayColor");
            Color night = material.GetColor("_NightColor");
            Assert.That(day.a, Is.GreaterThan(0f).And.LessThan(1f));
            Assert.That(night.a, Is.GreaterThan(0f).And.LessThan(1f));
            Assert.That(day.r + day.g + day.b,
                Is.GreaterThan(night.r + night.g + night.b));
            Assert.That(day.g, Is.GreaterThan(day.r));
            Assert.That(night.b, Is.GreaterThan(night.r));
        }

        [Test]
        public void FlowIsSlowAndRippleStrengthHasExplicitSmallBound()
        {
            Material material = MaterialAsset();
            Assert.That(material.GetFloat("_FlowSpeed"),
                Is.InRange(0f, 0.2f));
            Assert.That(material.GetFloat("_RippleStrength"),
                Is.InRange(0f, 0.1f));
            Assert.That(material.GetFloat("_RippleScale"),
                Is.InRange(0.05f, 0.8f));
            Vector4 flow = material.GetVector("_FlowDirection");
            Assert.That(new Vector2(flow.x, flow.y).sqrMagnitude,
                Is.GreaterThan(0.01f));
            Assert.That(Source(), Does.Contain("_Time.y"));
        }

        [Test]
        public void ShoreGradeIsApproximate_NotDepthTextureDependency()
        {
            Material material = MaterialAsset();
            Assert.That(material.GetFloat("_ShoreContrast"),
                Is.InRange(0f, 0.12f));
            Assert.That(material.GetFloat("_ShoreSlopeGain"),
                Is.GreaterThan(0f));
            Assert.That(Source(), Does.Contain(
                "fwidth(input.worldPosition.y)"));
            Assert.That(Source(), Does.Not.Contain(
                "LinearEyeDepth"));
        }

        [Test]
        public void ShaderUsesExistingGlobalNightAmount_NotOwnClockController()
        {
            string source = Source();
            Assert.That(source, Does.Contain("_LC_NightAmount"));
            Assert.That(source, Does.Contain(
                "lerp(_DayColor, _NightColor, night)"));
            Assert.That(source, Does.Not.Contain("NightLightEmitter"));
            Assert.That(source, Does.Not.Contain("WorldTimeSystem"));
        }

        [Test]
        public void NoUniqueTexturesOrTransientMaterialClone()
        {
            var material = MaterialAsset();
            string source = Source();
            Assert.That(source, Does.Not.Contain("sampler2D"));
            Assert.That(material.HasProperty("_MainTex"), Is.False);
            Assert.That(material.HasProperty("_BumpMap"), Is.False);
            Assert.That(material.enableInstancing, Is.True);
            Assert.That(AssetDatabase.GetAssetPath(material),
                Is.EqualTo(MaterialPath));
        }

        [Test]
        public void ExistingBridgeContractMaintainsScaleOneAndWaterline()
        {
            Assert.That(LittleCastle.World.FixedBridgeSiteProfile.WaterHeightOffset,
                Is.EqualTo(-0.85f));
            Assert.That(LittleCastle.World.FixedBridgeSiteProfile.IsSupported(
                new LittleCastle.World.WorldBridgeSiteData(
                    1, 2, 3,
                    LittleCastle.World.FixedBridgeSiteProfile.AssetId,
                    Vector2.zero, 0f, 5f, 10f,
                    LittleCastle.World.FixedBridgeSiteProfile.ContractVersion,
                    true)), Is.True);
            Assert.That(Source(), Does.Not.Contain("unity_WorldToObject"));
        }
    }
}
