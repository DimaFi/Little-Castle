using System;
using System.IO;
using System.Linq;
using LittleCastle.Rendering;
using LittleCastle.Research;
using LittleCastle.World;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object=UnityEngine.Object;

namespace LittleCastle.Editor
{
    public static class SpatialIndirectStudyBuild
    {
        public const string Root="Assets/_Game/Settings/Rendering/SpatialIndirectStudy";
        public const string Scene="Assets/_Game/Scenes/SpatialIndirectStudy_20261026.unity";
        public const string Output="E:/Games_Develop/Research/SpatialIndirect_20261026";
        [MenuItem("Little Castle/Rendering/Spatial Indirect/Create Limited Prototype")]
        public static void Setup()
        {
            if(Path.GetFileName(Path.GetDirectoryName(Application.dataPath))!="Little-Castle_LightingIntegration_20261025")throw new InvalidOperationException("Isolated lighting copy only.");
            Directory.CreateDirectory(Root);Directory.CreateDirectory(Output);AssetDatabase.Refresh();
            var scene=EditorSceneManager.OpenScene(LightingMeadowBuild.Scene,OpenSceneMode.Single);
            var old=Object.FindAnyObjectByType<LightingMeadowReview>();if(!old)throw new Exception("Validated Meadow lighting scene required.");
            var camera=old.reviewCamera;var clock=old.clock;var atmosphere=old.atmosphere;var globals=old.globals;var hdr=old.hdr;var ao=old.ao;
            if(old.demonstrator)Object.DestroyImmediate(old.demonstrator.gameObject);
            old.SetHour(12);Object.DestroyImmediate(old);
            Object.FindAnyObjectByType<MeadowSurfacePainter>().painting=false;
            var field=new GameObject("Bounded world-space indirect experiment").AddComponent<SpatialIndirectVolume>();field.globals=globals;field.gridSize=new Vector3Int(49,9,49);field.origin=new Vector3(-24,.05f,-24);field.spacing=1;field.rayDistance=6;field.raysPerProbe=128;field.maximumProbesPerFrame=256;field.workBudgetMs=2;
            var white=Material("Neutral matte",new Color(.80f,.78f,.72f));var wood=Material("Roof matte",new Color(.46f,.28f,.15f));var red=Material("Offscreen reflector",new Color(.85f,.12f,.04f));
            var roof=Box("Thin reference roof",new Bounds(new Vector3(-6,4,4),new Vector3(8,.12f,8)),wood);
            var back=Box("Thin back wall",new Bounds(new Vector3(-6,1.95f,7.8f),new Vector3(8,3.4f,.12f)),white);
            var divider=Box("Thin divider / light-leak fixture",new Bounds(new Vector3(-1.65f,1.95f,4),new Vector3(.12f,3.4f,7.6f)),white);
            var reflector=Box("Lit red reflector outside close camera",new Bounds(new Vector3(-6,.35f,-2.8f),new Vector3(7,.15f,2.2f)),red);
            var receiver=Box("Neutral bounce receiver near shelter entrance",new Bounds(new Vector3(-6,2,3),new Vector3(3,2.8f,.12f)),white);
            for(int x=0;x<2;x++)for(int z=0;z<2;z++)Box("Roof post",new Bounds(new Vector3(-9.8f+x*7.6f,2.05f,.2f+z*7.6f),new Vector3(.16f,3.9f,.16f)),wood);
            foreach(var p in new[]{new Vector3(-7,1.2f,4),new Vector3(3,1.2f,4)})
            {var ball=GameObject.CreatePrimitive(PrimitiveType.Sphere);ball.name=p.x<0?"Neutral receiver under roof":"Neutral receiver open sky";ball.transform.position=p;ball.transform.localScale=Vector3.one*1.5f;ball.GetComponent<Renderer>().sharedMaterial=white;Object.DestroyImmediate(ball.GetComponent<Collider>());}
            field.proxies=new[]{
                new SpatialIndirectVolume.ProxyObject{id="flat-ground-approximation",bounds=new Bounds(new Vector3(0,-.25f,0),new Vector3(80,1,80)),albedo=new Color(.32f,.43f,.205f)},
                Proxy("roof",roof,wood.GetColor("_Color")),Proxy("back-wall",back,white.GetColor("_Color")),Proxy("divider",divider,white.GetColor("_Color")),Proxy("reflector",reflector,red.GetColor("_Color")),Proxy("receiver-panel",receiver,white.GetColor("_Color"))};
            var review=camera.gameObject.AddComponent<SpatialIndirectStudyReview>();review.field=field;review.camera=camera;review.clock=clock;review.atmosphere=atmosphere;review.globals=globals;review.hdr=hdr;review.ao=ao;
            review.greyReflector=Material("Grey reflector",new Color(.45f,.45f,.45f));
            review.SetView(0);camera.nearClipPlane=.08f;camera.allowHDR=true;camera.GetUniversalAdditionalCameraData().renderPostProcessing=true;
            AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(scene,Scene);
            File.WriteAllText(Output+"/setup.txt","Experimental scene="+Scene+"\nValidated Meadow source scene retained. 49x9x49 probes, spacing1m, finite ray distance6m, 128 deterministic directions/probe.\nOne reflected direct/environment-light bounce and sky visibility. Six simplified AABB proxies, no foliage occluders.\nGeometry-only half-metre surface transport cache, 12 cosine directions per surface cell.\nCPU budget2ms, maximum256 probes/frame; exact contiguous GPU buffer updates.\nCommit-only geometry invalidation halo=2*rayDistance+.1m; no gameplay/map commands changed.\nGround proxy top=.25m approximates the actual slightly varying Meadow terrain; not a finished terrain representation.\n");
        }
        static SpatialIndirectVolume.ProxyObject Proxy(string id,Renderer r,Color c)=>new SpatialIndirectVolume.ProxyObject{id=id,bounds=r.bounds,albedo=c,visual=r};
        static Renderer Box(string name,Bounds bounds,Material material)
        {var o=GameObject.CreatePrimitive(PrimitiveType.Cube);o.name=name;o.transform.position=bounds.center;o.transform.localScale=bounds.size;Object.DestroyImmediate(o.GetComponent<Collider>());var r=o.GetComponent<Renderer>();r.sharedMaterial=material;return r;}
        static Material Material(string name,Color color)
        {
            string path=Root+"/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(!m){m=new Material(Shader.Find("Little Castle/Surface/LC Stylized Lit"));AssetDatabase.CreateAsset(m,path);}
            m.SetColor("_Color",color);m.SetFloat("_LightResponse",1);m.SetFloat("_ShadowTintStrength",.12f);m.SetFloat("_RimStrength",0);m.SetFloat("_SpecularStrength",.02f);m.SetFloat("_OcclusionStrength",0);m.enableInstancing=true;EditorUtility.SetDirty(m);return m;
        }
        public static void BuildLinear()
        {
            Directory.CreateDirectory(Output);string[] settings={"ProjectSettings/ProjectSettings.asset","ProjectSettings/GraphicsSettings.asset","ProjectSettings/QualitySettings.asset"};var bytes=settings.ToDictionary(p=>p,File.ReadAllBytes);
            var space=PlayerSettings.colorSpace;bool timing=PlayerSettings.enableFrameTimingStats;int quality=QualitySettings.GetQualityLevel();
            var paths=Enumerable.Range(0,QualitySettings.names.Length).Select(i=>AssetDatabase.GetAssetPath(QualitySettings.GetRenderPipelineAssetAt(i))).ToArray();string defaultPath=AssetDatabase.GetAssetPath(GraphicsSettings.defaultRenderPipeline);int exit=2;
            try
            {
                Setup();PlayerSettings.colorSpace=ColorSpace.Linear;PlayerSettings.enableFrameTimingStats=true;
                var profiles=new[]{"HDR","AO"}.Select(n=>AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(LightingMeadowBuild.Root+"/"+n+".asset")).ToArray();GraphicsSettings.defaultRenderPipeline=profiles[1];
                for(int i=0;i<paths.Length;i++){QualitySettings.SetQualityLevel(i,false);QualitySettings.renderPipeline=profiles[i%2];}QualitySettings.SetQualityLevel(quality,false);
                var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{Scene,LightingMeadowBuild.Scene},locationPathName=Output+"/Player/SpatialIndirectStudy.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.None});
                File.WriteAllText(Output+"/build.txt",$"Result={report.summary.result}\nErrors={report.summary.totalErrors}\nWarnings={report.summary.totalWarnings}\nTime={report.summary.totalTime}\nRelease; Linear; FrameTimingStats=true\n");
                File.WriteAllLines(Output+"/build-warnings.txt",report.steps.SelectMany(s=>s.messages).Where(m=>m.type==LogType.Warning||m.type==LogType.Error).Select(m=>m.type+": "+m.content));exit=report.summary.result==BuildResult.Succeeded&&report.summary.totalErrors==0?0:2;
            }
            catch(Exception e){File.WriteAllText(Output+"/build-error.txt",e.ToString());Debug.LogException(e);}
            finally
            {
                PlayerSettings.colorSpace=space;PlayerSettings.enableFrameTimingStats=timing;AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                for(int i=0;i<paths.Length;i++){QualitySettings.SetQualityLevel(i,false);QualitySettings.renderPipeline=AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(paths[i]);}QualitySettings.SetQualityLevel(quality,false);GraphicsSettings.defaultRenderPipeline=AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(defaultPath);AssetDatabase.SaveAssets();foreach(var p in settings)File.WriteAllBytes(p,bytes[p]);
            }
            if(Application.isBatchMode)UrpBatchExit.WhenIdle(exit);
        }
    }
}
