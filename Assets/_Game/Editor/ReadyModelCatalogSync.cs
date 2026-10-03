using System;
using System.Collections.Generic;
using LittleCastle.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace LittleCastle.Editor
{
    /// <summary>
    /// Creates local LOD prefabs and an ignored overlay catalog. The shared
    /// catalog stays usable in clean clones and is never rewritten here.
    /// </summary>
    [InitializeOnLoad]
    public static class ReadyModelCatalogSync
    {
        public const string ReadyRoot = "Assets/_Game/Models/Ready";
        private const string SharedCatalogPath =
            "Assets/_Game/Settings/World/MainWorldSpawnCatalog.asset";
        private const string LocalCatalogPath =
            ReadyRoot + "/LocalWorldSpawnCatalog.asset";
        public const string LocalTestOverrideCatalogPath =
            ReadyRoot + "/LocalTestWorldSpawnCatalog.asset";
        private static WorldSpawnCatalog localCatalog;
        private static WorldSpawnCatalog localTestOverrideCatalog;

        static ReadyModelCatalogSync()
        {
            WorldSpawnCatalog.EditorLocalResolver = TryResolveLocal;
        }

        [MenuItem("Little Castle/Assets/Sync Ready Models")]
        public static void Sync()
        {
            if (!AssetDatabase.IsValidFolder(ReadyRoot))
            {
                Debug.LogError("Ready model folder is missing: " + ReadyRoot);
                return;
            }

            var groups = new SortedDictionary<string, List<GameObject>>(
                StringComparer.Ordinal);
            string[] guids = AssetDatabase.FindAssets(
                "t:GameObject", new[] { ReadyRoot });
            var paths = new List<string>();
            foreach (string guid in guids)
                paths.Add(AssetDatabase.GUIDToAssetPath(guid));
            paths.Sort(StringComparer.Ordinal);
            int rejected = 0;

            foreach (string path in paths)
            {
                if (!path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase) ||
                    !TryReadIdentity(path, out string category,
                        out string archetypeId, out string variant))
                    continue;
                if (path.EndsWith("_LOD1.fbx", StringComparison.OrdinalIgnoreCase) ||
                    path.EndsWith("_LOD2.fbx", StringComparison.OrdinalIgnoreCase) ||
                    path.EndsWith("_LOD3.fbx", StringComparison.OrdinalIgnoreCase))
                    continue;

                GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (model == null ||
                    !TryBuildPrefab(model, path, category, archetypeId,
                        variant, out GameObject prefab))
                {
                    rejected++;
                    continue;
                }

                // Test releases are importable/inspectable locally but must
                // never replace procedural runtime visuals before approval.
                if (category.StartsWith("Test_", StringComparison.Ordinal))
                    continue;

                if (!groups.TryGetValue(archetypeId, out List<GameObject> prefabs))
                {
                    prefabs = new List<GameObject>();
                    groups.Add(archetypeId, prefabs);
                }
                prefabs.Add(prefab);
            }

            WorldSpawnCatalog catalog =
                AssetDatabase.LoadAssetAtPath<WorldSpawnCatalog>(LocalCatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<WorldSpawnCatalog>();
                AssetDatabase.CreateAsset(catalog, LocalCatalogPath);
            }

            var serialized = new SerializedObject(catalog);
            SerializedProperty entries = serialized.FindProperty("entries");
            entries.arraySize = groups.Count;
            int groupIndex = 0;
            foreach (KeyValuePair<string, List<GameObject>> group in groups)
            {
                SerializedProperty entry = entries.GetArrayElementAtIndex(groupIndex++);
                entry.FindPropertyRelative("archetypeId").stringValue = group.Key;
                entry.FindPropertyRelative("scaleMultiplier").floatValue = 1f;
                entry.FindPropertyRelative("rotationOffsetEuler").vector3Value =
                    Vector3.zero;
                SerializedProperty prefabs = entry.FindPropertyRelative("prefabs");
                prefabs.arraySize = group.Value.Count;
                for (int i = 0; i < group.Value.Count; i++)
                    prefabs.GetArrayElementAtIndex(i).objectReferenceValue = group.Value[i];
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            localCatalog = catalog;

            Debug.Log("[Little Castle Assets] Local catalog: " + groups.Count +
                      " archetypes; rejected models: " + rejected +
                      ". Shared catalog untouched. Run the production model " +
                      "validator on each prefab before approval.");
        }

        private static bool TryResolveLocal(
            WorldSpawnCatalog source, string archetypeId, long stableId,
            out WorldSpawnCatalogEntry entry, out GameObject prefab)
        {
            entry = null;
            prefab = null;
            if (AssetDatabase.GetAssetPath(source) != SharedCatalogPath)
                return false;
            if (localTestOverrideCatalog == null)
                localTestOverrideCatalog =
                    AssetDatabase.LoadAssetAtPath<WorldSpawnCatalog>(
                        LocalTestOverrideCatalogPath);

            if (localTestOverrideCatalog != null &&
                localTestOverrideCatalog.TryResolve(
                    archetypeId, stableId, out entry, out prefab))
            {
                return true;
            }

            if (localCatalog == null)
                localCatalog = AssetDatabase.LoadAssetAtPath<WorldSpawnCatalog>(
                    LocalCatalogPath);

            return localCatalog != null && localCatalog.TryResolve(
                archetypeId, stableId, out entry, out prefab);
        }

        private static bool TryReadIdentity(
            string path, out string category, out string archetypeId,
            out string variant)
        {
            category = archetypeId = variant = null;
            string prefix = ReadyRoot + "/";
            if (!path.StartsWith(prefix, StringComparison.Ordinal))
                return false;
            string[] parts = path.Substring(prefix.Length).Split('/');
            if (parts.Length != 3)
                return false;
            category = parts[0];
            archetypeId = parts[1];
            variant = System.IO.Path.GetFileNameWithoutExtension(parts[2]);
            if (variant.EndsWith("_LOD0", StringComparison.OrdinalIgnoreCase))
                variant = variant.Substring(0, variant.Length - 5);
            return !string.IsNullOrEmpty(category) &&
                   !string.IsNullOrEmpty(archetypeId) &&
                   !string.IsNullOrEmpty(variant);
        }

        private static bool TryBuildPrefab(
            GameObject model, string modelPath, string category, string archetypeId,
            string variant, out GameObject prefab)
        {
            prefab = null;
            string folder = ReadyRoot + "/" + category + "/" + archetypeId + "/";
            string lod1Path = folder + variant + "_LOD1.fbx";
            bool separateLods = AssetDatabase.LoadAssetAtPath<GameObject>(lod1Path) != null;
            GameObject instance = separateLods ? new GameObject(variant) :
                (GameObject)PrefabUtility.InstantiatePrefab(model);
            if (instance == null)
                return false;
            try
            {
                instance.name = variant;
                if (separateLods)
                {
                    for (int level = 0; level < 4; level++)
                    {
                        string lodPath = level == 0 ? modelPath :
                            folder + variant + "_LOD" + level + ".fbx";
                        GameObject lodModel = AssetDatabase.LoadAssetAtPath<GameObject>(lodPath);
                        if (lodModel == null)
                            break;
                        GameObject child = (GameObject)PrefabUtility.InstantiatePrefab(
                            lodModel, instance.transform);
                        child.name = "LOD" + level;
                        child.transform.localPosition = Vector3.zero;
                        child.transform.localRotation = Quaternion.identity;
                        child.transform.localScale = Vector3.one;
                    }
                }
                var nodes = new List<Transform>();
                var rendererSets = new List<Renderer[]>();
                for (int level = 0; level < 4; level++)
                {
                    if (!TryFindLodRenderers(
                            instance.transform,
                            level,
                            out Transform node,
                            out Renderer[] renderers))
                    {
                        break;
                    }

                    nodes.Add(node);
                    rendererSets.Add(renderers);
                }

                if (nodes.Count < 3)
                {
                    Debug.LogError("[Little Castle Assets] " + model.name +
                        " requires consecutive nonempty LOD0, LOD1 and LOD2 " +
                        "nodes (optional LOD3). Accepted embedded names include " +
                        "LOD0 or names ending in _LOD0.");
                    return false;
                }

                var assigned = new HashSet<Renderer>();
                int previousVertices = int.MaxValue;
                for (int level = 0; level < rendererSets.Count; level++)
                {
                    int vertices = 0;
                    foreach (Renderer renderer in rendererSets[level])
                    {
                        if (!assigned.Add(renderer))
                        {
                            Debug.LogError("[Little Castle Assets] Renderer in multiple " +
                                "LOD nodes: " + renderer.name);
                            return false;
                        }
                        MeshFilter filter = renderer.GetComponent<MeshFilter>();
                        if (filter != null && filter.sharedMesh != null)
                            vertices += filter.sharedMesh.vertexCount;
                        SkinnedMeshRenderer skinned = renderer as SkinnedMeshRenderer;
                        if (skinned != null && skinned.sharedMesh != null)
                            vertices += skinned.sharedMesh.vertexCount;
                    }
                    if (vertices == 0 || vertices >= previousVertices)
                    {
                        Debug.LogError("[Little Castle Assets] " + model.name +
                            " must have fewer vertices at each later LOD.");
                        return false;
                    }
                    previousVertices = vertices;
                }
                foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
                {
                    if (!assigned.Contains(renderer))
                    {
                        Debug.LogError("[Little Castle Assets] Renderer outside LOD " +
                            "nodes: " + renderer.name);
                        return false;
                    }
                }

                var lods = new LOD[nodes.Count];
                float[] heights = { 0.55f, 0.20f, 0.06f, 0.015f };
                for (int level = 0; level < nodes.Count; level++)
                {
                    bool farthest = level == nodes.Count - 1;
                    foreach (Renderer renderer in rendererSets[level])
                        ReadyModelMaterials.ConfigureRenderer(renderer, nodes[level],
                            category, archetypeId, variant, farthest);
                    lods[level] = new LOD(heights[level], rendererSets[level]);
                }

                LODGroup group = instance.GetComponent<LODGroup>();
                if (group == null)
                    group = instance.AddComponent<LODGroup>();
                group.SetLODs(lods);
                group.RecalculateBounds();
                string prefabPath = ReadyRoot + "/" + category + "/" +
                    archetypeId + "/" + variant + ".prefab";
                prefab = PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
                return prefab != null;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        private static bool TryFindLodRenderers(
            Transform root,
            int level,
            out Transform lodRoot,
            out Renderer[] renderers)
        {
            string exactName = "LOD" + level;

            Transform exact =
                FindNamedChild(
                    root,
                    exactName);

            if (exact != null)
            {
                renderers =
                    exact.GetComponentsInChildren<Renderer>(
                        true);

                if (renderers.Length > 0)
                {
                    lodRoot = exact;
                    return true;
                }
            }

            string suffix =
                "_LOD" + level;

            var collected =
                new List<Renderer>();

            var seen =
                new HashSet<Renderer>();

            Transform[] transforms =
                root.GetComponentsInChildren<Transform>(
                    true);

            for (int i = 0; i < transforms.Length; i++)
            {
                Transform candidate =
                    transforms[i];

                if (candidate == root ||
                    !candidate.name.EndsWith(
                        suffix,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                Renderer[] candidateRenderers =
                    candidate.GetComponentsInChildren<Renderer>(
                        true);

                for (int r = 0;
                     r < candidateRenderers.Length;
                     r++)
                {
                    Renderer renderer =
                        candidateRenderers[r];

                    if (renderer != null &&
                        seen.Add(renderer))
                    {
                        collected.Add(renderer);
                    }
                }
            }

            lodRoot = root;
            renderers = collected.ToArray();

            return renderers.Length > 0;
        }

        private static Transform FindNamedChild(Transform root, string name)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                if (child != root && child.name == name)
                    return child;
            return null;
        }
    }
}
