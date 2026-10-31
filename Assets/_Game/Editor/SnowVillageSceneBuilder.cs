using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using LittleCastle.CameraSystem;
using LittleCastle.Rendering;
using LittleCastle.Research;
using LittleCastle.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace LittleCastle.Editor
{
    /// <summary>Explicit, lease-guarded saved art scene; never replaces the World Editor map.</summary>
    [InitializeOnLoad]
    public static class SnowVillageSceneBuilder
    {
        public const string ScenePath = "Assets/_Game/Scenes/Studies/SnowVillageAtmosphere_20261030.unity";
        const string Generated = "Assets/_Game/Art/Studies/SnowVillage_20261030";
        const string Output = "E:/Games_Develop/Research/SkyAtmosphere_20261030/window-pass-20261031";
        const string Request = "Library/SnowVillageSceneRequest.json";
        const string Key = "LC.SnowVillageSceneBuilder.20261031.windows";
        const string Owner = "01a18b72-7012-7de0-8a5f-9a5a1f5f5265";
        [Serializable] sealed class Command { public string id, action; }
        [Serializable] sealed class Lease { public string ownerThreadId, status, purpose, updatedAtUtc; }
        [Serializable] sealed class Review
        {
            public string status, scene = ScenePath, error, originalScene, gpu, api;
            public bool saved, reopened, sourceAssetsPreserved, sourceMapPreserved, sourceEnvironmentRestored, originalDirtyPreserved, editMode;
            public int houses, trees, clocks, cameras, shaderErrors, softComposites, snowUploadsPending, nightActiveLights, nightEmitters;
            public List<string> messages = new List<string>();
        }
        static double nextPoll;
        static bool busy;
        static SnowVillageSceneBuilder()
        {
            if (!SessionState.GetBool(Key + ".disarmed", false)) EditorApplication.update += Poll;
        }
        static bool OwnsLease()
        {
            if (!File.Exists("Library/SharedPcControl.json")) return false;
            var value = JsonUtility.FromJson<Lease>(File.ReadAllText("Library/SharedPcControl.json"));
            return value != null && value.ownerThreadId == Owner && value.status == "active";
        }
        static void Release()
        {
            if (!OwnsLease()) return;
            var lease = JsonUtility.FromJson<Lease>(File.ReadAllText("Library/SharedPcControl.json"));
            lease.status = "released"; lease.purpose = "Snow village saved and reviewed; original scene restored in Edit Mode.";
            lease.updatedAtUtc = DateTime.UtcNow.ToString("o");
            File.WriteAllText("Library/SharedPcControl.json", JsonUtility.ToJson(lease, true));
        }
        static void Poll()
        {
            if (busy || EditorApplication.timeSinceStartup < nextPoll) return;
            nextPoll = EditorApplication.timeSinceStartup + 1;
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating ||
                !File.Exists(Request) || !OwnsLease()) return;
            var command = JsonUtility.FromJson<Command>(File.ReadAllText(Request));
            if (command == null || string.IsNullOrEmpty(command.id) || command.id == SessionState.GetString(Key + ".last", "")) return;
            SessionState.SetString(Key + ".last", command.id);
            if (command.action == "disarm")
            {
                EditorApplication.update -= Poll; SessionState.SetBool(Key + ".disarmed", true);
                Directory.CreateDirectory(Output); File.WriteAllText(Output + "/disarmed.json", "{\"disarmed\":true,\"playMode\":false}");
                Release(); return;
            }
            if (command.action != "build") return;
            busy = true;
            try { BuildAndCapture(); }
            catch (Exception error)
            { Directory.CreateDirectory(Output); File.WriteAllText(Output + "/result.json", JsonUtility.ToJson(new Review {status="FAIL",error=error.ToString()},true)); }
            finally { busy = false; Release(); }
        }

        [MenuItem("Little Castle/Rendering/Open Snow Village Scene")]
        static void Open()
        {
            if (!File.Exists(ScenePath)) throw new FileNotFoundException("Build the snow village first.");
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(ScenePath);
        }

        [MenuItem("Little Castle/Rendering/Build and Capture Snow Village")]
        public static void BuildAndCapture()
        {
            if (!OwnsLease()) throw new InvalidOperationException("The shared Unity lease is required.");
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Build in Edit Mode.");
            Directory.CreateDirectory(Output);
            var original = SceneManager.GetActiveScene();
            bool dirty = original.isDirty;
            string sceneHash = HashFile(original.path);
            var painter = original.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MeadowSurfacePainter>(true)).FirstOrDefault();
            var oldAtmosphere = original.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<DayNightLightingController>(true)).FirstOrDefault();
            var oldGlobals = original.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<StylizedLightingGlobals>(true)).FirstOrDefault();
            if (!painter || !oldAtmosphere || !oldGlobals) throw new InvalidOperationException("Keep the existing World Editor scene open.");
            string surfacePath = AssetDatabase.GetAssetPath(painter.source), surfaceHash = HashFile(surfacePath);
            string mapHash = MapWindowHash();
            var result = new Review {originalScene = original.path, gpu = SystemInfo.graphicsDeviceName, api = SystemInfo.graphicsDeviceType.ToString()};
            Scene scene = default;
            UrpPreviewQualityOverride quality = null;
            void Log(string message, string stack, LogType type)
            { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) result.messages.Add(type + ": " + message); }
            Application.logMessageReceived += Log;
            try
            {
                AtmosphereArchitectureAssets.Ensure();
                if (File.Exists(ScenePath)) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
                else
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
                    Directory.CreateDirectory(Generated);
                    scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                    SceneManager.SetActiveScene(scene);
                    CreateScene(scene, painter, oldAtmosphere, oldGlobals);
                    if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new IOException("Snow scene was not saved.");
                }
                SceneManager.SetActiveScene(scene);
                var presentation = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<SnowVillagePresentation>(true)).Single();
                RefineComposition(scene, presentation);
                ValidateScene(scene, presentation, result);
                quality = UrpPreviewQualityOverride.Create(TerrainStarterPreviewControls.PreviewQuality.High);
                presentation.atmosphere.SetAtmosphereQuality(TerrainStarterPreviewControls.PreviewQuality.High);
                foreach (var view in new[] {(13f,"01-snow-day"),(18.65f,"02-snow-golden"),(0f,"03-snow-night")})
                {
                    presentation.PresentAtHour(view.Item1);
                    for (int tick = 0; tick < 40; tick++) presentation.RefreshPresentation();
                    int count = TerrainStarterSoftSceneRendererFeature.CompositeDrawCount;
                    Capture(presentation.sceneCamera, Output + "/" + view.Item2 + ".png");
                    result.softComposites += TerrainStarterSoftSceneRendererFeature.CompositeDrawCount - count;
                    result.snowUploadsPending = presentation.PendingSnowUploads;
                    if (view.Item1 == 0)
                    {
                        var budget = presentation.GetComponent<NightLightBudgetManager>();
                        result.nightActiveLights = budget ? budget.ActiveRealtimeLightCount : 0;
                        result.nightEmitters = budget ? budget.RegisteredEmitterCount : 0;
                        if (result.nightActiveLights < 1 || result.nightActiveLights > 6)
                            throw new InvalidOperationException("Night budget did not illuminate the saved scene.");
                        var localLights = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Light>(true))
                            .Where(l => l.type == LightType.Point || l.type == LightType.Spot).ToArray();
                        bool[] enabled = localLights.Select(l => l.enabled).ToArray();
                        try
                        {
                            foreach (var light in localLights) light.enabled = false;
                            Capture(presentation.sceneCamera, Output + "/03b-snow-night-local-lights-off.png");
                        }
                        finally {for (int i = 0; i < localLights.Length; i++) localLights[i].enabled = enabled[i];}
                        var buildings = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<LODGroup>(true))
                            .Where(g => g.name.Contains("TownHall")).ToArray();
                        try
                        {
                            foreach (var building in buildings) building.ForceLOD(3);
                            Capture(presentation.sceneCamera, Output + "/03c-snow-night-LOD3.png");
                        }
                        finally {foreach (var building in buildings) building.ForceLOD(-1);}
                    }
                }
                // The saved default is a readable winter night. Runtime sky clones are never serialized.
                presentation.PresentAtHour(0);
                var skySource = new SerializedObject(presentation.atmosphere).FindProperty("skyboxMaterial").objectReferenceValue as Material;
                RenderSettings.skybox = skySource;
                if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new IOException("Final scene save failed.");
                result.saved = true;
                foreach (string shaderName in new[] {"Little Castle/Surface/LC Stylized Lit", "Little Castle/Distance/LC Distant Simple",
                    "Little Castle/Foliage/LC Foliage", "Little Castle/Foliage/LC Grass", "Little Castle/Terrain/LC Terrain",
                    "Little Castle/Presentation/Soft Scene", "Little Castle/Effects/LC Night Light Pool", "Little Castle/Sky/Stylized Day Night"})
                {
                    var shader = Shader.Find(shaderName);
                    if (!shader) throw new InvalidOperationException("Missing shader: " + shaderName);
                    foreach (var message in ShaderUtil.GetShaderMessages(shader))
                        if (message.severity.ToString() == "Error") {result.shaderErrors++; result.messages.Add(shaderName + ": " + message.message);}
                }
                EditorSceneManager.CloseScene(scene, true); scene = default;
                SceneManager.SetActiveScene(original);
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
                SceneManager.SetActiveScene(scene);
                var reopened = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<SnowVillagePresentation>(true)).Single();
                ValidateScene(scene, reopened, result); result.reopened = true;
            }
            catch (Exception error) { result.error = error.ToString(); }
            finally
            {
                try
                {
                    if (scene.IsValid() && scene.isLoaded)
                    {
                        foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
                            if (go && go.scene == scene && go.hideFlags == HideFlags.HideAndDontSave &&
                                (go.name == "Temporary snowy mountain village" || go.name.StartsWith("Fence preview · ")))
                                Object.DestroyImmediate(go);
                        EditorSceneManager.CloseScene(scene, true);
                    }
                    if (original.IsValid() && original.isLoaded) SceneManager.SetActiveScene(original);
                    quality?.Dispose();
                    oldAtmosphere.ApplyLighting(); oldGlobals.ApplyGlobals();
                    painter.EnvironmentView?.RestorePresentationOwnership();
                    SceneView.RepaintAll();
                }
                finally
                {
                    Application.logMessageReceived -= Log;
                    result.sourceAssetsPreserved = sceneHash == HashFile(original.path) && surfaceHash == HashFile(surfacePath);
                    result.sourceMapPreserved = mapHash == MapWindowHash();
                    result.sourceEnvironmentRestored = painter.EnvironmentView == null ||
                        MapEnvironmentPresentation.ActivePresentation == painter.EnvironmentView;
                    result.originalDirtyPreserved = dirty == original.isDirty;
                    result.editMode = !EditorApplication.isPlayingOrWillChangePlaymode;
                    result.status = string.IsNullOrEmpty(result.error) && result.messages.Count == 0 && result.saved && result.reopened &&
                        result.sourceAssetsPreserved && result.sourceMapPreserved && result.sourceEnvironmentRestored && result.originalDirtyPreserved && result.editMode &&
                        result.shaderErrors == 0 && result.softComposites >= 6 && result.snowUploadsPending == 0 ? "PASS" : "FAIL";
                    File.WriteAllText(Output + "/result.json", JsonUtility.ToJson(result, true));
                    Release();
                }
            }
        }

        static void CreateScene(Scene scene, MeadowSurfacePainter painter, DayNightLightingController source, StylizedLightingGlobals sourceGlobals)
        {
            GameObject Clone(GameObject value)
            {
                var copy = Object.Instantiate(value); copy.name = value.name;
                SceneManager.MoveGameObjectToScene(copy, scene); return copy;
            }
            var clock = Clone(source.TimeSystem.gameObject).GetComponent<WorldTimeSystem>();
            var sun = Clone(source.Sun.gameObject).GetComponent<Light>();
            var moon = Clone(source.Moon.gameObject).GetComponent<Light>();
            var atmosphere = Clone(source.gameObject).GetComponent<DayNightLightingController>();
            var globals = Clone(sourceGlobals.gameObject).GetComponent<StylizedLightingGlobals>();
            var camera = Clone(painter.studyCamera.gameObject).GetComponent<Camera>();
            camera.name = "Snow village camera"; camera.enabled = true;
            StripCameraDrivers(camera);
            Set(atmosphere, "timeSystem", clock); Set(atmosphere, "sun", sun); Set(atmosphere, "moon", moon);
            Set(globals, "timeSystem", clock); Set(globals, "atmosphere", atmosphere); Set(globals, "sun", sun); Set(globals, "moon", moon);
            var serialized = new SerializedObject(atmosphere); serialized.FindProperty("sunAzimuth").floatValue = 142;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            clock.RunAutomatically = false;
            RenderSettings.sun = sun;
            Material focusMaterial;
            using (var fixture = new AtmosphereArtFixture(camera, painter, true))
            {
                var root = fixture.ExportPersistentCopy(Generated + "/Generated");
                SceneManager.MoveGameObjectToScene(root, scene);
                var focus = camera.GetComponent<TerrainStarterSoftScene>();
                focusMaterial = new Material(focus.effectMaterial) {name = "Snow village focus", hideFlags = HideFlags.None};
                AssetDatabase.CreateAsset(focusMaterial, Generated + "/SnowVillageFocus.mat");
            }
            var soft = camera.GetComponent<TerrainStarterSoftScene>();
            if (!soft) soft = camera.gameObject.AddComponent<TerrainStarterSoftScene>();
            soft.effectMaterial = focusMaterial; soft.blurStrength = .55f; soft.focusBand = 8; soft.contactOcclusion = 0;
            soft.focusDistanceOverride = Vector3.Distance(camera.transform.position, new Vector3(280,4,302));
            soft.shadowFilterRadius = 3; soft.enabled = true;
            AddLanterns(scene);
            var presenter = new GameObject("Snow village local presentation").AddComponent<SnowVillagePresentation>();
            presenter.sceneCamera = camera; presenter.clock = clock; presenter.atmosphere = atmosphere; presenter.lightingGlobals = globals;
            presenter.hour = 0; presenter.snowAmount = .9f;
            presenter.RefreshPresentation();
            foreach (var material in new[] {focusMaterial}) AssetDatabase.SaveAssetIfDirty(material);
            RenderSettings.skybox = new SerializedObject(atmosphere).FindProperty("skyboxMaterial").objectReferenceValue as Material;
        }

        static void AddLanterns(Scene scene)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Models/Ready/Test_Prop/PROP_Lantern_01/PROP_Lantern_01.prefab");
            var glassSource = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Models/Ready/Test_Prop/PROP_Lantern_01/PROP_Lantern_01_S01_Warm amber glass_Solid_Near.mat");
            var glass = new Material(glassSource) {name = "Snow village lantern glass", enableInstancing = true};
            glass.SetTexture("_EmissionMap", Texture2D.whiteTexture); glass.SetColor("_EmissionColor", new Color(1.45f,.65f,.18f,1));
            glass.SetFloat("_EmissionDayStrength", .04f); glass.SetFloat("_EmissionNightStrength", 2.2f);
            AssetDatabase.CreateAsset(glass, Generated + "/LanternGlass.mat");
            var poolMaterial = new Material(AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Materials/Shared/LC_NightLightPool_Default.mat"))
                {name = "Snow village warm pools", enableInstancing = true};
            poolMaterial.SetFloat("_Intensity", .30f); poolMaterial.SetColor("_Color", new Color(1.2f,.51f,.12f,1));
            AssetDatabase.CreateAsset(poolMaterial, Generated + "/LanternPool.mat");
            var poleMaterial = new Material(Shader.Find("Little Castle/Surface/LC Stylized Lit")) {name = "Snow village lantern posts", enableInstancing = true};
            poleMaterial.SetColor("_Color", new Color(.24f,.18f,.13f)); poleMaterial.SetFloat("_BumpScale",0);
            AssetDatabase.CreateAsset(poleMaterial, Generated + "/LanternPosts.mat");
            var quad = new Mesh {name = "Snow village ground pool quad"};
            quad.vertices = new[] {new Vector3(-.5f,0,-.5f),new Vector3(.5f,0,-.5f),new Vector3(-.5f,0,.5f),new Vector3(.5f,0,.5f)};
            quad.uv = new[] {Vector2.zero,Vector2.right,Vector2.up,Vector2.one}; quad.triangles = new[] {0,2,1,1,2,3}; quad.RecalculateNormals(); quad.RecalculateBounds();
            AssetDatabase.CreateAsset(quad, Generated + "/LanternPoolQuad.asset");
            Mesh postMesh = null;
            foreach (Vector2 p in new[] {new Vector2(-3,4),new Vector2(4,10),new Vector2(-7,20),new Vector2(5,26),new Vector2(12,38)})
            {
                float height = GroundHeight(p.x,p.y);
                var pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder); pole.name = "Warm path lantern post";
                if (!postMesh)
                {
                    postMesh = Object.Instantiate(pole.GetComponent<MeshFilter>().sharedMesh); postMesh.name="Snow village post mesh";
                    AssetDatabase.CreateAsset(postMesh, Generated + "/LanternPostMesh.asset");
                }
                pole.GetComponent<MeshFilter>().sharedMesh = postMesh;
                pole.transform.position = new Vector3(280+p.x,height+1.15f,280+p.y); pole.transform.localScale = new Vector3(.09f,1.15f,.09f);
                Object.DestroyImmediate(pole.GetComponent<Collider>()); pole.GetComponent<Renderer>().sharedMaterial = poleMaterial;
                var lamp = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene); lamp.name = "Warm path lantern";
                var renderers = lamp.GetComponentsInChildren<Renderer>(true); Bounds b = renderers[0].bounds;
                foreach (var r in renderers) b.Encapsulate(r.bounds); lamp.transform.localScale *= .65f / Mathf.Max(.01f,b.size.y);
                lamp.transform.position = pole.transform.position + Vector3.up * 1.25f;
                foreach (var renderer in renderers)
                {
                    var materials = renderer.sharedMaterials;
                    for(int m=0;m<materials.Length;m++) if(materials[m]==glassSource) materials[m]=glass;
                    renderer.sharedMaterials=materials;
                }
                var pool = new GameObject("Cheap lantern ground glow"); pool.transform.position = new Vector3(280+p.x,height+.07f,280+p.y);
                pool.transform.localScale = new Vector3(6,1,6); pool.AddComponent<MeshFilter>().sharedMesh=quad;
                var poolRenderer=pool.AddComponent<MeshRenderer>(); poolRenderer.sharedMaterial=poolMaterial;
                poolRenderer.shadowCastingMode=ShadowCastingMode.Off; poolRenderer.receiveShadows=false;
            }
            AssetDatabase.SaveAssetIfDirty(glass); AssetDatabase.SaveAssetIfDirty(poolMaterial); AssetDatabase.SaveAssetIfDirty(poleMaterial);
        }
        static float GroundHeight(float x,float z) => Mathf.Sin(x*.14f)*Mathf.Cos(z*.10f)*.18f +
            Mathf.SmoothStep(0,1,Mathf.InverseLerp(28,85,z))*(2.5f+Mathf.Sin(x*.075f)*1.8f+Mathf.Cos(z*.08f));
        static void RefineComposition(Scene scene, SnowVillagePresentation presentation)
        {
            StripCameraDrivers(presentation.sceneCamera);
            foreach (var group in scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<LODGroup>(true)))
            {
                if (group.name.Contains("Oak_"))
                {
                    Vector3 local=group.transform.position-new Vector3(280,0,280);
                    Vector2 destination=default; bool move=false;
                    if (Mathf.Abs(local.x-10)<.1f&&Mathf.Abs(local.z)<.1f) {destination=new Vector2(22,-2);move=true;}
                    if (Mathf.Abs(local.x-11)<.1f&&Mathf.Abs(local.z-16)<.1f) {destination=new Vector2(25,18);move=true;}
                    if(move) group.transform.position=new Vector3(280+destination.x,GroundHeight(destination.x,destination.y),280+destination.y);
                }
                if (group.name=="Warm path lantern")
                {
                    var lods=group.GetLODs(); float[] thresholds={.06f,.018f,.0015f};
                    for(int i=0;i<lods.Length;i++)lods[i].screenRelativeTransitionHeight=thresholds[Mathf.Min(i,2)];
                    group.SetLODs(lods); group.RecalculateBounds();
                    var renderers=group.GetComponentsInChildren<Renderer>(true); Bounds b=renderers[0].bounds;
                    foreach(var renderer in renderers)b.Encapsulate(renderer.bounds);
                    Vector3 position=group.transform.position;
                    group.transform.position += Vector3.up*(GroundHeight(position.x-280,position.z-280)+2.4f-b.center.y);
                }
            }
            // Scene-local night art direction; leave the gameplay atmosphere profiles untouched.
            var source=AssetDatabase.LoadAssetAtPath<AtmosphereProfile>("Assets/_Game/Settings/Rendering/Atmosphere/LittleCastleAtmosphere.asset");
            var night=AssetDatabase.LoadAssetAtPath<AtmosphereLookProfile>(Generated+"/SnowVillageNight.asset");
            if(!night) {night=Object.Instantiate(source.snowyMoonlight);night.name="Snow village moonlight";AssetDatabase.CreateAsset(night,Generated+"/SnowVillageNight.asset");}
            var look=night.look;
            look.zenith=new Color(.035f,.06f,.20f);look.horizon=new Color(.17f,.23f,.48f);look.fog=new Color(.29f,.35f,.59f);
            look.ambientSky=new Color(.39f,.46f,.72f);look.ambientEquator=new Color(.29f,.35f,.58f);look.ambientGround=new Color(.30f,.36f,.58f);
            look.direct=new Color(.58f,.68f,1);look.moonIntensity=.24f;look.toneMapping=.4f;look.exposure=.05f;
            look.cloudLight=new Color(.35f,.40f,.68f);look.cloudShade=new Color(.13f,.17f,.39f);night.look=look;
            EditorUtility.SetDirty(night);AssetDatabase.SaveAssetIfDirty(night);
            var cycle=AssetDatabase.LoadAssetAtPath<AtmosphereProfile>(Generated+"/SnowVillageAtmosphere.asset");
            if(!cycle) {cycle=Object.Instantiate(source);cycle.name="Snow village atmosphere";AssetDatabase.CreateAsset(cycle,Generated+"/SnowVillageAtmosphere.asset");}
            cycle.snowyMoonlight=night;EditorUtility.SetDirty(cycle);AssetDatabase.SaveAssetIfDirty(cycle);
            presentation.atmosphere.UnifiedProfile=cycle;
            SnowVillageArtPass.Apply(scene, presentation, Generated);
        }
        static void StripCameraDrivers(Camera camera)
        {
            // Disabled demonstration drivers still run Awake. Keep only rendering settings.
            var pipeline = QualitySettings.renderPipeline;
            foreach (var script in camera.GetComponents<MonoBehaviour>())
                if (!(script is UniversalAdditionalCameraData) && !(script is TerrainStarterSoftScene))
                {
                    // Its OnDestroy also runs in Edit Mode: detach copied references before disposal.
                    if (script is MeadowPreview)
                    {
                        var copied = new SerializedObject(script);
                        copied.FindProperty("profile").objectReferenceValue = null;
                        copied.FindProperty("grass").objectReferenceValue = null;
                        copied.ApplyModifiedPropertiesWithoutUndo();
                    }
                    Object.DestroyImmediate(script);
                }
            QualitySettings.renderPipeline = pipeline;
        }
        static void Set(Object target,string field,Object value)
        {var serialized=new SerializedObject(target); serialized.FindProperty(field).objectReferenceValue=value; serialized.ApplyModifiedPropertiesWithoutUndo();}
        static void ValidateScene(Scene scene,SnowVillagePresentation p,Review result)
        {
            var roots=scene.GetRootGameObjects();
            result.clocks=roots.SelectMany(r=>r.GetComponentsInChildren<WorldTimeSystem>(true)).Count();
            result.cameras=roots.SelectMany(r=>r.GetComponentsInChildren<Camera>(true)).Count();
            result.houses=roots.SelectMany(r=>r.GetComponentsInChildren<LODGroup>(true)).Count(g=>g.name.Contains("TownHall"));
            result.trees=roots.SelectMany(r=>r.GetComponentsInChildren<LODGroup>(true)).Count(g=>g.name.Contains("Oak_"));
            if(result.clocks!=1||result.cameras!=1||result.houses!=5)throw new InvalidOperationException("Scene composition counts are invalid.");
            foreach(var reference in new Component[]{p.clock,p.atmosphere,p.lightingGlobals,p.sceneCamera,p.atmosphere.Sun,p.atmosphere.Moon})
                if(!reference||reference.gameObject.scene!=scene)throw new InvalidOperationException("Cross-scene lighting reference.");
            if(p.atmosphere.TimeSystem!=p.clock)throw new InvalidOperationException("Atmosphere clock not wired locally.");
            foreach(var filter in roots.SelectMany(r=>r.GetComponentsInChildren<MeshFilter>(true)))
                if(!filter.sharedMesh||!AssetDatabase.Contains(filter.sharedMesh))throw new InvalidOperationException("Missing/temporary saved mesh: "+filter.name);
            foreach(var renderer in roots.SelectMany(r=>r.GetComponentsInChildren<Renderer>(true)))
                foreach(var material in renderer.sharedMaterials)if(!material||!AssetDatabase.Contains(material))throw new InvalidOperationException("Missing/temporary material: "+renderer.name);
        }
        static void Capture(Camera camera,string path)
        {
            var target=RenderTexture.GetTemporary(1600,900,24,RenderTextureFormat.ARGB32);
            var active=RenderTexture.active; float aspect=camera.aspect; Texture2D pixels=null;
            try
            {
                camera.aspect=1600f/900; CameraCaptureCompatibility.Render(camera,target); CameraCaptureCompatibility.Render(camera,target);
                RenderTexture.active=target; pixels=new Texture2D(1600,900,TextureFormat.RGB24,false);
                pixels.ReadPixels(new Rect(0,0,1600,900),0,0); pixels.Apply(); File.WriteAllBytes(path,pixels.EncodeToPNG());
            }
            finally {camera.aspect=aspect; RenderTexture.active=active; if(pixels)Object.DestroyImmediate(pixels); RenderTexture.ReleaseTemporary(target);}
        }
        static string MapWindowHash()
        {return string.Join("|",Resources.FindObjectsOfTypeAll<MapAuthoringWindow>().Where(w=>w.VerificationSession!=null).Select(w=>Hash(JsonUtility.ToJson(w.VerificationSession.ExportMap()))));}
        static string HashFile(string path)=>File.Exists(path)?Hash(File.ReadAllBytes(path)):"absent";
        static string Hash(string value)=>Hash(System.Text.Encoding.UTF8.GetBytes(value));
        static string Hash(byte[] value){using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(value)).Replace("-","");}
    }
}
