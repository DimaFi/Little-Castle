using System;
using System.Collections.Generic;
using LittleCastle.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LittleCastle.Editor
{
    /// <summary>
    /// Creates the small, project-owned Unity vertical slice used by the first
    /// integration pass. It is intentionally idempotent so settings GUIDs are
    /// preserved when the setup is refreshed.
    /// </summary>
    public static class WorldIntegrationBootstrap
    {
        public const string WorldSettingsFolder = "Assets/_Game/Settings/World";
        public const string StageFolder = WorldSettingsFolder + "/Stages";
        public const string MaterialFolder = "Assets/_Game/Materials";
        public const string PrefabFolder = "Assets/_Game/Prefabs/Test";
        public const string SceneFolder = "Assets/_Game/Scenes";

        public const string WorldDefinitionPath =
            WorldSettingsFolder + "/MainWorldDefinition.asset";
        public const string GenerationSettingsPath =
            WorldSettingsFolder + "/MainWorldGenerationSettings.asset";
        public const string MacroSettingsPath =
            WorldSettingsFolder + "/MainMacroWorldPlannerSettings.asset";
        public const string StreamingSettingsPath =
            WorldSettingsFolder + "/MainWorldStreamingSettings.asset";
        public const string SpawnCatalogPath =
            WorldSettingsFolder + "/MainWorldSpawnCatalog.asset";
        public const string TerrainMaterialPath =
            MaterialFolder + "/WorldTerrainDebug.mat";
        public const string PlaceholderPrefabPath =
            PrefabFolder + "/WorldSpawnPlaceholder.prefab";
        public const string TestScenePath =
            SceneFolder + "/WorldGenerationTest.unity";

        [MenuItem("Little Castle/World/Create Or Refresh Integration Setup")]
        public static void CreateOrUpdate()
        {
            EnsureFolders();

            Material terrainMaterial = CreateOrLoadTerrainMaterial();
            GameObject placeholderPrefab = CreateOrLoadPlaceholderPrefab();

            LayeredTerrainStage terrain =
                CreateOrLoad<LayeredTerrainStage>(StageFolder + "/01_LayeredTerrain.asset");
            RiverTerrainCarvingStage rivers =
                CreateOrLoad<RiverTerrainCarvingStage>(StageFolder + "/02_RiverTerrainCarving.asset");
            TerrainClassificationStage classification =
                CreateOrLoad<TerrainClassificationStage>(StageFolder + "/03_TerrainClassification.asset");
            MacroFeatureProjectionStage macroProjection =
                CreateOrLoad<MacroFeatureProjectionStage>(StageFolder + "/04_MacroFeatureProjection.asset");
            ClimateStage climate =
                CreateOrLoad<ClimateStage>(StageFolder + "/05_Climate.asset");
            BiomeClassificationStage biome =
                CreateOrLoad<BiomeClassificationStage>(StageFolder + "/06_BiomeClassification.asset");
            TerrainSurfaceStage surface =
                CreateOrLoad<TerrainSurfaceStage>(StageFolder + "/07_TerrainSurface.asset");
            ForestDensityStage forest =
                CreateOrLoad<ForestDensityStage>(StageFolder + "/08_ForestDensity.asset");
            GroundCoverStage groundCover =
                CreateOrLoad<GroundCoverStage>(StageFolder + "/09_GroundCover.asset");
            ResourceDepositStage resources =
                CreateOrLoad<ResourceDepositStage>(StageFolder + "/10_ResourceDeposits.asset");
            ObjectScatterStage scatter =
                CreateOrLoad<ObjectScatterStage>(StageFolder + "/11_ObjectScatter.asset");

            ConfigureResourceRules(resources);
            ConfigureScatterRules(scatter);

            WorldGenerationSettings generation =
                CreateOrLoad<WorldGenerationSettings>(GenerationSettingsPath);
            ConfigureGenerationSettings(
                generation,
                new WorldGenerationStage[]
                {
                    terrain,
                    rivers,
                    classification,
                    macroProjection,
                    climate,
                    biome,
                    surface,
                    forest,
                    groundCover,
                    resources,
                    scatter
                });

            MacroWorldPlannerSettings macro =
                CreateOrLoad<MacroWorldPlannerSettings>(MacroSettingsPath);
            ConfigureMacroSettings(macro);

            WorldStreamingSettings streaming =
                CreateOrLoad<WorldStreamingSettings>(StreamingSettingsPath);
            ConfigureStreamingSettings(streaming);

            WorldSpawnCatalog catalog =
                CreateOrLoad<WorldSpawnCatalog>(SpawnCatalogPath);
            ConfigureSpawnCatalog(catalog, placeholderPrefab);

            WorldDefinition definition =
                CreateOrLoad<WorldDefinition>(WorldDefinitionPath);
            ConfigureWorldDefinition(
                definition,
                generation,
                macro,
                streaming,
                catalog);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            definition = AssetDatabase.LoadAssetAtPath<WorldDefinition>(WorldDefinitionPath);
            terrainMaterial = AssetDatabase.LoadAssetAtPath<Material>(TerrainMaterialPath);

            CreateTestScene(definition, terrainMaterial);

            Debug.Log(
                "Little Castle Unity integration setup created/refreshed at " +
                WorldSettingsFolder + ".");
        }

        [MenuItem("Little Castle/World/Run Preview Verification")]
        public static void RunPreviewVerification()
        {
            EditorSceneManager.OpenScene(
                TestScenePath,
                OpenSceneMode.Single);

            WorldGenerator generator =
                UnityEngine.Object.FindFirstObjectByType<WorldGenerator>();

            if (generator == null)
                throw new InvalidOperationException("WorldGenerator is missing from the test scene.");

            int originalSeed = generator.WorldSeed;
            generator.ValidateWorldConfiguration();
            generator.GeneratePreview();

            int previewCount = CountPreviewChunks();
            if (previewCount != 9)
            {
                throw new InvalidOperationException(
                    "Expected 9 preview chunks, but generated " + previewCount + ".");
            }

            generator.RunGenerationDiagnostics();
            generator.RegenerateWithNextSeed();

            if (generator.WorldSeed == originalSeed)
                throw new InvalidOperationException("Next-seed preview did not change the seed.");

            if (CountPreviewChunks() != 9)
                throw new InvalidOperationException("Next-seed preview did not regenerate all chunks.");

            generator.ClearPreview();

            var serialized = new SerializedObject(generator);
            serialized.FindProperty("worldSeed").intValue = originalSeed;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Debug.Log(
                "WorldGenerator preview verification passed: validation, 3x3 preview, " +
                "diagnostics, next seed and cleanup.");
        }

        private static int CountPreviewChunks()
        {
            int count = 0;
            GameObject[] objects = UnityEngine.Object.FindObjectsByType<GameObject>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            for (int i = 0; i < objects.Length; i++)
            {
                if (objects[i].name.StartsWith("Chunk_", StringComparison.Ordinal))
                    count++;
            }

            return count;
        }

        private static void EnsureFolders()
        {
            EnsureFolder("Assets", "_Game");
            EnsureFolder("Assets/_Game", "Settings");
            EnsureFolder("Assets/_Game/Settings", "World");
            EnsureFolder(WorldSettingsFolder, "Stages");
            EnsureFolder("Assets/_Game", "Materials");
            EnsureFolder("Assets/_Game", "Prefabs");
            EnsureFolder("Assets/_Game/Prefabs", "Test");
            EnsureFolder("Assets/_Game", "Scenes");
        }

        private static void EnsureFolder(string parent, string name)
        {
            string path = parent + "/" + name;

            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(parent, name);
        }

        private static T CreateOrLoad<T>(string path)
            where T : ScriptableObject
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);

            if (asset != null)
                return asset;

            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static Material CreateOrLoadTerrainMaterial()
        {
            Material material =
                AssetDatabase.LoadAssetAtPath<Material>(TerrainMaterialPath);

            if (material == null)
            {
                Shader shader = Shader.Find("Standard");

                if (shader == null)
                    shader = Shader.Find("Universal Render Pipeline/Lit");

                if (shader == null)
                {
                    throw new InvalidOperationException(
                        "No built-in terrain debug shader is available.");
                }

                material = new Material(shader)
                {
                    name = "WorldTerrainDebug"
                };

                AssetDatabase.CreateAsset(material, TerrainMaterialPath);
            }

            material.color = new Color(0.34f, 0.55f, 0.27f, 1f);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static GameObject CreateOrLoadPlaceholderPrefab()
        {
            GameObject prefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(PlaceholderPrefabPath);

            if (prefab != null)
                return prefab;

            GameObject temporary = GameObject.CreatePrimitive(PrimitiveType.Cube);
            temporary.name = "WorldSpawnPlaceholder";
            temporary.transform.localScale = new Vector3(0.8f, 1.6f, 0.8f);

            Collider collider = temporary.GetComponent<Collider>();
            if (collider != null)
                UnityEngine.Object.DestroyImmediate(collider);

            prefab = PrefabUtility.SaveAsPrefabAsset(temporary, PlaceholderPrefabPath);
            UnityEngine.Object.DestroyImmediate(temporary);
            return prefab;
        }

        private static void ConfigureGenerationSettings(
            WorldGenerationSettings settings,
            IReadOnlyList<WorldGenerationStage> stages)
        {
            var serialized = new SerializedObject(settings);
            serialized.FindProperty("profileId").stringValue = "main_world";
            serialized.FindProperty("generationVersion").intValue = 1;
            serialized.FindProperty("cellsPerSide").intValue = 32;
            serialized.FindProperty("chunkWorldSize").floatValue = 64f;

            SerializedProperty stageList = serialized.FindProperty("stages");
            stageList.arraySize = stages.Count;

            for (int i = 0; i < stages.Count; i++)
                stageList.GetArrayElementAtIndex(i).objectReferenceValue = stages[i];

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(settings);
        }

        private static void ConfigureResourceRules(ResourceDepositStage stage)
        {
            var serialized = new SerializedObject(stage);
            SerializedProperty rules = serialized.FindProperty("rules");
            rules.arraySize = 2;

            ConfigureResourceRule(
                rules.GetArrayElementAtIndex(0),
                "stone_surface_common",
                ResourceKind.Stone,
                "deposit_stone_01",
                96f,
                0.55f,
                TerrainClassMask.All,
                5f,
                12f,
                300,
                1500);

            ConfigureResourceRule(
                rules.GetArrayElementAtIndex(1),
                "iron_highland_common",
                ResourceKind.IronOre,
                "deposit_iron_01",
                320f,
                0.3f,
                TerrainClassMask.RollingHills |
                TerrainClassMask.Steep |
                TerrainClassMask.Highlands,
                5f,
                10f,
                500,
                3000);

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(stage);
        }

        private static void ConfigureResourceRule(
            SerializedProperty rule,
            string ruleId,
            ResourceKind kind,
            string archetypeId,
            float spacing,
            float chance,
            TerrainClassMask terrainMask,
            float minRadius,
            float maxRadius,
            int minCapacity,
            int maxCapacity)
        {
            rule.FindPropertyRelative("ruleId").stringValue = ruleId;
            rule.FindPropertyRelative("resourceKind").enumValueIndex = (int)kind;
            rule.FindPropertyRelative("visualArchetypeId").stringValue = archetypeId;
            rule.FindPropertyRelative("spacing").floatValue = spacing;
            rule.FindPropertyRelative("chance").floatValue = chance;
            rule.FindPropertyRelative("allowedTerrain").intValue = (int)terrainMask;
            rule.FindPropertyRelative("allowedBiomes").intValue = (int)BiomeMask.All;
            rule.FindPropertyRelative("minHeight").floatValue = -1000f;
            rule.FindPropertyRelative("maxHeight").floatValue = 1000f;
            rule.FindPropertyRelative("maxSlope").floatValue = 45f;
            rule.FindPropertyRelative("minRadius").floatValue = minRadius;
            rule.FindPropertyRelative("maxRadius").floatValue = maxRadius;
            rule.FindPropertyRelative("minRichness").floatValue = 0.25f;
            rule.FindPropertyRelative("maxRichness").floatValue = 1f;
            rule.FindPropertyRelative("minCapacity").intValue = minCapacity;
            rule.FindPropertyRelative("maxCapacity").intValue = maxCapacity;
            rule.FindPropertyRelative("createVisualSpawn").boolValue = true;
        }

        private static void ConfigureScatterRules(ObjectScatterStage stage)
        {
            var serialized = new SerializedObject(stage);
            SerializedProperty rules = serialized.FindProperty("rules");
            rules.arraySize = 2;

            ConfigureScatterRule(
                rules.GetArrayElementAtIndex(0),
                "tree_main_common",
                "tree_main_01",
                SpawnCategory.Tree,
                6f,
                0.8f,
                true,
                28f,
                0.9f,
                1.15f);

            ConfigureScatterRule(
                rules.GetArrayElementAtIndex(1),
                "rock_ground_common",
                "rock_ground_01",
                SpawnCategory.Rock,
                20f,
                0.4f,
                false,
                45f,
                0.8f,
                1.25f);

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(stage);
        }

        private static void ConfigureScatterRule(
            SerializedProperty rule,
            string ruleId,
            string archetypeId,
            SpawnCategory category,
            float spacing,
            float chance,
            bool useForestDensity,
            float maxSlope,
            float minScale,
            float maxScale)
        {
            rule.FindPropertyRelative("ruleId").stringValue = ruleId;
            rule.FindPropertyRelative("archetypeId").stringValue = archetypeId;
            rule.FindPropertyRelative("category").enumValueIndex = (int)category;
            rule.FindPropertyRelative("spacing").floatValue = spacing;
            rule.FindPropertyRelative("baseChance").floatValue = chance;
            rule.FindPropertyRelative("borderJitter").floatValue = 0.12f;
            rule.FindPropertyRelative("allowedTerrain").intValue = (int)TerrainClassMask.All;
            rule.FindPropertyRelative("allowedBiomes").intValue = (int)BiomeMask.All;
            rule.FindPropertyRelative("minHeight").floatValue = -1000f;
            rule.FindPropertyRelative("maxHeight").floatValue = 1000f;
            rule.FindPropertyRelative("maxSlope").floatValue = maxSlope;
            rule.FindPropertyRelative("multiplyByForestDensity").boolValue = useForestDensity;
            rule.FindPropertyRelative("forestDensityExponent").floatValue = 1f;
            rule.FindPropertyRelative("minScale").floatValue = minScale;
            rule.FindPropertyRelative("maxScale").floatValue = maxScale;
        }

        private static void ConfigureMacroSettings(MacroWorldPlannerSettings settings)
        {
            var serialized = new SerializedObject(settings);
            serialized.FindProperty("planningHalo").floatValue = 384f;

            SerializedProperty rules = serialized.FindProperty("pointFeatureRules");
            rules.arraySize = 2;

            ConfigureMacroRule(
                rules.GetArrayElementAtIndex(0),
                "neutral_village_common",
                WorldFeatureKind.NeutralSettlement,
                "neutral_village_01",
                320f,
                0.85f,
                55f);

            ConfigureMacroRule(
                rules.GetArrayElementAtIndex(1),
                "ruin_fortified_common",
                WorldFeatureKind.Ruin,
                "ruin_fortified_01",
                420f,
                0.55f,
                28f);

            SerializedProperty river = serialized.FindProperty("rivers");
            river.FindPropertyRelative("enabled").boolValue = true;
            river.FindPropertyRelative("sourceSpacing").floatValue = 256f;
            river.FindPropertyRelative("sourceChance").floatValue = 0.8f;
            river.FindPropertyRelative("sourceTerrain").intValue = (int)TerrainClassMask.All;
            river.FindPropertyRelative("minSourceHeight").floatValue = -1000f;
            river.FindPropertyRelative("maxSourceSlope").floatValue = 90f;
            river.FindPropertyRelative("traceStep").floatValue = 24f;
            river.FindPropertyRelative("maxSteps").intValue = 96;
            river.FindPropertyRelative("mergeDistance").floatValue = 28f;
            river.FindPropertyRelative("minimumPoints").intValue = 5;

            SerializedProperty roads = serialized.FindProperty("roadNetwork");
            roads.FindPropertyRelative("enabled").boolValue = true;
            roads.FindPropertyRelative("nearestConnectionsPerSettlement").intValue = 2;
            roads.FindPropertyRelative("maxConnectionDistance").floatValue = 900f;

            SerializedProperty paths = serialized.FindProperty("roadPaths");
            paths.FindPropertyRelative("enabled").boolValue = true;
            paths.FindPropertyRelative("gridStep").floatValue = 24f;
            paths.FindPropertyRelative("searchPadding").floatValue = 128f;
            paths.FindPropertyRelative("maxSlope").floatValue = 38f;
            paths.FindPropertyRelative("maxExpandedNodes").intValue = 6000;

            SerializedProperty bridges = serialized.FindProperty("bridges");
            bridges.FindPropertyRelative("enabled").boolValue = true;
            bridges.FindPropertyRelative("archetypeId").stringValue = "bridge_wood_small";
            bridges.FindPropertyRelative("extraSpan").floatValue = 2f;

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(settings);
        }

        private static void ConfigureMacroRule(
            SerializedProperty rule,
            string ruleId,
            WorldFeatureKind kind,
            string archetypeId,
            float spacing,
            float chance,
            float influenceRadius)
        {
            rule.FindPropertyRelative("ruleId").stringValue = ruleId;
            rule.FindPropertyRelative("kind").enumValueIndex = (int)kind;
            rule.FindPropertyRelative("archetypeId").stringValue = archetypeId;
            rule.FindPropertyRelative("spacing").floatValue = spacing;
            rule.FindPropertyRelative("chance").floatValue = chance;
            rule.FindPropertyRelative("borderJitter").floatValue = 0.2f;
            rule.FindPropertyRelative("influenceRadius").floatValue = influenceRadius;
            rule.FindPropertyRelative("avoidOtherPointFeatures").boolValue = true;
            rule.FindPropertyRelative("separationPadding").floatValue = 24f;
            rule.FindPropertyRelative("allowedTerrain").intValue = (int)TerrainClassMask.All;
            rule.FindPropertyRelative("minHeight").floatValue = -1000f;
            rule.FindPropertyRelative("maxHeight").floatValue = 1000f;
            rule.FindPropertyRelative("maxSlope").floatValue = 90f;
        }

        private static void ConfigureStreamingSettings(WorldStreamingSettings settings)
        {
            var serialized = new SerializedObject(settings);
            serialized.FindProperty("loadRadiusChunks").intValue = 1;
            serialized.FindProperty("unloadPaddingChunks").intValue = 1;
            serialized.FindProperty("circularLoading").boolValue = true;
            serialized.FindProperty("maxChunkLoadsPerFrame").intValue = 1;
            serialized.FindProperty("maxChunkUnloadsPerFrame").intValue = 2;
            serialized.FindProperty("maxCachedChunks").intValue = 9;
            serialized.FindProperty("macroPlanRadiusChunks").intValue = 10;
            serialized.FindProperty("macroEdgeWarningChunks").intValue = 2;
            serialized.FindProperty("renderGeneratedSpawns").boolValue = true;
            serialized.FindProperty("addMeshCollider").boolValue = false;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(settings);
        }

        private static void ConfigureSpawnCatalog(
            WorldSpawnCatalog catalog,
            GameObject placeholderPrefab)
        {
            string[] archetypes =
            {
                "tree_main_01",
                "rock_ground_01",
                "deposit_stone_01",
                "deposit_iron_01",
                "neutral_village_01",
                "ruin_fortified_01",
                "bridge_wood_small"
            };

            var serialized = new SerializedObject(catalog);
            SerializedProperty entries = serialized.FindProperty("entries");
            entries.arraySize = archetypes.Length;

            for (int i = 0; i < archetypes.Length; i++)
            {
                SerializedProperty entry = entries.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("archetypeId").stringValue = archetypes[i];
                entry.FindPropertyRelative("scaleMultiplier").floatValue = 1f;
                entry.FindPropertyRelative("rotationOffsetEuler").vector3Value = Vector3.zero;

                SerializedProperty prefabs = entry.FindPropertyRelative("prefabs");
                prefabs.arraySize = 1;
                prefabs.GetArrayElementAtIndex(0).objectReferenceValue = placeholderPrefab;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
        }

        private static void ConfigureWorldDefinition(
            WorldDefinition definition,
            WorldGenerationSettings generation,
            MacroWorldPlannerSettings macro,
            WorldStreamingSettings streaming,
            WorldSpawnCatalog catalog)
        {
            var serialized = new SerializedObject(definition);
            serialized.FindProperty("worldId").stringValue = "little_castle_main";
            serialized.FindProperty("generationSettings").objectReferenceValue = generation;
            serialized.FindProperty("macroPlannerSettings").objectReferenceValue = macro;
            serialized.FindProperty("streamingSettings").objectReferenceValue = streaming;
            serialized.FindProperty("spawnCatalog").objectReferenceValue = catalog;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);
        }

        private static void CreateTestScene(
            WorldDefinition definition,
            Material terrainMaterial)
        {
            if (definition == null)
                throw new InvalidOperationException("WorldDefinition could not be loaded before scene creation.");

            Scene scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Single);

            var worldRoot = new GameObject("WorldRoot");
            var presentationRoot = new GameObject("ChunkPresentationRoot");
            presentationRoot.transform.SetParent(worldRoot.transform, false);

            var focus = new GameObject("TestFocus");
            focus.transform.position = Vector3.zero;

            WorldGenerator generator = worldRoot.AddComponent<WorldGenerator>();
            var generatorData = new SerializedObject(generator);
            generatorData.FindProperty("worldSeed").intValue = 12345;
            generatorData.FindProperty("worldDefinition").objectReferenceValue = definition;
            generatorData.FindProperty("previewRadius").intValue = 1;
            generatorData.FindProperty("previewMaterial").objectReferenceValue = terrainMaterial;
            generatorData.FindProperty("renderGeneratedSpawns").boolValue = true;
            generatorData.FindProperty("generateOnStart").boolValue = false;
            generatorData.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(generator);

            if (generator.Definition != definition)
                throw new InvalidOperationException("WorldGenerator rejected the WorldDefinition reference.");

            WorldStreamer streamer = worldRoot.AddComponent<WorldStreamer>();
            var streamerData = new SerializedObject(streamer);
            streamerData.FindProperty("worldSeed").intValue = 12345;
            streamerData.FindProperty("worldDefinition").objectReferenceValue = definition;
            streamerData.FindProperty("focus").objectReferenceValue = focus.transform;
            streamerData.FindProperty("streamOnStart").boolValue = true;
            streamerData.FindProperty("chunkPresentationRoot").objectReferenceValue =
                presentationRoot.transform;
            streamerData.FindProperty("terrainMaterial").objectReferenceValue = terrainMaterial;
            streamerData.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(streamer);

            if (streamer.Definition != definition)
                throw new InvalidOperationException("WorldStreamer rejected the WorldDefinition reference.");

            var cameraObject = new GameObject("Camera");
            Camera camera = cameraObject.AddComponent<Camera>();
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(72f, 92f, -72f);
            cameraObject.transform.LookAt(new Vector3(32f, 0f, 32f));
            camera.farClipPlane = 2000f;

            var lightObject = new GameObject("Directional Light");
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            lightObject.transform.rotation = Quaternion.Euler(48f, -32f, 0f);

            EditorSceneManager.SaveScene(scene, TestScenePath);

            // A newly created scene can be serialized before a freshly
            // refreshed ScriptableObject dependency is registered in the
            // scene dependency graph. Rebind after the first scene save.
            definition = AssetDatabase.LoadAssetAtPath<WorldDefinition>(WorldDefinitionPath);
            generatorData = new SerializedObject(generator);
            generatorData.FindProperty("worldDefinition").objectReferenceValue = definition;
            generatorData.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(generator);

            streamerData = new SerializedObject(streamer);
            streamerData.FindProperty("worldDefinition").objectReferenceValue = definition;
            streamerData.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(streamer);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, TestScenePath);
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(TestScenePath, true)
            };
        }
    }
}
