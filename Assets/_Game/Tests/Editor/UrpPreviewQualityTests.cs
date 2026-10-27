using LittleCastle.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace LittleCastle.Tests
{
    public sealed class UrpPreviewQualityTests
    {
        [Test]
        public void RuntimeQuality_ChangesCloneAndRestoresSlotWithoutChangingSharedAsset()
        {
            var source = QualitySettings.renderPipeline ?? GraphicsSettings.defaultRenderPipeline;
            Assert.That(source, Is.Not.Null, "Run UrpQualityProfileSetup.Run before the URP quality test.");
            string before = EditorJsonUtility.ToJson(source);
            var previous = QualitySettings.renderPipeline;
            int previousLevel = QualitySettings.GetQualityLevel();
            UrpPreviewQualityOverride scope = null;
            try
            {
                scope = UrpPreviewQualityOverride.Create();
                Assert.That(scope, Is.Not.Null);
                Assert.That(scope.Pipeline, Is.Not.SameAs(source));
                Assert.That(scope.Pipeline.hideFlags, Is.EqualTo(HideFlags.HideAndDontSave));
                scope.SetQuality(TerrainStarterPreviewControls.PreviewQuality.Low);
                var low = UrpPreviewQuality.ReadActive();
                Assert.That(low.msaaSamples, Is.EqualTo(2));
                Assert.That(low.shadowCascades, Is.EqualTo(1));
                Assert.That(low.softShadows, Is.False);
                Assert.That(low.additionalLightsPerObject, Is.EqualTo(1));
                scope.SetQuality(TerrainStarterPreviewControls.PreviewQuality.Ultra);
                var ultra = UrpPreviewQuality.ReadActive();
                Assert.That(ultra.msaaSamples, Is.EqualTo(8));
                Assert.That(ultra.shadowCascades, Is.EqualTo(4));
                Assert.That(ultra.softShadows, Is.True);
                Assert.That(ultra.additionalLightShadows, Is.False);
                Assert.That(ultra.additionalLightsMode, Is.EqualTo("PerPixel"));
                Assert.That(EditorJsonUtility.ToJson(source), Is.EqualTo(before));
                // Disposal must restore the original slot even after an external level switch.
                int otherLevel = (previousLevel + 1) % QualitySettings.names.Length;
                QualitySettings.SetQualityLevel(otherLevel, false);
                scope.Dispose();
                Assert.That(QualitySettings.GetQualityLevel(), Is.EqualTo(otherLevel));
                QualitySettings.SetQualityLevel(previousLevel, false);
                Assert.That(QualitySettings.renderPipeline, Is.SameAs(previous));
                Assert.That(EditorJsonUtility.ToJson(source), Is.EqualTo(before));
                // Reapplying after a settings-menu level change must release the
                // original slot and restore the newly adopted slot on disposal.
                scope = UrpPreviewQualityOverride.Create();
                var otherOriginal = QualitySettings.GetRenderPipelineAssetAt(otherLevel);
                QualitySettings.SetQualityLevel(otherLevel, false);
                scope.SetQuality(TerrainStarterPreviewControls.PreviewQuality.High);
                scope.Dispose();
                Assert.That(QualitySettings.renderPipeline, Is.SameAs(otherOriginal));
                Assert.That(QualitySettings.GetRenderPipelineAssetAt(previousLevel), Is.SameAs(previous));
            }
            finally
            {
                scope?.Dispose();
                QualitySettings.SetQualityLevel(previousLevel, false);
                QualitySettings.renderPipeline = previous;
            }
        }
    }
}
