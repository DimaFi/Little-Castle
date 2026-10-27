using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace LittleCastle.World
{
    /// <summary>Presentation quality only. The light limit is per object, not a scene light budget.</summary>
    public static class UrpPreviewQuality
    {
        [Serializable]
        public sealed class Snapshot
        {
            public string pipeline, additionalLightsMode;
            public int msaaSamples, mainLightShadowResolution, shadowCascades, additionalLightsPerObject;
            public float shadowDistance;
            public bool softShadows, additionalLightShadows;
        }

        public static Snapshot ReadActive()
        {
            var current = QualitySettings.renderPipeline ?? GraphicsSettings.defaultRenderPipeline;
            if (!(current is UniversalRenderPipelineAsset asset)) return new Snapshot { pipeline = current ? current.name : "Built-in" };
            return new Snapshot { pipeline=asset.name, msaaSamples=asset.msaaSampleCount,
                mainLightShadowResolution=asset.mainLightShadowmapResolution, shadowCascades=asset.shadowCascadeCount,
                shadowDistance=asset.shadowDistance, additionalLightsPerObject=asset.maxAdditionalLightsCount,
                additionalLightsMode=asset.additionalLightsRenderingMode.ToString(), softShadows=asset.supportsSoftShadows,
                additionalLightShadows=asset.supportsAdditionalLightShadows };
        }

        public static bool Configure(RenderPipelineAsset pipeline, TerrainStarterPreviewControls.PreviewQuality quality)
        {
            if (!(pipeline is UniversalRenderPipelineAsset asset)) return false;
            bool low = quality == TerrainStarterPreviewControls.PreviewQuality.Low;
            bool high = quality == TerrainStarterPreviewControls.PreviewQuality.High;
            bool ultra = quality == TerrainStarterPreviewControls.PreviewQuality.Ultra;
            asset.msaaSampleCount = low ? 2 : ultra ? 8 : 4;
            asset.shadowDistance = low ? 45 : high ? 80 : ultra ? 100 : 65;
            asset.mainLightShadowmapResolution = low ? 1024 : ultra ? 4096 : 2048;
            // URP uses one cascade for uncascaded shadows, never zero.
            asset.shadowCascadeCount = low ? 1 : ultra ? 4 : 2;
            asset.maxAdditionalLightsCount = low ? 1 : high ? 4 : ultra ? 8 : 2;
            return true;
        }
    }

    /// <summary>Owns a transient pipeline clone; never edits the project's shared pipeline asset.</summary>
    public sealed class UrpPreviewQualityOverride : IDisposable
    {
        private int qualityLevel;
        private RenderPipelineAsset previousOverride;
        private readonly RenderPipelineAsset[] templates = new RenderPipelineAsset[4];
        private TerrainStarterPreviewControls.PreviewQuality currentQuality;
        public RenderPipelineAsset Pipeline { get; private set; }

        private UrpPreviewQualityOverride(TerrainStarterPreviewControls.PreviewQuality quality)
        {
            qualityLevel = QualitySettings.GetQualityLevel();
            previousOverride = QualitySettings.renderPipeline;
            string[] names = QualitySettings.names;
            foreach (TerrainStarterPreviewControls.PreviewQuality tier in Enum.GetValues(typeof(TerrainStarterPreviewControls.PreviewQuality)))
            {
                int level = Array.IndexOf(names, tier.ToString());
                var template = level >= 0 ? QualitySettings.GetRenderPipelineAssetAt(level) : null;
                if (!(template is UniversalRenderPipelineAsset))
                    throw new InvalidOperationException("Install URP quality profiles with UrpQualityProfileSetup.Run before using preview controls (missing " + tier + ").");
                templates[(int)tier] = template;
            }
            SetQuality(quality);
        }

        public static UrpPreviewQualityOverride Create(TerrainStarterPreviewControls.PreviewQuality quality = TerrainStarterPreviewControls.PreviewQuality.Medium)
        {
            var source = QualitySettings.renderPipeline ?? GraphicsSettings.defaultRenderPipeline;
            return source is UniversalRenderPipelineAsset ? new UrpPreviewQualityOverride(quality) : null;
        }

        public void SetQuality(TerrainStarterPreviewControls.PreviewQuality quality)
        {
            int activeLevel = QualitySettings.GetQualityLevel();
            if (activeLevel != qualityLevel)
            {
                // A settings menu may have selected another slot. Release our old
                // slot before adopting the newly active one, retaining both originals.
                QualitySettings.SetQualityLevel(qualityLevel, false);
                if (QualitySettings.renderPipeline == Pipeline) QualitySettings.renderPipeline = previousOverride;
                QualitySettings.SetQualityLevel(activeLevel, false);
                qualityLevel = activeLevel;
                previousOverride = QualitySettings.renderPipeline;
            }
            if (Pipeline != null && currentQuality == quality)
            {
                QualitySettings.renderPipeline = Pipeline;
                return;
            }
            var previousClone = Pipeline;
            var template = templates[(int)quality];
            // Capability switches with internal URP setters come from editor-authored
            // templates. Runtime code changes only public properties on this clone.
            Pipeline = UnityEngine.Object.Instantiate(template);
            Pipeline.name = template.name + " (Preview Runtime " + quality + ")";
            Pipeline.hideFlags = HideFlags.HideAndDontSave;
            UrpPreviewQuality.Configure(Pipeline, quality);
            currentQuality = quality;
            QualitySettings.renderPipeline = Pipeline;
            DestroyClone(previousClone);
        }

        public void Dispose()
        {
            if (Pipeline == null) return;
            int activeLevel = QualitySettings.GetQualityLevel();
            // Restore the slot we actually changed, even if another system selected
            // a different quality level while this preview was active.
            if (activeLevel != qualityLevel) QualitySettings.SetQualityLevel(qualityLevel, false);
            if (QualitySettings.renderPipeline == Pipeline) QualitySettings.renderPipeline = previousOverride;
            if (activeLevel != qualityLevel) QualitySettings.SetQualityLevel(activeLevel, false);
            DestroyClone(Pipeline);
            Pipeline = null;
        }

        private static void DestroyClone(RenderPipelineAsset clone)
        {
            if (clone == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(clone);
            else UnityEngine.Object.DestroyImmediate(clone);
        }
    }
}
