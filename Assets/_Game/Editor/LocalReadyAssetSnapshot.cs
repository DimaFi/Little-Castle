using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace LittleCastle.Editor
{
    public static class LocalReadyAssetSnapshot
    {
        private const string ReportPath =
            "docs/reports/local-ready-asset-snapshot.md";

        [MenuItem(
            "Little Castle/Assets/Export Local Ready Asset Snapshot",
            priority = 300)]
        public static void Export()
        {
            ReadyModelCatalogSync.Sync();

            string[] guids =
                AssetDatabase.FindAssets(
                    "t:Prefab",
                    new[] { ReadyModelCatalogSync.ReadyRoot });

            List<string> paths =
                guids.Select(AssetDatabase.GUIDToAssetPath)
                    .Where(
                        p =>
                            !string.IsNullOrEmpty(p) &&
                            p.IndexOf(
                                "/Test_",
                                StringComparison.OrdinalIgnoreCase) >= 0)
                    .OrderBy(
                        p => p,
                        StringComparer.OrdinalIgnoreCase)
                    .ToList();

            var sb =
                new StringBuilder();

            sb.AppendLine("# Local Ready Asset Snapshot");
            sb.AppendLine();
            sb.AppendLine(
                "> Text-only report generated from ignored local Unity assets.");
            sb.AppendLine();
            sb.AppendLine("- Unity: " + Application.unityVersion);
            sb.AppendLine("- Prefabs: " + paths.Count);
            sb.AppendLine();

            foreach (string path in paths)
            {
                GameObject prefab =
                    AssetDatabase.LoadAssetAtPath<GameObject>(
                        path);

                if (prefab == null)
                    continue;

                AppendPrefab(
                    sb,
                    prefab,
                    path);
            }

            string folder =
                Path.GetDirectoryName(
                    ReportPath);

            if (!string.IsNullOrEmpty(folder))
                Directory.CreateDirectory(folder);

            File.WriteAllText(
                ReportPath,
                sb.ToString(),
                new UTF8Encoding(false));

            AssetDatabase.Refresh();

            Selection.activeObject =
                AssetDatabase.LoadAssetAtPath<TextAsset>(
                    ReportPath);

            Debug.Log(
                "[Little Castle Assets] Local asset snapshot exported to " +
                ReportPath +
                ". Commit/push only this text file for remote review.");
        }

        private static void AppendPrefab(
            StringBuilder sb,
            GameObject prefab,
            string path)
        {
            sb.AppendLine("## " + prefab.name);
            sb.AppendLine();
            sb.AppendLine("- Prefab: " + path);

            string[] dependencies =
                AssetDatabase.GetDependencies(
                    path,
                    true);

            string sourceFbx =
                dependencies.FirstOrDefault(
                    d =>
                        d.EndsWith(
                            ".fbx",
                            StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrEmpty(sourceFbx))
            {
                sb.AppendLine("- Source FBX: " + sourceFbx);

                ModelImporter importer =
                    AssetImporter.GetAtPath(
                        sourceFbx)
                    as ModelImporter;

                if (importer != null)
                {
                    sb.AppendLine(
                        "- Import: scale=" +
                        importer.globalScale.ToString("0.###") +
                        ", readable=" +
                        importer.isReadable +
                        ", animations=" +
                        importer.importAnimation);
                }
            }

            LODGroup group =
                prefab.GetComponent<LODGroup>();

            if (group == null)
            {
                sb.AppendLine("- LODGroup: NONE");
            }
            else
            {
                LOD[] lods =
                    group.GetLODs();

                sb.AppendLine(
                    "- LOD levels: " +
                    lods.Length +
                    ", group size=" +
                    group.size.ToString("0.###"));

                for (int level = 0;
                     level < lods.Length;
                     level++)
                {
                    long vertices = 0;
                    long triangles = 0;

                    Renderer[] renderers =
                        lods[level].renderers ??
                        Array.Empty<Renderer>();

                    foreach (Renderer renderer in renderers)
                    {
                        Mesh mesh =
                            ResolveMesh(
                                renderer);

                        if (mesh == null)
                            continue;

                        vertices +=
                            mesh.vertexCount;

                        for (int sub = 0;
                             sub < mesh.subMeshCount;
                             sub++)
                        {
                            triangles +=
                                (long)mesh.GetIndexCount(sub) /
                                3L;
                        }
                    }

                    sb.AppendLine(
                        "  - LOD" +
                        level +
                        ": transition=" +
                        lods[level]
                            .screenRelativeTransitionHeight
                            .ToString("0.###") +
                        ", renderers=" +
                        renderers.Length +
                        ", vertices=" +
                        vertices +
                        ", triangles=" +
                        triangles);
                }
            }

            Renderer[] all =
                prefab.GetComponentsInChildren<Renderer>(
                    true);

            sb.AppendLine(
                "- Renderers: " +
                all.Length);

            foreach (Renderer renderer in all)
            {
                if (renderer == null)
                    continue;

                Mesh mesh =
                    ResolveMesh(
                        renderer);

                sb.AppendLine(
                    "  - " +
                    HierarchyPath(
                        renderer.transform,
                        prefab.transform) +
                    " | enabled=" +
                    renderer.enabled +
                    " | mesh=" +
                    (mesh != null
                        ? mesh.name
                        : "NONE"));

                Material[] materials =
                    renderer.sharedMaterials ??
                    Array.Empty<Material>();

                for (int slot = 0;
                     slot < materials.Length;
                     slot++)
                {
                    AppendMaterial(
                        sb,
                        materials[slot],
                        slot);
                }
            }

            List<string> textures =
                dependencies
                    .Where(IsTexturePath)
                    .OrderBy(
                        p => p,
                        StringComparer.OrdinalIgnoreCase)
                    .ToList();

            sb.AppendLine(
                "- Texture dependencies: " +
                textures.Count);

            foreach (string texture in textures)
                sb.AppendLine("  - " + texture);

            sb.AppendLine();
        }

        private static void AppendMaterial(
            StringBuilder sb,
            Material material,
            int slot)
        {
            if (material == null)
            {
                sb.AppendLine(
                    "    - slot " +
                    slot +
                    ": NULL");

                return;
            }

            sb.AppendLine(
                "    - slot " +
                slot +
                ": " +
                material.name +
                " | shader=" +
                (material.shader != null
                    ? material.shader.name
                    : "NONE") +
                " | instancing=" +
                material.enableInstancing);

            string[] properties =
            {
                "_MainTex",
                "_BaseMap",
                "_BumpMap",
                "_NormalMap",
                "_OcclusionMap",
                "_RoughnessMap",
                "_OpacityMap"
            };

            foreach (string property in properties)
            {
                if (!material.HasProperty(property))
                    continue;

                Texture texture =
                    material.GetTexture(property);

                if (texture == null)
                    continue;

                sb.AppendLine(
                    "      - " +
                    property +
                    ": " +
                    texture.name +
                    " -> " +
                    AssetDatabase.GetAssetPath(
                        texture));
            }

            if (material.HasProperty("_Color"))
            {
                sb.AppendLine(
                    "      - Color: " +
                    material.GetColor("_Color"));
            }

            if (material.HasProperty("_Cutoff"))
            {
                sb.AppendLine(
                    "      - Cutoff: " +
                    material.GetFloat("_Cutoff")
                        .ToString("0.###"));
            }
        }

        private static bool IsTexturePath(
            string path)
        {
            string extension =
                Path.GetExtension(path)
                    .ToLowerInvariant();

            return
                extension == ".png" ||
                extension == ".tga" ||
                extension == ".jpg" ||
                extension == ".jpeg" ||
                extension == ".exr" ||
                extension == ".psd";
        }

        private static Mesh ResolveMesh(
            Renderer renderer)
        {
            if (renderer is SkinnedMeshRenderer skinned)
                return skinned.sharedMesh;

            MeshFilter filter =
                renderer.GetComponent<MeshFilter>();

            return
                filter != null
                    ? filter.sharedMesh
                    : null;
        }

        private static string HierarchyPath(
            Transform current,
            Transform root)
        {
            var names =
                new List<string>();

            Transform node = current;

            while (node != null)
            {
                names.Add(node.name);

                if (node == root)
                    break;

                node = node.parent;
            }

            names.Reverse();

            return string.Join("/", names);
        }
    }
}
