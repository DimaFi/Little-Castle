using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace LittleCastle.Editor
{
    /// <summary>
    /// Maps Ready models to Little Castle shader families without destroying the
    /// material information already present in imported FBX files.
    ///
    /// Explicit Ready texture-contract files win when present. Local test
    /// releases often carry perfectly usable FBX materials instead, so those
    /// textures/colors are used as a fallback per material slot.
    /// </summary>
    internal static class ReadyModelMaterials
    {
        private const string SharedRoot = "Assets/_Game/Materials/Shared/";
        private const string TextureRoot = "Assets/_Game/Textures/Ready/";

        public static void ConfigureRenderer(
            Renderer renderer,
            Transform lodRoot,
            string category,
            string archetypeId,
            string variant,
            bool farthest)
        {
            Material[] sourceSlots =
                renderer.sharedMaterials;

            if (sourceSlots == null ||
                sourceSlots.Length == 0)
            {
                sourceSlots =
                    new Material[1];
            }

            Material[] converted =
                new Material[sourceSlots.Length];

            for (int slot = 0;
                 slot < sourceSlots.Length;
                 slot++)
            {
                Material source =
                    sourceSlots[slot];

                string family =
                    GetFamily(
                        renderer.transform,
                        lodRoot,
                        category,
                        source);

                string templateName =
                    farthest
                        ? "LC_DistantSimple_Default.mat"
                        : family == "Leaves"
                            ? "LC_Foliage_Default.mat"
                            : family == "Grass"
                                ? "LC_Grass_Default.mat"
                                : "LC_StylizedLit_Default.mat";

                Material template =
                    AssetDatabase.LoadAssetAtPath<Material>(
                        SharedRoot +
                        templateName);

                if (template == null)
                {
                    Debug.LogError(
                        "Missing shared material: " +
                        templateName);

                    converted[slot] =
                        source;

                    continue;
                }

                converted[slot] =
                    ResolveMaterial(
                        template,
                        source,
                        slot,
                        category,
                        archetypeId,
                        variant,
                        family,
                        farthest);
            }

            renderer.sharedMaterials =
                converted;

            if (farthest)
            {
                renderer.shadowCastingMode =
                    ShadowCastingMode.Off;

                renderer.receiveShadows =
                    false;

                renderer.lightProbeUsage =
                    LightProbeUsage.Off;

                renderer.reflectionProbeUsage =
                    ReflectionProbeUsage.Off;
            }
        }

        private static string GetFamily(
            Transform renderer,
            Transform lodRoot,
            string category,
            Material source)
        {
            if (ContainsAny(
                    category,
                    "Grass",
                    "Wheat"))
            {
                return "Grass";
            }

            string sourceName =
                source != null
                    ? source.name
                    : string.Empty;

            Texture sourceBase =
                GetFirstTexture(
                    source,
                    out _,
                    "_BaseMap",
                    "_MainTex",
                    "_BaseColorMap",
                    "_BaseColorTex",
                    "_Albedo");

            string textureName =
                sourceBase != null
                    ? sourceBase.name
                    : string.Empty;

            if (ContainsAny(
                    sourceName,
                    "Leaf",
                    "Leaves",
                    "Foliage",
                    "Crown",
                    "Needle") ||
                ContainsAny(
                    textureName,
                    "Leaf",
                    "Leaves",
                    "Foliage",
                    "Crown",
                    "Needle"))
            {
                return "Leaves";
            }

            for (Transform node = renderer;
                 node != null;
                 node = node.parent)
            {
                if (ContainsAny(
                        node.name,
                        "Leaf",
                        "Leaves",
                        "Foliage",
                        "Crown",
                        "Needle"))
                {
                    return "Leaves";
                }

                if (ContainsAny(
                        node.name,
                        "Grass",
                        "Wheat"))
                {
                    return "Grass";
                }

                if (node == lodRoot)
                    break;
            }

            return "Solid";
        }

        private static Material ResolveMaterial(
            Material template,
            Material source,
            int slot,
            string category,
            string archetypeId,
            string variant,
            string family,
            bool farthest)
        {
            string textureFolder =
                TextureRoot +
                category +
                "/" +
                archetypeId +
                "/" +
                variant +
                "/";

            string prefix =
                variant +
                "_" +
                family +
                "_";

            Texture2D contractBaseColor =
                FindTexture(
                    textureFolder,
                    prefix +
                    "BaseColor");

            if (contractBaseColor == null &&
                family == "Solid")
            {
                contractBaseColor =
                    FindTexture(
                        textureFolder,
                        variant +
                        "_BaseColor");
            }

            Texture2D contractNormal =
                farthest
                    ? null
                    : FindTexture(
                        textureFolder,
                        prefix +
                        "Normal");

            Texture2D contractAo =
                farthest
                    ? null
                    : FindTexture(
                        textureFolder,
                        prefix +
                        "AO");

            Texture2D contractRoughness =
                farthest
                    ? null
                    : FindTexture(
                        textureFolder,
                        prefix +
                        "Roughness");

            Texture2D contractOpacity =
                farthest
                    ? null
                    : FindTexture(
                        textureFolder,
                        prefix +
                        "Opacity");

            string sourceBaseProperty;
            Texture sourceBase =
                GetFirstTexture(
                    source,
                    out sourceBaseProperty,
                    "_BaseMap",
                    "_MainTex",
                    "_BaseColorMap",
                    "_BaseColorTex",
                    "_Albedo");

            Texture sourceNormal =
                farthest
                    ? null
                    : GetFirstTexture(
                        source,
                        out _,
                        "_BumpMap",
                        "_NormalMap",
                        "_NormalTex");

            Texture sourceAo =
                farthest
                    ? null
                    : GetFirstTexture(
                        source,
                        out _,
                        "_OcclusionMap",
                        "_AOMap",
                        "_AmbientOcclusion");

            Texture sourceRoughness =
                farthest
                    ? null
                    : GetFirstTexture(
                        source,
                        out _,
                        "_RoughnessMap",
                        "_RoughnessTex");

            Texture sourceOpacity =
                farthest
                    ? null
                    : GetFirstTexture(
                        source,
                        out _,
                        "_OpacityMap",
                        "_AlphaMap",
                        "_TransparencyMap");

            Texture baseColor =
                contractBaseColor != null
                    ? contractBaseColor
                    : sourceBase;

            Texture normal =
                contractNormal != null
                    ? contractNormal
                    : sourceNormal;

            Texture ao =
                contractAo != null
                    ? contractAo
                    : sourceAo;

            Texture roughness =
                contractRoughness != null
                    ? contractRoughness
                    : sourceRoughness;

            Texture opacity =
                contractOpacity != null
                    ? contractOpacity
                    : sourceOpacity;

            bool hasUsefulSource =
                source != null &&
                (baseColor != null ||
                 normal != null ||
                 ao != null ||
                 roughness != null ||
                 opacity != null ||
                 HasAnyColorProperty(source));

            if (!hasUsefulSource &&
                contractBaseColor == null &&
                contractNormal == null &&
                contractAo == null &&
                contractRoughness == null &&
                contractOpacity == null)
            {
                return template;
            }

            string suffix =
                farthest
                    ? "Far"
                    : "Near";

            string sourceTag =
                source != null
                    ? Sanitize(
                        source.name)
                    : "Slot";

            string materialPath =
                ReadyModelCatalogSync.ReadyRoot +
                "/" +
                category +
                "/" +
                archetypeId +
                "/" +
                variant +
                "_S" +
                slot.ToString("00") +
                "_" +
                sourceTag +
                "_" +
                family +
                "_" +
                suffix +
                ".mat";

            Material material =
                AssetDatabase.LoadAssetAtPath<Material>(
                    materialPath);

            if (material == null)
            {
                material =
                    new Material(
                        template);

                AssetDatabase.CreateAsset(
                    material,
                    materialPath);
            }
            else
            {
                material.CopyPropertiesFromMaterial(
                    template);

                material.shader =
                    template.shader;
            }

            SetTexture(
                material,
                "_MainTex",
                baseColor);

            if (baseColor != null &&
                source != null &&
                !string.IsNullOrEmpty(
                    sourceBaseProperty) &&
                source.HasProperty(
                    sourceBaseProperty) &&
                material.HasProperty(
                    "_MainTex"))
            {
                material.SetTextureScale(
                    "_MainTex",
                    source.GetTextureScale(
                        sourceBaseProperty));

                material.SetTextureOffset(
                    "_MainTex",
                    source.GetTextureOffset(
                        sourceBaseProperty));
            }

            SetTexture(
                material,
                "_BumpMap",
                normal);

            SetTexture(
                material,
                "_OcclusionMap",
                ao);

            SetTexture(
                material,
                "_RoughnessMap",
                roughness);

            SetTexture(
                material,
                "_OpacityMap",
                opacity);

            if (material.HasProperty(
                    "_OpacityMapStrength"))
            {
                material.SetFloat(
                    "_OpacityMapStrength",
                    opacity != null
                        ? 1f
                        : 0f);
            }

            if (TryGetSourceColor(
                    source,
                    out Color sourceColor) &&
                material.HasProperty(
                    "_Color"))
            {
                material.SetColor(
                    "_Color",
                    sourceColor);
            }

            ApplySourceSmoothnessFallback(
                material,
                source);

            float cutoff =
                GetSourceCutoff(
                    source,
                    0.42f);

            if (material.HasProperty(
                    "_Cutoff"))
            {
                material.SetFloat(
                    "_Cutoff",
                    cutoff);
            }

            if (farthest)
            {
                bool alphaFamily =
                    family == "Leaves" ||
                    family == "Grass";

                if (material.HasProperty(
                        "_UseAlphaClip"))
                {
                    material.SetFloat(
                        "_UseAlphaClip",
                        alphaFamily
                            ? 1f
                            : 0f);
                }

                if (material.HasProperty(
                        "_Cull"))
                {
                    material.SetFloat(
                        "_Cull",
                        alphaFamily
                            ? 0f
                            : 2f);
                }
            }

            material.enableInstancing =
                true;

            EditorUtility.SetDirty(
                material);

            return material;
        }

        private static Texture2D FindTexture(
            string folder,
            string stem)
        {
            string[] extensions =
            {
                ".png",
                ".tga",
                ".jpg",
                ".jpeg",
                ".exr"
            };

            foreach (string extension in extensions)
            {
                Texture2D texture =
                    AssetDatabase.LoadAssetAtPath<Texture2D>(
                        folder +
                        stem +
                        extension);

                if (texture != null)
                    return texture;
            }

            return null;
        }

        private static Texture GetFirstTexture(
            Material material,
            out string property,
            params string[] properties)
        {
            property = null;

            if (material == null)
                return null;

            for (int i = 0;
                 i < properties.Length;
                 i++)
            {
                string candidate =
                    properties[i];

                if (!material.HasProperty(
                        candidate))
                {
                    continue;
                }

                Texture texture =
                    material.GetTexture(
                        candidate);

                if (texture == null)
                    continue;

                property = candidate;
                return texture;
            }

            return null;
        }

        private static void SetTexture(
            Material material,
            string property,
            Texture texture)
        {
            if (texture != null &&
                material.HasProperty(
                    property))
            {
                material.SetTexture(
                    property,
                    texture);
            }
        }

        private static bool HasAnyColorProperty(
            Material material)
        {
            return
                material != null &&
                (material.HasProperty(
                     "_BaseColor") ||
                 material.HasProperty(
                     "_Color"));
        }

        private static bool TryGetSourceColor(
            Material material,
            out Color color)
        {
            color =
                Color.white;

            if (material == null)
                return false;

            if (material.HasProperty(
                    "_BaseColor"))
            {
                color =
                    material.GetColor(
                        "_BaseColor");

                return true;
            }

            if (material.HasProperty(
                    "_Color"))
            {
                color =
                    material.GetColor(
                        "_Color");

                return true;
            }

            return false;
        }

        private static void ApplySourceSmoothnessFallback(
            Material target,
            Material source)
        {
            if (target == null ||
                source == null ||
                !target.HasProperty(
                    "_Roughness"))
            {
                return;
            }

            float smoothness;

            if (source.HasProperty(
                    "_Smoothness"))
            {
                smoothness =
                    source.GetFloat(
                        "_Smoothness");
            }
            else if (source.HasProperty(
                         "_Glossiness"))
            {
                smoothness =
                    source.GetFloat(
                        "_Glossiness");
            }
            else
            {
                return;
            }

            target.SetFloat(
                "_Roughness",
                1f -
                Mathf.Clamp01(
                    smoothness));
        }

        private static float GetSourceCutoff(
            Material source,
            float fallback)
        {
            if (source == null)
                return fallback;

            string[] names =
            {
                "_Cutoff",
                "_AlphaClipThreshold",
                "_AlphaCutoff"
            };

            for (int i = 0;
                 i < names.Length;
                 i++)
            {
                if (source.HasProperty(
                        names[i]))
                {
                    return
                        Mathf.Clamp01(
                            source.GetFloat(
                                names[i]));
                }
            }

            return fallback;
        }

        private static bool ContainsAny(
            string value,
            params string[] tokens)
        {
            if (string.IsNullOrEmpty(
                    value))
            {
                return false;
            }

            for (int i = 0;
                 i < tokens.Length;
                 i++)
            {
                if (value.IndexOf(
                        tokens[i],
                        StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static string Sanitize(
            string value)
        {
            if (string.IsNullOrWhiteSpace(
                    value))
            {
                return "Material";
            }

            char[] invalid =
                Path.GetInvalidFileNameChars();

            for (int i = 0;
                 i < invalid.Length;
                 i++)
            {
                value =
                    value.Replace(
                        invalid[i],
                        '_');
            }

            value =
                value.Replace(
                    '/',
                    '_');

            return value;
        }
    }
}
