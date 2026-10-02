using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace LittleCastle.Editor
{
    /// <summary>
    /// Production-oriented validation for authored Blender/Astra/Codex models.
    ///
    /// The tool does not modify the asset. It reports likely performance and
    /// LOD-contract problems before a prefab is added to the spawn catalog.
    /// </summary>
    public static class ProductionAssetValidator
    {
        private const string DistantShaderName =
            "Little Castle/Distance/LC Distant Simple";

        [MenuItem(
            "Little Castle/Assets/Validate Selected Production Model")]
        public static void ValidateSelected()
        {
            GameObject root =
                ResolveSelectedGameObject();

            if (root == null)
            {
                Debug.LogError(
                    "[Little Castle Asset] Select a model/prefab GameObject " +
                    "or prefab asset first.");

                return;
            }

            int errors = 0;
            int warnings = 0;

            var uniqueMaterials =
                new HashSet<Material>();

            var uniqueTextures =
                new HashSet<Texture>();

            var renderers =
                root.GetComponentsInChildren<
                    Renderer>(true);

            if (renderers.Length == 0)
            {
                errors++;

                Debug.LogError(
                    "[Little Castle Asset] No Renderer found under '" +
                    root.name +
                    "'.");
            }

            foreach (Renderer renderer in renderers)
            {
                foreach (
                    Material material
                    in renderer.sharedMaterials)
                {
                    if (material == null)
                        continue;

                    uniqueMaterials.Add(
                        material);

                    string[] properties =
                        material.GetTexturePropertyNames();

                    foreach (string property in properties)
                    {
                        Texture texture =
                            material.GetTexture(
                                property);

                        if (texture != null)
                            uniqueTextures.Add(texture);
                    }
                }
            }

            ValidateLods(
                root,
                ref errors,
                ref warnings);

            ValidateMeshes(
                root,
                ref warnings);

            ValidateColliders(
                root,
                ref warnings);

            ValidateLights(
                root,
                ref warnings);

            ValidateMaterials(
                uniqueMaterials,
                ref warnings);

            ValidateTextures(
                uniqueTextures,
                ref warnings);

            Debug.Log(
                "[Little Castle Asset] '" +
                root.name +
                "': renderers=" +
                renderers.Length +
                ", unique materials=" +
                uniqueMaterials.Count +
                ", textures=" +
                uniqueTextures.Count +
                ".");

            string summary =
                "[Little Castle Asset] Validation finished for '" +
                root.name +
                "'. Errors=" +
                errors +
                ", warnings=" +
                warnings +
                ".";

            if (errors > 0)
                Debug.LogError(summary);
            else if (warnings > 0)
                Debug.LogWarning(summary);
            else
                Debug.Log(summary);
        }

        private static void ValidateLods(
            GameObject root,
            ref int errors,
            ref int warnings)
        {
            LODGroup[] groups =
                root.GetComponentsInChildren<
                    LODGroup>(true);

            if (groups.Length == 0)
            {
                warnings++;

                Debug.LogWarning(
                    "[Little Castle Asset] No LODGroup found. Major trees, " +
                    "buildings, village clusters and large props should have " +
                    "authored LODs before production-scale world use.");

                return;
            }

            Debug.Log(
                "[Little Castle Asset] Found " +
                groups.Length +
                " LODGroup component(s).");

            foreach (LODGroup group in groups)
            {
                ValidateLodGroup(
                    group,
                    ref errors,
                    ref warnings);
            }
        }

        private static void ValidateLodGroup(
            LODGroup group,
            ref int errors,
            ref int warnings)
        {
            if (group == null)
                return;

            LOD[] lods =
                group.GetLODs();

            string groupName =
                group.gameObject.name;

            if (lods.Length < 2)
            {
                warnings++;

                Debug.LogWarning(
                    "[Little Castle Asset] LODGroup '" +
                    groupName +
                    "' has only " +
                    lods.Length +
                    " level. A meaningful distant silhouette LOD is " +
                    "recommended.");
            }

            int previousVertices =
                int.MaxValue;

            for (int i = 0;
                 i < lods.Length;
                 i++)
            {
                LOD lod =
                    lods[i];

                int vertices = 0;
                int triangles = 0;

                foreach (
                    Renderer renderer
                    in lod.renderers)
                {
                    Mesh mesh =
                        ResolveMesh(
                            renderer);

                    if (mesh == null)
                        continue;

                    vertices +=
                        mesh.vertexCount;

                    triangles +=
                        GetTriangleCount(
                            mesh);
                }

                Debug.Log(
                    "[Little Castle Asset] " +
                    groupName +
                    " LOD" +
                    i +
                    ": renderers=" +
                    lod.renderers.Length +
                    ", vertices=" +
                    vertices +
                    ", triangles=" +
                    triangles +
                    ", screenHeight=" +
                    lod.screenRelativeTransitionHeight.ToString("0.###") +
                    ".");

                if (i > 0 &&
                    vertices > previousVertices &&
                    vertices > 0)
                {
                    warnings++;

                    Debug.LogWarning(
                        "[Little Castle Asset] " +
                        groupName +
                        " LOD" +
                        i +
                        " has more vertices than the previous LOD (" +
                        vertices +
                        " > " +
                        previousVertices +
                        "). Verify the LOD export/order.");
                }

                if (vertices > 0)
                    previousVertices = vertices;
            }

            if (lods.Length == 0)
                return;

            LOD farthest =
                lods[lods.Length - 1];

            foreach (
                Renderer renderer
                in farthest.renderers)
            {
                if (renderer == null)
                    continue;

                if (renderer.shadowCastingMode !=
                    ShadowCastingMode.Off)
                {
                    warnings++;

                    Debug.LogWarning(
                        "[Little Castle Asset] Farthest renderer '" +
                        renderer.name +
                        "' in LODGroup '" +
                        groupName +
                        "' still casts shadows. Distant silhouettes should " +
                        "normally use ShadowCastingMode.Off.");
                }

                if (renderer.receiveShadows)
                {
                    warnings++;

                    Debug.LogWarning(
                        "[Little Castle Asset] Farthest renderer '" +
                        renderer.name +
                        "' in LODGroup '" +
                        groupName +
                        "' still receives realtime shadows.");
                }

                if (renderer.lightProbeUsage !=
                    LightProbeUsage.Off)
                {
                    warnings++;

                    Debug.LogWarning(
                        "[Little Castle Asset] Farthest renderer '" +
                        renderer.name +
                        "' still uses Light Probes. Consider Off for the " +
                        "cheapest silhouette tier.");
                }

                if (renderer.reflectionProbeUsage !=
                    ReflectionProbeUsage.Off)
                {
                    warnings++;

                    Debug.LogWarning(
                        "[Little Castle Asset] Farthest renderer '" +
                        renderer.name +
                        "' still uses Reflection Probes.");
                }

                foreach (
                    Material material
                    in renderer.sharedMaterials)
                {
                    if (material == null ||
                        material.shader == null)
                    {
                        continue;
                    }

                    if (material.shader.name !=
                        DistantShaderName)
                    {
                        warnings++;

                        Debug.LogWarning(
                            "[Little Castle Asset] Farthest LOD material '" +
                            material.name +
                            "' uses shader '" +
                            material.shader.name +
                            "'. For forests/villages visible only as " +
                            "silhouettes, prefer '" +
                            DistantShaderName +
                            "' where visually acceptable.");
                    }
                }
            }
        }

        private static void ValidateMeshes(
            GameObject root,
            ref int warnings)
        {
            var filters =
                root.GetComponentsInChildren<
                    MeshFilter>(true);

            foreach (MeshFilter filter in filters)
            {
                Mesh mesh =
                    filter.sharedMesh;

                if (mesh == null)
                    continue;

                if (mesh.isReadable)
                {
                    warnings++;

                    Debug.LogWarning(
                        "[Little Castle Asset] Mesh '" +
                        mesh.name +
                        "' is CPU Read/Write enabled. Keep this only when " +
                        "runtime CPU mesh access is actually required.");
                }
            }
        }

        private static void ValidateColliders(
            GameObject root,
            ref int warnings)
        {
            var meshColliders =
                root.GetComponentsInChildren<
                    MeshCollider>(true);

            foreach (
                MeshCollider collider
                in meshColliders)
            {
                Mesh mesh =
                    collider.sharedMesh;

                if (mesh == null)
                    continue;

                int triangles =
                    GetTriangleCount(
                        mesh);

                if (triangles > 5000)
                {
                    warnings++;

                    Debug.LogWarning(
                        "[Little Castle Asset] MeshCollider '" +
                        collider.name +
                        "' uses about " +
                        triangles +
                        " triangles. Prefer a simplified collider mesh.");
                }
            }
        }

        private static void ValidateLights(
            GameObject root,
            ref int warnings)
        {
            Light[] lights =
                root.GetComponentsInChildren<
                    Light>(true);

            var emitters =
                root.GetComponentsInChildren<
                    LittleCastle.Rendering.NightLightEmitter>(true);

            var poolVisuals =
                root.GetComponentsInChildren<
                    LittleCastle.Rendering.NightLightPoolVisual>(true);

            int localLightCount = 0;

            foreach (Light light in lights)
            {
                if (light == null)
                    continue;

                if (light.type != LightType.Point &&
                    light.type != LightType.Spot)
                {
                    continue;
                }

                localLightCount++;

                var emitter =
                    light.GetComponent<
                        LittleCastle.Rendering.NightLightEmitter>();

                if (emitter == null)
                {
                    warnings++;

                    Debug.LogWarning(
                        "[Little Castle Asset] Local Light '" +
                        light.name +
                        "' is not managed by NightLightEmitter. " +
                        "Production lantern/fire/window lights should be " +
                        "budget-managed.");
                }
                else
                {
                    if (emitter.MaxDistance > 120f)
                    {
                        warnings++;

                        Debug.LogWarning(
                            "[Little Castle Asset] NightLightEmitter '" +
                            emitter.name +
                            "' has MaxDistance=" +
                            emitter.MaxDistance.ToString("0.#") +
                            " m. Realtime local-light LOD should normally be " +
                            "much shorter than visual/emissive distance.");
                    }

                    if (emitter.PoolMaxDistance <
                        emitter.MaxDistance)
                    {
                        warnings++;

                        Debug.LogWarning(
                            "[Little Castle Asset] NightLightEmitter '" +
                            emitter.name +
                            "' has a cheaper pool distance shorter than its " +
                            "realtime-light distance. Normally pool/emissive " +
                            "should survive farther than the real Light.");
                    }
                }

                if (light.shadows !=
                    LightShadows.None)
                {
                    warnings++;

                    Debug.LogWarning(
                        "[Little Castle Asset] Local Light '" +
                        light.name +
                        "' has realtime shadows enabled. This is expensive " +
                        "for repeated lanterns/torches; default production " +
                        "night lights should use no realtime shadows.");
                }

                if (light.renderMode ==
                    LightRenderMode.ForcePixel)
                {
                    warnings++;

                    Debug.LogWarning(
                        "[Little Castle Asset] Local Light '" +
                        light.name +
                        "' is ForcePixel. Prefer Auto unless this is a rare " +
                        "measured hero-light exception.");
                }

                if (light.range > 30f)
                {
                    warnings++;

                    Debug.LogWarning(
                        "[Little Castle Asset] Local Light '" +
                        light.name +
                        "' range is " +
                        light.range.ToString("0.#") +
                        " m. Large overlapping Point/Spot ranges multiply " +
                        "ForwardAdd cost; keep cozy lights compact.");
                }
            }

            if (localLightCount > 4)
            {
                warnings++;

                Debug.LogWarning(
                    "[Little Castle Asset] Selected prefab contains " +
                    localLightCount +
                    " local Lights. Repeated production buildings should " +
                    "usually expose fewer realtime emitters and carry most " +
                    "visual glow through emission/pool visuals.");
            }

            foreach (
                LittleCastle.Rendering.NightLightEmitter emitter
                in emitters)
            {
                if (emitter == null)
                    continue;

                if (emitter.TargetLight == null)
                {
                    warnings++;

                    Debug.LogWarning(
                        "[Little Castle Asset] NightLightEmitter '" +
                        emitter.name +
                        "' has no target Light.");
                }
            }

            foreach (
                LittleCastle.Rendering.NightLightPoolVisual pool
                in poolVisuals)
            {
                if (pool == null ||
                    pool.TargetRenderer == null)
                {
                    continue;
                }

                Material[] materials =
                    pool.TargetRenderer.sharedMaterials;

                bool foundCorrectShader = false;

                for (int i = 0;
                     i < materials.Length;
                     i++)
                {
                    Material material =
                        materials[i];

                    if (material != null &&
                        material.shader != null &&
                        material.shader.name ==
                        "Little Castle/Effects/LC Night Light Pool")
                    {
                        foundCorrectShader = true;
                        break;
                    }
                }

                if (!foundCorrectShader)
                {
                    warnings++;

                    Debug.LogWarning(
                        "[Little Castle Asset] NightLightPoolVisual '" +
                        pool.name +
                        "' is not using Little Castle/Effects/LC Night Light " +
                        "Pool on its renderer.");
                }
            }
        }

        private static void ValidateMaterials(
            HashSet<Material> materials,
            ref int warnings)
        {
            foreach (Material material in materials)
            {
                if (material == null)
                    continue;

                if (!material.enableInstancing)
                {
                    warnings++;

                    Debug.LogWarning(
                        "[Little Castle Asset] Material '" +
                        material.name +
                        "' has GPU Instancing disabled. Repeated world " +
                        "vegetation/props should enable it when compatible.");
                }
            }

            if (materials.Count > 8)
            {
                warnings++;

                Debug.LogWarning(
                    "[Little Castle Asset] Selected asset uses " +
                    materials.Count +
                    " unique materials. Verify that material slots are " +
                    "actually necessary and shared.");
            }
        }

        private static void ValidateTextures(
            HashSet<Texture> textures,
            ref int warnings)
        {
            foreach (Texture texture in textures)
            {
                string path =
                    AssetDatabase.GetAssetPath(
                        texture);

                if (string.IsNullOrEmpty(path))
                    continue;

                TextureImporter importer =
                    AssetImporter.GetAtPath(path)
                    as TextureImporter;

                if (importer == null)
                    continue;

                if (importer.isReadable)
                {
                    warnings++;

                    Debug.LogWarning(
                        "[Little Castle Asset] Texture '" +
                        path +
                        "' has Read/Write enabled. Disable unless CPU texture " +
                        "access is required.");
                }

                if (!importer.mipmapEnabled &&
                    texture.width >= 512 &&
                    texture.height >= 512)
                {
                    warnings++;

                    Debug.LogWarning(
                        "[Little Castle Asset] Texture '" +
                        path +
                        "' is " +
                        texture.width +
                        "x" +
                        texture.height +
                        " with mipmaps disabled. World materials normally " +
                        "need mipmaps for stable distant rendering.");
                }
            }
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
            if (mesh == null)
                return 0;

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
    }
}
