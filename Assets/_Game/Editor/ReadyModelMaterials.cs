using System;
using LittleCastle.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace LittleCastle.Editor
{
    /// <summary>Maps the Ready texture naming contract to shared shader families.</summary>
    internal static class ReadyModelMaterials
    {
        private const string SharedRoot = "Assets/_Game/Materials/Shared/";
        private const string TextureRoot = "Assets/_Game/Textures/Ready/";

        public static void ConfigureRenderer(
            Renderer renderer, Transform lodRoot, string category,
            string archetypeId, string variant, bool farthest)
        {
            string family = GetFamily(renderer.transform, lodRoot, category);
            string templateName = farthest ? "LC_DistantSimple_Default.mat" :
                family == "Leaves" ? "LC_Foliage_Default.mat" :
                family == "Grass" ? "LC_Grass_Default.mat" :
                "LC_StylizedLit_Default.mat";
            Material template = AssetDatabase.LoadAssetAtPath<Material>(
                SharedRoot + templateName);
            if (template == null)
            {
                Debug.LogError("Missing shared material: " + templateName);
                return;
            }

            Material material = ResolveMaterial(template, category,
                archetypeId, variant, family, farthest);
            Material[] slots = renderer.sharedMaterials;
            if (slots.Length == 0)
                slots = new Material[1];
            for (int i = 0; i < slots.Length; i++)
                slots[i] = material;
            renderer.sharedMaterials = slots;

            if (farthest)
            {
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.lightProbeUsage = LightProbeUsage.Off;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            }
        }

        private static string GetFamily(
            Transform renderer, Transform lodRoot, string category)
        {
            if (category.IndexOf("Grass", StringComparison.OrdinalIgnoreCase) >= 0 ||
                category.IndexOf("Wheat", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Grass";
            for (Transform node = renderer; node != null; node = node.parent)
            {
                if (node.name.IndexOf("Leaf", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    node.name.IndexOf("Foliage", StringComparison.OrdinalIgnoreCase) >= 0)
                    return "Leaves";
                if (node == lodRoot)
                    break;
            }
            return "Solid";
        }

        private static Material ResolveMaterial(
            Material template, string category, string archetypeId,
            string variant, string family, bool farthest)
        {
            string textureFolder = TextureRoot + category + "/" +
                archetypeId + "/" + variant + "/";
            string prefix = variant + "_" + family + "_";
            Texture2D baseColor = FindTexture(textureFolder, prefix + "BaseColor");
            if (baseColor == null && family == "Solid")
                baseColor = FindTexture(textureFolder,
                    variant + "_BaseColor");
            Texture2D normal = farthest ? null :
                FindTexture(textureFolder, prefix + "Normal");
            Texture2D ao = farthest ? null :
                FindTexture(textureFolder, prefix + "AO");
            Texture2D roughness = farthest ? null :
                FindTexture(textureFolder, prefix + "Roughness");
            Texture2D opacity = farthest ? null :
                FindTexture(textureFolder, prefix + "Opacity");

            if (baseColor == null && normal == null && ao == null &&
                roughness == null && opacity == null)
                return template;

            string suffix = farthest ? "Far" : "Near";
            string materialPath = ReadyModelCatalogSync.ReadyRoot + "/" +
                category + "/" + archetypeId + "/" + variant + "_" +
                family + "_" + suffix + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                material = new Material(template);
                AssetDatabase.CreateAsset(material, materialPath);
            }
            else
            {
                material.CopyPropertiesFromMaterial(template);
                material.shader = template.shader;
            }

            SetTexture(material, "_MainTex", baseColor);
            SetTexture(material, "_BumpMap", normal);
            SetTexture(material, "_OcclusionMap", ao);
            SetTexture(material, "_RoughnessMap", roughness);
            SetTexture(material, "_OpacityMap", opacity);
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Texture2D FindTexture(string folder, string stem)
        {
            string[] extensions = { ".png", ".tga", ".jpg", ".jpeg", ".exr" };
            foreach (string extension in extensions)
            {
                Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(
                    folder + stem + extension);
                if (texture != null)
                    return texture;
            }
            return null;
        }

        private static void SetTexture(Material material, string property, Texture texture)
        {
            if (texture != null && material.HasProperty(property))
                material.SetTexture(property, texture);
        }
    }
}
