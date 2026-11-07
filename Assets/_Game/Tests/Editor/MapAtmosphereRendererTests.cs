using System.Linq;
using LittleCastle.Experimental.MountainBorder.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.Rendering.Universal;

namespace LittleCastle.Tests
{
    public sealed class MapAtmosphereRendererTests
    {
        const string Directory = "Assets/_Game/Settings/Rendering/URP/";

        [TestCase("")]
        [TestCase("_Low")]
        [TestCase("_Medium")]
        [TestCase("_High")]
        [TestCase("_Ultra")]
        public void StandardPipelineKeepsItsDefaultAndRegistersAnIsolatedAtmosphereRenderer(string suffix)
        {
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(
                Directory + $"LittleCastle_URP{suffix}.asset");
            var baseline = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(
                Directory + "LittleCastle_URP_Renderer.asset");
            var optional = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(
                Directory + "LittleCastle_URP_AtmosphereRenderer.asset");
            Assert.That(pipeline, Is.Not.Null);
            Assert.That(baseline, Is.Not.Null);
            Assert.That(optional, Is.Not.Null);
            var config = new SerializedObject(pipeline);
            int defaultIndex = config.FindProperty("m_DefaultRendererIndex").intValue;
            Assert.That(pipeline.rendererDataList[defaultIndex], Is.SameAs(baseline));
            Assert.That(MapAtmospherePresentation.FindRendererIndex(pipeline, optional), Is.Not.EqualTo(defaultIndex));
            Assert.That(MapAtmospherePresentation.FindRendererIndex(pipeline, optional), Is.GreaterThanOrEqualTo(0));

            string[] names = { "PhysicallyBasedSkyURP", "VolumetricFogRendererFeature" };
            var baselineFeatures = baseline.rendererFeatures.Where(feature => feature).Select(feature => feature.GetType().Name);
            var optionalFeatures = optional.rendererFeatures.Where(feature => feature && feature.isActive)
                .Select(feature => feature.GetType().Name);
            foreach (string name in names)
            {
                Assert.That(baselineFeatures, Does.Not.Contain(name));
                Assert.That(optionalFeatures, Does.Contain(name));
            }
        }
    }
}
