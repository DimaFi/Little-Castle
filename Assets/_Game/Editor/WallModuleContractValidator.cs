using LittleCastle.Building;
using UnityEditor;
using UnityEngine;

namespace LittleCastle.Editor
{
    public static class WallModuleContractValidator
    {
        [MenuItem(
            "Little Castle/Building/Validate Selected Wall Definition")]
        public static void ValidateSelected()
        {
            WallPlacementDefinition definition =
                Selection.activeObject
                as WallPlacementDefinition;

            if (definition == null)
            {
                Debug.LogError(
                    "[Little Castle Wall] Select a WallPlacementDefinition " +
                    "asset first.");

                return;
            }

            GameObject prefab =
                definition.SegmentPrefab;

            if (prefab == null)
            {
                Debug.LogError(
                    "[Little Castle Wall] Definition '" +
                    definition.name +
                    "' has no segment prefab.");

                return;
            }

            int errors = 0;
            int warnings = 0;

            Transform root =
                prefab.transform;

            if (root.localPosition.sqrMagnitude >
                0.000001f)
            {
                warnings++;

                Debug.LogWarning(
                    "[Little Castle Wall] Prefab root local position is not " +
                    "zero. Prefer an identity root transform.");
            }

            if (Quaternion.Angle(
                    root.localRotation,
                    Quaternion.identity) >
                0.01f)
            {
                warnings++;

                Debug.LogWarning(
                    "[Little Castle Wall] Prefab root rotation is not " +
                    "identity. Apply/export the intended +Z wall axis.");
            }

            if ((root.localScale -
                 Vector3.one).sqrMagnitude >
                0.0001f)
            {
                warnings++;

                Debug.LogWarning(
                    "[Little Castle Wall] Prefab root scale is not 1,1,1. " +
                    "Apply transforms before production use.");
            }

            if (!TryGetCombinedLocalMeshBounds(
                    prefab,
                    out Bounds bounds))
            {
                errors++;

                Debug.LogError(
                    "[Little Castle Wall] No MeshFilter/sharedMesh was found " +
                    "under the segment prefab.");
            }
            else
            {
                float expected =
                    definition.SegmentLength;

                float actual =
                    bounds.size.z;

                float tolerance =
                    Mathf.Max(
                        0.08f,
                        expected * 0.12f);

                if (Mathf.Abs(
                        actual -
                        expected) >
                    tolerance)
                {
                    warnings++;

                    Debug.LogWarning(
                        "[Little Castle Wall] Authored +Z length is " +
                        actual.ToString("0.###") +
                        " m but WallPlacementDefinition.segmentLength is " +
                        expected.ToString("0.###") +
                        " m. Measure and align them.");
                }

                if (bounds.size.z <
                    bounds.size.x)
                {
                    warnings++;

                    Debug.LogWarning(
                        "[Little Castle Wall] Mesh is wider in local X than " +
                        "it is long in local Z. Verify that +Z is really the " +
                        "module length axis.");
                }

                float xTolerance =
                    Mathf.Max(
                        0.05f,
                        bounds.size.x *
                        0.15f);

                if (Mathf.Abs(
                        bounds.center.x) >
                    xTolerance)
                {
                    warnings++;

                    Debug.LogWarning(
                        "[Little Castle Wall] Mesh is not centered around " +
                        "local X=0. Verify the prefab pivot.");
                }

                float baseTolerance =
                    Mathf.Max(
                        0.05f,
                        bounds.size.y *
                        0.15f);

                if (Mathf.Abs(
                        bounds.min.y) >
                    baseTolerance)
                {
                    warnings++;

                    Debug.LogWarning(
                        "[Little Castle Wall] Mesh bottom is " +
                        bounds.min.y.ToString("0.###") +
                        " m from local Y=0. Preferred wall pivot is near " +
                        "the base.");
                }

                Debug.Log(
                    "[Little Castle Wall] Combined local mesh bounds: size=" +
                    bounds.size +
                    ", center=" +
                    bounds.center +
                    ".");
            }

            Collider[] colliders =
                prefab.GetComponentsInChildren<
                    Collider>(true);

            if (colliders.Length == 0)
            {
                warnings++;

                Debug.LogWarning(
                    "[Little Castle Wall] Segment prefab has no Collider. " +
                    "Production walls normally need a simple gameplay collider.");
            }

            MeshCollider[] meshColliders =
                prefab.GetComponentsInChildren<
                    MeshCollider>(true);

            for (int i = 0;
                 i < meshColliders.Length;
                 i++)
            {
                Mesh mesh =
                    meshColliders[i].sharedMesh;

                if (mesh != null &&
                    GetTriangleCount(
                        mesh) >
                    500)
                {
                    warnings++;

                    Debug.LogWarning(
                        "[Little Castle Wall] MeshCollider '" +
                        meshColliders[i].name +
                        "' is relatively detailed. Prefer a simple box/low-poly " +
                        "collision mesh for repeated wall modules.");
                }
            }

            Renderer[] renderers =
                prefab.GetComponentsInChildren<
                    Renderer>(true);

            for (int i = 0;
                 i < renderers.Length;
                 i++)
            {
                Material[] materials =
                    renderers[i].sharedMaterials;

                for (int m = 0;
                     m < materials.Length;
                     m++)
                {
                    Material material =
                        materials[m];

                    if (material != null &&
                        !material.enableInstancing)
                    {
                        warnings++;

                        Debug.LogWarning(
                            "[Little Castle Wall] Material '" +
                            material.name +
                            "' has GPU Instancing disabled. Repeated wall " +
                            "modules should normally share an instancing-ready " +
                            "material.");
                    }
                }
            }

            Debug.Log(
                "[Little Castle Wall] Validation finished for '" +
                definition.name +
                "'. errors=" +
                errors +
                ", warnings=" +
                warnings +
                ".");
        }

        private static bool TryGetCombinedLocalMeshBounds(
            GameObject root,
            out Bounds result)
        {
            MeshFilter[] filters =
                root.GetComponentsInChildren<
                    MeshFilter>(true);

            bool hasBounds = false;
            result = default;

            Matrix4x4 rootInverse =
                root.transform.worldToLocalMatrix;

            for (int i = 0;
                 i < filters.Length;
                 i++)
            {
                Mesh mesh =
                    filters[i].sharedMesh;

                if (mesh == null)
                    continue;

                Matrix4x4 toRoot =
                    rootInverse *
                    filters[i].transform.localToWorldMatrix;

                Bounds bounds =
                    mesh.bounds;

                Vector3 min =
                    bounds.min;

                Vector3 max =
                    bounds.max;

                for (int corner = 0;
                     corner < 8;
                     corner++)
                {
                    Vector3 local =
                        new Vector3(
                            (corner & 1) == 0
                                ? min.x
                                : max.x,
                            (corner & 2) == 0
                                ? min.y
                                : max.y,
                            (corner & 4) == 0
                                ? min.z
                                : max.z);

                    Vector3 point =
                        toRoot.MultiplyPoint3x4(
                            local);

                    if (!hasBounds)
                    {
                        result =
                            new Bounds(
                                point,
                                Vector3.zero);

                        hasBounds = true;
                    }
                    else
                    {
                        result.Encapsulate(
                            point);
                    }
                }
            }

            return hasBounds;
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
    }
}
