using System;
using System.IO;
using LittleCastle.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace LittleCastle.Editor
{
    /// <summary>Idempotent owned-profile setup for both the main project and the migration copy.</summary>
    public static class UrpQualityProfileSetup
    {
        public const string SettingsPath = "Assets/_Game/Settings/Rendering/URP";

        [MenuItem("Little Castle/Rendering/URP/Configure Quality Profiles")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Configure persistent quality profiles outside Play Mode.");
            var source = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(SettingsPath + "/LittleCastle_URP.asset");
            var renderer = AssetDatabase.LoadAssetAtPath<ScriptableObject>(SettingsPath + "/LittleCastle_URP_Renderer.asset");
            if (source == null || renderer == null)
                throw new InvalidOperationException("Install the owned LittleCastle URP pipeline and renderer before configuring quality.");
            // Validate every slot before changing any persistent settings.
            foreach (string name in QualitySettings.names) TierForName(name);
            var profiles = new RenderPipelineAsset[4];
            foreach (TerrainStarterPreviewControls.PreviewQuality quality in Enum.GetValues(typeof(TerrainStarterPreviewControls.PreviewQuality)))
            {
                string path = SettingsPath + "/LittleCastle_URP_" + quality + ".asset";
                var profile = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(path);
                if (profile == null)
                {
                    if (File.Exists(path)) throw new InvalidOperationException("Unexpected asset at " + path);
                    if (!AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(source), path))
                        throw new IOException("Could not create " + path);
                    profile = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(path);
                }
                if (!UrpPreviewQuality.Configure(profile, quality))
                    throw new InvalidOperationException("Expected a URP 17.5 pipeline at " + path);
                // All quality profiles share the one owned renderer (including LC SoftScene).
                var data = new SerializedObject(profile);
                // These capabilities have internal setters in URP 17.5; author them
                // here so runtime can select a template without serialized mutation.
                data.FindProperty("m_MainLightShadowsSupported").boolValue = true;
                data.FindProperty("m_SoftShadowsSupported").boolValue = quality != TerrainStarterPreviewControls.PreviewQuality.Low;
                data.FindProperty("m_AdditionalLightsRenderingMode").intValue = 1; // PerPixel
                data.FindProperty("m_AdditionalLightShadowsSupported").boolValue = false;
                var renderers = data.FindProperty("m_RendererDataList");
                renderers.arraySize = 1;
                renderers.GetArrayElementAtIndex(0).objectReferenceValue = renderer;
                data.FindProperty("m_DefaultRendererIndex").intValue = 0;
                data.ApplyModifiedPropertiesWithoutUndo();
                profile.name = "LittleCastle_URP_" + quality;
                EditorUtility.SetDirty(profile);
                profiles[(int)quality] = profile;
            }
            int previousLevel = QualitySettings.GetQualityLevel();
            try
            {
                string[] names = QualitySettings.names;
                for (int i = 0; i < names.Length; i++)
                {
                    var tier = TierForName(names[i]);
                    QualitySettings.SetQualityLevel(i, false);
                    QualitySettings.renderPipeline = profiles[(int)tier];
                    Debug.Log("LC_URP_QUALITY: " + names[i] + " -> " + tier);
                }
                GraphicsSettings.defaultRenderPipeline = profiles[(int)TerrainStarterPreviewControls.PreviewQuality.Medium];
            }
            finally { QualitySettings.SetQualityLevel(previousLevel, false); }
            AssetDatabase.SaveAssets();
            Debug.Log("LC_URP_QUALITY: four owned profiles installed; shared renderer preserved.");
        }

        private static TerrainStarterPreviewControls.PreviewQuality TierForName(string name)
        {
            switch (name)
            {
                case "Very Low": case "Low": return TerrainStarterPreviewControls.PreviewQuality.Low;
                case "Medium": return TerrainStarterPreviewControls.PreviewQuality.Medium;
                case "High": case "Very High": return TerrainStarterPreviewControls.PreviewQuality.High;
                case "Ultra": return TerrainStarterPreviewControls.PreviewQuality.Ultra;
                default: throw new InvalidOperationException("Define an explicit URP mapping for quality level '" + name + "'.");
            }
        }
    }
}
