using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace LittleCastle.Editor
{
    public static class StylizedRenderingValidator
    {
        private sealed class ShaderRequirement
        {
            public readonly string shaderName;
            public readonly string[] properties;

            public ShaderRequirement(
                string shaderName,
                params string[] properties)
            {
                this.shaderName = shaderName;
                this.properties = properties;
            }
        }

        private static readonly ShaderRequirement[] Requirements =
        {
            new ShaderRequirement(
                "Little Castle/Sky/Stylized Day Night",
                "_ZenithColor",
                "_HorizonColor",
                "_SunDirection",
                "_MoonDirection",
                "_Daylight",
                "_TwilightStrength"),

            new ShaderRequirement(
                "Little Castle/Surface/LC Stylized Lit",
                "_MainTex",
                "_BumpMap",
                "_OcclusionMap",
                "_EmissionMap",
                "_EmissionNightStrength"),

            new ShaderRequirement(
                "Little Castle/Foliage/LC Foliage",
                "_MainTex",
                "_Cutoff",
                "_WindAmplitude",
                "_CrownSway",
                "_VertexWave",
                "_LeafFlutter",
                "_TransmissionStrength"),

            new ShaderRequirement(
                "Little Castle/Foliage/LC Grass",
                "_MainTex",
                "_Cutoff",
                "_WindAmplitude",
                "_GustStrength",
                "_MicroFlutter",
                "_TransmissionStrength"),

            new ShaderRequirement(
                "Little Castle/Terrain/LC Terrain",
                "_GrassTex",
                "_DirtTex",
                "_RockTex",
                "_MacroStrength",
                "_UseVertexMasks")
        };

        private static readonly string[] RequiredMaterialPaths =
        {
            "Assets/_Game/Materials/StylizedDayNightSky.mat",
            "Assets/_Game/Materials/Shared/LC_StylizedLit_Default.mat",
            "Assets/_Game/Materials/Shared/LC_Foliage_Default.mat",
            "Assets/_Game/Materials/Shared/LC_Grass_Default.mat",
            "Assets/_Game/Materials/Material_terrain/LC_Terrain_Default.mat"
        };

        [MenuItem(
            "Little Castle/Rendering/Validate Stylized Rendering")]
        public static void ValidateStylizedRendering()
        {
            int errors = 0;
            int warnings = 0;

            Debug.Log(
                "[Little Castle Rendering] Validation started.");

            foreach (
                ShaderRequirement requirement
                in Requirements)
            {
                Shader shader =
                    Shader.Find(
                        requirement.shaderName);

                if (shader == null)
                {
                    errors++;
                    Debug.LogError(
                        "[Little Castle Rendering] Missing shader: " +
                        requirement.shaderName);

                    continue;
                }

                using (
                    var material =
                        new Material(shader))
                {
                    foreach (
                        string property
                        in requirement.properties)
                    {
                        if (!material.HasProperty(
                                property))
                        {
                            errors++;
                            Debug.LogError(
                                "[Little Castle Rendering] Shader '" +
                                requirement.shaderName +
                                "' is missing property '" +
                                property +
                                "'.");
                        }
                    }
                }

                if (TryShaderHasError(
                        shader,
                        out bool hasError) &&
                    hasError)
                {
                    errors++;
                    Debug.LogError(
                        "[Little Castle Rendering] Unity reports " +
                        "compile errors for shader: " +
                        requirement.shaderName);
                }
            }

            foreach (
                string path
                in RequiredMaterialPaths)
            {
                Material material =
                    AssetDatabase.LoadAssetAtPath<
                        Material>(path);

                if (material == null)
                {
                    errors++;
                    Debug.LogError(
                        "[Little Castle Rendering] Missing material: " +
                        path);
                }
            }

            if (PlayerSettings.colorSpace ==
                ColorSpace.Gamma)
            {
                warnings++;
                Debug.LogWarning(
                    "[Little Castle Rendering] Project is currently " +
                    "Gamma color space. Linear is a candidate for visual " +
                    "testing, not an automatic migration. Compare real " +
                    "screenshots/materials/performance before changing it.");
            }
            else
            {
                Debug.Log(
                    "[Little Castle Rendering] Project is currently " +
                    PlayerSettings.colorSpace +
                    " color space.");
            }

            string summary =
                "[Little Castle Rendering] Validation finished. " +
                "Errors: " +
                errors +
                ", warnings: " +
                warnings +
                ".";

            if (errors > 0)
                Debug.LogError(summary);
            else if (warnings > 0)
                Debug.LogWarning(summary);
            else
                Debug.Log(summary);
        }

        private static bool TryShaderHasError(
            Shader shader,
            out bool hasError)
        {
            hasError = false;

            try
            {
                Type shaderUtilType =
                    typeof(UnityEditor.Editor)
                        .Assembly
                        .GetType(
                            "UnityEditor.ShaderUtil");

                if (shaderUtilType == null)
                    return false;

                MethodInfo method =
                    shaderUtilType.GetMethod(
                        "ShaderHasError",
                        BindingFlags.Static |
                        BindingFlags.Public |
                        BindingFlags.NonPublic,
                        null,
                        new[] { typeof(Shader) },
                        null);

                if (method == null)
                    return false;

                object result =
                    method.Invoke(
                        null,
                        new object[] { shader });

                if (result is bool value)
                {
                    hasError = value;
                    return true;
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "[Little Castle Rendering] Could not query " +
                    "ShaderUtil compile status: " +
                    exception.Message);
            }

            return false;
        }
    }
}
