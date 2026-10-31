using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace LittleCastle.Editor
{
    /// <summary>Installs the project's adopted renderer and persistent quality profiles.</summary>
    public static class UrpAdoptionSetup
    {
        public static string EvidenceRoot => Path.GetFullPath(Environment.GetEnvironmentVariable("LC_URP_EVIDENCE_ROOT") ??
            Path.Combine(Application.dataPath, "../../Little-Castle_Assets/Research/URP_Adoption_2026-10-25"));

        [MenuItem("Little Castle/Rendering/URP/Apply Adopted Renderer Settings")]
        public static void Run()
        {
            try
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before configuring URP.");
                Directory.CreateDirectory(EvidenceRoot);
                const string root = UrpQualityProfileSetup.SettingsPath;
                var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(root + "/LittleCastle_URP_Renderer.asset");
                if (renderer == null) throw new InvalidOperationException("The adopted renderer asset is missing.");
                var features = renderer.rendererFeatures;
                if (features.Count != 1 || !(features[0] is LittleCastle.World.TerrainStarterSoftSceneRendererFeature))
                    throw new InvalidOperationException("Expected exactly one owned LC SoftScene feature.");
                string globalPath = root + "/LittleCastle_URP_GlobalSettings.asset";
                var global = AssetDatabase.LoadAssetAtPath<RenderPipelineGlobalSettings>(globalPath);
                if (global == null)
                {
                    string source = AssetDatabase.GetAllAssetPaths().Single(p => p.EndsWith("/Settings/UNS_URP_Global_Settings.asset"));
                    if (!AssetDatabase.CopyAsset(source, globalPath)) throw new IOException("Could not create owned URP global settings.");
                    global = AssetDatabase.LoadAssetAtPath<RenderPipelineGlobalSettings>(globalPath);
                }
                EditorGraphicsSettings.SetRenderPipelineGlobalSettingsAsset(typeof(UniversalRenderPipeline), global);
                UrpQualityProfileSetup.Run();
                int medium = Array.IndexOf(QualitySettings.names, "Medium");
                QualitySettings.SetQualityLevel(medium, true);
                // Persist the desktop default separately from the editor's current level.
                var quality = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset").Single());
                var defaults = quality.FindProperty("m_PerPlatformDefaultQuality");
                if (defaults != null)
                {
                    for (int i = 0; i < defaults.arraySize; i++)
                    {
                        var entry = defaults.GetArrayElementAtIndex(i);
                        if (entry.FindPropertyRelative("first").stringValue == "Standalone")
                            entry.FindPropertyRelative("second").intValue = medium;
                    }
                    quality.ApplyModifiedPropertiesWithoutUndo();
                }
                AssetDatabase.SaveAssets();
                var lines = new System.Collections.Generic.List<string>
                {
                    "Unity=" + Application.unityVersion,
                    "Project=" + Path.GetDirectoryName(Application.dataPath),
                    "Default pipeline=" + GraphicsSettings.defaultRenderPipeline,
                    "Current quality=" + QualitySettings.names[QualitySettings.GetQualityLevel()],
                    "Owned renderer=" + AssetDatabase.GetAssetPath(renderer),
                    "Owned global settings=" + globalPath,
                    "Renderer features=" + features.Count
                };
                for (int i = 0; i < QualitySettings.names.Length; i++)
                    lines.Add(QualitySettings.names[i] + "=" + QualitySettings.GetRenderPipelineAssetAt(i));
                File.WriteAllLines(Path.Combine(EvidenceRoot, "main_setup.txt"), lines);
                Debug.Log("LC_URP_ADOPTED: main project configured with owned settings and four quality profiles.");
                if (Application.isBatchMode) UrpBatchExit.WhenIdle(0);
            }
            catch (Exception ex)
            {
                Directory.CreateDirectory(EvidenceRoot);
                File.WriteAllText(Path.Combine(EvidenceRoot, "main_setup_failed.txt"), ex.ToString());
                Debug.LogException(ex);
                if (Application.isBatchMode) UrpBatchExit.WhenIdle(2);
            }
        }
    }
}
