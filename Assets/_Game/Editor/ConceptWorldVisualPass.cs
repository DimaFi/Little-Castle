using System;
using System.IO;
using LittleCastle.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace LittleCastle.Editor
{
    /// <summary>Explicit, isolated concept integration and real Play/GPU capture.</summary>
    [InitializeOnLoad]
    public static class ConceptWorldVisualPass
    {
        private const string Concept = "Assets/_Game/Settings/World/ConceptWorld_v001/";
        private const string Art = ConceptRuntimeArtKit.Destination;
        private const string Village = Concept + "ConceptVillage.prefab";
        private const string CaptureKey = "LC.ConceptVisualCapture";
        private static double deadline, nextFrame;
        private static int phase;
        private static bool framePrepared;
        private static bool captureInitialized;
        private static WorldStreamer streamer;
        private static Camera camera;
        private static Vector3 target;
        private static readonly string[] Images = { "bridge-day", "bridge-sunset", "bridge-night", "village-day" };

        static ConceptWorldVisualPass()
        {
            if (SessionState.GetBool(CaptureKey, false))
            {
                deadline = EditorApplication.timeSinceStartup + 600;
                EditorApplication.update += UpdateCapture;
            }
        }

        [MenuItem("Little Castle/World/Concept/Prepare Visual Pass")]
        public static void Prepare()
        {
            // Mandatory mesh/shader preflight BEFORE using production foliage.
            int errors = 0;
            Application.LogCallback callback = (message, trace, type) =>
            {
                if (type == LogType.Error || type == LogType.Exception) errors++;
            };
            Application.logMessageReceived += callback;
            string preflightRoot = AssetDatabase.LoadAssetAtPath<GameObject>(Art + "Prefabs/oak.prefab") != null ? Art : ConceptRuntimeArtKit.Source;
            try
            {
                Selection.activeObject = Require<GameObject>(preflightRoot + "Prefabs/oak.prefab");
                FoliageModelCompatibilityValidator.ValidateSelected();
                ProductionAssetValidator.ValidateSelected();
                Selection.activeObject = Require<GameObject>(preflightRoot + "Prefabs/house.prefab");
                ProductionAssetValidator.ValidateSelected();
            }
            finally { Application.logMessageReceived -= callback; }
            if (errors != 0) throw new InvalidOperationException("Production preflight failed. Inspect logs before integration.");
            ConceptRuntimeArtKit.CreateMissing();

            var material = Require<Material>(Concept + "Concept_Terrain_Masked.mat");
            material.SetTexture("_GrassTex", Require<Texture2D>(Art + "Study/Textures/Grass_MeadowSoft_BaseColor.png"));
            material.SetTexture("_DirtTex", Require<Texture2D>(Art + "Textures/DirtPath_A_BaseColor.png"));
            material.SetTexture("_SoilTex", Require<Texture2D>(Art + "Textures/DirtGround_A_BaseColor.png"));
            material.SetTexture("_RockTex", Require<Texture2D>(Art + "Textures/T_WallStoneSurface_A_BaseColor.png"));
            material.SetTexture("_DirtNormal", Require<Texture2D>(Art + "Study/Textures/DirtPath_A_Normal.png"));
            material.SetTexture("_DirtAO", Require<Texture2D>(Art + "Study/Textures/DirtPath_A_AO.png"));
            material.SetColor("_GrassColor", new Color(.35f, .43f, .22f));
            material.SetColor("_DirtColor", new Color(.53f, .38f, .24f));
            material.SetColor("_DryGrassColor", new Color(.43f, .46f, .24f));
            Set(material, "_WorldTiling", .36f);
            Set(material, "_TextureDetail", .22f);
            Set(material, "_GrassDetail", .40f);
            Set(material, "_DirtDetail", .65f);
            Set(material, "_SurfaceDetailStrength", .28f);
            Set(material, "_LightResponse", .55f);
            Set(material, "_ContactShadeStrength", .42f);
            Set(material, "_PathEdgeSharpness", .7f);
            EditorUtility.SetDirty(material);

            GameObject village = AssetDatabase.LoadAssetAtPath<GameObject>(Village);
            if (village == null)
            {
                var temporary = new GameObject("Concept Village (ground checked)");
                try
                {
                    var placement = temporary.AddComponent<ConceptVillageFootprintPresenter>();
                    placement.housePrefab = Require<GameObject>(Art + "Prefabs/house.prefab");
                    placement.wellPrefab = Require<GameObject>(Art + "Prefabs/well.prefab");
                    placement.houseHalfExtents = HouseFootprint(placement.housePrefab);
                    village = PrefabUtility.SaveAsPrefabAsset(temporary, Village);
                }
                finally { Object.DestroyImmediate(temporary); }
            }
            // This is our isolated prefab, never an imported art prefab.
            var contents = PrefabUtility.LoadPrefabContents(Village);
            try
            {
                var placement = contents.GetComponent<ConceptVillageFootprintPresenter>();
                placement.housePrefab = Require<GameObject>(Art + "Prefabs/house.prefab");
                placement.wellPrefab = Require<GameObject>(Art + "Prefabs/well.prefab");
                placement.houseHalfExtents = HouseFootprint(placement.housePrefab);
                PrefabUtility.SaveAsPrefabAsset(contents, Village);
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
            var catalog = Require<WorldSpawnCatalog>(Concept + "Concept_MainWorldSpawnCatalog.asset");
            var serialized = new SerializedObject(catalog);
            var entries = serialized.FindProperty("entries");
            for (int i = 0; i < entries.arraySize; i++)
            {
                var entry = entries.GetArrayElementAtIndex(i);
                string id = entry.FindPropertyRelative("archetypeId").stringValue;
                GameObject prefab = id == "tree_main_01" ? Require<GameObject>(Art + "Prefabs/oak.prefab") :
                    id == "rock_ground_01" ? Require<GameObject>(Art + "Prefabs/SM_Stone_Medium_A.prefab") :
                    id == "neutral_village_01" ? village : null;
                if (prefab == null) continue; // Resource/ruin placeholders are explicitly NOT production accepted.
                var variants = entry.FindPropertyRelative("prefabs");
                variants.arraySize = 1;
                variants.GetArrayElementAtIndex(0).objectReferenceValue = prefab;
                entry.FindPropertyRelative("scaleMultiplier").floatValue = 1f;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            const string skyPath = Concept + "Concept_Sky.mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(skyPath) == null)
                AssetDatabase.CreateAsset(new Material(Require<Material>("Assets/_Game/Materials/StylizedDayNightSky.mat")), skyPath);
            AssetDatabase.SaveAssets();
            if (!File.Exists(ConceptProceduralWorldSceneBuilder.ScenePath))
                ConceptProceduralWorldSceneBuilder.Create();
            Debug.Log("[Concept Visual] Prepared isolated production references/materials/scene. This is not visual acceptance.");
        }

        public static void Capture()
        {
            EditorSceneManager.OpenScene(ConceptProceduralWorldSceneBuilder.ScenePath);
            SessionState.SetBool(CaptureKey, true);
            deadline = EditorApplication.timeSinceStartup + 600;
            EditorApplication.update -= UpdateCapture;
            EditorApplication.update += UpdateCapture;
            EditorApplication.isPlaying = true;
        }

        private static void UpdateCapture()
        {
            try
            {
                if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Real Play streaming capture exceeded 600s.");
                if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
                if (!captureInitialized)
                {
                    streamer = Object.FindFirstObjectByType<WorldStreamer>();
                    camera = Camera.main;
                    if (streamer != null && streamer.Definition == null)
                        throw new InvalidOperationException("Scene lost its persistent WorldDefinition reference. Capture rejected.");
                    if (streamer == null || streamer.MacroPlan == null || camera == null) return;
                    var plan = streamer.MacroPlan;
                    if (!plan.BridgeAwareRoutingSatisfied || plan.BridgeSites.Count == 0)
                        throw new InvalidOperationException("Saved concept scene failed strict real-route/fixed-bridge contract.");
                    WorldBridgeSiteData bridge = default;
                    bool playableBridge = false;
                    foreach (var site in plan.BridgeSites)
                        if (site.isFixedSite && streamer.IsInsidePlayableBounds(new Vector3(site.worldPosition.x, 0, site.worldPosition.y)))
                        { bridge = site; playableBridge = true; break; }
                    if (!playableBridge) throw new InvalidOperationException("No fixed bridge INSIDE playable map. Halo-only bridge is not acceptance.");
                    target = new Vector3(bridge.worldPosition.x, bridge.baseElevation + 1, bridge.worldPosition.y);
                    var controller = camera.GetComponent<LittleCastle.CameraSystem.StrategyCameraController>();
                    if (controller != null) controller.enabled = false;
                    MoveFocus();
                    captureInitialized = true;
                }
                if (streamer.MissingDesiredChunkCount != 0 || EditorApplication.timeSinceStartup < nextFrame) return;
                var clock = Object.FindFirstObjectByType<WorldTimeSystem>();
                if (clock == null) throw new InvalidOperationException("Missing actual runtime clock.");
                if (!framePrepared)
                {
                    clock.RunAutomatically = false;
                    clock.RestoreState(new WorldTimeState(1, (phase == 1 ? 18 : phase == 2 ? 0 : 12) * 60));
                    if (phase == 3)
                        foreach (var village in Object.FindObjectsByType<ConceptVillageFootprintPresenter>(FindObjectsSortMode.None))
                            if (Vector2.Distance(new Vector2(village.transform.position.x, village.transform.position.z), new Vector2(target.x, target.z)) < 1)
                            { target.y = village.transform.position.y; camera.transform.position = target + new Vector3(24, 25, -32); camera.transform.LookAt(target); break; }
                    framePrepared = true;
                    nextFrame = EditorApplication.timeSinceStartup + 1;
                    return; // Lighting components apply the clock on the next runtime frame.
                }
                SaveFrame(Images[phase]);
                phase++;
                framePrepared = false;
                nextFrame = 0;
                if (phase == 3)
                {
                    bool found = false;
                    foreach (var feature in streamer.MacroPlan.PointFeatures)
                        if (feature.kind == WorldFeatureKind.NeutralSettlement && streamer.IsInsidePlayableBounds(new Vector3(feature.worldPosition.x, 0, feature.worldPosition.y)))
                        { target = new Vector3(feature.worldPosition.x, 0, feature.worldPosition.y); found = true; break; }
                    if (!found) throw new InvalidOperationException("No actual generated village to capture.");
                    MoveFocus();
                    nextFrame = EditorApplication.timeSinceStartup + 35;
                }
                if (phase == 4)
                {
                    Directory.CreateDirectory(Output());
                    File.WriteAllText(Path.Combine(Output(), "runtime.txt"),
                        FormattableString.Invariant($"Real Play scene; seed={streamer.MacroPlan.WorldSeed}; generationVersion={streamer.Definition.GenerationSettings.GenerationVersion}; rivers={streamer.MacroPlan.Rivers.Count}; fixedSites={streamer.MacroPlan.BridgeSites.Count}; roads={streamer.MacroPlan.Roads.Count}; activeChunks={streamer.ActiveChunkCount}; cachedChunks={streamer.CachedChunkCount}; terrainColliders={streamer.ActiveTerrainColliderCount}; houses={CountHouses()}; strictRoutes={streamer.MacroPlan.BridgeAwareRoutingSatisfied}; worstStage={streamer.WorstGenerationStageName}; worstStageMs={streamer.WorstGenerationStageMilliseconds:F3}; lastMeshMs={streamer.LastMeshBuildMilliseconds:F3}; lastSpawnMs={streamer.LastSpawnPresentationMilliseconds:F3}\nNot an FPS/balance/full-map acceptance."));
                    Stop(0);
                }
            }
            catch (Exception error) { Debug.LogException(error); Stop(1); }
        }

        private static int CountHouses()
        {
            int result = 0;
            foreach (var village in Object.FindObjectsByType<ConceptVillageFootprintPresenter>(FindObjectsSortMode.None)) result += village.PlacedHouseCount;
            return result;
        }

        private static void MoveFocus()
        {
            GameObject.Find("Camera Streaming Focus").transform.position = target;
            streamer.RefreshStreamingNow();
            camera.transform.position = target + new Vector3(16, 16, -22);
            camera.transform.LookAt(target);
            nextFrame = 0;
        }

        private static string Output() => Environment.GetEnvironmentVariable("LC_VISUAL_OUTPUT") ?? "Temp/ConceptVisualPass";
        private static void SaveFrame(string name)
        {
            Directory.CreateDirectory(Output());
            var texture = new RenderTexture(1440, 900, 24);
            var pixels = new Texture2D(1440, 900, TextureFormat.RGB24, false);
            RenderTexture old = RenderTexture.active;
            RenderTexture previousTarget = camera.targetTexture;
            try
            {
                camera.targetTexture = texture;
                camera.Render();
                RenderTexture.active = texture;
                pixels.ReadPixels(new Rect(0, 0, 1440, 900), 0, 0);
                pixels.Apply();
                File.WriteAllBytes(Path.Combine(Output(), name + ".png"), pixels.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = old;
                texture.Release();
                Object.DestroyImmediate(texture);
                Object.DestroyImmediate(pixels);
            }
        }
        private static void Stop(int code)
        {
            SessionState.SetBool(CaptureKey, false);
            EditorApplication.update -= UpdateCapture;
            EditorApplication.Exit(code);
        }
        private static T Require<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>(path) ?? throw new InvalidOperationException("Missing reviewed asset: " + path);
        private static Vector2 HouseFootprint(GameObject prefab)
        {
            var collider = prefab.GetComponent<BoxCollider>();
            if (collider == null) throw new InvalidOperationException("House lacks reviewed root footprint bounds.");
            // Include overhangs conservatively until an authored foundation footprint exists.
            return new Vector2(Mathf.Abs(collider.center.x) + collider.size.x * .5f + .1f,
                Mathf.Abs(collider.center.z) + collider.size.z * .5f + .1f);
        }
        private static void Set(Material material, string property, float value) { if (material.HasProperty(property)) material.SetFloat(property, value); }
    }
}
