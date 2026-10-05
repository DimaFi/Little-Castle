using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LittleCastle.CameraSystem;
using LittleCastle.Rendering;
using LittleCastle.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace LittleCastle.Editor
{
    /// <summary>Reproducible local art fixture; never rewrites the match scene or catalog.</summary>
    [InitializeOnLoad]
    public static class TerrainStarterSceneBuilder
    {
        public const string Root = "Assets/_Game/Art/Imported/TerrainStarter_v001";
        public const string ScenePath = Root + "/MeadowVillage.unity";
        public const string StudyPath = Root + "/CottageGarden.unity";
        private static string generatedRoot=Root;
        private const string Request = "Temp/TerrainStarter.request";
        private static double nextPoll;
        static TerrainStarterSceneBuilder() { EditorApplication.update += Poll; }

        private static void Poll()
        {
            if (EditorApplication.timeSinceStartup < nextPoll) return;
            nextPoll = EditorApplication.timeSinceStartup + 1;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || !File.Exists(Request)) return;
            string action = File.ReadAllText(Request).Trim();
            File.Delete(Request);
            try
            {
                if (action == "build") Build();
                else if (action == "capture") Capture();
                else if (action == "play") EditorApplication.isPlaying = true;
                else if (action == "stop") EditorApplication.isPlaying = false;
                else if (action == "player") BuildPlayer();
            }
            catch (Exception e) { Debug.LogException(e); File.WriteAllText("Logs/terrain-starter-error.txt", e.ToString()); }
        }

        [MenuItem("Little Castle/Testing/Build Meadow Village (selected art)")]
        public static void Build()
        {
            BuildScene(false);
        }

        [MenuItem("Little Castle/Testing/Build Cottage Garden (art study)")]
        public static void BuildArtStudy()
        {
            BuildScene(true);
        }

        // Diagnostic captures use the same close camera and never save scene changes.
        public static void CaptureShadowStudy()
        {
            EditorSceneManager.OpenScene(StudyPath);
            UnityEngine.Object.FindFirstObjectByType<StylizedLightingGlobals>().ApplyGlobals();
            var camera=Camera.main;
            camera.transform.position=new Vector3(-19,12,-16);
            camera.transform.LookAt(new Vector3(-10,2,-3)); camera.orthographicSize=10;
            int oldAA=QualitySettings.antiAliasing, oldCascades=QualitySettings.shadowCascades;
            float oldDistance=QualitySettings.shadowDistance;
            var oldQuality=QualitySettings.shadows; var oldResolution=QualitySettings.shadowResolution;
            try {
            QualitySettings.antiAliasing=4; QualitySettings.shadowDistance=65;
            QualitySettings.shadows=ShadowQuality.All;
            QualitySettings.shadowResolution=ShadowResolution.High; QualitySettings.shadowCascades=2;
            foreach(float bias in new[] {.18f,.4f,.8f}) {
                RenderSettings.sun.shadowNormalBias=bias;
                RenderCapture(camera,"Logs/TerrainStarter/shadow-study-"+bias.ToString("0.00",System.Globalization.CultureInfo.InvariantCulture)+".png");
            }
            RenderSettings.sun.shadows=LightShadows.None;
            RenderCapture(camera,"Logs/TerrainStarter/shadow-study-off.png");
            } finally {
                QualitySettings.antiAliasing=oldAA; QualitySettings.shadowDistance=oldDistance;
                QualitySettings.shadows=oldQuality; QualitySettings.shadowResolution=oldResolution;
                QualitySettings.shadowCascades=oldCascades;
            }
        }

        private static void BuildScene(bool study)
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Save the modified open scene before building Meadow Village.");
            if (!File.Exists(Root + "/provenance.json")) throw new FileNotFoundException("Run Tools/import_terrain_starter.py first.");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            // Study tuning must not rewrite the village's shared materials/prefabs.
            generatedRoot=study ? Root+"/Study" : Root;
            Directory.CreateDirectory(generatedRoot + "/Materials");
            Directory.CreateDirectory(generatedRoot + "/Prefabs");
            Directory.CreateDirectory(Root + "/Meshes");
            AssetDatabase.Refresh();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var prefabs = new Dictionary<string, GameObject>();
            foreach (string name in new[] {"house", "oak", "well", "barrel", "crate", "fence", "bench"})
                prefabs[name] = BuildThreeLod(name);
            foreach (string name in new[] {"SM_Wall_Stone_2m_A", "SM_Wall_Stone_Pillar_A", "SM_ArcherTower_A"})
                prefabs[name] = BuildEmbedded(name);
            foreach (string name in new[] {"SM_Stone_Small_A", "SM_Stone_Medium_A", "SM_FlowerCluster_A", "SM_FlowerCluster_B", "SM_LeafCluster_Small_A"})
                prefabs[name] = BuildSingle(name);

            var plan = new TerrainStarterPlan(314159,study);
            string meshFolder=Root+(study ? "/Meshes/Garden" : "/Meshes/Meadow");
            Directory.CreateDirectory(meshFolder); AssetDatabase.Refresh();
            var terrain = MaterialAsset("Meadow", "Little Castle/Terrain/LC Terrain");
            terrain.SetTexture("_GrassTex", Texture(study ? "Grass_MeadowSoft_BaseColor" : "Grass_Base_A_BaseColor"));
            terrain.SetTexture("_DirtTex", Texture("DirtPath_A_BaseColor"));
            if(study) {
                terrain.SetTexture("_SoilTex", Texture("DirtGround_A_BaseColor"));
                // New calm meadow albedo has no matching PBR maps. Do not reuse
                // normals/AO of painted stones from the old ground underneath it.
                terrain.SetTexture("_GrassNormal", Texture2D.normalTexture);
                terrain.SetTexture("_DirtNormal", Texture("DirtPath_A_Normal"));
                terrain.SetTexture("_GrassAO", Texture2D.whiteTexture);
                terrain.SetTexture("_DirtAO", Texture("DirtPath_A_AO"));
                terrain.SetTexture("_GrassHeight", Texture2D.grayTexture);
                terrain.SetFloat("_GroundPalette",1); terrain.SetFloat("_GrassDetail",.40f);
                terrain.SetFloat("_DirtDetail",.65f); terrain.SetFloat("_SurfaceDetailStrength",.28f);
                terrain.SetFloat("_LightResponse",.55f);
                terrain.SetColor("_DryGrassColor",new Color(.43f,.46f,.24f));
            }
            terrain.SetColor("_GrassColor", new Color(.35f,.43f,.22f));
            terrain.SetColor("_DirtColor", new Color(.53f,.38f,.24f));
            terrain.SetFloat("_TextureDetail", .22f);
            terrain.SetFloat("_ContactShadeStrength", .42f);
            terrain.SetFloat("_PathEdgeSharpness", .7f);
            terrain.SetFloat("_UseVertexMasks", 1);
            terrain.SetFloat("_WorldTiling", .36f);
            terrain.SetFloat("_MacroStrength", .08f);
            terrain.SetFloat("_MacroScale", .07f);
            terrain.SetFloat("_RockSlopeStart", .55f);
            terrain.SetFloat("_RockSlopeEnd", .85f);
            EditorUtility.SetDirty(terrain);
            var ground = new GameObject("Meadow • 96m finite terrain • seed 314159");
            for (int z = -4; z < 4; z++) for (int x = -4; x < 4; x++)
            {
                Mesh mesh = plan.BuildChunk(x, z);
                string path = meshFolder + $"/Meadow_{x}_{z}.asset";
                SaveAsset(mesh, path);
                mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                var chunk = new GameObject($"Chunk {x},{z}", typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider));
                chunk.layer = 8;
                chunk.transform.SetParent(ground.transform);
                chunk.transform.position = new Vector3(x * 24, 0, z * 24);
                chunk.GetComponent<MeshFilter>().sharedMesh = mesh;
                chunk.GetComponent<MeshRenderer>().sharedMaterial = terrain;
                chunk.GetComponent<MeshCollider>().sharedMesh = mesh;
                if(x < -2 || x > 1 || z < -2 || z > 1)
                    UnityEngine.Object.DestroyImmediate(chunk.GetComponent<MeshCollider>());
            }
            var village = new GameObject("Village • House Cottage A v027 selection");
            foreach (Vector3 location in plan.Houses)
            {
                if (!plan.CanPlaceFootprint(new Vector2(location.x, location.z), new Vector2(8, 7)))
                    throw new InvalidOperationException("Unsuitable house footprint.");
                Place(prefabs["house"], location, 180, village.transform);
                Place(prefabs["barrel"], location + new Vector3(4,0,1), 15, village.transform);
                Place(prefabs["crate"], location + new Vector3(4,0,-1), -10, village.transform);
                for(int f=0;f<(study ? 0 : 30);f++)
                {
                    var flower=Place(prefabs[f%2==0 ? "SM_FlowerCluster_A" : "SM_FlowerCluster_B"],
                        location+new Vector3(-3.45f+(f%6)*.33f,0,-3.45f-(f/6)*.24f),f*137.5f,village.transform);
                    flower.transform.localScale=Vector3.one*(.85f+(f%3)*.14f);
                    var collider=flower.GetComponent<Collider>();
                    if(collider) UnityEngine.Object.DestroyImmediate(collider);
                }
                for(int f=0;f<7;f++)
                {
                    var bush=Place(prefabs["SM_LeafCluster_Small_A"],location+new Vector3(-4.2f,0,-3+f*.8f),f*61,village.transform);
                    bush.transform.localScale=new Vector3(1.2f,.9f,1.2f)*(1+(f%3)*.12f);
                    if(f<3) Place(prefabs["fence"],location+new Vector3(-4.7f,0,-2+f*1.8f),90,village.transform);
                }
                Place(prefabs["bench"],location+new Vector3(3.4f,0,-4.5f),-90,village.transform);
                foreach(var p in new[] {new Vector3(3.5f,0,-3.4f),new Vector3(-3.6f,0,-3.5f)})
                {
                    var shrub=Place(prefabs["SM_LeafCluster_Small_A"],location+p,50,village.transform);
                    shrub.transform.localScale=new Vector3(1.3f,1.1f,1.3f);
                }
            }
            Place(prefabs["well"], new Vector3(study ? -2 : -4,0,study ? -7 : 4), 0, village.transform);
            AddGrass(plan,meshFolder);
            var forest = new GameObject("Oak Kit • instanced shared meshes and materials");
            int index = 0;
            foreach (Vector3 p in plan.Trees)
            {
                var tree = Place(prefabs["oak"], p - Vector3.up * .06f, (index * 137.5f) % 360, forest.transform);
                float scale=.80f+(index%7)*.055f;
                tree.transform.localScale=new Vector3(scale*(.94f+(index%3)*.055f),scale*(.94f+(index%4)*.045f),scale);
                tree.transform.position=p-Vector3.up*(.48f*scale);
                index++;
            }
            var rocks = new GameObject("Oak Kit rocks");
            for (int i = 0; i < 16; i++)
            {
                float x = (i % 2 == 0 ? -1 : 1) * (22 + i % 4 * 3);
                float z = -25 + i * 3;
                Place(prefabs[i % 2 == 0 ? "SM_Stone_Small_A" : "SM_Stone_Medium_A"],
                    new Vector3(x, plan.Height(x,z) - .04f, z), i * 83, rocks.transform);
            }
            var walls = new GameObject("Fortifications v004 • rigid 2m modules");
            if(study)
            {
                for(int side=-1;side<=1;side+=2) for(int i=0;i<3;i++)
                    Place(prefabs["SM_Wall_Stone_2m_A"],new Vector3(-9.8f+side*(2.7f+i*1.92f),-.06f,-11.8f),90,walls.transform);
                for(int i=0;i<4;i++)
                    Place(prefabs["SM_Wall_Stone_2m_A"],new Vector3(-17.5f,-.06f,-10+i*1.92f),0,walls.transform);
            }
            else for (int side = -1; side <= 1; side += 2)
            {
                for (int i = 0; i < 7; i++)
                {
                    float x = side * (4.8f + i * 1.92f);
                    Place(prefabs["SM_Wall_Stone_2m_A"], new Vector3(x, plan.Height(x,-17)-.04f,-17), 90, walls.transform);
                }
                Place(prefabs["SM_ArcherTower_A"], new Vector3(side * 3,0,-17), 90, walls.transform);
            }
            if(study) TerrainStarterGardenPlants.BuildAndPlace(generatedRoot,plan);
            var lightObject = new GameObject("Sun", typeof(Light));
            var sun = lightObject.GetComponent<Light>();
            sun.type = LightType.Directional; sun.intensity = .90f;
            sun.color = new Color(1,.93f,.80f); sun.shadows = LightShadows.Soft;
            sun.shadowStrength=.82f; sun.shadowBias=.025f; sun.shadowNormalBias=.18f;
            sun.transform.rotation = Quaternion.Euler(48,-32,0);
            RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.50f,.57f,.68f);
            RenderSettings.ambientEquatorColor = new Color(.42f,.46f,.53f);
            RenderSettings.ambientGroundColor = new Color(.25f,.27f,.19f);
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(.60f,.67f,.62f);
            RenderSettings.fogStartDistance = 85; RenderSettings.fogEndDistance = 140;
            var globals = new GameObject("Shared stylized lighting", typeof(StylizedLightingGlobals));
            globals.GetComponent<StylizedLightingGlobals>().ApplyGlobals();
            var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.GetComponent<Camera>();
            camera.nearClipPlane = .2f; camera.farClipPlane = 240;
            camera.fieldOfView = 36; camera.clearFlags = CameraClearFlags.SolidColor;
            camera.orthographic=true; camera.orthographicSize=28;
            camera.backgroundColor = RenderSettings.fogColor;
            camera.transform.position = new Vector3(32,35,-43);
            camera.transform.LookAt(new Vector3(0,0,2));
            if(study) { camera.transform.position=new Vector3(-27,19,-29);
                camera.transform.LookAt(new Vector3(-8,1,-3)); camera.orthographicSize=15; }
            var controller = cameraObject.AddComponent<StrategyCameraController>();
            var serialized = new SerializedObject(controller);
            serialized.FindProperty("groundMask").intValue = 1 << 8;
            serialized.FindProperty("minimumPitch").floatValue = 28;
            serialized.FindProperty("maximumDistance").floatValue = 105;
            serialized.FindProperty("minimumDistance").floatValue = 8;
            serialized.FindProperty("enableEdgeScroll").boolValue = false;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            cameraObject.AddComponent<TerrainStarterPreviewControls>();
            cameraObject.AddComponent<TerrainStarterSoftScene>().effectMaterial =
                MaterialAsset("SoftScene","Little Castle/Presentation/Soft Scene");
            foreach (string key in new[] {"oak", "house", "SM_Wall_Stone_2m_A"})
            {
                Selection.activeObject = prefabs[key];
                ProductionAssetValidator.ValidateSelected();
                if (key == "oak") FoliageModelCompatibilityValidator.ValidateSelected();
            }
            Selection.activeObject = null;
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene, study ? StudyPath : ScenePath);
            Capture();
            File.WriteAllText("Logs/terrain-starter-built.json", JsonUtility.ToJson(new BuildReport {
                houses = plan.Houses.Count, trees = plan.Trees.Count,
                renderers = UnityEngine.Object.FindObjectsByType<Renderer>().Length,
                scene = study ? StudyPath : ScenePath }, true));
            Debug.Log("[Terrain Starter] Scene saved. Shared assets, three LODs, 16 playable terrain chunks + 48 visual-only border chunks.");
        }

        [Serializable] private class BuildReport { public string scene; public int houses, trees, renderers; }
        private static void AddGrass(TerrainStarterPlan plan,string meshFolder)
        {
            var material=MaterialAsset("MeadowGrass","Little Castle/Foliage/LC Grass");
            material.SetFloat("_UseRootAnchor",1);
            material.SetFloat("_FadeStart",40); material.SetFloat("_FadeEnd",70);
            material.SetColor("_Color",new Color(.36f,.43f,.22f));
            material.SetColor("_RootColor",new Color(.30f,.36f,.17f));
            material.SetColor("_TipColor",new Color(.47f,.54f,.30f));
            material.SetColor("_DryColor",new Color(.43f,.46f,.24f));
            material.SetFloat("_MeadowPaletteStrength",1);
            material.SetFloat("_ColorVariation",.045f);
            material.SetFloat("_TransmissionStrength",.15f);
            material.SetFloat("_WindAmplitude",.09f); material.SetFloat("_MicroFlutter",.02f);
            var parent=new GameObject("Meadow grass • sixteen batched patches");
            for(int z=-2;z<2;z++) for(int x=-2;x<2;x++)
            {
                var obj=new GameObject($"Grass {x},{z}",typeof(LODGroup));
                obj.transform.SetParent(parent.transform,false);
                var lods=new LOD[3];
                for(int level=0;level<3;level++)
                {
                    string path=meshFolder+$"/Grass_{x}_{z}_{level}.asset";
                    SaveAsset(TerrainStarterGrass.Build(plan,x,z,level==0 ? 6400 : level==1 ? 2200 : 500),path);
                    var child=new GameObject("LOD"+level,typeof(MeshFilter),typeof(MeshRenderer));
                    child.transform.SetParent(obj.transform,false);
                    child.GetComponent<MeshFilter>().sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                    var renderer=child.GetComponent<MeshRenderer>(); renderer.sharedMaterial=material;
                    renderer.shadowCastingMode=ShadowCastingMode.Off;
                    renderer.lightProbeUsage=LightProbeUsage.Off; renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
                    lods[level]=new LOD(level==0 ? .7f : level==1 ? .28f : .06f,new Renderer[] {renderer});
                }
                obj.GetComponent<LODGroup>().SetLODs(lods);
                obj.GetComponent<LODGroup>().RecalculateBounds();
            }
            EditorUtility.SetDirty(material);
            Selection.activeObject=parent;
            FoliageModelCompatibilityValidator.ValidateSelected();
        }
        private static Texture2D Texture(string name) => AssetDatabase.LoadAssetAtPath<Texture2D>(generatedRoot + "/Textures/" + name + ".png")
            ?? AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Textures/" + name + ".png")
            ?? throw new FileNotFoundException(name);
        private static void SaveAsset(UnityEngine.Object value, string path)
        {
            var existing = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
            if (existing == null) AssetDatabase.CreateAsset(value,path);
            else { EditorUtility.CopySerialized(value,existing); EditorUtility.SetDirty(existing); UnityEngine.Object.DestroyImmediate(value); }
        }
        private static Material MaterialAsset(string name, string shader)
        {
            string path = generatedRoot + "/Materials/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader s = Shader.Find(shader) ?? throw new InvalidOperationException("Missing shader " + shader);
            if (m == null) { m = new Material(s); AssetDatabase.CreateAsset(m,path); }
            m.shader = s; m.enableInstancing = true; return m;
        }
        private static Material ResolveMaterial(string key, string slot, bool distant)
        {
            bool leaves = slot.Contains("Foliage");
            string shader = distant ? "Little Castle/Distance/LC Distant Simple" :
                leaves ? "Little Castle/Foliage/LC Foliage" : "Little Castle/Surface/LC Stylized Lit";
            string token = key.StartsWith("SM_Wall") || key == "SM_ArcherTower_A" ? slot : key + (leaves ? "_Leaves" : "");
            var m = MaterialAsset(token.Replace('.', '_') + (distant ? "_Far" : ""),shader);
            string texture = null;
            Color color = Color.white;
            if (key == "oak" || key.Contains("LeafCluster")) { texture = leaves ? "T_Foliage_BaseColor" : "oak_BaseColor"; if (leaves) color = new Color(.66f,.78f,.43f); }
            else if (key.StartsWith("SM_Stone") || key.Contains("FlowerCluster")) texture = "T_Oak_Kit_Palette";
            else if (!key.StartsWith("SM_")) texture = key + "_BaseColor";
            else if (slot.Contains("Wall_Stone")) texture = "T_WallStoneSurface_A_BaseColor";
            else if (slot.Contains("Roof")) { texture = "RoofTile_BaseColor"; color = new Color(.58f,.30f,.15f).gamma; }
            else if (slot.Contains("Timber")) texture = "DoorWood_A_BaseColor";
            else color = new Color(.3f,.27f,.22f);
            if (texture != null) m.SetTexture("_MainTex", Texture(texture));
            m.SetColor("_Color",color);
            if(m.HasProperty("_LightResponse")) m.SetFloat("_LightResponse",.8f);
            if (m.HasProperty("_BumpScale")) m.SetFloat("_BumpScale",0);
            if (m.HasProperty("_Roughness")) m.SetFloat("_Roughness",.85f);
            if (key == "house" && m.HasProperty("_Cull")) m.SetFloat("_Cull",2);
            if (leaves && distant) { m.SetFloat("_UseAlphaClip",1); m.SetFloat("_Cull",0); m.SetFloat("_Cutoff",.45f); }
            if (leaves) m.SetFloat("_TextureDetail",.25f);
            if (leaves && !distant) { m.SetFloat("_WindAmplitude",.08f); m.SetFloat("_LeafFlutter",.04f); m.SetFloat("_TransmissionStrength",.18f);
                m.SetFloat("_CanopyNormalBlend",.85f); m.SetFloat("_ShadowSoftness",.75f); m.SetFloat("_ColorVariation",.13f); }
            if (distant) { m.SetFloat("_AmbientStrength",.85f); m.SetFloat("_TopLightStrength",1); }
            if(key.Contains("LeafCluster")) { m.SetFloat("_CanopyNormalBlend",0); m.SetFloat("_WindAmplitude",0); }
            if (leaves && distant) { m.SetFloat("_AmbientStrength",.95f); m.SetFloat("_TopLightStrength",.95f); }
            EditorUtility.SetDirty(m); return m;
        }
        private static void Configure(Renderer renderer, string key, bool far)
        {
            renderer.sharedMaterials = renderer.sharedMaterials.Select(m => ResolveMaterial(key,m.name,far)).ToArray();
            if(key == "oak" && renderer.name.Contains("Leaves"))
            {
                Vector3 center=renderer.GetComponent<MeshFilter>().sharedMesh.bounds.center;
                foreach(var material in renderer.sharedMaterials)
                {
                    material.SetVector("_CanopyCenter",new Vector4(center.x,center.y,center.z,0));
                    material.SetFloat("_CanopyNormalBlend",.85f);
                    EditorUtility.SetDirty(material);
                }
            }
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            if (far) { renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false; renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion; }
        }
        private static GameObject Model(string file)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(generatedRoot + "/Models/" + file + ".fbx")
                ?? AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Models/" + file + ".fbx");
            if (model == null) throw new FileNotFoundException(file);
            return (GameObject)PrefabUtility.InstantiatePrefab(model);
        }
        private static GameObject BuildThreeLod(string key)
        {
            var root = new GameObject(key);
            var levels = new LOD[3];
            for (int i = 0; i < 3; i++)
            {
                var model = Model(key + "_LOD" + i); model.transform.SetParent(root.transform,false);
                var renderers = model.GetComponentsInChildren<Renderer>();
                foreach (Renderer r in renderers) Configure(r,key,i == 2);
                bool major=key=="house" || key=="oak";
                levels[i] = new LOD(i == 0 ? (major ? .32f : .10f) : i == 1 ? (major ? .075f : .018f) : .005f,renderers);
            }
            var lod = root.AddComponent<LODGroup>(); lod.SetLODs(levels); lod.RecalculateBounds();
            if (key == "oak") { var c = root.AddComponent<CapsuleCollider>(); c.radius = .48f; c.height = 3; c.center = Vector3.up * 1.5f; }
            else AddBox(root,levels[0].renderers);
            return SavePrefab(root,key);
        }
        private static GameObject BuildEmbedded(string key)
        {
            var root = new GameObject(key);
            var model = Model(key); model.transform.SetParent(root.transform,false);
            foreach (var old in model.GetComponentsInChildren<LODGroup>()) UnityEngine.Object.DestroyImmediate(old);
            var renderers = model.GetComponentsInChildren<Renderer>();
            // FBX includes authored collision guides as meshes; they are not visuals.
            foreach (var helper in renderers.Where(r => !Enumerable.Range(0,3).Any(i => HasLod(r.transform,i,model.transform))))
            {
                Debug.Log("[Terrain Starter] Excluding non-LOD guide: " + helper.name);
                UnityEngine.Object.DestroyImmediate(helper);
            }
            renderers = model.GetComponentsInChildren<Renderer>();
            var levels = new LOD[3];
            for (int i=0;i<3;i++)
            {
                var level = renderers.Where(r => HasLod(r.transform,i,model.transform)).ToArray();
                if (level.Length == 0) throw new InvalidOperationException(key + " missing LOD " + i);
                foreach(var r in level) Configure(r,key,i == 2);
                levels[i] = new LOD(i == 0 ? .25f : i == 1 ? .045f : .008f,level);
            }
            var group = root.AddComponent<LODGroup>(); group.SetLODs(levels); group.RecalculateBounds();
            AddBox(root,levels[0].renderers);
            return SavePrefab(root,key);
        }
        private static bool HasLod(Transform t,int level,Transform root)
        {
            while(t != null) { if(t.name.Contains("LOD"+level)) return true; if(t == root) break; t=t.parent; } return false;
        }
        private static GameObject BuildSingle(string key)
        {
            var root = new GameObject(key); var model = Model(key); model.transform.SetParent(root.transform,false);
            var renderers = model.GetComponentsInChildren<Renderer>();
            Bounds bounds=renderers[0].bounds; foreach(var r in renderers) bounds.Encapsulate(r.bounds);
            model.transform.position-=new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);
            foreach(var r in renderers) Configure(r,key,false);
            root.AddComponent<LODGroup>().SetLODs(new[] {new LOD(.02f, renderers)});
            AddBox(root,renderers); return SavePrefab(root,key);
        }
        private static void AddBox(GameObject root,Renderer[] renderers)
        {
            Bounds b=renderers[0].bounds; foreach(var r in renderers) b.Encapsulate(r.bounds);
            var collider=root.AddComponent<BoxCollider>(); collider.center=b.center; collider.size=b.size;
        }
        private static GameObject SavePrefab(GameObject root,string key)
        {
            var prefab=PrefabUtility.SaveAsPrefabAsset(root,generatedRoot+"/Prefabs/"+key+".prefab");
            UnityEngine.Object.DestroyImmediate(root); return prefab;
        }
        private static GameObject Place(GameObject prefab,Vector3 position,float yaw,Transform parent)
        {
            var obj=(GameObject)PrefabUtility.InstantiatePrefab(prefab);
            obj.transform.SetParent(parent,false); obj.transform.position=position;
            obj.transform.rotation=Quaternion.Euler(0,yaw,0); return obj;
        }

        [MenuItem("Little Castle/Testing/Capture Meadow Village")]
        public static void Capture()
        {
            var camera=Camera.main;
            if(camera == null) throw new InvalidOperationException("No scene camera");
            UnityEngine.Object.FindAnyObjectByType<StylizedLightingGlobals>()?.ApplyGlobals();
            Directory.CreateDirectory("Logs/TerrainStarter");
            Vector3 position=camera.transform.position; Quaternion rotation=camera.transform.rotation;
            float ortho=camera.orthographicSize;
            string prefix=camera.gameObject.scene.path==StudyPath ? "garden-" : "village-";
            RenderCapture(camera,"Logs/TerrainStarter/"+prefix+"overview.png");
            camera.transform.position=new Vector3(-19,12,-16); camera.transform.LookAt(new Vector3(-10,2,-3));
            camera.orthographicSize=10;
            RenderCapture(camera,"Logs/TerrainStarter/"+prefix+"close.png");
            camera.transform.position=position; camera.transform.rotation=rotation;
            camera.orthographicSize=ortho;
        }
        private static void RenderCapture(Camera camera,string path)
        {
            var rt=RenderTexture.GetTemporary(1600,1000,24,RenderTextureFormat.Default,RenderTextureReadWrite.Default,4);
            var previous=RenderTexture.active; var old=camera.targetTexture;
            var image=new Texture2D(1600,1000,TextureFormat.RGB24,false);
            try { camera.targetTexture=rt; camera.Render(); RenderTexture.active=rt;
                image.ReadPixels(new Rect(0,0,1600,1000),0,0); image.Apply(); File.WriteAllBytes(path,image.EncodeToPNG()); }
            finally { camera.targetTexture=old; RenderTexture.active=previous; RenderTexture.ReleaseTemporary(rt); UnityEngine.Object.DestroyImmediate(image); }
        }
        public static void BuildPlayer()
        {
            var result=BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes=new[] {ScenePath},
                locationPathName="Builds/TerrainStarter/MeadowVillage.exe", target=BuildTarget.StandaloneWindows64,
                options=BuildOptions.Development });
            File.WriteAllText("Logs/terrain-starter-player.txt",result.summary.result+" "+result.summary.totalErrors);
        }
        public static void BuildStudyPlayer()
        {
            bool previous=PlayerSettings.enableFrameTimingStats;
            try {
                PlayerSettings.enableFrameTimingStats=true;
                var result=BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes=new[] {StudyPath},
                    locationPathName="Builds/CottageGarden/CottageGarden.exe", target=BuildTarget.StandaloneWindows64,
                    options=BuildOptions.Development });
                File.WriteAllText("Logs/cottage-garden-player.txt",result.summary.result+" "+result.summary.totalErrors);
            }
            finally { PlayerSettings.enableFrameTimingStats=previous; }
        }
        [MenuItem("Little Castle/Testing/Build terrain art review package")]
        public static void BuildReviewPackage()
        {
            Build();
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            BuildArtStudy();
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            BuildPlayer();
            BuildStudyPlayer();
        }
    }

    public sealed class TerrainStarterImportDefaults : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            if(!assetPath.StartsWith(TerrainStarterSceneBuilder.Root+"/")) return;
            var importer=(TextureImporter)assetImporter;
            importer.mipmapEnabled=true; importer.isReadable=false; importer.sRGBTexture=true;
            importer.maxTextureSize=assetPath.Contains("house_BaseColor") ? 4096 : 2048;
            importer.textureCompression=TextureImporterCompression.Compressed;
            importer.alphaIsTransparency=assetPath.Contains("T_Foliage");
            importer.anisoLevel=4;
            importer.filterMode=FilterMode.Trilinear;
            if(assetPath.Contains("/Study/Textures/") && !assetPath.Contains("house_BaseColor")) {
                importer.maxTextureSize=1024;
                importer.wrapMode=TextureWrapMode.Repeat;
                importer.sRGBTexture=assetPath.Contains("BaseColor");
                if(assetPath.Contains("_Normal")) importer.textureType=TextureImporterType.NormalMap;
            }
            if(assetPath.Contains("Palette")) { importer.sRGBTexture=true; importer.mipmapEnabled=false; importer.filterMode=FilterMode.Point; importer.textureCompression=TextureImporterCompression.Uncompressed; }
        }
        private void OnPreprocessModel()
        {
            if(!assetPath.StartsWith(TerrainStarterSceneBuilder.Root+"/")) return;
            var importer=(ModelImporter)assetImporter;
            importer.importAnimation=false; importer.isReadable=false;
            importer.importCameras=false; importer.importLights=false;
            importer.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;
        }
    }
}
