using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace LittleCastle.Editor
{
    // Runs only in the isolated migration project; never converts vendor materials.
    public static class UrpMigrationBootstrap
    {
        const string Settings = "Assets/_Game/Settings/Rendering/URP";
        static double readyAt;
        static readonly List<string> Errors = new List<string>();
        static int exitCode;
        static double exitAt;
        static string Output => Path.GetDirectoryName(InnerverseStarterResearch.Output);

        [MenuItem("Little Castle/Research/URP/Configure and Verify Migration Copy")]
        public static void Run()
        {
            try
            {
                Errors.Clear();
                if (!Path.GetFileName(Path.GetDirectoryName(Application.dataPath)).StartsWith("Little-Castle_URP_"))
                    throw new InvalidOperationException("This bootstrap is restricted to the isolated URP copy.");
                Directory.CreateDirectory(Output);
                Directory.CreateDirectory(InnerverseStarterResearch.Output);
                Directory.CreateDirectory(Settings);
                AssetDatabase.Refresh();
                var paths = AssetDatabase.GetAllAssetPaths();
                Copy(paths.Single(p => p.EndsWith("/Settings/UNS_URP.asset")), Settings + "/LittleCastle_URP.asset");
                Copy(paths.Single(p => p.EndsWith("/Settings/UNS_URP_Renderer.asset")), Settings + "/LittleCastle_URP_Renderer.asset");
                var renderer = AssetDatabase.LoadAssetAtPath<ScriptableObject>(Settings + "/LittleCastle_URP_Renderer.asset");
                var rendererData = new SerializedObject(renderer);
                rendererData.FindProperty("m_RendererFeatures").ClearArray();
                rendererData.FindProperty("m_RendererFeatureMap").ClearArray();
                var feature = AssetDatabase.LoadAllAssetsAtPath(Settings + "/LittleCastle_URP_Renderer.asset")
                    .OfType<ScriptableObject>().FirstOrDefault(a => a.GetType().FullName == "LittleCastle.World.TerrainStarterSoftSceneRendererFeature");
                if (feature == null)
                {
                    feature = ScriptableObject.CreateInstance("LittleCastle.World.TerrainStarterSoftSceneRendererFeature");
                    if (feature == null) throw new InvalidOperationException("The URP soft-scene feature is unavailable.");
                    feature.name = "LittleCastle_SoftScene";
                    AssetDatabase.AddObjectToAsset(feature, renderer);
                }
                var features = rendererData.FindProperty("m_RendererFeatures");
                features.arraySize = 1;
                features.GetArrayElementAtIndex(0).objectReferenceValue = feature;
                Set(rendererData, "m_RenderingMode", 0);
                Set(rendererData, "m_DepthPrimingMode", 0);
                // The baseline uses the LC feature, with no Volume post-processing stack.
                rendererData.FindProperty("postProcessData").objectReferenceValue = null;
                rendererData.ApplyModifiedPropertiesWithoutUndo();
                var pipeline = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(Settings + "/LittleCastle_URP.asset");
                var data = new SerializedObject(pipeline);
                data.FindProperty("m_RendererDataList").GetArrayElementAtIndex(0).objectReferenceValue = renderer;
                Set(data, "m_RequireDepthTexture", false);
                Set(data, "m_RequireOpaqueTexture", false);
                Set(data, "m_SupportsHDR", false);
                Set(data, "m_MSAA", 4);
                Set(data, "m_MainLightShadowmapResolution", 2048);
                Set(data, "m_AdditionalLightsRenderingMode", 1);
                Set(data, "m_AdditionalLightShadowsSupported", false);
                Set(data, "m_SupportDataDrivenLensFlare", false);
                Set(data, "m_SupportScreenSpaceLensFlare", false);
                Set(data, "m_ShadowDistance", 80f);
                Set(data, "m_ShadowCascadeCount", 2);
                data.ApplyModifiedPropertiesWithoutUndo();
                GraphicsSettings.defaultRenderPipeline = pipeline;
                int previousQuality = QualitySettings.GetQualityLevel();
                for (int i = 0; i < QualitySettings.names.Length; i++)
                {
                    QualitySettings.SetQualityLevel(i, false);
                    QualitySettings.renderPipeline = pipeline;
                }
                QualitySettings.SetQualityLevel(previousQuality, false);
                AssetDatabase.SaveAssets();
                EditorSceneManager.OpenScene(InnerverseStarterStudyScene.ScenePath);
                Application.logMessageReceived += CollectLog;
                readyAt = EditorApplication.timeSinceStartup + 3;
                EditorApplication.update += Verify;
                Debug.Log("LC_URP: configuration complete; waiting for render pipeline initialization.");
            }
            catch (Exception ex) { Fail(ex); }
        }

        static void Copy(string source, string destination)
        {
            if (AssetDatabase.LoadMainAssetAtPath(destination) == null && !AssetDatabase.CopyAsset(source, destination))
                throw new IOException("Could not copy " + source);
        }
        static void Set(SerializedObject data, string name, object value)
        {
            var p = data.FindProperty(name);
            if (p == null) throw new MissingFieldException(name);
            if (value is bool b) p.boolValue = b;
            else if (value is int i) p.intValue = i;
            else if (value is float f) p.floatValue = f;
        }
        static void CollectLog(string message, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) Errors.Add(message);
        }
        static void Verify()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.timeSinceStartup < readyAt) return;
            EditorApplication.update -= Verify;
            try
            {
                // StandardRequest creates the pipeline if the editor has not drawn a frame yet.
                InnerverseStarterStudyScene.RefreshStudy();
                foreach (string name in new[] { "LC_StylizedLit", "LC_Terrain", "LC_Grass", "LC_Foliage", "LC_DistantSimple", "StylizedDayNightSky", "LC_EmissiveGlow", "LC_NightLightPool", "LC_SoftScene" })
                {
                    string path = AssetDatabase.GetAllAssetPaths().Single(p => p.EndsWith("/" + name + ".shader"));
                    var shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
                    foreach (var message in ShaderUtil.GetShaderMessages(shader))
                    {
                        string line = name + ": " + message.severity + ": " + message.message + " (" + message.file + ":" + message.line + ")";
                        Debug.Log(line);
                        if (message.severity.ToString() == "Error") Errors.Add(line);
                    }
                }
                var grass = UnityEngine.Object.FindAnyObjectByType<LittleCastle.Research.InnerverseGrassStudy>();
                string report = "Timestamp UTC=" + DateTime.UtcNow.ToString("O") + "\nUnity=" + Application.unityVersion + "\nPipeline=" + GraphicsSettings.currentRenderPipeline +
                    "\nGPU=" + SystemInfo.graphicsDeviceName + "\nAPI=" + SystemInfo.graphicsDeviceType +
                    "\nAuthored clumps=" + grass.authoredMatrices.Length + "\nLast submitted clumps=" + grass.lastVisibleClumps +
                    "\nLast submitted triangles=" + grass.lastSubmittedTriangles + "\nErrors=" + Errors.Count + "\n" + string.Join("\n", Errors);
                File.WriteAllText(Path.Combine(Output, "verification.txt"), report);
                AssetDatabase.SaveAssets();
                Debug.Log("LC_URP: verification finished. " + report);
                Application.logMessageReceived -= CollectLog;
                if (Application.isBatchMode)
                {
                    exitCode = Errors.Count == 0 ? 0 : 2;
                    exitAt = EditorApplication.timeSinceStartup + 5;
                    EditorApplication.update += ExitWhenIdle;
                }
            }
            catch (Exception ex) { Fail(ex); }
        }
        static void ExitWhenIdle()
        {
            if (EditorApplication.timeSinceStartup < exitAt || EditorApplication.isCompiling || EditorApplication.isUpdating || ShaderUtil.anythingCompiling) return;
            EditorApplication.update -= ExitWhenIdle;
            EditorApplication.Exit(exitCode);
        }
        static void Fail(Exception ex)
        {
            Debug.LogException(ex);
            Directory.CreateDirectory(Output);
            File.WriteAllText(Path.Combine(Output, "verification_failed.txt"), ex.ToString());
            if (Application.isBatchMode) EditorApplication.Exit(3);
        }
    }
}
