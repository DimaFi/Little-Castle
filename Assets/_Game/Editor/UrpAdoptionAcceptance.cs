using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using LittleCastle.Rendering;
using LittleCastle.Research;
using LittleCastle.World;
using Unity.Collections;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace LittleCastle.Editor
{
    /// <summary>
    /// Read-only adoption evidence. Opens existing scenes without saving, creates
    /// disposable geometry/materials, and renders through URP's StandardRequest.
    /// Run with a graphics device and without -quit (the method owns batch exit).
    /// </summary>
    public static class UrpAdoptionAcceptance
    {
        const int Width = 1000, Height = 750;
        const string BridgeScene = "Assets/_Game/Scenes/ConceptBridgeTest.unity";
        const string StudyScene = "Assets/_Game/Scenes/InnerverseStarterStudy_20261023.unity";
        const string StudyMaterials = "Assets/_Game/Art/Studies/InnerverseStarter_20261023/";
        static readonly List<Object> Temporary = new List<Object>();
        static readonly List<string> Failures = new List<string>();
        static readonly StringBuilder Report = new StringBuilder();
        static readonly StringBuilder Metrics = new StringBuilder();
        static string output;
        static Camera camera;
        static Light sun;
        static GameObject tree, grass, ground, solid;
        static Material litMaterial, grassMaterial;
        static Mesh[] grassMeshes;

        [MenuItem("Little Castle/Research/URP/Run Adoption Acceptance")]
        public static void Run()
        {
            output = ResolveOutput();
            Directory.CreateDirectory(output);
            Failures.Clear(); Temporary.Clear(); Report.Clear(); Metrics.Clear();
            Metrics.AppendLine("check\tmean_absolute_rgb_0_255\tchanged_pixels\tmax_rgb_delta\trequired");
            var setup = EditorSceneManager.GetSceneManagerSetup();
            int quality = QualitySettings.GetQualityLevel();
            bool srpBatcher = GraphicsSettings.useScriptableRenderPipelineBatching;
            bool changedScenes = false;
            string[] globalNames = { "_LC_SunDirection", "_LC_MoonDirection", "_LC_SunColor", "_LC_MoonColor", "_LC_AmbientColor", "_LC_AmbientSkyColor", "_LC_AmbientEquatorColor", "_LC_AmbientGroundColor", "_LC_ShadowTint", "_LC_Wind", "_LC_GameTime" };
            var globals = globalNames.ToDictionary(n => n, Shader.GetGlobalVector);
            string[] floatNames = { "_LC_Daylight", "_LC_Twilight", "_LC_NightAmount" };
            var floats = floatNames.ToDictionary(n => n, Shader.GetGlobalFloat);
            Application.logMessageReceived += CollectErrors;
            try
            {
                Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Run outside Play Mode.");
                for (int i = 0; i < SceneManager.sceneCount; i++)
                    Require(!SceneManager.GetSceneAt(i).isDirty, "Unsaved scene exists; acceptance preserves user work.");
                Require(SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null, "A graphics device is required; omit -nographics.");
                Require(IsUrp(GraphicsSettings.currentRenderPipeline), "The active render pipeline must be URP.");
                Report.AppendLine("UTC=" + DateTime.UtcNow.ToString("O") + "\nUnity=" + Application.unityVersion + "\nGPU=" + SystemInfo.graphicsDeviceName + "\nAPI=" + SystemInfo.graphicsDeviceType);
                Report.AppendLine("Images are actual camera renders. Numeric differences prove contribution, not artistic quality or GPU timings.");
                changedScenes = true;
                CaptureBridge();
                BuildFixture();
                CaptureNightTiers();
                CaptureWindAndShadows();
                CaptureCameraPath();
                CheckInstancing();
                CheckQualityTiers();
                AuditShaders();
            }
            catch (Exception ex) { Failures.Add(ex.ToString()); }
            finally
            {
                Application.logMessageReceived -= CollectErrors;
                QualitySettings.SetQualityLevel(quality, false);
                GraphicsSettings.useScriptableRenderPipelineBatching = srpBatcher;
                foreach (var item in Temporary.AsEnumerable().Reverse()) if (item != null) Object.DestroyImmediate(item);
                Temporary.Clear();
                // The disposable scene is never saved. Restore original loaded scenes.
                if (changedScenes && setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup);
                foreach (var pair in globals) Shader.SetGlobalVector(pair.Key, pair.Value);
                foreach (var pair in floats) Shader.SetGlobalFloat(pair.Key, pair.Value);
                File.WriteAllText(Path.Combine(output, "acceptance_metrics.tsv"), Metrics.ToString());
                Report.AppendLine("\nLimitations: visual review required for bridge/water, close/far wind, silhouette/shadow alignment and night readability. Foliage/grass additional lights are diagnostic only: the established contract is sun/moon lighting. Instancing verifies submitted geometry and visible pixels, not GPU draw-call count. Quality captures are correctness evidence, not performance benchmarks. No scene, material, catalog, simulation data or pipeline asset is saved.");
                Report.AppendLine("\nRESULT=" + (Failures.Count == 0 ? "PASS_WITH_VISUAL_REVIEW_REQUIRED" : "FAIL") + "\nFailures=" + Failures.Count + "\n" + string.Join("\n", Failures));
                File.WriteAllText(Path.Combine(output, "acceptance_result.txt"), Report.ToString());
                Debug.Log("LC_URP_ACCEPTANCE result=" + (Failures.Count == 0 ? "PASS_WITH_VISUAL_REVIEW_REQUIRED" : "FAIL") + " output=" + output);
                if (Application.isBatchMode) EditorApplication.Exit(Failures.Count == 0 ? 0 : 2);
            }
        }

        static string ResolveOutput()
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-lcUrpAcceptanceOutput");
            string value = index >= 0 && index + 1 < args.Length ? args[index + 1] : Environment.GetEnvironmentVariable("LC_URP_ACCEPTANCE_OUTPUT");
            if (string.IsNullOrWhiteSpace(value)) value = Path.Combine(Application.dataPath, "../../Little-Castle_Assets/Research/URP_Adoption_2026-10-25/Acceptance");
            return Path.GetFullPath(value);
        }

        static void CaptureBridge()
        {
            EditorSceneManager.OpenScene(BridgeScene, OpenSceneMode.Single);
            camera = Object.FindAnyObjectByType<Camera>();
            var bridge = GameObject.Find("Bridge_Stone_A_v002");
            Require(camera != null && bridge != null, "ConceptBridgeTest camera or authored bridge missing.");
            var bridgeRenderers = bridge.GetComponentsInChildren<MeshRenderer>().Where(r => r.enabled).ToArray();
            var waters = Object.FindObjectsByType<MeshRenderer>().Where(r => r.name == "RiverWater").ToArray();
            Require(bridgeRenderers.Length > 0 && waters.Length > 0, "Real authored bridge and generated river-water meshes must exist.");
            var geometry = new StringBuilder("object\tmesh_asset\tvertices\ttriangles\tmaterials\n");
            foreach (var renderer in bridgeRenderers.Concat(waters))
            {
                var mesh = renderer.GetComponent<MeshFilter>()?.sharedMesh;
                Require(mesh != null && mesh.vertexCount > 0, "Empty bridge/water mesh " + renderer.name);
                geometry.AppendLine(renderer.name + "\t" + AssetDatabase.GetAssetPath(mesh) + "\t" + mesh.vertexCount + "\t" + mesh.GetIndexCount(0) / 3 + "\t" + string.Join(";", renderer.sharedMaterials.Select(m => m != null ? m.shader.name : "NULL")));
            }
            File.WriteAllText(Path.Combine(output, "bridge_water_geometry.tsv"), geometry.ToString());
            float y = bridge.transform.position.y;
            SetWorldHour(12);
            Pose(new Vector3(11, y + 8, -14), new Vector3(0, y + .6f, 0));
            var day = Capture("bridge_water_close_day");
            foreach (var water in waters) water.enabled = false;
            var withoutWater = Capture("bridge_water_close_day_water_hidden");
            Compare("actual_water_contribution", day, withoutWater, true);
            foreach (var water in waters) water.enabled = true;
            foreach (var renderer in bridgeRenderers) renderer.enabled = false;
            var withoutBridge = Capture("bridge_water_close_day_bridge_hidden");
            Compare("actual_bridge_contribution", day, withoutBridge, true);
            foreach (var renderer in bridgeRenderers) renderer.enabled = true;
            Pose(new Vector3(21, y + 25, -31), new Vector3(0, y + .6f, 0));
            Capture("bridge_water_gameplay_day");
            SetWorldHour(1);
            Pose(new Vector3(11, y + 8, -14), new Vector3(0, y + .6f, 0));
            Capture("bridge_water_close_night");
            Pose(new Vector3(45, y + 50, -60), new Vector3(0, y + .6f, 0));
            Capture("bridge_water_far_night");
            Report.AppendLine("Bridge/water: existing ConceptBridgeTest, real authored meshes, no regenerated bridge or substituted water. Separate hide controls prove both contribute visible pixels.");
        }

        static void SetWorldHour(float hour)
        {
            var clock = Object.FindAnyObjectByType<WorldTimeSystem>();
            if (clock != null) { clock.TryInitialize(); clock.RunAutomatically = false; clock.RestoreState(new WorldTimeState(1, hour * 60)); }
            Object.FindAnyObjectByType<DayNightLightingController>()?.ApplyLighting();
            Object.FindAnyObjectByType<StylizedLightingGlobals>()?.ApplyGlobals();
        }

        static void BuildFixture()
        {
            EditorSceneManager.OpenScene(StudyScene, OpenSceneMode.Single);
            var source = Object.FindObjectsByType<MeshRenderer>()
                .FirstOrDefault(r => r.name.Contains("Spruce_02") && r.name.EndsWith("LOD0") && r.sharedMaterials.Any(m => m != null && m.shader.name == "Little Castle/Foliage/LC Foliage"));
            Require(source != null, "Existing adapted UNS_Spruce_02 mesh missing from study scene.");
            tree = new GameObject("Acceptance actual UNS Spruce 02 foliage + trunk submeshes");
            tree.AddComponent<MeshFilter>().sharedMesh = source.GetComponent<MeshFilter>().sharedMesh;
            var renderer = tree.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = source.sharedMaterials.Select(CloneMaterial).ToArray();
            var old = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.MoveGameObjectToScene(tree, scene);
            SceneManager.SetActiveScene(scene);
            EditorSceneManager.CloseScene(old, true);
            AuditMesh(tree.GetComponent<MeshFilter>().sharedMesh, renderer.sharedMaterials);
            var selected = Selection.activeObject;
            var validatorLog = new StringBuilder();
            void CollectValidator(string message, string stack, LogType type) { if (message.Contains("[Little Castle Foliage]")) validatorLog.AppendLine(type + ": " + message); }
            Application.logMessageReceived += CollectValidator;
            try { Selection.activeGameObject = tree; FoliageModelCompatibilityValidator.ValidateSelected(); }
            finally { Selection.activeObject = selected; Application.logMessageReceived -= CollectValidator; }
            File.WriteAllText(Path.Combine(output, "actual_foliage_validator.txt"), validatorLog.ToString());
            var bounds = renderer.bounds;
            float scale = 5f / Mathf.Max(.01f, bounds.size.y);
            tree.transform.localScale = Vector3.one * scale;
            tree.transform.position = new Vector3(0, -bounds.min.y * scale, 0);
            grassMeshes = AssetDatabase.FindAssets("UNS_Grass t:Model")
                .Select(AssetDatabase.GUIDToAssetPath).SelectMany(AssetDatabase.LoadAllAssetsAtPath).OfType<Mesh>()
                .Where(m => m.name.StartsWith("Grass_LOD")).OrderBy(m => m.name).Take(4).ToArray();
            Require(grassMeshes.Length == 4, "Expected four actual UNS Grass authored LOD meshes.");
            AuditGrass(grassMeshes[0]);
            grassMaterial = CloneMaterial(AssetDatabase.LoadAssetAtPath<Material>(StudyMaterials + "Study_UNS_Grass.mat"));
            grass = new GameObject("Acceptance actual UNS grass LOD0");
            grass.AddComponent<MeshFilter>().sharedMesh = grassMeshes[0];
            grass.AddComponent<MeshRenderer>().sharedMaterial = grassMaterial;
            grass.transform.localScale = Vector3.one * 2;
            litMaterial = NewMaterial("Little Castle/Surface/LC Stylized Lit");
            litMaterial.SetColor("_Color", new Color(.62f, .62f, .55f));
            ground = Primitive("Shared LC lit shadow receiver", PrimitiveType.Cube, new Vector3(0, -.15f, 0), new Vector3(24, .3f, 24), litMaterial);
            solid = Primitive("Shared LC lit local light receiver", PrimitiveType.Sphere, new Vector3(0, 1.2f, 0), Vector3.one * 2.4f, litMaterial);
            camera = new GameObject("Acceptance strategy camera").AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.045f, .055f, .08f);
            camera.nearClipPlane = .05f; camera.farClipPlane = 180; camera.fieldOfView = 48;
            sun = new GameObject("Acceptance shadow sun").AddComponent<Light>();
            sun.type = LightType.Directional; sun.intensity = 1; sun.shadows = LightShadows.Hard;
            sun.transform.rotation = Quaternion.Euler(48, -35, 0); RenderSettings.sun = sun;
            RenderSettings.fog = false; RenderSettings.skybox = null; RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = Color.black;
            SetPresentation(false, 2);
        }

        static void CaptureNightTiers()
        {
            SetPresentation(true, 2); sun.enabled = false; ground.SetActive(false); tree.SetActive(false); grass.SetActive(false);
            Pose(new Vector3(5, 4, -7), new Vector3(0, 1, 0));
            var baseline = Capture("night_solid_no_local_light");
            var local = new GameObject("Budget managed compact local light").AddComponent<Light>();
            local.type = LightType.Point; local.range = 9; local.color = new Color(1, .55f, .18f); local.intensity = 4; local.shadows = LightShadows.None;
            local.transform.position = new Vector3(0, 3, -2);
            var emitter = local.gameObject.AddComponent<NightLightEmitter>();
            SetSerialized(emitter, "baseIntensity", 4f); SetSerialized(emitter, "realtimeFlickerStrength", 0f);
            emitter.SetBudgetActive(true, 1); emitter.TickPresentation(1);
            Require(local.enabled && emitter.CurrentStrength > .99f && local.shadows == LightShadows.None, "Budget emitter must enable compact shadowless Point light.");
            Compare("Point_on_shared_LC_lit", baseline, Capture("night_solid_point"), true);
            local.type = LightType.Spot; local.spotAngle = 85; local.innerSpotAngle = 50;
            local.transform.LookAt(new Vector3(0, 1, 0));
            Compare("Spot_on_shared_LC_lit", baseline, Capture("night_solid_spot"), true);
            emitter.SetBudgetActive(false, 1); emitter.TickPresentation(1);
            Require(!local.enabled && emitter.CurrentStrength == 0, "Budget emitter must complete fade-out and disable realtime light.");
            solid.SetActive(false);
            foreach (var surface in new[] { tree, grass })
            {
                surface.SetActive(true);
                // Isolate foliage submeshes so a lit trunk cannot masquerade as a foliage response.
                var r = surface.GetComponent<MeshRenderer>(); var original = r.sharedMaterials;
                if (surface == tree)
                {
                    var invisible = CloneMaterial(original.First(m => m.shader.name == "Little Castle/Foliage/LC Foliage")); invisible.SetFloat("_Cutoff", 2);
                    r.sharedMaterials = original.Select(m => m.shader.name == "Little Castle/Foliage/LC Foliage" ? m : invisible).ToArray();
                }
                Pose(new Vector3(6, 4, -8), new Vector3(0, surface == tree ? 2.5f : .5f, 0));
                var off = Capture("night_" + surface.name.Replace(' ', '_') + "_local_off");
                foreach (var type in new[] { LightType.Point, LightType.Spot })
                {
                    local.type = type; local.transform.position = new Vector3(0, 4, -2); local.transform.LookAt(new Vector3(0, 2, 0));
                    emitter.SetBudgetActive(true, 1); emitter.TickPresentation(1);
                    Compare(type + "_" + (surface == tree ? "foliage" : "grass") + "_diagnostic_global_only_contract", off, Capture("night_" + (surface == tree ? "foliage" : "grass") + "_" + type), false);
                    emitter.ForceOffImmediate();
                }
                r.sharedMaterials = original; surface.SetActive(false);
            }
            ground.SetActive(true); Pose(new Vector3(5, 4, -7), new Vector3(0, .3f, 0));
            var noGlow = Capture("night_ground_no_glow_pool");
            var glowMaterial = NewMaterial("Little Castle/Effects/LC Emissive Glow"); glowMaterial.SetFloat("_FlickerStrength", 0);
            var glow = Primitive("Independent emissive glow", PrimitiveType.Sphere, new Vector3(0, 1, 0), Vector3.one * .7f, glowMaterial);
            Compare("independent_emissive_glow_with_realtime_off", noGlow, Capture("night_emissive_only"), true);
            glow.SetActive(false);
            var poolMaterial = NewMaterial("Little Castle/Effects/LC Night Light Pool"); poolMaterial.SetFloat("_FlickerStrength", 0);
            var pool = Primitive("Cheap pool", PrimitiveType.Quad, new Vector3(0, .015f, 0), Vector3.one * 6, poolMaterial);
            pool.transform.rotation = Quaternion.Euler(90, 0, 0);
            var visual = pool.AddComponent<NightLightPoolVisual>(); SetSerialized(visual, "flickerStrength", 0f); visual.ApplyProperties();
            Compare("independent_cheap_pool_with_realtime_off", noGlow, Capture("night_pool_only"), true);
            SetSerialized(emitter, "poolVisual", visual);
            emitter.SetDistancePresentation(emitter.PoolMaxDistance + 1, 1); Require(!visual.IsPresentationVisible, "Far pool must be culled.");
            emitter.SetDistancePresentation(1, 1); Require(visual.IsPresentationVisible, "Near night pool must be visible.");
            emitter.SetDistancePresentation(1, 0); Require(!visual.IsPresentationVisible, "Daytime pool must be culled.");
            Object.DestroyImmediate(local.gameObject); Object.DestroyImmediate(glow); Object.DestroyImmediate(pool);
            Report.AppendLine("Night: separate off/on rendered controls for emissive, cheap pool, budget-managed Point and Spot on own LC Stylized Lit. Public emitter fade and pool distance/day culling exercised; manager selection budget is covered by existing runtime tests, not claimed here.");
        }

        static void CaptureWindAndShadows()
        {
            tree.SetActive(true); grass.SetActive(false); solid.SetActive(false); ground.SetActive(true); sun.enabled = true;
            Pose(new Vector3(8, 7, -11), new Vector3(0, 2.2f, 0));
            SetPresentation(false, 2); var a = Capture("foliage_wind_time_2_with_shadow");
            SetPresentation(false, 5.5f); var b = Capture("foliage_wind_time_5_5_with_shadow");
            Compare("actual_foliage_fixed_time_motion", a, b, true);
            var treeRenderer = tree.GetComponent<MeshRenderer>(); treeRenderer.shadowCastingMode = ShadowCastingMode.Off;
            var noShadow = Capture("foliage_time_5_5_shadow_off");
            Compare("actual_foliage_cast_shadow", b, noShadow, true);
            treeRenderer.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
            SetPresentation(false, 2); var shadowA = Capture("foliage_shadow_only_time_2");
            SetPresentation(false, 5.5f); var shadowB = Capture("foliage_shadow_only_time_5_5");
            Compare("animated_foliage_shadow_without_visible_crown", shadowA, shadowB, true);
            treeRenderer.shadowCastingMode = ShadowCastingMode.On;
            Pose(new Vector3(25, 20, -35), new Vector3(0, 2, 0)); Capture("foliage_far_fixed_time_5_5");
            Report.AppendLine("Wind: actual audited UNS_Spruce_02 LOD0 mesh/material submeshes; existing masks unchanged. Visible geometry and isolated ShadowsOnly images at exact _LC_GameTime.z=2 and 5.5; separate shadow-off control. Pixel differences do not certify perfect silhouette alignment.");
        }

        static void CaptureCameraPath()
        {
            SetPresentation(false, 3); tree.SetActive(true); grass.SetActive(true); grass.transform.position = new Vector3(2, 0, -1);
            for (int i = 0; i < 7; i++)
            {
                float t = i / 6f;
                Pose(Vector3.Lerp(new Vector3(6, 5, -8), new Vector3(-14, 13, -20), t), new Vector3(0, 2, 0));
                Capture("camera_path_" + i.ToString("00"));
            }
            File.WriteAllText(Path.Combine(output, "camera_path.txt"), "Seven downward-looking camera positions; fixed simulation presentation time z=3, wind constant. Image sequence supports motion/close-to-far review; it does not measure temporal aliasing at gameplay frame rates.\n");
            grass.SetActive(false);
        }

        static void CheckInstancing()
        {
            Require(SystemInfo.supportsInstancing, "GPU instancing is required by grass study.");
            tree.SetActive(false); ground.SetActive(false); solid.SetActive(false); grass.SetActive(false);
            SetPresentation(false, 2); Pose(new Vector3(5, 6, -8), new Vector3(0, .4f, 0));
            var empty = Capture("grass_instances_empty_control");
            var go = new GameObject("Fixed 64 actual grass instances");
            var batch = go.AddComponent<InnerverseGrassStudy>();
            batch.lodMeshes = grassMeshes; batch.grassMaterial = grassMaterial;
            batch.authoredMatrices = Enumerable.Range(0, 64).Select(i => Matrix4x4.TRS(new Vector3((i % 8 - 3.5f) * .55f, 0, (i / 8 - 3.5f) * .55f), Quaternion.Euler(0, i * 47 % 360, 0), Vector3.one)).ToArray();
            Require(grassMaterial.enableInstancing, "Actual grass study material must enable instancing.");
            var instanced = Capture("grass_64_instanced");
            Require(batch.lastVisibleClumps == 64 && batch.lastSubmittedTriangles == 64L * (long)grassMeshes[0].GetIndexCount(0) / 3, "Expected all 64 actual LOD0 grass clumps submitted.");
            Compare("instanced_grass_visible_contribution", empty, instanced, true);
            batch.enabled = false;
            var reference = new GameObject("Non-instanced geometry reference");
            foreach (var matrix in batch.authoredMatrices)
            {
                var item = new GameObject("Reference grass"); item.transform.SetParent(reference.transform);
                item.transform.SetPositionAndRotation(matrix.GetColumn(3), matrix.rotation);
                item.AddComponent<MeshFilter>().sharedMesh = grassMeshes[0];
                var renderer = item.AddComponent<MeshRenderer>(); renderer.sharedMaterial = grassMaterial;
                renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false; renderer.lightProbeUsage = LightProbeUsage.Off;
            }
            var ordinary = Capture("grass_64_renderer_reference");
            double mean = Compare("instanced_vs_renderer_reference", instanced, ordinary, false);
            Require(mean < 3, "Instanced grass differs substantially from same-matrix MeshRenderer reference: mean RGB delta=" + mean);
            Report.AppendLine("Instancing: 64 fixed actual UNS grass LOD0 clumps; material instancing flag, platform support, submission count/triangles, empty-control visibility and same-matrix MeshRenderer image reference checked. A GPU frame debugger/profiler is still needed for actual draw-call/batching cost.");
            Object.DestroyImmediate(reference); Object.DestroyImmediate(go);
        }

        static void CheckQualityTiers()
        {
            tree.SetActive(true); ground.SetActive(true); solid.SetActive(true); solid.transform.position = new Vector3(3, 1.2f, 0);
            Pose(new Vector3(8, 7, -11), new Vector3(0, 2, 0)); SetPresentation(false, 2);
            var table = new StringBuilder("quality\teffective_pipeline\trender_scale\tshadow_distance\tcascades\tadditional_mode\tadditional_count\tlocal_shadows\tsrp_batcher\n");
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
                Require(IsUrp(pipeline), "Quality tier must resolve to URP: " + QualitySettings.names[i]);
                Require(pipeline.additionalLightsRenderingMode == LightRenderingMode.PerPixel, "Quality tier must retain existing Point/Spot per-pixel contract: " + QualitySettings.names[i]);
                table.AppendLine(QualitySettings.names[i] + "\t" + AssetDatabase.GetAssetPath(pipeline) + "\t" + pipeline.renderScale + "\t" + pipeline.shadowDistance + "\t" + pipeline.shadowCascadeCount + "\t" + pipeline.additionalLightsRenderingMode + "\t" + pipeline.maxAdditionalLightsCount + "\t" + pipeline.supportsAdditionalLightShadows + "\t" + GraphicsSettings.useScriptableRenderPipelineBatching);
                var image = Capture("quality_" + i.ToString("00") + "_" + SafeName(QualitySettings.names[i]));
                Require(image.Select(p => p.r + p.g + p.b).Distinct().Take(8).Count() >= 8, "Quality capture appears empty/flat: " + QualitySettings.names[i]);
            }
            File.WriteAllText(Path.Combine(output, "quality_tiers.tsv"), table.ToString());
        }

        static void AuditMesh(Mesh mesh, Material[] materials)
        {
            var b = new StringBuilder("mesh=" + mesh.name + "\nasset=" + AssetDatabase.GetAssetPath(mesh) + "\nvertices=" + mesh.vertexCount + "\nbounds=" + mesh.bounds + "\n");
            using (var array = Mesh.AcquireReadOnlyMeshData(mesh))
            {
                var data = array[0];
                foreach (VertexAttribute attribute in new[] { VertexAttribute.Normal, VertexAttribute.Tangent, VertexAttribute.TexCoord0, VertexAttribute.Color })
                    b.AppendLine(attribute + "=" + data.HasVertexAttribute(attribute));
                Require(data.HasVertexAttribute(VertexAttribute.Normal) && data.HasVertexAttribute(VertexAttribute.TexCoord0), "Actual tree needs normals and UV0.");
                if (data.HasVertexAttribute(VertexAttribute.Color))
                {
                    using (var colors = new NativeArray<Color>(data.vertexCount, Allocator.Temp))
                    {
                        data.GetColors(colors);
                        float min = 1, max = 0; int rigid = 0, flexible = 0;
                        for (int i = 0; i < colors.Length; i++) { min = Mathf.Min(min, colors[i].r); max = Mathf.Max(max, colors[i].r); if (colors[i].r < .05f) rigid++; if (colors[i].r > .95f) flexible++; }
                        b.AppendLine("vertex_color_R_min=" + min + "\nvertex_color_R_max=" + max + "\nR_below_0_05=" + rigid + "\nR_above_0_95=" + flexible);
                    }
                }
            }
            for (int i = 0; i < materials.Length; i++)
            {
                var m = materials[i]; b.AppendLine("submesh=" + i + " material=" + m.name + " shader=" + m.shader.name + " indices=" + mesh.GetIndexCount(i));
                foreach (string property in new[] { "_UseHeightWindMask", "_UseVertexWindMask", "_WindAnchorHeight", "_WindHeightRange", "_WindAmplitude", "_CrownSway", "_VertexWave", "_LeafFlutter" })
                    if (m.HasProperty(property)) b.AppendLine(property + "=" + m.GetFloat(property));
            }
            b.AppendLine("Compatibility=COMPATIBLE for disposable visual fixture: actual leaf/trunk material submeshes, normals/UV0, dense distributed geometry. No mask or mesh reauthoring. Prior detailed mesh/validator audit: Innerverse_2026-10-23/UnityStarter. Close/far quality remains visual review.");
            File.WriteAllText(Path.Combine(output, "actual_foliage_mesh_preflight.txt"), b.ToString());
        }

        static void AuditGrass(Mesh mesh)
        {
            var report = new StringBuilder("asset=" + AssetDatabase.GetAssetPath(mesh) + "\nmesh=" + mesh.name + "\nvertices=" + mesh.vertexCount + "\n");
            using (var meshes = Mesh.AcquireReadOnlyMeshData(mesh))
            {
                var data = meshes[0];
                Require(data.HasVertexAttribute(VertexAttribute.Normal) && data.HasVertexAttribute(VertexAttribute.TexCoord0), "Actual grass requires normals and UV0.");
                using (var vertices = new NativeArray<Vector3>(data.vertexCount, Allocator.Temp))
                using (var uv = new NativeArray<Vector2>(data.vertexCount, Allocator.Temp))
                {
                    data.GetVertices(vertices); data.GetUVs(0, uv);
                    double sy = 0, su = 0, syy = 0, suu = 0, syu = 0;
                    float min = float.MaxValue, max = float.MinValue;
                    for (int i = 0; i < vertices.Length; i++)
                    {
                        double y = vertices[i].y, u = uv[i].y; sy += y; su += u; syy += y * y; suu += u * u; syu += y * u;
                        min = Mathf.Min(min, uv[i].y); max = Mathf.Max(max, uv[i].y);
                    }
                    double n = vertices.Length;
                    double correlation = (n * syu - sy * su) / Math.Max(.000001, Math.Sqrt((n * syy - sy * sy) * (n * suu - su * su)));
                    report.AppendLine("uv0_y_min=" + min + "\nuv0_y_max=" + max + "\nuv0_y_local_height_correlation=" + correlation);
                    Require(correlation > .8 && min < .05 && max > .9, "Grass UV Y must run root 0 to tip 1.");
                }
                report.AppendLine("normals=" + data.HasVertexAttribute(VertexAttribute.Normal) + "\ntangents=" + data.HasVertexAttribute(VertexAttribute.Tangent) + "\nvertex_colors=" + data.HasVertexAttribute(VertexAttribute.Color));
            }
            report.AppendLine("Compatibility=COMPATIBLE for UV Y weighted grass wind; original vertices/UVs unchanged, root-anchor extension disabled unless already authored. Actual current mesh inspected without enabling importer Read/Write.");
            File.WriteAllText(Path.Combine(output, "actual_grass_mesh_preflight.txt"), report.ToString());
        }

        static void AuditShaders()
        {
            var messages = new StringBuilder();
            foreach (var path in AssetDatabase.FindAssets("t:Shader", new[] { "Assets/_Game/Shaders" }).Select(AssetDatabase.GUIDToAssetPath))
                foreach (var message in ShaderUtil.GetShaderMessages(AssetDatabase.LoadAssetAtPath<Shader>(path)))
                {
                    string line = path + ":" + message.line + " " + message.severity + " " + message.message;
                    messages.AppendLine(line); if (message.severity.ToString() == "Error") Failures.Add(line);
                }
            File.WriteAllText(Path.Combine(output, "shader_messages.txt"), messages.ToString());
        }

        static void SetPresentation(bool night, float seconds)
        {
            Vector3 direction = -sun.transform.forward;
            Shader.SetGlobalVector("_LC_SunDirection", direction); Shader.SetGlobalVector("_LC_MoonDirection", direction);
            Shader.SetGlobalColor("_LC_SunColor", night ? Color.black : new Color(1, .97f, .88f));
            Shader.SetGlobalColor("_LC_MoonColor", night ? new Color(.04f, .055f, .09f) : Color.black);
            Color ambient = night ? new Color(.025f, .035f, .06f) : new Color(.35f, .4f, .45f);
            foreach (string name in new[] { "_LC_AmbientColor", "_LC_AmbientSkyColor", "_LC_AmbientEquatorColor", "_LC_AmbientGroundColor" }) Shader.SetGlobalColor(name, ambient);
            Shader.SetGlobalColor("_LC_ShadowTint", new Color(.6f, .7f, .85f));
            Shader.SetGlobalFloat("_LC_Daylight", night ? 0 : 1); Shader.SetGlobalFloat("_LC_Twilight", 0); Shader.SetGlobalFloat("_LC_NightAmount", night ? 1 : 0);
            Shader.SetGlobalVector("_LC_Wind", new Vector4(.94f, .34f, .55f, 1)); Shader.SetGlobalVector("_LC_GameTime", new Vector4(0, 0, seconds, 0));
        }

        static Color32[] Capture(string name)
        {
            Require(camera != null, "Missing capture camera.");
            var rt = RenderTexture.GetTemporary(Width, Height, 24, RenderTextureFormat.ARGB32);
            var previous = RenderTexture.active; var target = camera.targetTexture;
            var image = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = rt;
                var request = new RenderPipeline.StandardRequest { destination = rt };
                Require(RenderPipeline.SupportsRenderRequest(camera, request), "URP StandardRequest unsupported.");
                // First request warms a new camera/quality pipeline; second is evidence.
                RenderPipeline.SubmitRenderRequest(camera, request); RenderPipeline.SubmitRenderRequest(camera, request);
                RenderTexture.active = rt; image.ReadPixels(new Rect(0, 0, Width, Height), 0, 0); image.Apply();
                File.WriteAllBytes(Path.Combine(output, SafeName(name) + ".png"), image.EncodeToPNG());
                var pixels = image.GetPixels32();
                Require(!pixels.Any(p => p.r > 235 && p.b > 235 && p.g < 35), "Error-shader magenta pixels in " + name);
                return pixels;
            }
            finally { camera.targetTexture = target; RenderTexture.active = previous; RenderTexture.ReleaseTemporary(rt); Object.DestroyImmediate(image); }
        }

        static double Compare(string name, Color32[] a, Color32[] b, bool required)
        {
            long total = 0; int changed = 0, max = 0;
            for (int i = 0; i < a.Length; i++)
            {
                int r = Math.Abs(a[i].r - b[i].r), g = Math.Abs(a[i].g - b[i].g), blue = Math.Abs(a[i].b - b[i].b);
                int peak = Math.Max(r, Math.Max(g, blue)); total += r + g + blue; if (peak > 3) changed++; max = Math.Max(max, peak);
            }
            double mean = total / (a.Length * 3.0);
            Metrics.AppendLine(name + "\t" + mean.ToString("F6", System.Globalization.CultureInfo.InvariantCulture) + "\t" + changed + "\t" + max + "\t" + required);
            if (required && (changed < 20 || mean < .002)) Failures.Add("No meaningful rendered contribution: " + name + " changed=" + changed + " mean=" + mean);
            return mean;
        }

        static void Pose(Vector3 eye, Vector3 target) { camera.transform.position = eye; camera.transform.LookAt(target); }
        static Material NewMaterial(string shaderName)
        {
            var shader = Shader.Find(shaderName); Require(shader != null && shader.isSupported, "Missing/unsupported " + shaderName);
            var material = new Material(shader) { enableInstancing = true }; Temporary.Add(material); return material;
        }
        static Material CloneMaterial(Material source) { Require(source != null, "Missing source material."); var m = new Material(source); Temporary.Add(m); return m; }
        static GameObject Primitive(string name, PrimitiveType type, Vector3 position, Vector3 scale, Material material)
        {
            var go = GameObject.CreatePrimitive(type); go.name = name; go.transform.position = position; go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = material; Object.DestroyImmediate(go.GetComponent<Collider>()); return go;
        }
        static void SetSerialized(Object target, string field, object value)
        {
            var serialized = new SerializedObject(target); var property = serialized.FindProperty(field);
            Require(property != null, "Missing fixture configuration field " + field);
            if (value is float number) property.floatValue = number; else property.objectReferenceValue = value as Object;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        static string SafeName(string name) { foreach (char c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_'); return name; }
        static bool IsUrp(RenderPipelineAsset pipeline) => pipeline is UniversalRenderPipelineAsset;
        static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        static void CollectErrors(string condition, string stackTrace, LogType type) { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) Failures.Add(condition); }
    }
}
