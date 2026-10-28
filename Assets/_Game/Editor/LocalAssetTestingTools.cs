using System;
using System.IO;
using LittleCastle.Building;
using LittleCastle.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LittleCastle.Editor
{
    /// <summary>
    /// Editor-only bridge for testing ignored local Ready/Test_* assets without
    /// promoting them to production catalogs or changing authoritative world data.
    /// </summary>
    public static class LocalAssetTestingTools
    {
        private const string TestScenePath =
            "Assets/_Game/Scenes/WorldGenerationTest.unity";

        private const string StressRootName =
            "__LocalAssetStressTest";

        private const string WallRootName =
            "__LocalWallAlgorithmTest";

        [MenuItem("Little Castle/Testing/Procedural Override/Use Selected As Tree")]
        private static void UseAsTree() =>
            AssignSelectedToArchetype("tree_main_01");

        [MenuItem("Little Castle/Testing/Procedural Override/Use Selected As Rock")]
        private static void UseAsRock() =>
            AssignSelectedToArchetype("rock_ground_01");

        [MenuItem("Little Castle/Testing/Procedural Override/Use Selected As Stone Deposit")]
        private static void UseAsStoneDeposit() =>
            AssignSelectedToArchetype("deposit_stone_01");

        [MenuItem("Little Castle/Testing/Procedural Override/Use Selected As Iron Deposit")]
        private static void UseAsIronDeposit() =>
            AssignSelectedToArchetype("deposit_iron_01");

        [MenuItem("Little Castle/Testing/Procedural Override/Use Selected As Neutral Village / House")]
        private static void UseAsNeutralVillage() =>
            AssignSelectedToArchetype("neutral_village_01");

        [MenuItem("Little Castle/Testing/Procedural Override/Use Selected As Ruin / Landmark")]
        private static void UseAsRuin() =>
            AssignSelectedToArchetype("ruin_fortified_01");

        [MenuItem("Little Castle/Testing/Procedural Override/Use Selected As Small Bridge")]
        private static void UseAsBridge() =>
            AssignSelectedToArchetype("bridge_wood_small");

        [MenuItem("Little Castle/Testing/Procedural Override/Clear All Local Overrides")]
        private static void ClearOverrides()
        {
            if (AssetDatabase.DeleteAsset(
                    ReadyModelCatalogSync.LocalTestOverrideCatalogPath))
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            Debug.Log(
                "[Little Castle Testing] Local procedural prefab overrides cleared.");
        }

        [MenuItem("Little Castle/Testing/Regenerate Procedural Preview")]
        private static void RegenerateProceduralPreview()
        {
            if (!EnsureTestSceneOpen())
                return;

            WorldGenerator generator =
                UnityEngine.Object.FindAnyObjectByType<WorldGenerator>();

            if (generator == null)
            {
                Debug.LogError(
                    "[Little Castle Testing] WorldGenerationTest has no WorldGenerator.");
                return;
            }

            generator.GeneratePreview();
            Selection.activeGameObject = generator.gameObject;

            Debug.Log(
                "[Little Castle Testing] Procedural preview regenerated with local editor overrides.");
        }

        [MenuItem("Little Castle/Testing/Stress Grid/Selected Prefab 10x10")]
        private static void Stress10() =>
            CreateStressGrid(10);

        [MenuItem("Little Castle/Testing/Stress Grid/Selected Prefab 25x25")]
        private static void Stress25() =>
            CreateStressGrid(25);

        [MenuItem("Little Castle/Testing/Stress Grid/Clear")]
        private static void ClearStressGrid()
        {
            DestroyNamedRoot(StressRootName);
            Debug.Log("[Little Castle Testing] Stress grid cleared.");
        }

        [MenuItem("Little Castle/Testing/Wall/Create Rectangle From Selected Segment")]
        private static void CreateWallRectangle()
        {
            GameObject segment =
                ResolveSelectedReadyPrefab(
                    Selection.activeObject as GameObject,
                    true);

            if (segment == null)
                return;

            GameObject pillar = null;
            foreach (UnityEngine.Object selected in Selection.objects)
            {
                GameObject candidate = selected as GameObject;
                if (candidate == null || candidate == Selection.activeObject)
                    continue;

                pillar = ResolveSelectedReadyPrefab(candidate, false);
                if (pillar != null)
                    break;
            }

            if (!Application.isPlaying &&
                !EnsureTestSceneOpen())
            {
                return;
            }

            DestroyNamedRoot(WallRootName);

            Bounds bounds =
                GetCombinedLocalMeshBounds(segment);

            float segmentLength =
                Mathf.Max(0.1f, bounds.size.z);

            WallPlacementDefinition definition =
                ScriptableObject.CreateInstance<WallPlacementDefinition>();

            definition.hideFlags = HideFlags.HideAndDontSave;

            var definitionData =
                new SerializedObject(definition);

            definitionData.FindProperty("definitionId").stringValue =
                "local_test_wall";
            definitionData.FindProperty("segmentLength").floatValue =
                segmentLength;
            definitionData.FindProperty("spacingMultiplier").floatValue =
                0.96f;
            definitionData.FindProperty("segmentPrefab").objectReferenceValue =
                segment;
            definitionData.FindProperty("startTowerPrefab").objectReferenceValue =
                pillar;
            definitionData.FindProperty("repeatTowerPrefab").objectReferenceValue =
                pillar;
            definitionData.FindProperty("automaticTowerSpacing").floatValue =
                pillar != null ? 14f : 0f;
            definitionData.ApplyModifiedPropertiesWithoutUndo();

            var root =
                new GameObject(WallRootName);

            WallPathPresenter presenter =
                root.AddComponent<WallPathPresenter>();

            var presenterData =
                new SerializedObject(presenter);

            presenterData.FindProperty("snapToGround").boolValue =
                Application.isPlaying;
            presenterData.FindProperty("groundProbeHeight").floatValue =
                500f;
            presenterData.ApplyModifiedPropertiesWithoutUndo();

            Vector3 center =
                ResolveTestCenter();

            if (!Application.isPlaying)
                center.y = 12f;

            var wall =
                new WallRuntimeState
                {
                    wallId = 900001,
                    ownerPlayerId = 1,
                    definitionId = definition.DefinitionId,
                    closedLoop = true
                };

            wall.AddControlPoint(
                center + new Vector3(-18f, 0f, -12f),
                WallControlPointMode.Sharp);
            wall.AddControlPoint(
                center + new Vector3(18f, 0f, -12f),
                WallControlPointMode.Sharp);
            wall.AddControlPoint(
                center + new Vector3(18f, 0f, 12f),
                WallControlPointMode.Sharp);
            wall.AddControlPoint(
                center + new Vector3(-18f, 0f, 12f),
                WallControlPointMode.Sharp);

            bool valid =
                presenter.Rebuild(
                    definition,
                    wall,
                    false);

            Selection.activeGameObject = root;

            Debug.Log(
                "[Little Castle Testing] Wall rectangle built from '" +
                segment.name +
                "'. Measured +Z length=" +
                segmentLength.ToString("0.###") +
                " m, sections=" +
                presenter.LastSections.Count +
                ", towers=" +
                presenter.LastTowers.Count +
                ", valid=" +
                valid +
                ". " +
                presenter.LastValidationMessage);

            if (Application.isPlaying)
                UnityEngine.Object.Destroy(definition);
            else
                UnityEngine.Object.DestroyImmediate(definition);
        }

        [MenuItem("Little Castle/Testing/Wall/Clear")]
        private static void ClearWall()
        {
            DestroyNamedRoot(WallRootName);
            Debug.Log("[Little Castle Testing] Wall test cleared.");
        }

        private static void AssignSelectedToArchetype(
            string archetypeId)
        {
            ReadyModelCatalogSync.Sync();

            GameObject prefab =
                ResolveSelectedReadyPrefab(
                    Selection.activeObject as GameObject,
                    true);

            if (prefab == null)
                return;

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
                new SerializedObject(catalog);

            SerializedProperty entries =
                serialized.FindProperty("entries");

            int targetIndex = -1;

            for (int i = 0; i < entries.arraySize; i++)
            {
                if (entries.GetArrayElementAtIndex(i)
                        .FindPropertyRelative("archetypeId")
                        .stringValue == archetypeId)
                {
                    targetIndex = i;
                    break;
                }
            }

            if (targetIndex < 0)
            {
                targetIndex = entries.arraySize;
                entries.arraySize++;
            }

            SerializedProperty entry =
                entries.GetArrayElementAtIndex(targetIndex);

            entry.FindPropertyRelative("archetypeId").stringValue =
                archetypeId;
            entry.FindPropertyRelative("scaleMultiplier").floatValue =
                1f;
            entry.FindPropertyRelative("rotationOffsetEuler").vector3Value =
                Vector3.zero;

            SerializedProperty prefabs =
                entry.FindPropertyRelative("prefabs");

            prefabs.arraySize = 1;
            prefabs.GetArrayElementAtIndex(0).objectReferenceValue =
                prefab;

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();

            Debug.Log(
                "[Little Castle Testing] Local-only override: '" +
                archetypeId +
                "' -> '" +
                prefab.name +
                "'. The shared production catalog was not changed.");
        }

        private static GameObject ResolveSelectedReadyPrefab(
            GameObject selected,
            bool logErrors)
        {
            if (selected == null)
            {
                if (logErrors)
                {
                    Debug.LogError(
                        "[Little Castle Testing] Select a generated Ready prefab " +
                        "or its LOD0 FBX in the Project window first.");
                }

                return null;
            }

            string path =
                AssetDatabase.GetAssetPath(selected);

            if (string.IsNullOrEmpty(path) ||
                !path.StartsWith(
                    ReadyModelCatalogSync.ReadyRoot + "/",
                    StringComparison.Ordinal))
            {
                if (logErrors)
                {
                    Debug.LogError(
                        "[Little Castle Testing] Selection must be inside " +
                        ReadyModelCatalogSync.ReadyRoot +
                        ".");
                }

                return null;
            }

            if (path.EndsWith(
                    ".prefab",
                    StringComparison.OrdinalIgnoreCase))
            {
                return selected;
            }

            if (!path.EndsWith(
                    ".fbx",
                    StringComparison.OrdinalIgnoreCase))
            {
                if (logErrors)
                {
                    Debug.LogError(
                        "[Little Castle Testing] Select a Ready prefab or FBX.");
                }

                return null;
            }

            string directory =
                Path.GetDirectoryName(path)?.Replace('\\', '/');

            string fileName =
                Path.GetFileNameWithoutExtension(path);

            if (fileName.EndsWith(
                    "_LOD0",
                    StringComparison.OrdinalIgnoreCase))
            {
                fileName =
                    fileName.Substring(
                        0,
                        fileName.Length - 5);
            }

            string prefabPath =
                directory + "/" +
                fileName +
                ".prefab";

            GameObject prefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    prefabPath);

            if (prefab == null && logErrors)
            {
                Debug.LogError(
                    "[Little Castle Testing] No generated prefab found at '" +
                    prefabPath +
                    "'. Run Little Castle > Assets > Sync Ready Models and " +
                    "check the Console for rejected LOD sets.");
            }

            return prefab;
        }

        private static bool EnsureTestSceneOpen()
        {
            if (SceneManager.GetActiveScene().path ==
                TestScenePath)
            {
                return true;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return false;

            SceneAsset scene =
                AssetDatabase.LoadAssetAtPath<SceneAsset>(
                    TestScenePath);

            if (scene == null)
            {
                WorldIntegrationBootstrap.CreateOrUpdate();
                scene =
                    AssetDatabase.LoadAssetAtPath<SceneAsset>(
                        TestScenePath);
            }

            if (scene == null)
            {
                Debug.LogError(
                    "[Little Castle Testing] Could not create or load " +
                    TestScenePath +
                    ".");
                return false;
            }

            EditorSceneManager.OpenScene(
                TestScenePath,
                OpenSceneMode.Single);

            return true;
        }

        private static void CreateStressGrid(
            int side)
        {
            GameObject prefab =
                ResolveSelectedReadyPrefab(
                    Selection.activeObject as GameObject,
                    true);

            if (prefab == null)
                return;

            DestroyNamedRoot(StressRootName);

            Bounds bounds =
                GetCombinedLocalMeshBounds(prefab);

            float spacing =
                Mathf.Max(
                    1.5f,
                    Mathf.Max(
                        bounds.size.x,
                        bounds.size.z) +
                    1f);

            var root =
                new GameObject(StressRootName);

            Vector3 center =
                ResolveTestCenter() +
                new Vector3(0f, 18f, 0f);

            float half =
                (side - 1) *
                spacing *
                0.5f;

            int created = 0;

            for (int z = 0; z < side; z++)
            {
                for (int x = 0; x < side; x++)
                {
                    GameObject instance;

                    if (Application.isPlaying)
                    {
                        instance =
                            UnityEngine.Object.Instantiate(
                                prefab,
                                root.transform);
                    }
                    else
                    {
                        instance =
                            PrefabUtility.InstantiatePrefab(
                                prefab,
                                root.transform)
                            as GameObject;
                    }

                    if (instance == null)
                        continue;

                    instance.transform.position =
                        center +
                        new Vector3(
                            x * spacing - half,
                            0f,
                            z * spacing - half);

                    created++;
                }
            }

            Selection.activeGameObject = root;

            Debug.Log(
                "[Little Castle Testing] Created " +
                created +
                " instances of '" +
                prefab.name +
                "' in a " +
                side +
                "x" +
                side +
                " stress grid. Use the Game view/Profiler/F8 overlay for measurements.");
        }

        private static Vector3 ResolveTestCenter()
        {
            GameObject focus =
                GameObject.Find("TestFocus");

            return focus != null
                ? focus.transform.position
                : Vector3.zero;
        }

        private static Bounds GetCombinedLocalMeshBounds(
            GameObject root)
        {
            MeshFilter[] filters =
                root.GetComponentsInChildren<MeshFilter>(
                    true);

            bool hasBounds = false;
            Bounds result = default;

            Matrix4x4 rootInverse =
                root.transform.worldToLocalMatrix;

            for (int i = 0; i < filters.Length; i++)
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

                Vector3 min = bounds.min;
                Vector3 max = bounds.max;

                for (int corner = 0;
                     corner < 8;
                     corner++)
                {
                    Vector3 local =
                        new Vector3(
                            (corner & 1) == 0 ? min.x : max.x,
                            (corner & 2) == 0 ? min.y : max.y,
                            (corner & 4) == 0 ? min.z : max.z);

                    Vector3 point =
                        toRoot.MultiplyPoint3x4(local);

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
                        result.Encapsulate(point);
                    }
                }
            }

            return hasBounds
                ? result
                : new Bounds(
                    Vector3.zero,
                    Vector3.one);
        }

        private static void DestroyNamedRoot(
            string name)
        {
            GameObject root =
                GameObject.Find(name);

            if (root == null)
                return;

            if (Application.isPlaying)
                UnityEngine.Object.Destroy(root);
            else
                UnityEngine.Object.DestroyImmediate(root);
        }
    }
}
