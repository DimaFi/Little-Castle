using System;
using System.IO;
using LittleCastle.CameraSystem;
using LittleCastle.Rendering;
using LittleCastle.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace LittleCastle.Editor
{
    /// <summary>
    /// Creates a real streaming concept scene, NOT a frozen 16-chunk bridge
    /// fixture. All terrain/river/road/spawn geometry is generated at runtime.
    /// Explicit menu or batch invocation only; will never overwrite a scene.
    /// </summary>
    public static class ConceptProceduralWorldSceneBuilder
    {
        public const string ScenePath =
            "Assets/_Game/Scenes/ConceptProceduralWorld.unity";
        private const string TerrainPath =
            "Assets/_Game/Settings/World/ConceptWorld_v001/" +
            "Concept_Terrain_Masked.mat";
        private const string WaterPath =
            "Assets/_Game/Models/Concept/BridgeValidation/Water.mat";

        [MenuItem("Little Castle/World/Concept/Create Procedural World Scene")]
        public static void Create()
        {
            if (File.Exists(ScenePath))
                throw new InvalidOperationException(
                    "Procedural concept scene already exists: " +
                    ScenePath + ". Refusing to overwrite user work.");

            if (EditorSceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException(
                    "Active scene has unsaved edits. Save or discard manually " +
                    "before creating the isolated concept scene.");

            WorldDefinition definition =
                AssetDatabase.LoadAssetAtPath<WorldDefinition>(
                    ConceptWorldProfileBuilder.DefinitionPath);
            Material terrain =
                AssetDatabase.LoadAssetAtPath<Material>(TerrainPath);
            Material water =
                AssetDatabase.LoadAssetAtPath<Material>(WaterPath);
            GameObject bridge =
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    BridgeStoneAssetIntegration.PrefabPath);

            if (definition == null ||
                definition.GenerationSettings == null ||
                definition.StreamingSettings == null ||
                definition.MapRules == null ||
                definition.SpawnCatalog == null)
                throw new InvalidOperationException(
                    "First import the isolated concept world definition.");

            if (terrain == null || terrain.shader == null ||
                terrain.shader.name != "Little Castle/Terrain/LC Terrain" ||
                !terrain.HasProperty("_UseVertexMasks") ||
                terrain.GetFloat("_UseVertexMasks") < 0.5f)
                throw new InvalidOperationException(
                    "Concept masked terrain material is missing or invalid.");

            if (water == null)
                throw new InvalidOperationException(
                    "Reviewed water material from verified bridge fixture " +
                    "is not available. Run the desktop bridge import first.");

            if (bridge == null ||
                !definition.SpawnCatalog.TryResolve(
                    FixedBridgeSiteProfile.AssetId, 12345,
                    out _, out GameObject resolvedBridge) ||
                resolvedBridge != bridge)
                throw new InvalidOperationException(
                    "The concept spawn catalog must reference the actual " +
                    "verified Bridge_Stone_A_v002 prefab. No placeholders.");

            if (!definition.MapRules.TryResolvePreset(
                "concept_preview", 2, out _, out string error))
                throw new InvalidOperationException(
                    "Concept 2-player finite map preset invalid: " + error);

            // Explicit new asset only, no edits to Main or BridgeTest scene.
            EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var focus = new GameObject("Camera Streaming Focus");
            focus.transform.position = Vector3.zero;
            var chunks = new GameObject("Streamed Chunk Root");
            var root = new GameObject("Concept WorldStreamer");
            var streamer = root.AddComponent<WorldStreamer>();
            var state = new SerializedObject(streamer);
            state.FindProperty("worldSeed").intValue = 12345;
            state.FindProperty("worldDefinition").objectReferenceValue =
                definition;
            state.FindProperty("useFiniteSessionMap").boolValue = true;
            state.FindProperty("sessionPlayerCount").intValue = 2;
            state.FindProperty("mapSizePresetId").stringValue =
                "concept_preview";
            state.FindProperty("focus").objectReferenceValue =
                focus.transform;
            state.FindProperty("streamOnStart").boolValue = true;
            state.FindProperty("chunkPresentationRoot").objectReferenceValue =
                chunks.transform;
            state.FindProperty("terrainMaterial").objectReferenceValue =
                terrain;
            state.FindProperty("riverWaterMaterial").objectReferenceValue =
                water;
            state.ApplyModifiedPropertiesWithoutUndo();

            // Reuse existing RTS controls, with focus tied to the
            // authority-neutral streaming camera target; no flying upward.
            var cameraGo = new GameObject("Concept RTS Camera");
            var camera = cameraGo.AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.73f, .80f, .83f);
            camera.nearClipPlane = .1f;
            camera.farClipPlane = 1800f;
            camera.transform.position = new Vector3(35f, 64f, -82f);
            camera.transform.LookAt(new Vector3(0f, 0f, 0f));
            var cameraControl =
                cameraGo.AddComponent<StrategyCameraController>();
            var cameraState = new SerializedObject(cameraControl);
            cameraState.FindProperty("streamingFocus").objectReferenceValue =
                focus.transform;
            cameraState.FindProperty("worldStreamer").objectReferenceValue =
                streamer;
            cameraState.FindProperty("enableEdgeScroll").boolValue = false;
            cameraState.FindProperty("followGroundHeight").boolValue = true;
            cameraState.FindProperty("initialDistance").floatValue = 100f;
            cameraState.FindProperty("initialPitch").floatValue = 38f;
            cameraState.FindProperty("minimumPitch").floatValue = 12f;
            cameraState.FindProperty("maximumPitch").floatValue = 65f;
            cameraState.ApplyModifiedPropertiesWithoutUndo();

            var sun = new GameObject("Concept Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.transform.rotation = Quaternion.Euler(48f, -32f, 0f);
            sun.intensity = 0.9f;
            sun.shadows = LightShadows.Soft;
            RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.52f, .58f, .65f);

            var moon = new GameObject("Concept Moon").AddComponent<Light>();
            moon.type = LightType.Directional;
            moon.shadows = LightShadows.None;

            var clock = new GameObject("Concept Time").AddComponent<WorldTimeSystem>();
            var timeData = new SerializedObject(clock);
            timeData.FindProperty("worldDefinition").objectReferenceValue =
                definition;
            timeData.FindProperty("runAutomatically").boolValue = true;
            timeData.ApplyModifiedPropertiesWithoutUndo();

            var atmosphere = new GameObject("Concept Day-Night")
                .AddComponent<DayNightLightingController>();
            var atmosphereData = new SerializedObject(atmosphere);
            atmosphereData.FindProperty("timeSystem").objectReferenceValue =
                clock;
            atmosphereData.FindProperty("sun").objectReferenceValue =
                sun;
            atmosphereData.FindProperty("moon").objectReferenceValue =
                moon;
            atmosphereData.ApplyModifiedPropertiesWithoutUndo();

            var lighting = new GameObject("Concept Stylized Lighting")
                .AddComponent<StylizedLightingGlobals>();
            var globals = new SerializedObject(lighting);
            globals.FindProperty("sun").objectReferenceValue = sun;
            globals.FindProperty("moon").objectReferenceValue = moon;
            globals.FindProperty("timeSystem").objectReferenceValue = clock;
            globals.FindProperty("atmosphere").objectReferenceValue =
                atmosphere;
            globals.FindProperty("ambientMultiplier").floatValue = 0.65f;
            globals.FindProperty("sunMultiplier").floatValue = 0.75f;
            globals.ApplyModifiedPropertiesWithoutUndo();

            // Serialized only: the real world comes from the planner
            // on Play, not from authoring baked chunks at edit time.
            Directory.CreateDirectory(
                Path.GetDirectoryName(ScenePath));
            if (!EditorSceneManager.SaveScene(
                    EditorSceneManager.GetActiveScene(), ScenePath))
                throw new InvalidOperationException(
                    "Could not save isolated streamed concept scene.");

            AssetDatabase.SaveAssets();
            Selection.activeGameObject = root;
            Debug.Log(
                "[Concept Procedural] Created isolated true streaming " +
                "scene: " + ScenePath +
                " | seed=12345, players=2, concept_preview finite map; " +
                "runtime content is generated in Play. This is NOT a PASS " +
                "for macro routes, fairness, GPU or the finished visuals. " +
                "Cold macro startup may be expensive: profile it.");
        }
    }
}
