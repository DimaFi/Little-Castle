using System;
using System.IO;
using LittleCastle.World;
using LittleCastle.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace LittleCastle.Editor
{
    /// <summary>Reproducible crossing fixture, not a generated or balanced village.</summary>
    public static class ConceptBridgeSceneBuilder
    {
        public const string ScenePath = "Assets/_Game/Scenes/ConceptBridgeTest.unity";
        private const string Output = "Assets/_Game/Models/Concept/BridgeValidation";
        private const string Evidence = "docs/reports/desktop-verification-2026-10-08";
        private const string NightRefinementEvidence =
            "docs/reports/concept-night-refinement-evidence-2026-10-09-v2";

        [MenuItem("Little Castle/World/Bridge/Create Contract Test Scene")]
        public static void Create()
        {
            if (File.Exists(ScenePath) || (Directory.Exists(Output) &&
                Directory.GetFileSystemEntries(Output).Length > 0))
                throw new InvalidOperationException("Refusing to overwrite an existing bridge test scene.");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Directory.CreateDirectory(Output);
            AssetDatabase.Refresh();
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BridgeStoneAssetIntegration.PrefabPath);
            if (prefab == null) throw new InvalidOperationException("Import the reviewed real bridge first.");
            var definition = AssetDatabase.LoadAssetAtPath<WorldDefinition>(ConceptWorldProfileBuilder.DefinitionPath);
            if (definition == null) throw new InvalidOperationException("Concept profile missing.");

            const int seed = 12345;
            var probe = new WorldTerrainProbe(new WorldGenerationPipeline(definition.GenerationSettings),
                definition.GenerationSettings, seed);
            float baseY = (probe.Sample(new Vector2(0f, -5.4f)).height +
                probe.Sample(new Vector2(0f, 5.4f)).height) * 0.5f;
            var plan = new MacroWorldPlan(seed);
            var river = new WorldRiverData { stableId = 901, nominalWidth = 5.7f, nominalDepth = 1.22f };
            river.centerline.Add(new Vector2(-100f, 0f));
            river.centerline.Add(new Vector2(100f, 0f));
            plan.AddRiver(river);
            var road = new WorldRoadData { stableId = 902, width = 2.6f, roadKind = RoadKind.DirtRoad };
            road.centerline.Add(new Vector2(0f, -100f));
            road.centerline.Add(new Vector2(0f, 100f));
            plan.AddRoad(road);
            var site = new WorldBridgeSiteData(903, 902, 901, FixedBridgeSiteProfile.AssetId,
                Vector2.zero, 0f, 10.8f, baseY, FixedBridgeSiteProfile.ContractVersion, true);
            plan.AddBridgeSite(site);
            var pipeline = new WorldGenerationPipeline(definition.GenerationSettings, plan);

            var terrain = new Material(Shader.Find("Little Castle/Terrain/LC Terrain")) {
                name = "ConceptBridgeTerrain", enableInstancing = true };
            terrain.SetColor("_GrassColor", new Color(.30f, .36f, .16f));
            terrain.SetColor("_DirtColor", new Color(.42f, .34f, .23f));
            terrain.SetColor("_RockColor", new Color(.54f, .51f, .43f));
            terrain.SetFloat("_UseVertexMasks", 1f);
            AssetDatabase.CreateAsset(terrain, Output + "/Terrain.mat");
            var water = new Material(Shader.Find("Little Castle/Surface/LC Stylized Lit")) {
                name = "ConceptRiverWater", enableInstancing = true };
            water.SetColor("_Color", new Color(.10f, .26f, .32f));
            water.SetFloat("_Roughness", .3f);
            water.SetFloat("_SpecularStrength", .25f);
            water.SetFloat("_Cull", 0f);
            AssetDatabase.CreateAsset(water, Output + "/Water.mat");

            // One generated chunk mesh and one water owner per chunk. The
            // fixed fixture crosses x=0/z=0 seams, including negative chunks.
            for (int z = -2; z < 2; z++)
                for (int x = -2; x < 2; x++)
                {
                    var coordinate = new ChunkCoordinate(x, z);
                    WorldChunkData chunk = pipeline.GenerateChunk(seed, coordinate);
                    var root = new GameObject($"ContractChunk_{x}_{z}");
                    root.transform.position = coordinate.GetWorldOrigin(32f);
                    Mesh mesh = ChunkMeshBuilder.Build(chunk, 32f);
                    var colors = new Color[mesh.vertexCount];
                    Vector3[] vertices = mesh.vertices;
                    for (int i = 0; i < vertices.Length; i++)
                    {
                        Vector3 world = vertices[i] + root.transform.position;
                        float path = 1f - Mathf.SmoothStep(0f, 1f,
                            Mathf.InverseLerp(1.1f, 1.7f, Mathf.Abs(world.x)));
                        float wet = 1f - Mathf.SmoothStep(0f, 1f,
                            Mathf.InverseLerp(2.5f, 3.7f, Mathf.Abs(world.z)));
                        colors[i] = new Color(path * (1f - wet), wet, 0f, 1f);
                    }
                    mesh.colors = colors;
                    AssetDatabase.CreateAsset(mesh, Output + $"/Terrain_{x}_{z}.asset");
                    root.AddComponent<MeshFilter>().sharedMesh = mesh;
                    root.AddComponent<MeshRenderer>().sharedMaterial = terrain;
                    root.AddComponent<MeshCollider>().sharedMesh = mesh;
                    Mesh waterMesh = RiverWaterMeshBuilder.Build(chunk, 32f, plan);
                    if (waterMesh != null)
                    {
                        // Baked preview assets are NOT runtime-owned meshes.
                        AssetDatabase.CreateAsset(waterMesh, Output + $"/Water_{x}_{z}.asset");
                        var surface = new GameObject("RiverWater");
                        surface.transform.SetParent(root.transform, false);
                        surface.AddComponent<MeshFilter>().sharedMesh = waterMesh;
                        var renderer = surface.AddComponent<MeshRenderer>();
                        renderer.sharedMaterial = water;
                        renderer.shadowCastingMode = ShadowCastingMode.Off;
                    }
                }

            var bridge = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            bridge.transform.position = new Vector3(0f, baseY, 0f);
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.transform.rotation = Quaternion.Euler(48f, -32f, 0f);
            sun.intensity = 1f;
            sun.shadows = LightShadows.Soft;
            RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.52f, .58f, .66f);
            var clock = new GameObject("Concept Clock").AddComponent<WorldTimeSystem>();
            var clockSettings = new SerializedObject(clock);
            clockSettings.FindProperty("worldDefinition").objectReferenceValue = definition;
            clockSettings.FindProperty("runAutomatically").boolValue = false;
            clockSettings.ApplyModifiedPropertiesWithoutUndo();
            clock.TryInitialize();
            var moon = new GameObject("Moon").AddComponent<Light>();
            moon.type = LightType.Directional;
            moon.shadows = LightShadows.None;
            var atmosphere = new GameObject("Concept Atmosphere").AddComponent<DayNightLightingController>();
            var atmosphereSettings = new SerializedObject(atmosphere);
            atmosphereSettings.FindProperty("timeSystem").objectReferenceValue = clock;
            atmosphereSettings.FindProperty("sun").objectReferenceValue = sun;
            atmosphereSettings.FindProperty("moon").objectReferenceValue = moon;
            atmosphereSettings.ApplyModifiedPropertiesWithoutUndo();
            atmosphere.ApplyLighting();
            var globals = new GameObject("Stylized Lighting").AddComponent<StylizedLightingGlobals>();
            var settings = new SerializedObject(globals);
            settings.FindProperty("sun").objectReferenceValue = sun;
            settings.FindProperty("moon").objectReferenceValue = moon;
            settings.FindProperty("timeSystem").objectReferenceValue = clock;
            settings.FindProperty("atmosphere").objectReferenceValue = atmosphere;
            settings.FindProperty("ambientMultiplier").floatValue = .55f;
            settings.FindProperty("sunMultiplier").floatValue = .65f;
            settings.ApplyModifiedPropertiesWithoutUndo();
            globals.ApplyGlobals();
            var camera = new GameObject("Camera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.73f, .80f, .83f);
            camera.transform.position = new Vector3(12f, baseY + 10f, -16f);
            camera.transform.LookAt(new Vector3(0f, baseY + .6f, 0f));
            camera.nearClipPlane = .1f;
            camera.farClipPlane = 500f;
            EditorSceneManager.SaveScene(camera.gameObject.scene, ScenePath);
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = bridge;
            FoliageModelCompatibilityValidator.ValidateSelected();
            ProductionAssetValidator.ValidateSelected();
            Debug.Log("CONCEPT_BRIDGE_FIXTURE created: seed=12345, 16 generated chunks, 1 fixed bridge; " +
                "authored road/river fixture, NOT a procedural session or visual approval.");
        }

        // Fixture-only palette calibration. No Main profile, source texture,
        // shared shader or imported bridge material is modified.
        public static void CalibrateFixture()
        {
            EditorSceneManager.OpenScene(ScenePath);
            var terrain = AssetDatabase.LoadAssetAtPath<Material>(Output + "/Terrain.mat");
            terrain.SetColor("_GrassColor", new Color(.30f, .36f, .16f));
            terrain.SetColor("_DirtColor", new Color(.42f, .34f, .23f));
            var water = AssetDatabase.LoadAssetAtPath<Material>(Output + "/Water.mat");
            water.SetColor("_Color", new Color(.10f, .26f, .32f));
            EditorUtility.SetDirty(terrain);
            EditorUtility.SetDirty(water);
            var globals = UnityEngine.Object.FindAnyObjectByType<StylizedLightingGlobals>();
            var settings = new SerializedObject(globals);
            settings.FindProperty("ambientMultiplier").floatValue = .55f;
            settings.FindProperty("sunMultiplier").floatValue = .65f;
            settings.ApplyModifiedPropertiesWithoutUndo();
            globals.ApplyGlobals();
            EditorSceneManager.SaveScene(globals.gameObject.scene, ScenePath);
            AssetDatabase.SaveAssets();
            Capture();
        }

        [MenuItem("Little Castle/World/Bridge/Refine Night Readability")]
        public static void RefineNightFixture()
        {
            EditorSceneManager.OpenScene(ScenePath);
            var atmosphere = UnityEngine.Object.FindAnyObjectByType<DayNightLightingController>();
            if (atmosphere == null)
                throw new InvalidOperationException("Concept bridge atmosphere is missing.");

            // Fixture-only night endpoint. Day ambient and the calibrated
            // .55 ambient/.65 sun multipliers remain untouched.
            var settings = new SerializedObject(atmosphere);
            settings.FindProperty("maxMoonIntensity").floatValue = .28f;
            settings.FindProperty("moonLightColor").colorValue =
                new Color(.62f, .72f, 1f, 1f);
            settings.FindProperty("nightAmbientSky").colorValue =
                new Color(.66f, .73f, .92f, 1f);
            settings.FindProperty("nightAmbientEquator").colorValue =
                new Color(.52f, .60f, .78f, 1f);
            settings.FindProperty("nightAmbientGround").colorValue =
                new Color(.39f, .46f, .64f, 1f);
            settings.ApplyModifiedPropertiesWithoutUndo();
            atmosphere.ApplyLighting();

            var globals = UnityEngine.Object.FindAnyObjectByType<StylizedLightingGlobals>();
            if (globals == null)
                throw new InvalidOperationException("Concept bridge stylized lighting globals are missing.");
            var globalSettings = new SerializedObject(globals);
            globalSettings.FindProperty("nightShadowTint").colorValue =
                new Color(.58f, .66f, .86f, 1f);
            globalSettings.ApplyModifiedPropertiesWithoutUndo();
            globals.ApplyGlobals();

            EditorSceneManager.SaveScene(atmosphere.gameObject.scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("CONCEPT_BRIDGE_NIGHT_REFINED: fixture night palette serialized; " +
                "day ambient/shadow and .55 ambient/.65 sun calibration unchanged.");
        }

        public static void CaptureNightRefinement()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                throw new InvalidOperationException("Screenshots require a real graphics device, omit -nographics.");
            RefineNightFixture();
            EditorSceneManager.OpenScene(ScenePath);
            var camera = UnityEngine.Object.FindAnyObjectByType<Camera>();
            Directory.CreateDirectory(NightRefinementEvidence);
            float baseY = GameObject.Find("Bridge_Stone_A_v002").transform.position.y;
            Render(camera, NightRefinementEvidence, "Bridge_Close_Day", new Vector3(11f, baseY + 8f, -14f), baseY, false);
            Render(camera, NightRefinementEvidence, "Bridge_Close_Night", new Vector3(11f, baseY + 8f, -14f), baseY, true);
            Debug.Log("CONCEPT_BRIDGE_NIGHT_CAPTURE graphics=" + SystemInfo.graphicsDeviceName +
                "; new evidence folder; actual Unity camera renders; not FPS/GPU benchmark.");
        }

        public static void Capture()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                throw new InvalidOperationException("Screenshots require a real graphics device, omit -nographics.");
            EditorSceneManager.OpenScene(ScenePath);
            var camera = UnityEngine.Object.FindAnyObjectByType<Camera>();
            var globals = UnityEngine.Object.FindAnyObjectByType<StylizedLightingGlobals>();
            globals.ApplyGlobals();
            Directory.CreateDirectory(Evidence);
            float baseY = GameObject.Find("Bridge_Stone_A_v002").transform.position.y;
            Render(camera, Evidence, "Bridge_Close_Day", new Vector3(11f, baseY + 8f, -14f), baseY, false);
            Render(camera, Evidence, "Bridge_Gameplay_Day", new Vector3(21f, baseY + 25f, -31f), baseY, false);
            Render(camera, Evidence, "Bridge_Far_Day", new Vector3(45f, baseY + 50f, -60f), baseY, false);
            Render(camera, Evidence, "Bridge_Close_Night", new Vector3(11f, baseY + 8f, -14f), baseY, true);
            Debug.Log("CONCEPT_BRIDGE_CAPTURE graphics=" + SystemInfo.graphicsDeviceName +
                "; actual Unity camera renders; not FPS/GPU benchmark.");
        }

        private static void Render(Camera camera, string evidence, string name, Vector3 position, float baseY, bool night)
        {
            camera.transform.position = position;
            camera.transform.LookAt(new Vector3(0f, baseY + .6f, 0f));
            var clock = UnityEngine.Object.FindAnyObjectByType<WorldTimeSystem>();
            clock.TryInitialize();
            clock.RestoreState(new WorldTimeState(1, night ? 0d : 12d * 60d));
            UnityEngine.Object.FindAnyObjectByType<DayNightLightingController>().ApplyLighting();
            camera.backgroundColor = night ? new Color(.035f, .05f, .09f)
                : new Color(.73f, .80f, .83f);
            var globals = UnityEngine.Object.FindAnyObjectByType<StylizedLightingGlobals>();
            globals.ApplyGlobals();
            var target = new RenderTexture(1440, 1000, 24);
            var image = new Texture2D(1440, 1000, TextureFormat.RGB24, false);
            RenderTexture previous = RenderTexture.active;
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 1440, 1000), 0, 0);
                image.Apply();
                File.WriteAllBytes(evidence + "/" + name + ".png", image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previous;
                target.Release();
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(image);
            }
        }
    }
}
