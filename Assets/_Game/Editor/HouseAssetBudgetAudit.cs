using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace LittleCastle.Editor
{
    /// <summary>
    /// Read-only authored house LOD intake. This does not profile GPU work,
    /// simplify meshes, edit prefabs, change LOD thresholds or import models.
    /// </summary>
    public static class HouseAssetBudgetAudit
    {
        public const string ApprovedConceptHousePath =
            "Assets/_Game/Art/Imported/ConceptWorldKit_v001/Prefabs/house.prefab";

        // Screening suggestions only; NOT release or target-device budgets.
        private const long SuggestedNearTriangles = 40000;
        private const long SuggestedMediumTriangles = 15000;
        private const long SuggestedFarTriangles = 3000;

        [Serializable]
        public sealed class LodRecord
        {
            public string groupPath;
            public int index;
            public float screenHeight;
            public float transitionPixels1080;
            public long triangles;
            public long vertices;
            public int rendererCount;
            public int submeshCount;
            public int estimatedBasePassDraws;
            public int uniqueMaterials;
            public int nullMaterialSlots;
            public int shadowCasterCount;
            public int shadowReceiverCount;
            public int nonOffMotionVectorCount;
            public int unsupportedTopologyCount;
            public float fractionOfLod0Triangles;
        }

        [Serializable]
        public sealed class Report
        {
            public string sourcePath;
            public string sourceName;
            public string utc;
            public int lodGroups;
            public int unassignedEnabledRenderers;
            public int multiplyAssignedRenderers;
            public int missingMeshRenderers;
            public int distinctMeshes;
            public int distinctMaterials;
            public int warningsCount;
            public bool requiresArtReview;
            public List<LodRecord> levels = new List<LodRecord>();
            public List<string> warnings = new List<string>();
            public string caveat =
                "Submesh submissions are a lower-level estimate, NOT measured " +
                "GPU draw calls. Shadow/extra light passes, SRP batching, " +
                "instancing, camera FOV and screen size require Player profiling.";
        }

        [MenuItem("Little Castle/Assets/House LOD Budget/Audit Approved Concept House")]
        public static void AuditApprovedConceptHouse()
        {
            GameObject root = AssetDatabase.LoadAssetAtPath<GameObject>(
                ApprovedConceptHousePath);
            if (root == null)
                throw new FileNotFoundException(
                    "Approved portable house prefab has not been imported.",
                    ApprovedConceptHousePath);

            WriteReport(Analyze(root));
        }

        [MenuItem("Little Castle/Assets/House LOD Budget/Audit Selected Prefab")]
        public static void AuditSelected()
        {
            GameObject root = Selection.activeObject as GameObject;
            if (root == null || !PrefabUtility.IsPartOfPrefabAsset(root))
                throw new InvalidOperationException(
                    "Select an imported prefab GameObject asset, not a " +
                    "scene instance, before auditing.");

            WriteReport(Analyze(root));
        }

        /// <summary>
        /// Side-effect-free data extraction. The caller owns the input asset.
        /// Call with a prefab or temporary synthetic GameObject for tests.
        /// </summary>
        public static Report Analyze(GameObject root)
        {
            if (root == null)
                throw new ArgumentNullException(nameof(root));

            var report = new Report
            {
                sourcePath = AssetDatabase.GetAssetPath(root),
                sourceName = root.name,
                utc = DateTime.UtcNow.ToString("o")
            };

            LODGroup[] groups = root.GetComponentsInChildren<LODGroup>(true);
            Array.Sort(groups, (a, b) =>
                string.CompareOrdinal(
                    HierarchyPath(a.transform, root.transform),
                    HierarchyPath(b.transform, root.transform)));
            report.lodGroups = groups.Length;
            if (groups.Length == 0)
                Warn(report, "No LODGroup. Building is not ready for authored LOD review.");

            var assignments = new HashSet<Renderer>();
            var meshes = new HashSet<Mesh>();
            var materials = new HashSet<Material>();

            foreach (LODGroup group in groups)
            {
                LOD[] lods = group.GetLODs();
                string groupPath = HierarchyPath(group.transform, root.transform);
                if (lods.Length < 3)
                    Warn(report, groupPath + ": fewer than 3 authored LOD levels.");

                long firstTriangles = 0;
                float previousThreshold = float.PositiveInfinity;
                for (int levelIndex = 0; levelIndex < lods.Length; levelIndex++)
                {
                    LOD lod = lods[levelIndex];
                    var record = new LodRecord
                    {
                        groupPath = groupPath,
                        index = levelIndex,
                        screenHeight = lod.screenRelativeTransitionHeight,
                        transitionPixels1080 = lod.screenRelativeTransitionHeight * 1080f
                    };
                    var levelMaterials = new HashSet<Material>();
                    if (!IsFinite(record.screenHeight) ||
                        record.screenHeight < 0f ||
                        record.screenHeight > 1f ||
                        record.screenHeight >= previousThreshold)
                    {
                        Warn(report, groupPath + ": invalid / unsorted LOD" +
                            levelIndex + " screen height.");
                    }
                    previousThreshold = record.screenHeight;

                    Renderer[] renderers = lod.renderers ?? new Renderer[0];
                    foreach (Renderer renderer in renderers)
                    {
                        if (renderer == null)
                        {
                            Warn(report, groupPath + ": LOD" + levelIndex +
                                " contains a missing renderer reference.");
                            continue;
                        }

                        record.rendererCount++;
                        if (!assignments.Add(renderer))
                            report.multiplyAssignedRenderers++;

                        Mesh mesh = MeshFor(renderer);
                        if (mesh == null)
                        {
                            report.missingMeshRenderers++;
                            Warn(report, groupPath + ": LOD" + levelIndex +
                                " renderer " + renderer.name + " has no mesh.");
                            continue;
                        }

                        meshes.Add(mesh);
                        record.vertices += mesh.vertexCount;
                        Material[] slots = renderer.sharedMaterials;
                        int submeshes = mesh.subMeshCount;
                        record.submeshCount += submeshes;
                        record.estimatedBasePassDraws +=
                            Math.Max(submeshes, slots.Length);

                        for (int sub = 0; sub < submeshes; sub++)
                        {
                            if (mesh.GetTopology(sub) != MeshTopology.Triangles)
                            {
                                record.unsupportedTopologyCount++;
                                Warn(report, renderer.name +
                                    ": non-triangle topology at submesh " + sub +
                                    " cannot be priced as triangles.");
                                continue;
                            }
                            record.triangles += (long)mesh.GetIndexCount(sub) / 3L;
                        }

                        foreach (Material material in slots)
                        {
                            if (material == null)
                            {
                                record.nullMaterialSlots++;
                                continue;
                            }
                            levelMaterials.Add(material);
                            materials.Add(material);
                        }

                        if (renderer.shadowCastingMode != ShadowCastingMode.Off)
                            record.shadowCasterCount++;
                        if (renderer.receiveShadows)
                            record.shadowReceiverCount++;
                        if (renderer.motionVectorGenerationMode !=
                            MotionVectorGenerationMode.ForceNoMotion)
                            record.nonOffMotionVectorCount++;
                    }

                    record.uniqueMaterials = levelMaterials.Count;
                    if (levelIndex == 0)
                        firstTriangles = record.triangles;
                    record.fractionOfLod0Triangles = firstTriangles > 0
                        ? (float)((double)record.triangles / firstTriangles)
                        : 0f;

                    if (record.triangles == 0)
                        Warn(report, groupPath + ": LOD" + levelIndex +
                            " has no measurable triangle geometry.");
                    if (record.nullMaterialSlots > 0)
                        Warn(report, groupPath + ": LOD" + levelIndex +
                            " contains null material slots.");
                    if (levelIndex > 0 &&
                        firstTriangles > 0 &&
                        record.triangles >= firstTriangles)
                        Warn(report, groupPath + ": LOD" + levelIndex +
                            " has not reduced geometry from LOD0.");
                    if (levelIndex == lods.Length - 1 &&
                        (record.shadowCasterCount > 0 ||
                         record.shadowReceiverCount > 0))
                        Warn(report, groupPath +
                            ": farthest LOD still uses realtime shadows.");

                    long screening = levelIndex == 0
                        ? SuggestedNearTriangles
                        : levelIndex == lods.Length - 1
                            ? SuggestedFarTriangles
                            : SuggestedMediumTriangles;
                    if (record.triangles > screening)
                        Warn(report, groupPath + ": LOD" + levelIndex +
                            " triangles=" + record.triangles +
                            " exceeds provisional art-intake screening=" +
                            screening + " (not an approved performance budget).");

                    report.levels.Add(record);
                }
            }

            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null || assignments.Contains(renderer))
                    continue;
                if (renderer.enabled && renderer.gameObject.activeInHierarchy)
                    report.unassignedEnabledRenderers++;
            }

            report.distinctMeshes = meshes.Count;
            report.distinctMaterials = materials.Count;
            if (report.unassignedEnabledRenderers > 0)
                Warn(report, "Enabled renderers not assigned to an LODGroup: " +
                    report.unassignedEnabledRenderers +
                    ". These may render at every distance.");
            if (report.multiplyAssignedRenderers > 0)
                Warn(report, "Renderers assigned to more than one LOD/group: " +
                    report.multiplyAssignedRenderers);

            report.requiresArtReview =
                report.warningsCount > 0 || report.levels.Count == 0;
            return report;
        }

        private static Mesh MeshFor(Renderer renderer)
        {
            if (renderer is SkinnedMeshRenderer skinned)
                return skinned.sharedMesh;

            MeshFilter filter = renderer.GetComponent<MeshFilter>();
            return filter != null ? filter.sharedMesh : null;
        }

        private static string HierarchyPath(Transform transform, Transform root)
        {
            if (transform == root)
                return root.name;
            var parts = new List<string>();
            for (Transform current = transform;
                 current != null && current != root;
                 current = current.parent)
            {
                parts.Add(current.name);
            }
            parts.Add(root.name);
            parts.Reverse();
            return string.Join("/", parts.ToArray());
        }

        private static void Warn(Report report, string warning)
        {
            report.warningsCount++;
            report.warnings.Add(warning);
        }

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);

        private static void WriteReport(Report report)
        {
            string projectRoot = Path.GetFullPath(
                Path.Combine(Application.dataPath, ".."));
            string logs = Path.Combine(projectRoot, "Logs");
            Directory.CreateDirectory(logs);
            string name = "HouseBudget-" +
                DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + ".json";
            string output = Path.Combine(logs, name);
            File.WriteAllText(
                output,
                JsonUtility.ToJson(report, true),
                new UTF8Encoding(false));

            var summary = new StringBuilder();
            summary.Append("[House Budget] ");
            summary.Append(report.sourceName);
            summary.Append(" groups=").Append(report.lodGroups);
            summary.Append(" levels=").Append(report.levels.Count);
            summary.Append(" materials=").Append(report.distinctMaterials);
            summary.Append(" warnings=").Append(report.warningsCount);
            summary.Append(" path=").Append(output);
            summary.Append(". Counts are authored estimates, NOT measured draw calls/FPS.");
            Debug.Log(summary.ToString());
            foreach (string warning in report.warnings)
                Debug.LogWarning("[House Budget] " + warning);
        }
    }
}
