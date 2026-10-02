using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace LittleCastle.Editor
{
    public static class FoliageModelCompatibilityValidator
    {
        private const string FoliageShaderName =
            "Little Castle/Foliage/LC Foliage";

        private const string GrassShaderName =
            "Little Castle/Foliage/LC Grass";

        private const string SolidShaderName =
            "Little Castle/Surface/LC Stylized Lit";

        [MenuItem(
            "Little Castle/Rendering/Validate Selected Foliage Model")]
        public static void ValidateSelected()
        {
            GameObject root =
                ResolveSelectedGameObject();

            if (root == null)
            {
                Debug.LogError(
                    "[Little Castle Foliage] Select a tree, bush, grass, " +
                    "wheat GameObject or prefab asset first.");

                return;
            }

            var renderers =
                root.GetComponentsInChildren<
                    Renderer>(true);

            if (renderers.Length == 0)
            {
                Debug.LogError(
                    "[Little Castle Foliage] Selected object has no " +
                    "renderers: " +
                    root.name);

                return;
            }

            int errors = 0;
            int warnings = 0;
            int foliageRenderers = 0;
            int grassRenderers = 0;
            int solidRenderers = 0;

            Debug.Log(
                "[Little Castle Foliage] Compatibility review started: " +
                root.name);

            foreach (Renderer renderer in renderers)
            {
                Mesh mesh =
                    ResolveMesh(renderer);

                if (mesh == null)
                {
                    warnings++;

                    Debug.LogWarning(
                        "[Little Castle Foliage] Renderer has no readable " +
                        "mesh reference: " +
                        GetHierarchyPath(
                            renderer.transform,
                            root.transform));

                    continue;
                }

                Material[] materials =
                    renderer.sharedMaterials;

                bool usesFoliage = false;
                bool usesGrass = false;
                bool usesSolid = false;
                bool requiresVertexMask = false;
                bool requiresHeightMask = false;

                foreach (Material material in materials)
                {
                    if (material == null ||
                        material.shader == null)
                    {
                        continue;
                    }

                    string shaderName =
                        material.shader.name;

                    usesFoliage |=
                        shaderName ==
                        FoliageShaderName;

                    usesGrass |=
                        shaderName ==
                        GrassShaderName;

                    usesSolid |=
                        shaderName ==
                        SolidShaderName;

                    if (shaderName ==
                            FoliageShaderName &&
                        material.HasProperty(
                            "_UseVertexWindMask") &&
                        material.GetFloat(
                            "_UseVertexWindMask") >
                        0.5f)
                    {
                        requiresVertexMask =
                            true;
                    }

                    if (shaderName ==
                            FoliageShaderName &&
                        material.HasProperty(
                            "_UseHeightWindMask") &&
                        material.GetFloat(
                            "_UseHeightWindMask") >
                        0.5f)
                    {
                        requiresHeightMask =
                            true;
                    }
                }

                if (!usesFoliage &&
                    !usesGrass &&
                    !usesSolid)
                {
                    continue;
                }

                if (usesFoliage)
                    foliageRenderers++;

                if (usesGrass)
                    grassRenderers++;

                if (usesSolid)
                    solidRenderers++;

                string path =
                    GetHierarchyPath(
                        renderer.transform,
                        root.transform);

                int vertexCount =
                    mesh.vertexCount;

                int triangleCount =
                    GetTriangleCount(mesh);

                bool normalsValid =
                    mesh.HasVertexAttribute(
                        UnityEngine.Rendering.VertexAttribute.Normal);

                bool colorsValid =
                    mesh.HasVertexAttribute(
                        UnityEngine.Rendering.VertexAttribute.Color);

                bool uvValid =
                    mesh.HasVertexAttribute(
                        UnityEngine.Rendering.VertexAttribute.TexCoord0);

                Debug.Log(
                    "[Little Castle Foliage] Mesh '" +
                    path +
                    "': vertices=" +
                    vertexCount +
                    ", triangles=" +
                    triangleCount +
                    ", normals=" +
                    (normalsValid ? "OK" : "MISSING") +
                    ", vertexColors=" +
                    (colorsValid ? "YES" : "NO") +
                    ", UV0=" +
                    (uvValid ? "OK" : "MISSING") +
                    ", shaders=[" +
                    DescribeShaders(
                        materials) +
                    "].");

                if ((usesFoliage ||
                     usesGrass) &&
                    !normalsValid)
                {
                    errors++;

                    Debug.LogError(
                        "[Little Castle Foliage] " +
                        path +
                        " needs valid normals. Leaf flutter/backlighting " +
                        "cannot be trusted without them.");
                }

                if ((usesFoliage ||
                     usesGrass) &&
                    vertexCount < 24)
                {
                    warnings++;

                    Debug.LogWarning(
                        "[Little Castle Foliage] " +
                        path +
                        " has very few vertices (" +
                        vertexCount +
                        "). Verify that Crown Sway / Vertex Wave / " +
                        "Flutter can deform it internally rather than " +
                        "moving it like one rigid piece.");
                }

                if (requiresVertexMask &&
                    !colorsValid)
                {
                    errors++;

                    Debug.LogError(
                        "[Little Castle Foliage] " +
                        path +
                        " enables Vertex Color R wind masking but the mesh " +
                        "does not contain a full vertex-color channel.");
                }

                if (requiresHeightMask)
                {
                    float height =
                        mesh.bounds.size.y;

                    if (height <= 0.001f)
                    {
                        errors++;

                        Debug.LogError(
                            "[Little Castle Foliage] " +
                            path +
                            " enables height wind masking but mesh bounds " +
                            "have no meaningful local Y height.");
                    }
                    else
                    {
                        Debug.Log(
                            "[Little Castle Foliage] " +
                            path +
                            " uses automatic height wind masking. Verify " +
                            "that local Y/pivot actually represents " +
                            "rigid-base -> flexible-top.");
                    }
                }

                if (usesGrass)
                {
                    ValidateGrassUv(
                        mesh,
                        path,
                        ref warnings,
                        ref errors);
                }

                if (usesFoliage &&
                    usesSolid)
                {
                    Debug.Log(
                        "[Little Castle Foliage] " +
                        path +
                        " contains both foliage and solid material regions. " +
                        "This can be valid when submeshes separate leaves " +
                        "from trunk/branches. Verify submesh assignment.");
                }
            }

            if (foliageRenderers == 0 &&
                grassRenderers == 0)
            {
                warnings++;

                Debug.LogWarning(
                    "[Little Castle Foliage] No renderer currently uses " +
                    "LC_Foliage or LC_Grass. If this is a production " +
                    "vegetation asset, verify material migration before " +
                    "judging wind compatibility.");
            }

            string summary =
                "[Little Castle Foliage] Compatibility review finished for '" +
                root.name +
                "'. foliageRenderers=" +
                foliageRenderers +
                ", grassRenderers=" +
                grassRenderers +
                ", solidRenderers=" +
                solidRenderers +
                ", errors=" +
                errors +
                ", warnings=" +
                warnings +
                ". Manual close/far visual validation is still required.";

            if (errors > 0)
                Debug.LogError(summary);
            else if (warnings > 0)
                Debug.LogWarning(summary);
            else
                Debug.Log(summary);
        }

        private static GameObject ResolveSelectedGameObject()
        {
            if (Selection.activeGameObject != null)
                return Selection.activeGameObject;

            Object selected =
                Selection.activeObject;

            if (selected == null)
                return null;

            string path =
                AssetDatabase.GetAssetPath(
                    selected);

            if (string.IsNullOrEmpty(path))
                return null;

            return
                AssetDatabase.LoadAssetAtPath<
                    GameObject>(path);
        }

        private static Mesh ResolveMesh(
            Renderer renderer)
        {
            if (renderer is SkinnedMeshRenderer skinned)
                return skinned.sharedMesh;

            MeshFilter filter =
                renderer.GetComponent<
                    MeshFilter>();

            return
                filter != null
                    ? filter.sharedMesh
                    : null;
        }

        private static int GetTriangleCount(
            Mesh mesh)
        {
            int total = 0;

            for (int i = 0;
                 i < mesh.subMeshCount;
                 i++)
            {
                total +=
                    (int)mesh.GetIndexCount(i) /
                    3;
            }

            return total;
        }

        private static void ValidateGrassUv(
            Mesh mesh,
            string path,
            ref int warnings,
            ref int errors)
        {
            if (!mesh.HasVertexAttribute(
                    UnityEngine.Rendering.VertexAttribute.TexCoord0))
            {
                errors++;

                Debug.LogError(
                    "[Little Castle Foliage] " +
                    path +
                    " uses LC_Grass but has no UV0 channel. " +
                    "Grass/wheat bending expects UV Y as root-to-tip.");

                return;
            }

            if (!mesh.isReadable)
            {
                warnings++;

                Debug.LogWarning(
                    "[Little Castle Foliage] " +
                    path +
                    " has UV0 but Read/Write is disabled, so the validator " +
                    "cannot measure UV Y range. Do not enable Read/Write " +
                    "permanently just for wind; inspect the source mesh or " +
                    "temporarily validate authoring in the Editor.");

                return;
            }

            Vector2[] uv =
                mesh.uv;

            float minY =
                float.PositiveInfinity;

            float maxY =
                float.NegativeInfinity;

            foreach (Vector2 value in uv)
            {
                minY =
                    Mathf.Min(
                        minY,
                        value.y);

                maxY =
                    Mathf.Max(
                        maxY,
                        value.y);
            }

            float range =
                maxY -
                minY;

            if (range < 0.65f)
            {
                warnings++;

                Debug.LogWarning(
                    "[Little Castle Foliage] " +
                    path +
                    " uses LC_Grass but UV Y range is only " +
                    range.ToString("0.###") +
                    ". Verify UV Y really represents root -> tip.");
            }
            else
            {
                Debug.Log(
                    "[Little Castle Foliage] " +
                    path +
                    " grass/wheat UV Y range=" +
                    range.ToString("0.###") +
                    ". Visual root-to-tip orientation still requires " +
                    "manual inspection.");
            }
        }

        private static string DescribeShaders(
            Material[] materials)
        {
            var names =
                new List<string>();

            foreach (Material material in materials)
            {
                if (material == null)
                {
                    names.Add(
                        "<null material>");

                    continue;
                }

                names.Add(
                    material.shader != null
                        ? material.shader.name
                        : "<no shader>");
            }

            return
                string.Join(
                    ", ",
                    names);
        }

        private static string GetHierarchyPath(
            Transform transform,
            Transform root)
        {
            if (transform == root)
                return transform.name;

            var names =
                new List<string>();

            Transform current =
                transform;

            while (current != null)
            {
                names.Add(
                    current.name);

                if (current == root)
                    break;

                current =
                    current.parent;
            }

            names.Reverse();

            return
                string.Join(
                    "/",
                    names);
        }
    }
}
