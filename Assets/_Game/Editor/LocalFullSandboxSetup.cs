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

        private const string CanonicalHousePrefabPath =
            ReadyModelCatalogSync.ReadyRoot +
            "/Test_Building/BLD_House_Cottage_A/BLD_House_Cottage_A.prefab";

        private const string CanonicalWall2mPrefabPath =
            ReadyModelCatalogSync.ReadyRoot +
            "/Test_ModularComponent/MOD_Wall_Stone_2m_A/MOD_Wall_Stone_2m_A.prefab";

        private const string CanonicalWallPillarPrefabPath =
            ReadyModelCatalogSync.ReadyRoot +
            "/Test_ModularComponent/MOD_Wall_Stone_Pillar_A/MOD_Wall_Stone_Pillar_A.prefab";

        private const string CanonicalWallEndPrefabPath =
            ReadyModelCatalogSync.ReadyRoot +
            "/Test_ModularComponent/MOD_Wall_Stone_End_A/MOD_Wall_Stone_End_A.prefab";

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
                LoadCanonicalCandidate(
                    CanonicalHousePrefabPath) ??
                FindHouseCandidate(candidates);

            List<Candidate> rocks =
                FindRockCandidates(candidates);

            Candidate bridge =
                FindBest(
                    candidates,
                    c => Contains(c, "bridge"),
                    c => ScoreName(c, "bridge"));

            Candidate wallSegment =
                LoadCanonicalCandidate(
                    CanonicalWall2mPrefabPath) ??
                FindBest(
                    candidates,
                    IsWallSegment,
                    c =>
                        ScoreName(c, "wall") +
                        ScoreName(c, "2m") * 5 +
                        ScoreName(c, "stone") * 2);

            Candidate wallPillar =
                LoadCanonicalCandidate(
                    CanonicalWallPillarPrefabPath) ??
                FindBest(
                    candidates,
                    c =>
                        Contains(c, "wall") &&
                        (Contains(c, "pillar") ||
                         Contains(c, "post")),
                    c =>
                        ScoreName(c, "SM_Wall_Stone_Pillar_A") * 20 +
                        ScoreName(c, "pillar") * 5 +
                        ScoreName(c, "stone") * 2);

            Candidate wallEnd =
                LoadCanonicalCandidate(
                    CanonicalWallEndPrefabPath) ??
                FindBest(
                    candidates,
                    c =>
                        Contains(c, "wall") &&
                        Contains(c, "end"),
                    c =>
                        ScoreName(c, "SM_Wall_Stone_End_A") * 20 +
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

            // Wall modules keep their authored +Z axis and scale, but the
            // local test wrapper normalizes the visual base to Y=0. This makes
            // placement diagnostics about seam/axis quality rather than an
            // arbitrary FBX origin offset.
            if (wallSegment != null)
            {
                wallSegment.prefab =
                    CreateGroundedWrapper(
                        wallSegment.prefab,
                        "WallSegment");
            }

            if (wallPillar != null)
            {
                wallPillar.prefab =
                    CreateGroundedWrapper(
                        wallPillar.prefab,
                        "WallPillar");
            }

            if (wallEnd != null)
            {
                wallEnd.prefab =
                    CreateGroundedWrapper(
                        wallEnd.prefab,
                        "WallEnd");
            }

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

            ConfigureLocalTerrainMaterial();
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

        private static Candidate FindCandidateByPrefabName(
            string prefabName)
        {
            string[] guids =
                AssetDatabase.FindAssets(
                    prefabName + " t:Prefab",
                    new[]
                    {
                        ReadyModelCatalogSync.ReadyRoot
                    });

            for (int i = 0; i < guids.Length; i++)
            {
                string path =
                    AssetDatabase.GUIDToAssetPath(
                        guids[i]);

                GameObject prefab =
                    AssetDatabase.LoadAssetAtPath<GameObject>(
                        path);

                if (prefab == null ||
                    !string.Equals(
                        prefab.name,
                        prefabName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

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

                Debug.Log(
                    "[Little Castle Sandbox] Recovered local prefab '" +
                    prefabName +
                    "' from " +
                    path +
                    ".");

                return
                    new Candidate
                    {
                        prefab = prefab,
                        path = path,
                        bounds = bounds
                    };
            }

            return null;
        }

        private static Candidate LoadCanonicalCandidate(
            string path)
        {
            GameObject prefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    path);

            if (prefab == null)
                return null;

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

            return
                new Candidate
                {
                    prefab = prefab,
                    path = path,
                    bounds = bounds
                };
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
            Candidate canonical =
                FindBest(
                    candidates,
                    c => Contains(c, "BLD_House_Cottage_A"),
                    c => 100000 + ScoreHouse(c));

            if (canonical != null)
                return canonical;

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
            if (!Contains(c, "wall"))
                return false;

            // Asset-book test packages may classify the wall FBX as
            // Architecture or ModularComponent. The production identity is the
            // model name/contract, not the temporary Test_* folder.
            bool looksLikePrimaryTwoMeterSegment =
                Contains(c, "SM_Wall_Stone_2m_A") ||
                Contains(c, "Wall_Stone_2m") ||
                Contains(c, "2m") ||
                Contains(c, "segment");

            return
                looksLikePrimaryTwoMeterSegment &&
                !ContainsAny(
                    c,
                    "gate",
                    "banner",
                    "pillar",
                    "post",
                    "end",
                    "leaf",
                    "trim",
                    "hinge");
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
            if (candidate == null ||
                string.IsNullOrEmpty(token))
            {
                return false;
            }

            if (candidate.prefab.name.IndexOf(
                    token,
                    StringComparison.OrdinalIgnoreCase) >= 0 ||
                candidate.path.IndexOf(
                    token,
                    StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            // Some imported test releases keep an AssetBook/package name at
            // the prefab root and put the real model IDs on child transforms
            // or meshes. Search the hierarchy too so exact authored IDs such as
            // SM_Wall_Stone_2m_A are still discoverable.
            Transform[] transforms =
                candidate.prefab.GetComponentsInChildren<Transform>(
                    true);

            for (int i = 0; i < transforms.Length; i++)
            {
                if (transforms[i] != null &&
                    transforms[i].name.IndexOf(
                        token,
                        StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            MeshFilter[] filters =
                candidate.prefab.GetComponentsInChildren<MeshFilter>(
                    true);

            for (int i = 0; i < filters.Length; i++)
            {
                Mesh mesh =
                    filters[i] != null
                        ? filters[i].sharedMesh
                        : null;

                if (mesh != null &&
                    mesh.name.IndexOf(
                        token,
                        StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
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
            // Never rely on heuristic discovery for the canonical wall kit.
            // The local snapshot proved these exact prefabs exist, so recover
            // them by exact path/name if an earlier candidate scan missed them.
            segment =
                segment ??
                LoadCanonicalCandidate(
                    CanonicalWall2mPrefabPath) ??
                FindCandidateByPrefabName(
                    "MOD_Wall_Stone_2m_A");

            pillar =
                pillar ??
                LoadCanonicalCandidate(
                    CanonicalWallPillarPrefabPath) ??
                FindCandidateByPrefabName(
                    "MOD_Wall_Stone_Pillar_A");

            end =
                end ??
                LoadCanonicalCandidate(
                    CanonicalWallEndPrefabPath) ??
                FindCandidateByPrefabName(
                    "MOD_Wall_Stone_End_A");

            if (segment == null ||
                segment.prefab == null)
            {
                Debug.LogError(
                    "[Little Castle Sandbox] Canonical wall segment " +
                    "MOD_Wall_Stone_2m_A could not be loaded even though it " +
                    "was present in the last local asset snapshot.");
                return null;
            }

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

            // The canonical production contract for MOD_Wall_Stone_2m_A is
            // exactly two metres along local +Z. Do not infer gameplay spacing
            // from a wrapper/render bounds measurement.
            float segmentLength =
                segment.path == CanonicalWall2mPrefabPath
                    ? 2f
                    : Mathf.Max(
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

        private static void ConfigureLocalTerrainMaterial()
        {
            Material terrain =
                AssetDatabase.LoadAssetAtPath<Material>(
                    WorldIntegrationBootstrap.TerrainMaterialPath);

            if (terrain == null)
                return;

            const string terrainFolder =
                "Assets/_Game/Materials/Material_terrain";

            Texture2D grass =
                FindBestTerrainTexture(
                    terrainFolder,
                    "GrassGround",
                    "Grass_Ground",
                    "GroundGrass",
                    "Grass");

            Texture2D dirt =
                FindBestTerrainTexture(
                    terrainFolder,
                    "DirtPath",
                    "Dirt_Path",
                    "GroundDirt",
                    "Dirt",
                    "Path");

            Texture2D rock =
                FindBestTerrainTexture(
                    terrainFolder,
                    "RockGround",
                    "StoneGround",
                    "GroundRock",
                    "Rock",
                    "Stone");

            bool changed = false;

            if (grass != null &&
                terrain.HasProperty("_GrassTex"))
            {
                terrain.SetTexture("_GrassTex", grass);
                terrain.SetColor("_GrassColor", Color.white);
                changed = true;
            }

            if (dirt != null &&
                terrain.HasProperty("_DirtTex"))
            {
                terrain.SetTexture("_DirtTex", dirt);
                terrain.SetColor("_DirtColor", Color.white);
                changed = true;
            }

            if (rock != null &&
                terrain.HasProperty("_RockTex"))
            {
                terrain.SetTexture("_RockTex", rock);
                terrain.SetColor("_RockColor", Color.white);
                changed = true;
            }

            if (changed)
            {
                EditorUtility.SetDirty(terrain);
                AssetDatabase.SaveAssets();

                Debug.Log(
                    "[Little Castle Sandbox] Local terrain textures linked: " +
                    "grass=" + (grass != null ? grass.name : "fallback") +
                    ", dirt=" + (dirt != null ? dirt.name : "fallback") +
                    ", rock=" + (rock != null ? rock.name : "fallback") + ".");
            }
        }

        private static Texture2D FindBestTerrainTexture(
            string folder,
            params string[] preferredTokens)
        {
            if (!AssetDatabase.IsValidFolder(folder))
                return null;

            string[] guids =
                AssetDatabase.FindAssets(
                    "t:Texture2D",
                    new[] { folder });

            Texture2D best = null;
            int bestScore = int.MinValue;

            for (int i = 0; i < guids.Length; i++)
            {
                string path =
                    AssetDatabase.GUIDToAssetPath(
                        guids[i]);

                string name =
                    Path.GetFileNameWithoutExtension(
                        path);

                if (name.IndexOf(
                        "Normal",
                        StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf(
                        "Rough",
                        StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf(
                        "_AO",
                        StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf(
                        "Height",
                        StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    continue;
                }

                int score = 0;

                for (int t = 0; t < preferredTokens.Length; t++)
                {
                    if (name.IndexOf(
                            preferredTokens[t],
                            StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        score +=
                            (preferredTokens.Length - t) *
                            10;
                    }
                }

                if (score <= bestScore)
                    continue;

                Texture2D texture =
                    AssetDatabase.LoadAssetAtPath<Texture2D>(
                        path);

                if (texture == null)
                    continue;

                best = texture;
                bestScore = score;
            }

            return
                bestScore > 0
                    ? best
                    : null;
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

            if (wallDefinition == null)
            {
                wallDefinition =
                    AssetDatabase.LoadAssetAtPath<
                        WallPlacementDefinition>(
                        WallDefinitionPath);
            }

            if (wallDefinition == null)
            {
                wallDefinition =
                    BuildLocalWallDefinition(
                        null,
                        null,
                        null);
            }

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
            EditorUtility.SetDirty(sandbox);

            if (wallDefinition != null)
            {
                Debug.Log(
                    "[Little Castle Sandbox] Wall definition bound: " +
                    AssetDatabase.GetAssetPath(
                        wallDefinition) +
                    " | segment=" +
                    (wallDefinition.SegmentPrefab != null
                        ? wallDefinition.SegmentPrefab.name
                        : "NONE") +
                    ".");
            }
            else
            {
                Debug.LogError(
                    "[Little Castle Sandbox] Failed to bind LocalStoneWall " +
                    "after exact-path and name-based recovery.");
            }

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
