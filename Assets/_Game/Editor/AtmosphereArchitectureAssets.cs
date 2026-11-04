using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace LittleCastle.Editor
{
    /// <summary>Explicit local art import. Does not open scenes or modify maps/catalogs.</summary>
    public static class AtmosphereArchitectureAssets
    {
        public const string Root = "Assets/_Game/Art/Studies/AtmosphereArchitecture_20261030";
        public const string TownHallL1 = Root + "/Prefabs/TownHall_L1.prefab";
        public const string TownHallL2 = Root + "/Prefabs/TownHall_L2.prefab";

        [MenuItem("Little Castle/Rendering/Prepare Atmosphere Architecture Assets")]
        public static void Ensure()
        {
            if (!Directory.Exists(Root)) throw new DirectoryNotFoundException(Root);
            Directory.CreateDirectory(Root + "/Materials");
            Directory.CreateDirectory(Root + "/Prefabs");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var near = Shader.Find("Little Castle/Surface/LC Stylized Lit");
            var far = Shader.Find("Little Castle/Distance/LC Distant Simple");
            if (!near || !far) throw new InvalidOperationException("Existing LC shaders are required.");
            // Scope is deliberately limited to the two reviewed four-LOD release folders.
            foreach (string file in new[] { "TownHall_L1", "TownHall_L2" }.SelectMany(
                name => Directory.GetFiles(Root + "/" + name, "*", SearchOption.AllDirectories)))
            {
                string path = file.Replace('\\', '/');
                string extension = Path.GetExtension(path).ToLowerInvariant();
                if (extension == ".png" || extension == ".jpg")
                {
                    var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                    bool normal = path.IndexOf("_Normal.", StringComparison.OrdinalIgnoreCase) >= 0;
                    bool srgb = path.IndexOf("_BaseColor.", StringComparison.OrdinalIgnoreCase) >= 0;
                    var type = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                    if (importer.textureType == type && importer.sRGBTexture == srgb
                        && importer.mipmapEnabled && !importer.isReadable && importer.maxTextureSize == 4096) continue;
                    importer.textureType = type;
                    importer.sRGBTexture = srgb;
                    importer.mipmapEnabled = true;
                    importer.isReadable = false;
                    importer.maxTextureSize = 4096;
                    importer.SaveAndReimport();
                }
                else if (extension == ".fbx")
                {
                    var importer = (ModelImporter)AssetImporter.GetAtPath(path);
                    if (importer.materialImportMode == ModelImporterMaterialImportMode.None
                        && !importer.importAnimation && !importer.importCameras && !importer.importLights
                        && !importer.isReadable && importer.importNormals == ModelImporterNormals.Import
                        && importer.importTangents == ModelImporterTangents.CalculateMikk) continue;
                    importer.materialImportMode = ModelImporterMaterialImportMode.None;
                    importer.importAnimation = false;
                    importer.importCameras = false;
                    importer.importLights = false;
                    importer.isReadable = false;
                    importer.importNormals = ModelImporterNormals.Import;
                    importer.importTangents = ModelImporterTangents.CalculateMikk;
                    importer.SaveAndReimport();
                }
            }
            Build("TownHall_L1", 4, near, far);
            Build("TownHall_L2", 4, near, far);
            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] {Root}))
                AssetDatabase.SaveAssetIfDirty(AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid)));
            Verify("TownHall_L1");
            Verify("TownHall_L2");
            Debug.Log("Ready Mountain town halls prepared and verified: " + TownHallL1 + ", " + TownHallL2
                + ". Both retain four authored LODs and bound textures; no lights, scenes or catalogs changed.");
        }

        static void Build(string name, int count, Shader near, Shader far)
        {
            string output = Root + "/Prefabs/" + name + ".prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(output)) return;
            var root = new GameObject(name);
            try
            {
                var lods = new LOD[count];
                float[] heights = { .50f, .25f, .10f, .015f };
                Vector3 groundOffset = Vector3.zero;
                for (int lod = 0; lod < count; lod++)
                {
                    string path = Root + "/" + name + "/Meshes/" + name + "_LOD" + lod + ".fbx";
                    var source = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (!source) throw new FileNotFoundException("Architecture model missing", path);
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(source, root.transform);
                    instance.name = "LOD" + lod;
                    var renderers = instance.GetComponentsInChildren<MeshRenderer>(true);
                    if (renderers.Length == 0) throw new InvalidOperationException("No mesh renderers: " + path);
                    if (lod == 0)
                    {
                        Bounds bounds = renderers[0].bounds;
                        foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
                        groundOffset = new Vector3(-bounds.center.x, -bounds.min.y, -bounds.center.z);
                    }
                    instance.transform.localPosition += groundOffset;
                    bool distant = lod == count - 1;
                    foreach (var renderer in renderers)
                    {
                        var mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
                        var materials = new Material[mesh.subMeshCount];
                        for (int slot = 0; slot < materials.Length; slot++)
                            materials[slot] = Material(name, slot, distant, near, far);
                        renderer.sharedMaterials = materials;
                        renderer.lightProbeUsage = LightProbeUsage.Off;
                        renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                        renderer.shadowCastingMode = distant ? ShadowCastingMode.Off : ShadowCastingMode.On;
                        renderer.receiveShadows = !distant;
                        renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
                    }
                    foreach (var light in instance.GetComponentsInChildren<Light>(true)) Object.DestroyImmediate(light);
                    foreach (var collider in instance.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);
                    lods[lod] = new LOD(heights[lod], renderers);
                }
                var group = root.AddComponent<LODGroup>();
                group.fadeMode = LODFadeMode.None;
                group.SetLODs(lods);
                group.RecalculateBounds();
                PrefabUtility.SaveAsPrefabAsset(root, output);
            }
            finally { Object.DestroyImmediate(root); }
        }

        static Material Material(string name, int slot, bool distant, Shader near, Shader far)
        {
            string surface = name == "TownHall_L1" ? "TH1_Atlas_" + slot : "TH2";
            string path = Root + "/Materials/" + surface + (distant ? "_Distant" : "_Near") + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material) return material;
            string prefix = Root + "/" + name + "/Textures/" + surface;
            const string extension = ".png";
            var albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(prefix + "_BaseColor" + extension);
            if (!albedo) throw new FileNotFoundException("Architecture base color missing", prefix);
            material = new Material(distant ? far : near) { name = surface + (distant ? "_Distant" : "_Near"), enableInstancing = true };
            material.SetTexture("_MainTex", albedo);
            material.SetColor("_Color", Color.white);
            // Authored town halls contain open decorative shells; preserve two-sided appearance.
            material.SetFloat("_Cull", 0);
            material.SetFloat("_AmbientStrength", 1);
            if (!distant)
            {
                material.SetFloat("_LightResponse", 1);
                material.SetFloat("_OcclusionStrength", 0);
                material.SetFloat("_ShadowTintStrength", .25f);
                material.SetFloat("_LocalLightStrength", 0);
                material.SetFloat("_EmissionDayStrength", 0);
                material.SetFloat("_EmissionNightStrength", 0);
                material.SetFloat("_SpecularStrength", .045f);
                var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(prefix + "_Normal" + extension);
                if (normal) { material.SetTexture("_BumpMap", normal); material.SetFloat("_BumpScale", .45f); }
                else material.SetFloat("_BumpScale", 0);
                var roughness = AssetDatabase.LoadAssetAtPath<Texture2D>(prefix + "_Roughness" + extension);
                material.SetFloat("_Roughness", .78f);
                if (roughness) { material.SetTexture("_RoughnessMap", roughness); material.SetFloat("_RoughnessMapStrength", 1); }
            }
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        static void Verify(string name)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/" + name + ".prefab");
            var group = prefab ? prefab.GetComponent<LODGroup>() : null;
            if (!group || group.GetLODs().Length != 4)
                throw new InvalidOperationException(name + " must retain all four authored LODs.");
            if (prefab.GetComponentsInChildren<Light>(true).Length != 0)
                throw new InvalidOperationException(name + " unexpectedly contains realtime lights.");
            var lods = group.GetLODs();
            for (int lod = 0; lod < lods.Length; lod++)
            {
                if (lods[lod].renderers.Length == 0) throw new InvalidOperationException(name + " has an empty LOD" + lod);
                foreach (var renderer in lods[lod].renderers)
                {
                    var filter = renderer.GetComponent<MeshFilter>();
                    if (!filter || !filter.sharedMesh || renderer.sharedMaterials.Length != filter.sharedMesh.subMeshCount)
                        throw new InvalidOperationException(name + " has invalid material slots at LOD" + lod);
                    var materials = renderer.sharedMaterials;
                    for (int slot = 0; slot < materials.Length; slot++)
                    {
                        var material = materials[slot];
                        string surface = name == "TownHall_L1" ? "TH1_Atlas_" + slot : "TH2";
                        string prefix = Root + "/" + name + "/Textures/" + surface;
                        string expectedShader = lod == 3 ? "Little Castle/Distance/LC Distant Simple" : "Little Castle/Surface/LC Stylized Lit";
                        if (!material || !material.shader || material.shader.name != expectedShader)
                            throw new InvalidOperationException(name + " has an unexpected shader at LOD" + lod);
                        var expectedAlbedo = AssetDatabase.LoadAssetAtPath<Texture2D>(prefix + "_BaseColor.png");
                        if (!expectedAlbedo || material.GetTexture("_MainTex") != expectedAlbedo)
                            throw new InvalidOperationException(name + " base color binding is missing/wrong: " + surface);
                        var colorImporter = (TextureImporter)AssetImporter.GetAtPath(prefix + "_BaseColor.png");
                        if (!colorImporter.sRGBTexture) throw new InvalidOperationException(surface + " base color must be sRGB.");
                        if (lod != 3 && name == "TownHall_L2")
                        {
                            var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(prefix + "_Normal.png");
                            var roughness = AssetDatabase.LoadAssetAtPath<Texture2D>(prefix + "_Roughness.png");
                            if (!normal || !roughness || material.GetTexture("_BumpMap") != normal
                                || material.GetTexture("_RoughnessMap") != roughness || material.GetFloat("_RoughnessMapStrength") != 1)
                                throw new InvalidOperationException("TownHall L2 normal/roughness bindings are missing/wrong.");
                            var normalImporter = (TextureImporter)AssetImporter.GetAtPath(prefix + "_Normal.png");
                            var roughnessImporter = (TextureImporter)AssetImporter.GetAtPath(prefix + "_Roughness.png");
                            if (normalImporter.textureType != TextureImporterType.NormalMap || normalImporter.sRGBTexture || roughnessImporter.sRGBTexture)
                                throw new InvalidOperationException("TownHall L2 normal/roughness maps must use linear data import.");
                        }
                    }
                    if (lod == 3 && (renderer.shadowCastingMode != ShadowCastingMode.Off || renderer.receiveShadows))
                        throw new InvalidOperationException(name + " distant LOD must not cast/receive realtime shadows.");
                }
            }
        }
    }
}
