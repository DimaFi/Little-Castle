using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using LittleCastle.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace LittleCastle.Editor
{
    /// <summary>
    /// Strict opt-in, immutable Bridge Stone A v002 importer.
    /// Operates only on hydrated, byte-verified art release supplied by user.
    /// Never substitutes primitive geometry, rewrites Main assets, or modifies scenes.
    /// </summary>
    public static class BridgeStoneAssetIntegration
    {
        public const string ImportRoot =
            "Assets/_Game/Art/Imported/Bridge_Stone_A/v002";
        public const string PrefabPath =
            "Assets/_Game/Models/Concept/Bridge_Stone_A/Bridge_Stone_A_v002.prefab";
        public const string CatalogPath =
            "Assets/_Game/Settings/World/ConceptWorld_v001/Concept_MainWorldSpawnCatalog.asset";

        private static readonly string[] ModelFiles =
        {
            "Meshes/SM_Bridge_Stone_A_LOD0.fbx",
            "Meshes/SM_Bridge_Stone_A_LOD1.fbx",
            "Meshes/SM_Bridge_Stone_A_LOD2.fbx",
            "Meshes/SM_Bridge_Dressing_A_LOD0.fbx",
            "Meshes/SM_Bridge_Dressing_A_LOD1.fbx",
            "Meshes/SM_Bridge_Dressing_A_LOD2.fbx",
            "Meshes/COL_Bridge_A.fbx"
        };

        private static readonly int[] StructuralTriangles = { 13598, 7704, 1736 };
        private static readonly int[] DressingTriangles = { 12428, 5022, 80 };

        [Serializable]
        private sealed class ReleaseHeader
        {
            public string asset_id;
            public string version;
            public bool unity_tested;
        }

        [MenuItem("Little Castle/Assets/Bridge v002/Import Verified Release to Concept")]
        public static void ImportFromEnvironment()
        {
            string source = Environment.GetEnvironmentVariable(
                "LITTLE_CASTLE_BRIDGE_RELEASE");
            if (string.IsNullOrWhiteSpace(source))
                throw new InvalidOperationException(
                    "Set LITTLE_CASTLE_BRIDGE_RELEASE to the fully hydrated " +
                    "Little-Castle_Assets/Releases/Bridge_Stone_A/v002 directory. " +
                    "First run Python Tools/verify_portable_releases.py and git lfs pull.");
            ImportVerifiedRelease(source);
        }

        public static void ImportVerifiedRelease(string sourceFolder)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null ||
                Directory.Exists(ImportRoot))
                throw new InvalidOperationException(
                    "Refusing to overwrite existing v002 imported content. " +
                    "Use a reviewed new version or perform explicit cleanup.");

            string releaseDirectory = Path.GetFullPath(sourceFolder);
            string manifestPath = Path.Combine(releaseDirectory, "release.json");
            if (!File.Exists(manifestPath))
                throw new FileNotFoundException(
                    "Bridge v002 release.json was not found", manifestPath);

            string manifest = File.ReadAllText(manifestPath);
            var header = JsonUtility.FromJson<ReleaseHeader>(manifest);
            if (header == null ||
                header.asset_id != FixedBridgeSiteProfile.AssetId ||
                header.version != FixedBridgeSiteProfile.ContractVersion)
                throw new InvalidOperationException("Unexpected bridge asset/version.");

            Dictionary<string, string> entries = ParseFileHashes(manifest);
            foreach (KeyValuePair<string, string> entry in entries)
            {
                string relative = entry.Key.Replace('/', Path.DirectorySeparatorChar);
                string absolute = Path.GetFullPath(Path.Combine(releaseDirectory, relative));
                if (!absolute.StartsWith(releaseDirectory + Path.DirectorySeparatorChar,
                        StringComparison.OrdinalIgnoreCase) ||
                    !File.Exists(absolute))
                    throw new InvalidOperationException("Missing/escaping release file: " + entry.Key);

                using (FileStream stream = File.OpenRead(absolute))
                {
                    byte[] headerBytes = new byte[Math.Min(96, (int)Math.Min(stream.Length, 96L))];
                    stream.Read(headerBytes, 0, headerBytes.Length);
                    string headText = System.Text.Encoding.ASCII.GetString(headerBytes);
                    if (headText.StartsWith("version https://git-lfs.github.com/spec/v1",
                            StringComparison.Ordinal))
                        throw new InvalidOperationException(
                            "Git LFS pointer, not hydrated payload: " + entry.Key);
                    stream.Position = 0;
                    using (var sha = SHA256.Create())
                    {
                        string actual = BitConverter.ToString(sha.ComputeHash(stream))
                            .Replace("-", "").ToLowerInvariant();
                        if (actual != entry.Value)
                            throw new InvalidOperationException(
                                "Release SHA-256 mismatch: " + entry.Key);
                    }
                }
            }

            foreach (string path in ModelFiles)
                if (!entries.ContainsKey(path))
                    throw new InvalidOperationException("Required FBX absent: " + path);

            foreach (string texture in new[]
            {
                "Textures/T_Bridge_StoneAtlas_BaseColor.png",
                "Textures/T_Bridge_Palette_BaseColor.png",
                "Textures/T_Bridge_Grass_BaseColor.png"
            })
                if (!entries.ContainsKey(texture))
                    throw new InvalidOperationException("Required texture absent: " + texture);

            // All source data is verified before creating Unity assets.
            foreach (KeyValuePair<string, string> entry in entries)
            {
                string target = ImportRoot + "/" + entry.Key;
                string absoluteSource = Path.Combine(releaseDirectory,
                    entry.Key.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                File.Copy(absoluteSource, target, false);
            }
            File.Copy(manifestPath, ImportRoot + "/release.json", false);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            GameObject root = new GameObject("Bridge_Stone_A_v002");
            try
            {
                var materials = CreateMaterials();
                LOD[] structures = new LOD[3];
                LOD[] dressing = new LOD[3];
                float[] thresholds = { 0.32f, 0.10f, 0.025f };
                for (int i = 0; i < 3; i++)
                {
                    string suffix = "LOD" + i;
                    structures[i] = new LOD(thresholds[i],
                        AddModel(root, ImportRoot + "/Meshes/SM_Bridge_Stone_A_" +
                            suffix + ".fbx", "Structural_" + suffix,
                            materials, StructuralTriangles[i], i == 2));
                    dressing[i] = new LOD(thresholds[i],
                        AddModel(root, ImportRoot + "/Meshes/SM_Bridge_Dressing_A_" +
                            suffix + ".fbx", "Dressing_" + suffix,
                            materials, DressingTriangles[i], i == 2));
                }

                var structureGroup = root.AddComponent<LODGroup>();
                structureGroup.SetLODs(structures);
                structureGroup.RecalculateBounds();

                var dressingRoot = new GameObject("Dressing_LODGroup");
                dressingRoot.transform.SetParent(root.transform, false);
                // Move dressing meshes under own LOD group, preserving local world transforms.
                for (int i = 0; i < dressing.Length; i++)
                    foreach (Renderer renderer in dressing[i].renderers)
                        renderer.transform.root.GetComponent<Transform>(); // Parent below by named group.
                for (int i = 0; i < 3; i++)
                {
                    Transform child = root.transform.Find("Dressing_LOD" + i);
                    if (child == null)
                        throw new InvalidOperationException("Missing dressing hierarchy.");
                    child.SetParent(dressingRoot.transform, false);
                }
                var dressingGroup = dressingRoot.AddComponent<LODGroup>();
                dressingGroup.SetLODs(dressing);
                dressingGroup.RecalculateBounds();

                AddCollision(root);
                root.transform.localScale = Vector3.one;
                root.transform.localRotation = Quaternion.identity;
                root.transform.localPosition = Vector3.zero;
                Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                if (prefab == null)
                    throw new InvalidOperationException("Failed to save authored v002 bridge prefab.");

                RegisterConceptCatalog(prefab);
                AssetDatabase.SaveAssets();
                Debug.Log("[Bridge v002] Imported reviewed real FBX LODs and concept-only " +
                    "catalog entry. NOT a Unity scene/visual/performance acceptance. " +
                    "Check collision near/far budget and screenshots on PC.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static Dictionary<string, string> ParseFileHashes(string manifest)
        {
            Match block = Regex.Match(manifest,
                "\"files_sha256\"\\s*:\\s*\\{(?<files>[^}]+)\\}",
                RegexOptions.Singleline);
            if (!block.Success)
                throw new InvalidOperationException("Missing files_sha256 manifest.");
            var hashes = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (Match item in Regex.Matches(block.Groups["files"].Value,
                "\"(?<name>[^\"\\\\]+)\"\\s*:\\s*\"(?<hash>[a-f0-9]{64})\""))
            {
                string relative = item.Groups["name"].Value;
                if (relative.StartsWith("/") || relative.Contains("..") ||
                    relative.Contains(":") || relative.Contains("\\"))
                    throw new InvalidOperationException("Unsafe release path " + relative);
                if (!hashes.TryAdd(relative, item.Groups["hash"].Value))
                    throw new InvalidOperationException("Duplicate manifest entry " + relative);
            }
            if (hashes.Count != 31)
                throw new InvalidOperationException(
                    "Bridge v002 must have exactly 31 reviewed files, got " + hashes.Count);
            return hashes;
        }

        private static Dictionary<string, Material> CreateMaterials()
        {
            string materialRoot =
                "Assets/_Game/Models/Concept/Bridge_Stone_A/Materials";
            Directory.CreateDirectory(materialRoot);
            var settings = new[]
            {
                new[] { "M_Bridge_Stone", "T_Bridge_StoneAtlas_BaseColor.png" },
                new[] { "M_Bridge_Palette", "T_Bridge_Palette_BaseColor.png" },
                new[] { "M_Bridge_Grass", "T_Bridge_Grass_BaseColor.png" },
                new[] { "M_Bridge_FactionCloth", "" }
            };
            Shader shader = Shader.Find("Little Castle/Surface/LC Stylized Lit");
            if (shader == null)
                throw new InvalidOperationException(
                    "Required Built-in LC Stylized Lit shader missing.");

            var result = new Dictionary<string, Material>();
            foreach (string[] item in settings)
            {
                var material = new Material(shader) { name = item[0] };
                if (!string.IsNullOrEmpty(item[1]))
                {
                    Texture texture = AssetDatabase.LoadAssetAtPath<Texture>(
                        ImportRoot + "/Textures/" + item[1]);
                    if (texture == null)
                        throw new InvalidOperationException("Texture import failed: " + item[1]);
                    material.SetTexture("_MainTex", texture);
                }
                material.SetFloat("_Roughness", item[0] == "M_Bridge_FactionCloth" ? 0.96f : 0.86f);
                string path = materialRoot + "/" + item[0] + ".mat";
                AssetDatabase.CreateAsset(material, path);
                result.Add(item[0], material);
            }
            return result;
        }

        private static Renderer[] AddModel(GameObject root, string assetPath,
            string label, Dictionary<string, Material> materials,
            int expectedTriangles, bool distant)
        {
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (model == null)
                throw new InvalidOperationException("Real FBX import missing: " + assetPath);
            GameObject instance = UnityEngine.Object.Instantiate(model, root.transform);
            instance.name = label;
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;

            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
            int triangles = 0;
            foreach (Renderer renderer in renderers)
            {
                var filter = renderer.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null)
                    throw new InvalidOperationException("FBX renderer has no real mesh: " + renderer.name);
                Mesh mesh = filter.sharedMesh;
                for (int sub = 0; sub < mesh.subMeshCount; sub++)
                    triangles += (int)(mesh.GetIndexCount(sub) / 3);
                string name = renderer.gameObject.name;
                string materialId =
                    name.Contains("FactionCloth") ? "M_Bridge_FactionCloth" :
                    name.Contains("Grass") ? "M_Bridge_Grass" :
                    name.Contains("Stone") || name.Contains("Rocks") ? "M_Bridge_Stone" :
                    "M_Bridge_Palette";
                var assigned = new Material[mesh.subMeshCount];
                for (int i = 0; i < assigned.Length; i++)
                    assigned[i] = materials[materialId];
                renderer.sharedMaterials = assigned;

                if (distant)
                {
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                    renderer.lightProbeUsage = LightProbeUsage.Off;
                    renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                    renderer.motionVectorGenerationMode =
                        MotionVectorGenerationMode.ForceNoMotion;
                }
            }
            if (triangles != expectedTriangles || renderers.Length == 0)
                throw new InvalidOperationException(
                    label + " FBX triangle count mismatch: " + triangles +
                    " expected " + expectedTriangles + ". No placeholder fallback.");
            return renderers;
        }

        private static void AddCollision(GameObject root)
        {
            GameObject original = AssetDatabase.LoadAssetAtPath<GameObject>(
                ImportRoot + "/Meshes/COL_Bridge_A.fbx");
            if (original == null)
                throw new InvalidOperationException("Missing reviewed COLLISION FBX.");
            GameObject instance = UnityEngine.Object.Instantiate(original, root.transform);
            instance.name = "NearCollision";
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;
            int triangleCount = 0;
            foreach (MeshFilter filter in instance.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null)
                    throw new InvalidOperationException("Collider missing mesh.");
                triangleCount += (int)(filter.sharedMesh.triangles.Length / 3);
                var collider = filter.gameObject.AddComponent<MeshCollider>();
                collider.sharedMesh = filter.sharedMesh;
                collider.convex = false;
                // This importer does not know runtime camera distance: keep
                // collider disabled pending explicit near-budget presentation.
                collider.enabled = false;
            }
            foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
                renderer.enabled = false;
            if (triangleCount != 286)
                throw new InvalidOperationException(
                    "Expected authored 286-triangle collision, got " + triangleCount);
        }

        private static void RegisterConceptCatalog(GameObject prefab)
        {
            WorldSpawnCatalog catalog =
                AssetDatabase.LoadAssetAtPath<WorldSpawnCatalog>(CatalogPath);
            if (catalog == null)
                throw new InvalidOperationException(
                    "Isolated concept catalog missing: " + CatalogPath);

            SerializedObject serialized = new SerializedObject(catalog);
            SerializedProperty entries = serialized.FindProperty("entries");
            for (int i = 0; i < entries.arraySize; i++)
            {
                if (entries.GetArrayElementAtIndex(i)
                    .FindPropertyRelative("archetypeId").stringValue ==
                    FixedBridgeSiteProfile.AssetId)
                    throw new InvalidOperationException(
                        "Fixed bridge already registered; no implicit replacement.");
            }

            int index = entries.arraySize;
            entries.InsertArrayElementAtIndex(index);
            SerializedProperty newEntry = entries.GetArrayElementAtIndex(index);
            newEntry.FindPropertyRelative("archetypeId").stringValue =
                FixedBridgeSiteProfile.AssetId;
            var prefabs = newEntry.FindPropertyRelative("prefabs");
            prefabs.arraySize = 1;
            prefabs.GetArrayElementAtIndex(0).objectReferenceValue = prefab;
            newEntry.FindPropertyRelative("scaleMultiplier").floatValue = 1f;
            newEntry.FindPropertyRelative("rotationOffsetEuler").vector3Value =
                Vector3.zero;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
        }
    }
}
