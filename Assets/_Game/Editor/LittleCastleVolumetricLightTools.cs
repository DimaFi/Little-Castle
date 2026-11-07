using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace LittleCastle.Editor
{
    /// <summary>
    /// Explicit optional light authoring. Never scans all world lights or
    /// silently changes the user's current map / previously placed objects.
    /// </summary>
    public static class LittleCastleVolumetricLightTools
    {
        [MenuItem("Little Castle/Atmosphere/Add volumetric to selected scene lights")]
        public static void TagSelectedSceneLights()
        {
            int added = 0;
            foreach (GameObject item in Selection.gameObjects)
            {
                if (!item || !item.scene.IsValid() || !item.scene.isLoaded) continue;
                Light light = item.GetComponent<Light>();
                if (!light || (light.type != LightType.Point && light.type != LightType.Spot))
                    continue;
                if (item.GetComponent<VolumetricAdditionalLight>()) continue;
                Undo.AddComponent<VolumetricAdditionalLight>(item);
                added++;
            }
            Debug.Log("[LC Atmosphere] Volumetric components added to " + added +
                " selected scene point/spot lights. Prefab assets unchanged.");
        }

        [MenuItem("Little Castle/Atmosphere/Add volumetric to selected prefab assets")]
        public static void TagSelectedPrefabAssets()
        {
            // Explicit selection of ORIGINAL prefab assets only, not instances.
            var paths = new HashSet<string>();
            foreach (UnityEngine.Object asset in Selection.objects)
            {
                if (!asset) continue;
                string path = AssetDatabase.GetAssetPath(asset);
                if (!string.IsNullOrEmpty(path) &&
                    path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                    paths.Add(path);
            }
            int prefabCount = 0, lightCount = 0;
            foreach (string path in paths)
            {
                GameObject root = null;
                try
                {
                    root = PrefabUtility.LoadPrefabContents(path);
                    int additions = 0;
                    foreach (Light light in root.GetComponentsInChildren<Light>(true))
                    {
                        if (light.type != LightType.Point && light.type != LightType.Spot)
                            continue;
                        if (light.GetComponent<VolumetricAdditionalLight>()) continue;
                        light.gameObject.AddComponent<VolumetricAdditionalLight>();
                        additions++;
                    }
                    if (additions == 0) continue;
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    prefabCount++;
                    lightCount += additions;
                }
                finally
                {
                    if (root) PrefabUtility.UnloadPrefabContents(root);
                }
            }
            Debug.Log("[LC Atmosphere] Added " + lightCount +
                " volumetric components to " + prefabCount +
                " explicitly selected source prefabs. Scene instances were not edited.");
        }

        [MenuItem("Little Castle/Atmosphere/Verify optional integration")]
        public static void Verify()
        {
            var urp = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline
                as UniversalRenderPipelineAsset;
            var skyPackage = UnityEditor.PackageManager.PackageInfo.FindForAssembly(
                typeof(PhysicallyBasedSkyURP).Assembly);
            var fogPackage = UnityEditor.PackageManager.PackageInfo.FindForAssembly(
                typeof(VolumetricFogRendererFeature).Assembly);
            Debug.Log("[LC Atmosphere] URP: " + (urp ? urp.name : "NOT ACTIVE") +
                "; Physically Based Sky package: " +
                (skyPackage != null ? skyPackage.name : "NOT RESOLVED") +
                "; Cristian Qiu fog package: " +
                (fogPackage != null ? fogPackage.name : "not resolved by assembly") +
                ". Reminder: this is READ-ONLY; add both Renderer Features to a" +
                " duplicated URP Renderer Data and enable the scene bridge in Play.");
        }
    }
}
