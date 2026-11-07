using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using LittleCastle.Experimental.MountainBorder.Presentation;
using LittleCastle.Rendering;
using LittleCastle.Research;
using LittleCastle.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;
using Debug=UnityEngine.Debug;

namespace LittleCastle.Experimental.MountainBorder.Editor
{
    /// <summary>Explicit commands only. An idle editor never changes assets or scenes.</summary>
    [InitializeOnLoad]
    public static class LandscapeIntegrationV2
    {
        public const string Evidence="E:/Games_Develop/Research/WaterfallVolume_20261029";
        public const string OwnerThread="01a1884e-2c88-78b1-872c-874f75849e21";
        public const string ScenePath="Assets/_Game/Scenes/WorldEditor_20261027.unity";
        const string Art="Assets/_Game/Art/Studies/MountainBorder_20261029";
        const string TextureRoot="Assets/_Game/Experimental/MountainBorder/Textures/UnitySampleV1/";
        static double nextPoll;
        [Serializable] sealed class Lease{public string ownerThreadId,status;}
        public static bool OwnsLease()
        {
            try{string path=Application.dataPath+"/../Library/SharedPcControl.json";if(!File.Exists(path))return false;var lease=JsonUtility.FromJson<Lease>(File.ReadAllText(path));return lease?.status=="active"&&lease.ownerThreadId==OwnerThread;}catch{return false;}
        }
        [Serializable] sealed class Request{public string id,action;}
        static LandscapeIntegrationV2(){EditorApplication.update+=Poll;}
        static void Poll()
        {
            if(EditorApplication.timeSinceStartup<nextPoll||EditorApplication.isCompiling||EditorApplication.isUpdating||!OwnsLease())return;
            nextPoll=EditorApplication.timeSinceStartup+.5;
            string path=Evidence+"/editor-request.json";if(!File.Exists(path))return;
            if(DateTime.UtcNow-File.GetLastWriteTimeUtc(path)>TimeSpan.FromMinutes(10))return;
            try
            {
                var request=JsonUtility.FromJson<Request>(File.ReadAllText(path));
                if(request==null||string.IsNullOrEmpty(request.id)||request.id==SessionState.GetString("LC.LandscapeV2.Command",null))return;
                if(request.id!=SessionState.GetString("LC.LandscapeV2.Refreshed",null))
                {
                    SessionState.SetString("LC.LandscapeV2.Refreshed",request.id);
                    AssetDatabase.Refresh();return;
                }
                SessionState.SetString("LC.LandscapeV2.Command",request.id);
                File.WriteAllText(Evidence+"/editor-result.json",JsonUtility.ToJson(new Reply{ id=request.id,action=request.action,status="RUNNING"}));
                switch(request.action){case "audit":Audit();break;case "build":Build();break;case "capture":Capture();break;case "focus":Focus();break;default:throw new ArgumentException("Unknown landscape command.");}
                File.WriteAllText(Evidence+"/editor-result.json",JsonUtility.ToJson(new Reply{id=request.id,action=request.action,status="PASS"}));
            }
            catch(Exception e){File.WriteAllText(Evidence+"/editor-result.json",JsonUtility.ToJson(new Reply{status="FAIL",error=e.ToString()}));Debug.LogException(e);}
        }
        [Serializable] sealed class Reply{public string id,action,status,error;}
        static MeadowSurfacePainter Map(bool open)
        {
            var active=SceneManager.GetActiveScene();
            if(active.path!=ScenePath&&open)
            {
                if(active.isDirty)throw new InvalidOperationException("Unsaved current scene: preserve it before opening WorldEditor.");
                EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
            }
            return Object.FindAnyObjectByType<MeadowSurfacePainter>();
        }
        [MenuItem("Little Castle/World Editor/Landscape/Inspect current integration")]
        public static void Audit()
        {
            Directory.CreateDirectory(Evidence);var scene=SceneManager.GetActiveScene();var painter=Map(false);
            var report=new System.Text.StringBuilder();
            report.AppendLine("Scene="+scene.path+" dirty="+scene.isDirty+" play="+EditorApplication.isPlaying);
            foreach(var root in scene.GetRootGameObjects())report.AppendLine("Root="+root.name+" pos="+root.transform.position);
            var border=Object.FindAnyObjectByType<LandscapeBorderPresentation>();
            if(border&&border.profile)
            {
                report.AppendLine("Border error="+border.lastError+" landLOD="+border.activeLandLod);
                foreach(var filter in border.GetComponentsInChildren<MeshFilter>(true))report.AppendLine("Mesh="+filter.name+" "+filter.sharedMesh?.name+" material="+filter.GetComponent<Renderer>()?.sharedMaterial?.name);
                foreach(var mesh in border.profile.lodMeshes)
                {
                    float cut=0;int samples=0;foreach(var vertex in mesh.vertices)if(vertex.z>90){float delta=MountainMassBlockoutV1.Height(border.profile.seed,new Vector2(vertex.x,vertex.z))-vertex.y;cut=Mathf.Max(cut,delta);if(delta>2)samples++;}
                    report.AppendLine(mesh.name+" maximum alpine cut="+cut+" significant samples="+samples);
                }
            }
            if(painter)
            {
                report.AppendLine("Map="+AssetDatabase.GetAssetPath(painter.source)+" bounds="+painter.source.bounds);
                report.AppendLine("Ground="+AssetDatabase.GetAssetPath(painter.terrainMaterial)+" water="+AssetDatabase.GetAssetPath(painter.riverCatalogOverride?.waterMaterial));
                var ground=MeadowSurfacePainter.FindGround(painter.terrainMaterial);
                if(ground)report.AppendLine("Ground mesh="+ground.sharedMesh.bounds+" world="+ground.transform.position);
            }
            File.WriteAllText(Evidence+"/scene-audit.txt",report.ToString());
        }
        [MenuItem("Little Castle/World Editor/Landscape/Build mountains and waterfall in this map")]
        public static void Build()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Build outside Play Mode.");
            Directory.CreateDirectory(Evidence);var painter=Map(true);if(!painter)throw new InvalidOperationException("Existing Meadow painter required.");
            // The authored all-tools mask predates the added fence bit. Preserve
            // explicitly restricted roles, and migrate only that exact old mask.
            if(painter.allowedTools==(MapEditAccess.All&~MapEditAccess.Fences)){painter.allowedTools=MapEditAccess.All;EditorUtility.SetDirty(painter);}
            var scene=SceneManager.GetActiveScene();
            string backup=Evidence+"/WorldEditor-before-landscape.unity";
            if(!File.Exists(backup))File.Copy(ScenePath,backup);
            Directory.CreateDirectory(Art);AssetDatabase.Refresh();
            var ground=MeadowSurfacePainter.FindGround(painter.terrainMaterial);
            var session=new MapEditSession(MeadowSurfacePainter.CreateDocument(painter.source,ground,MapTreeCatalog.Default));
            var profile=AssetDatabase.LoadAssetAtPath<MountainBorderProfile>(Art+"/MountainBorderProfile.asset");
            if(!profile){profile=ScriptableObject.CreateInstance<MountainBorderProfile>();AssetDatabase.CreateAsset(profile,Art+"/MountainBorderProfile.asset");}
            var stopwatch=Stopwatch.StartNew();var source=MountainMassBlockoutV1.Build(profile.seed);
            // Match the actual existing meadow along the joining edge. Authored
            // height data is read, never applied/rebuilt/replaced by this sector.
            float groundEdge=ground?ground.transform.TransformPoint(ground.sharedMesh.bounds.max).z:115;
            float offsetZ=groundEdge+70;
            var vertices=source.vertices;
            for(int i=0;i<vertices.Length;i++)if(vertices[i].z<-50)
            {
                var p=vertices[i];float weight=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(-70,-50,p.z));
                if(p.x>=profile.rockDetail.protectedWaterMinimum.x&&p.x<=profile.rockDetail.protectedWaterMaximum.x&&p.z>=profile.rockDetail.protectedWaterMinimum.y&&p.z<=profile.rockDetail.protectedWaterMaximum.y)continue;
                p.y=Mathf.Lerp(p.y,session.SampleHeight(p.x,groundEdge),weight);vertices[i]=p;
            }
            source.vertices=vertices;source.RecalculateBounds();
            try
            {
                var settings=JsonUtility.FromJson<MountainRockDetailSettings>(JsonUtility.ToJson(profile.rockDetail));
                settings.additionalLockedPoints=MountainMassBlockoutV1.AuthoredControlPoints(profile.seed).Concat(settings.additionalLockedPoints??Array.Empty<Vector3>()).ToArray();
                profile.lodMeshes=new Mesh[3];
                for(int lod=0;lod<3;lod++)
                {
                    var mesh=MountainRockDetailBuilder.Build(source,profile.seed,lod,settings);
                    var masks=new Color32[mesh.vertexCount];for(int i=0;i<masks.Length;i++)masks[i]=new Color32(0,0,0,255);mesh.colors32=masks;
                    profile.lodMeshes[lod]=Store(mesh,Art+"/Mountain_Lod"+lod+".asset");
                    Debug.Log("LANDSCAPE_V2_BAKE lod="+lod+" triangles="+profile.lodMeshes[lod].triangles.Length/3+" elapsedMs="+stopwatch.ElapsedMilliseconds);
                }
            }
            finally{Object.DestroyImmediate(source);}
            profile.outcropLodMeshes=new Mesh[3];
            for(int lod=0;lod<3;lod++)profile.outcropLodMeshes[lod]=Store(MountainCliffOutcropBuilder.Build(profile.lodMeshes[0],profile.seed,lod),Art+"/CliffShoulder_Lod"+lod+".asset");
            if(ground)profile.meadowJoinMesh=Store(BuildJoin(ground,session,profile.lodMeshes[0],offsetZ),Art+"/MeadowJoin.asset");
            profile.groundMaterial=painter.terrainMaterial;
            profile.alpineMaterial=Store(new Material(painter.terrainMaterial){name="Mountain · existing meadow terrain family"},Art+"/AlpineTerrain.mat");
            var land=profile.alpineMaterial;land.SetFloat("_AlpineEnabled",1);land.SetFloat("_RockTriplanar",1);land.SetFloat("_UseSurfacePaint",0);land.DisableKeyword("_LC_SURFACE_PAINT");
            land.SetFloat("_UseVertexMasks",0);land.SetFloat("_SnowAcceptance",1);land.SetFloat("_RockSlopeStart",.18f);land.SetFloat("_RockSlopeEnd",.48f);
            land.SetFloat("_AlpineSnowLine",profile.snowAltitude);land.SetFloat("_AlpineSnowBlend",profile.snowTransition);land.enableInstancing=true;EditorUtility.SetDirty(land);
            profile.studyRockCatalog=BakeStudyRocks(land,profile.seed);
            var catalog=painter.riverCatalogOverride?painter.riverCatalogOverride:MapRockCatalog.Default;
            if(!catalog||!catalog.waterMaterial)throw new InvalidOperationException("Existing river profile missing.");
            profile.riverMaterial=Store(new Material(catalog.waterMaterial){name="Alpine river · existing water profile"},Art+"/AlpineRiver.mat");
            // A single fixed planar reflection cannot represent both connected
            // elevations. Keep the existing flow/normal/depth/refraction family.
            profile.riverMaterial.DisableKeyword("_ENABLEPLANERREFLECTION");profile.riverMaterial.SetFloat("_ENABLEPLANERREFLECTION",0);
            profile.riverMaterial.SetFloat("_LC_UseWorldLighting",1);
            profile.riverMaterial.SetColor("_Color_Shallow",new Color(.08f,.36f,.43f,.65f));
            profile.riverMaterial.SetColor("_Color_Deep",new Color(.025f,.14f,.23f,.95f));
            profile.riverMaterial.SetFloat("_Fresnel_Opacity",.15f);
            profile.waterfallMaterial=Store(new Material(NeedShader("Little Castle/Experimental/Waterfall Volume V2")),Art+"/WaterfallVolume.mat");
            profile.mistMaterial=Store(new Material(NeedShader("Little Castle/Experimental/Water Mist V2")),Art+"/WaterMist.mat");
            const string foamMaterialPath="Assets/_Game/Resources/MapAuthoring/RiverSurfaceEffects.mat";
            if(!AssetDatabase.LoadAssetAtPath<Material>(foamMaterialPath))Store(new Material(NeedShader("Little Castle/Rivers/Surface Effects")),foamMaterialPath);
            const string riverQualityPath="Assets/_Game/Resources/MapAuthoring/RiverVisualQuality.asset";
            if(!AssetDatabase.LoadAssetAtPath<RiverVisualQualityProfile>(riverQualityPath))AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<RiverVisualQualityProfile>(),riverQualityPath);
            profile.normalTexture=AssetDatabase.LoadAssetAtPath<Texture>(TextureRoot+"Water_NormalPacked.png");
            profile.foamTexture=AssetDatabase.LoadAssetAtPath<Texture>(TextureRoot+"Water_FoamPacked.png");
            profile.phaseTexture=AssetDatabase.LoadAssetAtPath<Texture>(TextureRoot+"Water_PhaseNoise.tga");
            profile.riverBodyMaterial=Store(new Material(profile.waterfallMaterial){name="Two-level alpine river · shared water family"},Art+"/AlpineRiverBody.mat");
            profile.riverBodyMaterial.SetFloat("_SurfaceMode",2);profile.riverBodyMaterial.SetFloat("_FlowSpeed",1.6f);profile.riverBodyMaterial.SetFloat("_NormalStrength",.65f);
            profile.riverBodyMaterial.SetColor("_WaterColor",new Color(.065f,.29f,.43f,1));profile.riverBodyMaterial.SetColor("_FoamColor",profile.waterfall.foamColor);
            profile.riverBodyMaterial.SetTexture("_NormalTex",profile.normalTexture);profile.riverBodyMaterial.SetTexture("_FoamTex",profile.foamTexture);profile.riverBodyMaterial.SetTexture("_PhaseTex",profile.phaseTexture);EditorUtility.SetDirty(profile.riverBodyMaterial);
            profile.sprucePrefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Art/Studies/ValleyArt_Pass03/Spruce_A_Study.prefab");
            var nativeGrass=Object.FindObjectsByType<MeadowGrassRenderer>(FindObjectsSortMode.None).FirstOrDefault(g=>g.data&&g.material&&!g.GetComponentInParent<LandscapeBorderPresentation>());
            if(nativeGrass){profile.grassSource=nativeGrass.data;profile.grassMaterial=nativeGrass.material;}
            ValidateSpruce(profile.sprucePrefab);
            EditorUtility.SetDirty(profile);AssetDatabase.SaveAssets();
            var border=Object.FindAnyObjectByType<LandscapeBorderPresentation>();
            if(!border){var root=new GameObject("Mountain border · north sector");root.transform.position=new Vector3(0,0,offsetZ);border=root.AddComponent<LandscapeBorderPresentation>();}
            else if(border.transform.position==new Vector3(0,0,155))border.transform.position=new Vector3(0,0,offsetZ);
            border.profile=profile;border.Rebuild();if(!string.IsNullOrEmpty(border.lastError))throw new InvalidOperationException(border.lastError);
            var atmosphere=Object.FindAnyObjectByType<DayNightLightingController>();
            if(atmosphere)
            {
                var serialized=new SerializedObject(atmosphere);
                serialized.FindProperty("dayFogStart").floatValue=260;serialized.FindProperty("dayFogEnd").floatValue=950;
                serialized.FindProperty("nightFogStart").floatValue=210;serialized.FindProperty("nightFogEnd").floatValue=800;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            EditorUtility.SetDirty(border);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            EditorApplication.ExecuteMenuItem("Little Castle/Map/World Editor");
            File.WriteAllText(Evidence+"/build-result.json","{\"status\":\"PASS\",\"bakeMs\":"+stopwatch.ElapsedMilliseconds+",\"lodTriangles\":["+string.Join(",",profile.lodMeshes.Select(m=>m.triangles.Length/3))+"],\"trees\":"+border.treeCount+"}");
            Audit();Focus();
        }
        static Shader NeedShader(string name){var shader=Shader.Find(name);if(!shader)throw new InvalidOperationException("Missing shader "+name);return shader;}
        static MapRockCatalog BakeStudyRocks(Material terrain,int seed)
        {
            var material=Store(new Material(terrain){name="Lightweight stone · shared terrain rock maps"},Art+"/Stone.mat");
            material.SetFloat("_RockSlopeStart",-.02f);material.SetFloat("_RockSlopeEnd",-.01f);EditorUtility.SetDirty(material);
            string path=Art+"/LightweightRockCatalog.asset";var catalog=AssetDatabase.LoadAssetAtPath<MapRockCatalog>(path);
            if(!catalog){catalog=ScriptableObject.CreateInstance<MapRockCatalog>();AssetDatabase.CreateAsset(catalog,path);}
            catalog.entries=new MapRockCatalog.Entry[4];
            for(int variant=0;variant<4;variant++)
            {
                var root=new GameObject("AlpineStone_"+variant);var lods=new LOD[3];Bounds bounds=default;
                try
                {
                    for(int lod=0;lod<3;lod++)
                    {
                        var mesh=Store(LightweightRockBuilder.Build(seed+variant*7919,lod),Art+"/Stone_"+variant+"_Lod"+lod+".asset");
                        if(lod==0)bounds=mesh.bounds;
                        var child=new GameObject("LOD"+lod);child.transform.SetParent(root.transform,false);child.AddComponent<MeshFilter>().sharedMesh=mesh;
                        var renderer=child.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=lod==2?UnityEngine.Rendering.ShadowCastingMode.Off:UnityEngine.Rendering.ShadowCastingMode.On;renderer.receiveShadows=lod!=2;renderer.lightProbeUsage=UnityEngine.Rendering.LightProbeUsage.Off;renderer.reflectionProbeUsage=UnityEngine.Rendering.ReflectionProbeUsage.Off;
                        lods[lod]=new LOD(lod==0?.12f:lod==1?.035f:.004f,new Renderer[]{renderer});
                    }
                    var group=root.AddComponent<LODGroup>();group.SetLODs(lods);group.RecalculateBounds();
                    var prefab=PrefabUtility.SaveAsPrefabAsset(root,Art+"/AlpineStone_"+variant+".prefab");
                    catalog.entries[variant]=new MapRockCatalog.Entry{title="Alpine stone "+(variant+1),prefab=prefab,bounds=bounds,sourcePath=Art,definition=new MapRockDefinition{id="alpine-stone-"+variant,version=1,length=bounds.size.z,height=bounds.size.y,thickness=bounds.size.x,embed=.15f}};
                }
                finally{Object.DestroyImmediate(root);}
            }
            EditorUtility.SetDirty(catalog);return catalog;
        }
        static Mesh BuildJoin(MeshFilter ground,MapEditSession session,Mesh mountain,float offsetZ)
        {
            float edge=ground.transform.TransformPoint(ground.sharedMesh.bounds.max).z;
            var xs=ground.sharedMesh.vertices.Where(v=>Mathf.Abs(v.z-ground.sharedMesh.bounds.max.z)<.01f).Select(v=>ground.transform.TransformPoint(v).x).Distinct().OrderBy(x=>x).ToArray();
            var sampler=new MountainRockDetailBuilder.HeightSampler(new System.Collections.Generic.List<Vector3>(mountain.vertices),new System.Collections.Generic.List<int>(mountain.triangles));
            var vertices=new Vector3[xs.Length*2];var triangles=new int[(xs.Length-1)*6];var masks=new Color32[vertices.Length];
            for(int x=0;x<xs.Length;x++)
            {
                vertices[x*2]=new Vector3(xs[x],session.SampleHeight(xs[x],edge-.6f)+.015f,edge-.6f-offsetZ);
                vertices[x*2+1]=new Vector3(xs[x],sampler.Height(xs[x],-69.4f)+.015f,edge+.6f-offsetZ);
                masks[x*2]=masks[x*2+1]=new Color32(0,0,0,255);
                if(x==xs.Length-1)continue;int i=x*6,a=x*2;triangles[i]=a;triangles[i+1]=a+1;triangles[i+2]=a+2;triangles[i+3]=a+2;triangles[i+4]=a+1;triangles[i+5]=a+3;
            }
            var mesh=new Mesh{name="Existing meadow to alpine foothill join"};mesh.vertices=vertices;mesh.triangles=triangles;mesh.colors32=masks;mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
        static T Store<T>(T created,string path)where T:Object
        {
            created.hideFlags=HideFlags.None;
            var existing=AssetDatabase.LoadAssetAtPath<T>(path);
            if(existing){EditorUtility.CopySerialized(created,existing);EditorUtility.SetDirty(existing);Object.DestroyImmediate(created);return existing;}
            AssetDatabase.CreateAsset(created,path);return created;
        }
        static void ValidateSpruce(GameObject source)
        {
            if(!source)return;var previous=Selection.activeObject;
            try{Selection.activeObject=source;var type=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("LittleCastle.Editor.FoliageModelCompatibilityValidator")).FirstOrDefault(t=>t!=null);type?.GetMethod("ValidateSelected")?.Invoke(null,null);}
            finally{Selection.activeObject=previous;}
        }
        [MenuItem("Little Castle/World Editor/Landscape/See meadow to mountain transition")]
        public static void Focus()
        {
            var view=SceneView.lastActiveSceneView;if(!view)return;
            view.sceneLighting=true;view.LookAt(new Vector3(0,32,120),Quaternion.Euler(30,0,0),185,false,true);view.Repaint();
        }
        [MenuItem("Little Castle/World Editor/Landscape/Capture integrated map")]
        public static void Capture()
        {
            var painter=Map(true);var border=Object.FindAnyObjectByType<LandscapeBorderPresentation>();
            if(!painter||!border)throw new InvalidOperationException("Build the integrated border first.");
            border.Rebuild();if(!string.IsNullOrEmpty(border.lastError))throw new InvalidOperationException(border.lastError);
            foreach(var shader in new[]{border.profile.alpineMaterial.shader,border.profile.waterfallMaterial.shader,border.profile.mistMaterial.shader,NeedShader("Little Castle/Rivers/Surface Effects")})
                typeof(ShaderUtil).GetMethod("ClearShaderMessages",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic)?.Invoke(null,new object[]{shader});
            var clock=Object.FindAnyObjectByType<WorldTimeSystem>();var atmosphere=Object.FindAnyObjectByType<DayNightLightingController>();var globals=Object.FindAnyObjectByType<StylizedLightingGlobals>();
            var clockState=clock?clock.CaptureState():default;bool automatic=clock&&clock.RunAutomatically;
            var cameraRoot=new GameObject("Temporary landscape evidence camera"){hideFlags=HideFlags.HideAndDontSave};var camera=cameraRoot.AddComponent<Camera>();
            var sourceCamera=painter.studyCamera;if(sourceCamera)camera.CopyFrom(sourceCamera);camera.enabled=false;camera.fieldOfView=42;camera.nearClipPlane=.2f;camera.farClipPlane=850;
            var data=camera.GetUniversalAdditionalCameraData();data.requiresColorTexture=true;data.requiresDepthTexture=true;data.renderPostProcessing=sourceCamera&&sourceCamera.GetUniversalAdditionalCameraData().renderPostProcessing;
            if(sourceCamera)
            {
                var sourceData=sourceCamera.GetUniversalAdditionalCameraData();var serializedData=new SerializedObject(sourceData);
                data.SetRenderer(serializedData.FindProperty("m_RendererIndex").intValue);data.volumeLayerMask=sourceData.volumeLayerMask;
            }
            var rt=RenderTexture.GetTemporary(1600,1000,24,RenderTextureFormat.ARGB32);var old=RenderTexture.active;
            try
            {
                if(clock){clock.RunAutomatically=false;clock.RestoreState(new WorldTimeState(0,13*60));}atmosphere?.ApplyLighting();globals?.ApplyGlobals();
                Directory.CreateDirectory(Evidence+"/captures");
                Shot("01-whole-map",new Vector3(0,45,150),27,0,410);
                Shot("02-meadow-transition",new Vector3(0,24,105),24,-10,285);
                Shot("03-rock-and-snow",border.transform.TransformPoint(new Vector3(5,83,121)),28,12,210);
                Shot("04-waterfall",border.transform.TransformPoint(new Vector3(-58,15,18)),22,22,82);
                Shot("05-rts42",new Vector3(0,38,145),42,0,360);
                Shot("07-waterfall-later",border.transform.TransformPoint(new Vector3(-58,15,18)),22,22,82,13);
                Shot("11-water-close",border.transform.TransformPoint(new Vector3(-58,12,13)),12,16,43);
                Shot("12-impact-close",border.transform.TransformPoint(new Vector3(-58,2,5)),17,25,27);
                Shot("13-upper-lip",border.transform.TransformPoint(new Vector3(-58,24,25)),25,30,28);
                Shot("08-waterfall-side",border.transform.TransformPoint(new Vector3(-58,14,17)),18,62,64);
                var savedQuality=border.profile.waterfall.quality;
                try
                {
                    border.profile.waterfall.quality=WaterfallVisualQuality.Low;border.Rebuild();
                    Shot("09-waterfall-low",border.transform.TransformPoint(new Vector3(-58,15,18)),22,22,82);
                }
                finally{border.profile.waterfall.quality=savedQuality;border.Rebuild();}
                Shot("10-waterfall-far",border.transform.TransformPoint(new Vector3(-58,15,18)),22,22,190);
                Shot("04-waterfall",border.transform.TransformPoint(new Vector3(-58,15,18)),22,22,82);
                Benchmark();
                if(clock)clock.RestoreState(new WorldTimeState(0,23*60));atmosphere?.ApplyLighting();globals?.ApplyGlobals();
                Shot("14-waterfall-night",border.transform.TransformPoint(new Vector3(-58,12,13)),12,16,43);
                Shot("06-night",new Vector3(0,38,145),32,0,360);
                foreach(var shader in new[]{border.profile.alpineMaterial.shader,border.profile.waterfallMaterial.shader,border.profile.mistMaterial.shader,NeedShader("Little Castle/Rivers/Surface Effects")})
                {
                    var errors=ShaderUtil.GetShaderMessages(shader).Where(m=>m.severity==UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error).ToArray();
                    if(errors.Length>0)throw new Exception(shader.name+": "+string.Join("\n",errors.Select(e=>e.message)));
                }
                File.WriteAllText(Evidence+"/capture-result.json","{\"status\":\"PASS\",\"scene\":\""+ScenePath+"\",\"landTriangles\":"+border.landTriangles+",\"trees\":"+border.treeCount+"}");
            }
            finally
            {
                border.ReleaseCapture();
                if(clock){clock.RestoreState(clockState);clock.RunAutomatically=automatic;}atmosphere?.ApplyLighting();globals?.ApplyGlobals();
                RenderTexture.active=old;RenderTexture.ReleaseTemporary(rt);Object.DestroyImmediate(cameraRoot);
            }
            void Shot(string name,Vector3 target,float pitch,float yaw,float distance,float phase=12)
            {
                camera.transform.rotation=Quaternion.Euler(pitch,yaw,0);camera.transform.position=target-camera.transform.forward*distance;
                camera.aspect=1.6f;border.TickPresentation(camera,phase);border.PrepareCapture(phase);
                var request=new RenderPipeline.StandardRequest{destination=rt};
                for(int frame=0;frame<3;frame++)RenderPipeline.SubmitRenderRequest(camera,request);
                RenderTexture.active=rt;var pixels=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);
                pixels.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);pixels.Apply();File.WriteAllBytes(Evidence+"/captures/"+name+".png",pixels.EncodeToPNG());Object.DestroyImmediate(pixels);
                File.AppendAllText(Evidence+"/particle-capture.txt",DateTime.UtcNow.ToString("o")+" "+name+" "+string.Join(", ",border.GetComponentsInChildren<ParticleSystem>().Select(p=>p.name+"="+p.particleCount+"/"+p.main.maxParticles))+"\n");
                if(name=="12-impact-close")foreach(var system in border.GetComponentsInChildren<ParticleSystem>())
                {
                    var samples=new ParticleSystem.Particle[system.main.maxParticles];int count=system.GetParticles(samples);
                    File.AppendAllText(Evidence+"/particle-capture.txt",system.name+" origin="+system.transform.position+" alive="+count+" samples="+string.Join(";",samples.Take(Math.Min(3,count)).Select(p=>system.transform.TransformPoint(p.position)+" size="+p.GetCurrentSize(system)+" color="+p.GetCurrentColor(system)))+"\n");
                }
            }
            void Benchmark()
            {
                var quality=border.profile.waterfall.quality;var report=new System.Text.StringBuilder();
                report.AppendLine("Device="+SystemInfo.graphicsDeviceName+" / "+SystemInfo.graphicsDeviceType);
                report.AppendLine("1600x1000 existing map, existing shared lighting. Synchronous URP render request + 1-pixel readback latency; not a standalone GPU frame-time measurement.");
                var pixel=new Texture2D(1,1,TextureFormat.RGB24,false);
                try
                {
                    foreach(var setting in new[]{WaterfallVisualQuality.Off,WaterfallVisualQuality.Medium})
                    {
                        border.profile.waterfall.quality=setting;border.Rebuild();border.PrepareCapture(12);
                        var request=new RenderPipeline.StandardRequest{destination=rt};var samples=new double[20];
                        for(int frame=-5;frame<20;frame++)
                        {
                            var timer=Stopwatch.StartNew();RenderPipeline.SubmitRenderRequest(camera,request);RenderTexture.active=rt;pixel.ReadPixels(new Rect(0,0,1,1),0,0,false);
                            if(frame>=0)samples[frame]=timer.Elapsed.TotalMilliseconds;
                        }
                        Array.Sort(samples);int particles=border.GetComponentsInChildren<ParticleSystem>().Sum(p=>p.main.maxParticles);
                        report.AppendLine(setting+" medianMs="+samples[10].ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+" p95Ms="+samples[18].ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+" landTriangles="+border.landTriangles+" particleCap="+particles+" grassClumps="+border.grassClumps);
                    }
                }
                finally{border.profile.waterfall.quality=quality;border.Rebuild();Object.DestroyImmediate(pixel);}
                File.WriteAllText(Evidence+"/performance.txt",report.ToString());
            }
        }
    }
}
