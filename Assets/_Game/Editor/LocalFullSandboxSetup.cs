using System;
using System.Collections.Generic;
using System.IO;
using LittleCastle.Building;
using LittleCastle.CameraSystem;
using LittleCastle.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LittleCastle.Editor
{
    /// <summary>
    /// Builds one ignored, local-only playable scene from whatever real assets
    /// currently exist under Models/Ready/Test_*. No binary art is committed and
    /// the shared production catalog remains untouched.
    /// </summary>
    public static class LocalFullSandboxSetup
    {
        private const string SandboxRoot =
            ReadyModelCatalogSync.ReadyRoot + "/LocalSandbox";

        private const string NormalizedRoot =
            SandboxRoot + "/Normalized";

        private const string SandboxScenePath =
            SandboxRoot + "/FullLocalSandbox.unity";

        private const string WallDefinitionPath =
            SandboxRoot + "/LocalStoneWall.asset";

        private sealed class Candidate
        {
            public GameObject prefab;
            public string path;
            public Bounds bounds;
        }

        [MenuItem(
            "Little Castle/Testing/BUILD & OPEN FULL LOCAL SANDBOX",
            priority = 1)]
        public static void BuildAndOpen()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning(
                    "[Little Castle Sandbox] Exit Play Mode before rebuilding the sandbox.");
                return;
            }

            ReadyModelCatalogSync.Sync();

            EnsureFolder(
                ReadyModelCatalogSync.ReadyRoot,
                "LocalSandbox");

            EnsureFolder(
                SandboxRoot,
                "Normalized");

            List<Candidate> candidates =
                CollectReadyPrefabs();

            List<Candidate> trees =
                FindTreeCandidates(candidates);

            Candidate house =
                FindHouseCandidate(candidates);

            List<Candidate> rocks =
                FindRockCandidates(candidates);

            Candidate bridge =
                FindBest(
                    candidates,
                    c => Contains(c, "bridge"),
                    c => ScoreName(c, "bridge"));

            Candidate wallSegment =
                FindBest(
                    candidates,
                    IsWallSegment,
                    c =>
                        ScoreName(c, "wall") +
                        ScoreName(c, "2m") * 5 +
                        ScoreName(c, "stone") * 2);

            Candidate wallPillar =
                FindBest(
                    candidates,
                    c =>
                        Contains(c, "wall") &&
                        (Contains(c, "pillar") ||
                         Contains(c, "post")),
                    c =>
                        ScoreName(c, "pillar") * 5 +
                        ScoreName(c, "stone") * 2);

            Candidate wallEnd =
                FindBest(
                    candidates,
                    c =>
                        Contains(c, "wall") &&
                        Contains(c, "end"),
                    c =>
                        ScoreName(c, "end") * 4);

            GameObject[] treeVariants =
                NormalizeMany(
                    trees,
                    "Tree");

            GameObject normalizedHouse =
                house != null
                    ? CreateGroundedWrapper(
                        house.prefab,
                        "House")
                    : null;

            GameObject[] rockVariants =
                NormalizeMany(
                    rocks,
                    "Rock");

            GameObject normalizedBridge =
                bridge != null
                    ? CreateGroundedWrapper(
                        bridge.prefab,
                        "Bridge")
                    : null;

            BuildLocalWorldOverrides(
                treeVariants,
                normalizedHouse,
                rockVariants,
                normalizedBridge);

            WallPlacementDefinition wallDefinition =
                BuildLocalWallDefinition(
                    wallSegment,
                    wallPillar,
                    wallEnd);

            WorldIntegrationBootstrap.CreateOrUpdate();

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(
                    WorldIntegrationBootstrap.TestScenePath) == null)
            {
                Debug.LogError(
                    "[Little Castle Sandbox] Base world test scene is missing.");
                return;
            }

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(
                    SandboxScenePath) != null)
            {
                AssetDatabase.DeleteAsset(
                    SandboxScenePath);
            }

            if (!AssetDatabase.CopyAsset(
                    WorldIntegrationBootstrap.TestScenePath,
                    SandboxScenePath))
            {
                Debug.LogError(
                    "[Little Castle Sandbox] Could not create local sandbox scene.");
                return;
            }

            AssetDatabase.Refresh();

            Scene scene =
                EditorSceneManager.OpenScene(
                    SandboxScenePath,
                    OpenSceneMode.Single);

            WirePlayableSandbox(
                normalizedHouse,
                wallDefinition);

            EditorSceneManager.MarkSceneDirty(
                scene);

            EditorSceneManager.SaveScene(
                scene);

            AssetDatabase.SaveAssets();

            LogSummary(
                candidates.Count,
                treeVariants,
                normalizedHouse,
                rockVariants,
                normalizedBridge,
                wallSegment,
                wallPillar);

            Debug.Log(
                "[Little Castle Sandbox] Ready. Press Play. " +
                "B draws a real modular wall; F7 toggles controls; F8 toggles performance.");
        }

        private static List<Candidate> CollectReadyPrefabs()
        {
            string[] guids =
                AssetDatabase.FindAssets(
                    "t:Prefab",
                    new[]
                    {
                        ReadyModelCatalogSync.ReadyRoot
                    });

            var result =
                new List<Candidate>();

            for (int i = 0; i < guids.Length; i++)
            {
                string path =
                    AssetDatabase.GUIDToAssetPath(
                        guids[i]);

                if (!path.Contains(
                        "/Test_",
                        StringComparison.OrdinalIgnoreCase) ||
                    path.Contains(
                        "/LocalSandbox/",
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                GameObject prefab =
                    AssetDatabase.LoadAssetAtPath<GameObject>(
                        path);

                if (prefab == null)
                    continue;

                GameObject instance =
                    PrefabUtility.InstantiatePrefab(
                        prefab)
                    as GameObject;

                Bounds bounds =
                    instance != null
                        ? GetCombinedLocalRendererBounds(
                            instance)
                        : new Bounds(
                            Vector3.zero,
                            Vector3.one);

                if (instance != null)
                {
                    UnityEngine.Object.DestroyImmediate(
                        instance);
                }

                result.Add(
                    new Candidate
                    {
                        prefab = prefab,
                        path = path,
                        bounds = bounds
                    });
            }

            return result;
        }

        private static List<Candidate> FindTreeCandidates(
            List<Candidate> candidates)
        {
            var result =
                new List<Candidate>();

            for (int i = 0; i < candidates.Count; i++)
            {
                Candidate c =
                    candidates[i];

                bool vegetation =
                    c.path.Contains(
                        "/Test_Vegetation/",
                        StringComparison.OrdinalIgnoreCase);

                if (!vegetation ||
                    !Contains(c, "tree") ||
                    ContainsAny(
                        c,
                        "leaf",
                        "leaves",
                        "branch",
                        "trunk",
                        "bark",
                        "stump",
                        "root",
                        "ground"))
                {
                    continue;
                }

                if (c.bounds.size.y < 1.5f)
                    continue;

                result.Add(c);
            }

            result.Sort(
                (a, b) =>
                    ScoreTree(b).CompareTo(
                        ScoreTree(a)));

            if (result.Count > 6)
            {
                result.RemoveRange(
                    6,
                    result.Count - 6);
            }

            return result;
        }

        private static int ScoreTree(
            Candidate c)
        {
            int score =
                ScoreName(c, "tree") * 2 +
                ScoreName(c, "oak") * 4;

            score +=
                Mathf.RoundToInt(
                    Mathf.Min(
                        20f,
                        c.bounds.size.y));

            return score;
        }

        private static Candidate FindHouseCandidate(
            List<Candidate> candidates)
        {
            Candidate named =
                FindBest(
                    candidates,
                    c =>
                        c.path.Contains(
                            "/Test_Building/",
                            StringComparison.OrdinalIgnoreCase) &&
                        ContainsAny(
                            c,
                            "house",
                            "cottage",
                            "farmhouse",
                            "home") &&
                        !ContainsAny(
                            c,
                            "door",
                            "window",
                            "roof",
                            "shutter",
                            "barrel",
                            "brick",
                            "board",
                            "plank"),
                    ScoreHouse);

            if (named != null)
                return named;

            return
                FindBest(
                    candidates,
                    c =>
                        c.path.Contains(
                            "/Test_Building/",
                            StringComparison.OrdinalIgnoreCase) &&
                        !ContainsAny(
                            c,
                            "door",
                            "window",
                            "roof",
                            "shutter",
                            "barrel",
                            "brick",
                            "board",
                            "plank",
                            "wall",
                            "gate") &&
                        c.bounds.size.y >= 2f &&
                        c.bounds.size.x >= 2f &&
                        c.bounds.size.z >= 2f,
                    ScoreHouse);
        }

        private static int ScoreHouse(
            Candidate c)
        {
            float volume =
                c.bounds.size.x *
                c.bounds.size.y *
                c.bounds.size.z;

            return
                ScoreName(c, "house") * 20 +
                ScoreName(c, "cottage") * 20 +
                ScoreName(c, "farmhouse") * 20 +
                Mathf.RoundToInt(
                    Mathf.Min(
                        volume,
                        1000f));
        }

        private static List<Candidate> FindRockCandidates(
            List<Candidate> candidates)
        {
            var result =
                new List<Candidate>();

            for (int i = 0; i < candidates.Count; i++)
            {
                Candidate c =
                    candidates[i];

                if (!ContainsAny(
                        c,
                        "rock",
                        "stone") ||
                    ContainsAny(
                        c,
                        "wall",
                        "pillar",
                        "gate",
                        "building",
                        "house"))
                {
                    continue;
                }

                result.Add(c);
            }

            result.Sort(
                (a, b) =>
                    b.bounds.size.sqrMagnitude.CompareTo(
                        a.bounds.size.sqrMagnitude));

            if (result.Count > 6)
            {
                result.RemoveRange(
                    6,
                    result.Count - 6);
            }

            return result;
        }

        private static bool IsWallSegment(
            Candidate c)
        {
            return
                c.path.Contains(
                    "/Test_Architecture/",
                    StringComparison.OrdinalIgnoreCase) &&
                Contains(c, "wall") &&
                ContainsAny(
                    c,
                    "2m",
                    "segment") &&
                !ContainsAny(
                    c,
                    "gate",
                    "banner",
                    "pillar",
                    "end",
                    "leaf");
        }

        private static Candidate FindBest(
            List<Candidate> candidates,
            Func<Candidate, bool> predicate,
            Func<Candidate, int> score)
        {
            Candidate best = null;
            int bestScore = int.MinValue;

            for (int i = 0; i < candidates.Count; i++)
            {
                Candidate candidate =
                    candidates[i];

                if (!predicate(candidate))
                    continue;

                int value =
                    score(candidate);

                if (best == null ||
                    value > bestScore)
                {
                    best = candidate;
                    bestScore = value;
                }
            }

            return best;
        }

        private static int ScoreName(
            Candidate candidate,
            string token)
        {
            return
                Contains(candidate, token)
                    ? 10
                    : 0;
        }

        private static bool Contains(
            Candidate candidate,
            string token)
        {
            return
                candidate.prefab.name.IndexOf(
                    token,
                    StringComparison.OrdinalIgnoreCase) >= 0 ||
                candidate.path.IndexOf(
                    token,
                    StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool ContainsAny(
            Candidate candidate,
            params string[] tokens)
        {
            for (int i = 0; i < tokens.Length; i++)
            {
                if (Contains(
                        candidate,
                        tokens[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private static GameObject[] NormalizeMany(
            List<Candidate> candidates,
            string prefix)
        {
            var result =
                new List<GameObject>();

            for (int i = 0; i < candidates.Count; i++)
            {
                GameObject wrapper =
                    CreateGroundedWrapper(
                        candidates[i].prefab,
                        prefix);

                if (wrapper != null)
                    result.Add(wrapper);
            }

            return result.ToArray();
        }

        private static GameObject CreateGroundedWrapper(
            GameObject source,
            string family)
        {
            if (source == null)
                return null;

            string safeName =
                source.name.Replace(
                    Path.DirectorySeparatorChar,
                    '_');

            string path =
                NormalizedRoot +
                "/" +
                family +
                "_" +
                safeName +
                "_Grounded.prefab";

            GameObject existing =
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    path);

            GameObject sourceInstance =
                PrefabUtility.InstantiatePrefab(
                    source)
                as GameObject;

            if (sourceInstance == null)
                return existing;

            var root =
                new GameObject(
                    source.name +
                    "_Grounded");

            sourceInstance.transform.SetParent(
                root.transform,
                false);

            Bounds bounds =
                GetCombinedLocalRendererBounds(
                    root);

            sourceInstance.transform.localPosition +=
                Vector3.up *
                (-bounds.min.y);

            GameObject saved =
                PrefabUtility.SaveAsPrefabAsset(
                    root,
                    path);

            UnityEngine.Object.DestroyImmediate(
                root);

            return saved;
        }

        private static void BuildLocalWorldOverrides(
            GameObject[] treeVariants,
            GameObject house,
            GameObject[] rockVariants,
            GameObject bridge)
        {
            WorldSpawnCatalog catalog =
                AssetDatabase.LoadAssetAtPath<WorldSpawnCatalog>(
                    ReadyModelCatalogSync.LocalTestOverrideCatalogPath);

            if (catalog == null)
            {
                catalog =
                    ScriptableObject.CreateInstance<WorldSpawnCatalog>();

                AssetDatabase.CreateAsset(
                    catalog,
                    ReadyModelCatalogSync.LocalTestOverrideCatalogPath);
            }

            var serialized =
                new SerializedObject(
                    catalog);

            SerializedProperty entries =
                serialized.FindProperty(
                    "entries");

            entries.arraySize = 0;

            AddCatalogEntry(
                entries,
                "tree_main_01",
                treeVariants);

            AddCatalogEntry(
                entries,
                "neutral_village_01",
                house != null
                    ? new[] { house }
                    : Array.Empty<GameObject>());

            AddCatalogEntry(
                entries,
                "rock_ground_01",
                rockVariants);

            if (rockVariants.Length > 0)
            {
                AddCatalogEntry(
                    entries,
                    "deposit_stone_01",
                    rockVariants);

                AddCatalogEntry(
                    entries,
                    "deposit_iron_01",
                    rockVariants);
            }

            if (bridge != null)
            {
                AddCatalogEntry(
                    entries,
                    "bridge_wood_small",
                    new[] { bridge });
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(
                catalog);

            AssetDatabase.SaveAssets();
        }

        private static void AddCatalogEntry(
            SerializedProperty entries,
            string archetypeId,
            GameObject[] prefabs)
        {
            if (prefabs == null ||
                prefabs.Length == 0)
            {
                return;
            }

            int index =
                entries.arraySize;

            entries.arraySize++;

            SerializedProperty entry =
                entries.GetArrayElementAtIndex(
                    index);

            entry.FindPropertyRelative(
                    "archetypeId").stringValue =
                archetypeId;

            entry.FindPropertyRelative(
                    "scaleMultiplier").floatValue =
                1f;

            entry.FindPropertyRelative(
                    "rotationOffsetEuler").vector3Value =
                Vector3.zero;

            SerializedProperty refs =
                entry.FindPropertyRelative(
                    "prefabs");

            refs.arraySize =
                prefabs.Length;

            for (int i = 0; i < prefabs.Length; i++)
            {
                refs.GetArrayElementAtIndex(
                        i)
                    .objectReferenceValue =
                    prefabs[i];
            }
        }

        private static WallPlacementDefinition BuildLocalWallDefinition(
            Candidate segment,
            Candidate pillar,
            Candidate end)
        {
            if (segment == null)
                return null;

            WallPlacementDefinition definition =
                AssetDatabase.LoadAssetAtPath<
                    WallPlacementDefinition>(
                    WallDefinitionPath);

            if (definition == null)
            {
                definition =
                    ScriptableObject.CreateInstance<
                        WallPlacementDefinition>();

                AssetDatabase.CreateAsset(
                    definition,
                    WallDefinitionPath);
            }

            float segmentLength =
                Mathf.Max(
                    0.1f,
                    segment.bounds.size.z);

            var serialized =
                new SerializedObject(
                    definition);

            serialized.FindProperty(
                "definitionId").stringValue =
                "local_stone_wall";

            serialized.FindProperty(
                "segmentLength").floatValue =
                segmentLength;

            serialized.FindProperty(
                "spacingMultiplier").floatValue =
                0.96f;

            serialized.FindProperty(
                "segmentPrefab").objectReferenceValue =
                segment.prefab;

            serialized.FindProperty(
                "startTowerPrefab").objectReferenceValue =
                pillar != null
                    ? pillar.prefab
                    : null;

            serialized.FindProperty(
                "repeatTowerPrefab").objectReferenceValue =
                pillar != null
                    ? pillar.prefab
                    : null;

            serialized.FindProperty(
                "automaticTowerSpacing").floatValue =
                pillar != null
                    ? 14f
                    : 0f;

            serialized.FindProperty(
                "maximumGroundSlopeDegrees").floatValue =
                28f;

            serialized.FindProperty(
                "maximumHeightStep").floatValue =
                1.25f;

            serialized.FindProperty(
                "endCapPrefab").objectReferenceValue =
                end != null
                    ? end.prefab
                    : null;

            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(
                definition);

            AssetDatabase.SaveAssets();

            return definition;
        }

        private static void WirePlayableSandbox(
            GameObject showcaseHouse,
            WallPlacementDefinition wallDefinition)
        {
            GameObject existing =
                GameObject.Find(
                    "LocalSandbox");

            if (existing != null)
                UnityEngine.Object.DestroyImmediate(
                    existing);

            var root =
                new GameObject(
                    "LocalSandbox");

            var previewRoot =
                new GameObject(
                    "Wall Preview");

            previewRoot.transform.SetParent(
                root.transform,
                false);

            WallPathPresenter presenter =
                previewRoot.AddComponent<
                    WallPathPresenter>();

            WallPlacementController placement =
                root.AddComponent<
                    WallPlacementController>();

            var placementData =
                new SerializedObject(
                    placement);

            placementData.FindProperty(
                "previewPresenter").objectReferenceValue =
                presenter;

            placementData.ApplyModifiedPropertiesWithoutUndo();

            WallBuildInputController input =
                root.AddComponent<
                    WallBuildInputController>();

            var inputData =
                new SerializedObject(
                    input);

            inputData.FindProperty(
                "placementController").objectReferenceValue =
                placement;

            inputData.FindProperty(
                "buildCamera").objectReferenceValue =
                Camera.main;

            inputData.ApplyModifiedPropertiesWithoutUndo();

            LocalSandboxController sandbox =
                root.AddComponent<
                    LocalSandboxController>();

            var sandboxData =
                new SerializedObject(
                    sandbox);

            sandboxData.FindProperty(
                "wallInput").objectReferenceValue =
                input;

            sandboxData.FindProperty(
                "wallPlacement").objectReferenceValue =
                placement;

            sandboxData.FindProperty(
                "wallDefinition").objectReferenceValue =
                wallDefinition;

            sandboxData.FindProperty(
                "strategyCamera").objectReferenceValue =
                UnityEngine.Object.FindFirstObjectByType<
                    StrategyCameraController>();

            sandboxData.FindProperty(
                "showcaseHousePrefab").objectReferenceValue =
                showcaseHouse;

            sandboxData.ApplyModifiedPropertiesWithoutUndo();

            Selection.activeGameObject =
                root;
        }

        private static Bounds GetCombinedLocalRendererBounds(
            GameObject root)
        {
            Renderer[] renderers =
                root.GetComponentsInChildren<Renderer>(
                    true);

            bool hasBounds = false;
            Bounds result = default;

            Matrix4x4 rootInverse =
                root.transform.worldToLocalMatrix;

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer =
                    renderers[i];

                if (renderer == null ||
                    !renderer.enabled)
                {
                    continue;
                }

                Bounds world =
                    renderer.bounds;

                Vector3 min =
                    world.min;

                Vector3 max =
                    world.max;

                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 point =
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

                    point =
                        rootInverse.MultiplyPoint3x4(
                            point);

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

            return hasBounds
                ? result
                : new Bounds(
                    Vector3.zero,
                    Vector3.one);
        }

        private static void EnsureFolder(
            string parent,
            string child)
        {
            string path =
                parent +
                "/" +
                child;

            if (!AssetDatabase.IsValidFolder(
                    path))
            {
                AssetDatabase.CreateFolder(
                    parent,
                    child);
            }
        }

        private static void LogSummary(
            int totalCandidates,
            GameObject[] trees,
            GameObject house,
            GameObject[] rocks,
            GameObject bridge,
            Candidate wallSegment,
            Candidate wallPillar)
        {
            Debug.Log(
                "[Little Castle Sandbox] Asset scan: " +
                totalCandidates +
                " local prefabs. Trees=" +
                trees.Length +
                ", house=" +
                (house != null
                    ? house.name
                    : "NONE") +
                ", rocks=" +
                rocks.Length +
                ", bridge=" +
                (bridge != null
                    ? bridge.name
                    : "NONE") +
                ", wall=" +
                (wallSegment != null
                    ? wallSegment.prefab.name
                    : "NONE") +
                ", pillar=" +
                (wallPillar != null
                    ? wallPillar.prefab.name
                    : "NONE") +
                ".");

            if (trees.Length == 0)
            {
                Debug.LogWarning(
                    "[Little Castle Sandbox] No complete tree prefab was auto-detected.");
            }

            if (house == null)
            {
                Debug.LogWarning(
                    "[Little Castle Sandbox] No complete house prefab was auto-detected. " +
                    "The generated world still runs; only the immediate showcase house is omitted.");
            }

            if (wallSegment == null)
            {
                Debug.LogWarning(
                    "[Little Castle Sandbox] No 2m wall segment was auto-detected, " +
                    "so the B-key wall tool is unavailable.");
            }
        }
    }
}
