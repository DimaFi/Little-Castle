using System;
using System.IO;
using System.Linq;
using System.Reflection;
using LittleCastle.Research;
using LittleCastle.Rendering;
using LittleCastle.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Object=UnityEngine.Object;

namespace LittleCastle.Editor
{
    // Explicit leased requests, scoped to this audit. Does not author the user's map.
    [InitializeOnLoad] public static partial class WaterRecoveryReview
    {
        public const string Root="E:/Games_Develop/Research/WaterReintegration_20261031";
        public const string Owner="01a191ef-b3bd-7be0-8e9d-bd5a9894b22c";
        public const string BuildTag="W07";
        [Serializable] sealed class Lease{public string ownerThreadId,status;}
        [Serializable] sealed class Request{public string id,action,revision;}
        [Serializable] sealed class Result{public string id,status,error;}
        static double next;
        static WaterRecoveryReview(){EditorApplication.update+=Poll;EditorApplication.update+=PollCurrentPlay;if(PlayStage!="")Application.logMessageReceived+=CollectPlayError;}
        public static bool Owns(){try{var l=JsonUtility.FromJson<Lease>(File.ReadAllText("Library/SharedPcControl.json"));return l.status=="active"&&l.ownerThreadId==Owner;}catch{return false;}}
        public static void Release(){if(Owns())File.WriteAllText("Library/SharedPcControl.json","{\"ownerThreadId\":\""+Owner+"\",\"status\":\"released\",\"purpose\":\"Water recovery review finished\",\"updatedAtUtc\":\""+DateTime.UtcNow.ToString("o")+"\"}");}
        static MapAuthoringWindow Window()=>Resources.FindObjectsOfTypeAll<MapAuthoringWindow>().Single(w=>w.VerificationSession!=null);
        static MapRiverToolState RiverState(MapAuthoringWindow w)=>(MapRiverToolState)typeof(MapAuthoringWindow).GetField("riverTools",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(w);
        static void Poll()
        {
            if(EditorApplication.timeSinceStartup<next)return;next=EditorApplication.timeSinceStartup+.5;
            if(!Owns()||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
            var file=Root+"/request.json";if(!File.Exists(file)||DateTime.UtcNow-File.GetLastWriteTimeUtc(file)>TimeSpan.FromMinutes(10))return;
            var r=JsonUtility.FromJson<Request>(File.ReadAllText(file));if(r==null||r.revision!=BuildTag||r.id==SessionState.GetString("LC.WaterRecovery.Request",""))return;
            if(!Resources.FindObjectsOfTypeAll<MapAuthoringWindow>().Any(w=>w.VerificationSession!=null))return;
            SessionState.SetString("LC.WaterRecovery.Request",r.id);var result=new Result{id=r.id,status="PASS"};
            try{if(r.action=="baseline")Baseline();else if(r.action=="review")Review();else if(r.action=="play-current")StartCurrentPlay();else if(r.action=="cleanup")Disarm();else throw new InvalidOperationException("Unknown request");}
            catch(Exception e){result.status="FAIL";result.error=e.ToString();}
            finally{File.WriteAllText(Root+"/result.json",JsonUtility.ToJson(result,true));if(PlayStage=="")Release();}
        }
        static void Baseline()
        {
            var w=Window();var s=w.VerificationSession;if(s.HasPending)throw new InvalidOperationException("User stroke active");
            var folder=Root+"/live-before";Directory.CreateDirectory(folder);
            File.WriteAllText(folder+"/map.json",JsonUtility.ToJson(s.ExportMap(),true));File.WriteAllText(folder+"/save.json",s.ExportJson());
            File.WriteAllText(folder+"/draft.json",RiverState(w).ExportDraftJson());File.WriteAllText(folder+"/window.json",EditorJsonUtility.ToJson(w,true));
        }
        static void Disarm()
        {
            EditorApplication.update-=Poll;EditorApplication.update-=PollCurrentPlay;Application.logMessageReceived-=CollectPlayError;
            var type=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("LittleCastle.Tests.WaterRecoveryTestRunner")).FirstOrDefault(t=>t!=null);type?.GetMethod("Disarm").Invoke(null,null);
        }
        static void Review()
        {
            var w=Window();var s=w.VerificationSession;if(s.HasPending)throw new InvalidOperationException("User stroke active");
            string before=JsonUtility.ToJson(s.ExportMap()),draft=RiverState(w).ExportDraftJson(),folder=Root+"/trial";Directory.CreateDirectory(folder);
            File.WriteAllText(folder+"/candidate-save.json",s.ExportJson());File.WriteAllText(folder+"/candidate-map.json",JsonUtility.ToJson(s.ExportMap(),true));
            var painter=Object.FindAnyObjectByType<MeadowSurfacePainter>();var cameraRoot=new GameObject("Water recovery review camera"){hideFlags=HideFlags.HideAndDontSave};
            var camera=cameraRoot.AddComponent<Camera>();if(painter.studyCamera)camera.CopyFrom(painter.studyCamera);camera.enabled=false;camera.nearClipPlane=.05f;camera.farClipPlane=300;camera.fieldOfView=48;camera.orthographic=false;
            var data=camera.GetUniversalAdditionalCameraData();data.requiresDepthTexture=true;data.requiresColorTexture=true;
            MapTerrainPresentation terrain=null;MapRiverPresentation rivers=null;RiverVisualQualityProfile qualityProfile=null;RiverEffectsQuality originalQuality=RiverEffectsQuality.Off;
            try
            {
                w.RestorePreview();terrain=new MapTerrainPresentation(s,MeadowSurfacePainter.FindGround(painter.terrainMaterial),Object.FindAnyObjectByType<MeadowGrassRenderer>(),Object.FindAnyObjectByType<InnerverseGrassStudy>());terrain.FlushColliderBudget(int.MaxValue);
                rivers=new MapRiverPresentation(s,painter.riverCatalogOverride?painter.riverCatalogOverride:MapRockCatalog.Default,painter.waterOnlyRivers){creationBudget=1024,maximumTiles=128,maximumDistance=140};
                var center=s.GetWaterfall(810003).route.Bounds.center;rivers.Tick(center,true);
                qualityProfile=rivers.EffectsProfile;originalQuality=qualityProfile.quality;
                void Shot(string name,Vector3 eye,Vector3 target){camera.transform.SetPositionAndRotation(eye,Quaternion.LookRotation(target-eye));Capture(camera,folder+"/"+name+".png");}
                var lower=s.RiverNetwork.Endpoint(810002,true);var join=new Vector3(lower.position.x,lower.waterHeight,lower.position.y);
                Shot("landing-close",join+new Vector3(4,1.7f,-4),join+new Vector3(-.8f,.15f,0));Shot("overview",center+new Vector3(21,14,-21),center);
                var path=new MapRiverPath(s.GetRiver(810101));var f=path.At(path.Length*.5f);var mc=new Vector3(f.center.x,f.water,f.center.y);
                Shot("meander-overview",mc+new Vector3(20,27,-21),mc);Shot("meander-water-level",mc+new Vector3(7,.7f,-9),mc);Shot("meander-underwater",mc+new Vector3(0,-.6f,-2),mc+Vector3.forward*5);
                camera.orthographic=true;camera.orthographicSize=14;Shot("orthographic",mc+new Vector3(15,18,-16),mc);camera.orthographic=false;
                qualityProfile.quality=RiverEffectsQuality.High;
                camera.orthographic=true;camera.orthographicSize=52;
                Shot("two-height-high",new Vector3(62,53,-18),new Vector3(6,5,22));
                if(rivers.Reflections.RenderedSlots!=2||!rivers.Reflections.IndependentSlots)throw new InvalidOperationException("High needs two independent visible planes; got "+rivers.Reflections.RenderedSlots);
                string reflection="High slots="+rivers.Reflections.RenderedSlots+" heights="+string.Join(",",rivers.Reflections.Heights)+" frame="+rivers.Reflections.LastFrame+" generation="+rivers.Reflections.Generation;
                int index=0;foreach(var texture in rivers.Reflections.Textures)SaveTexture(texture,folder+"/reflection-"+(index++)+".png");
                CheckOffscreenObject(rivers.Reflections,camera,folder);
                int generation=rivers.Reflections.Generation;
                Shot("two-height-moved",new Vector3(59,51,-21),new Vector3(6,5,22));if(rivers.Reflections.Generation<=generation)throw new InvalidOperationException("Reflection did not refresh after moving camera");
                qualityProfile.quality=RiverEffectsQuality.Medium;Shot("two-height-medium",new Vector3(62,53,-18),new Vector3(6,5,22));if(rivers.Reflections.RenderedSlots!=1)throw new InvalidOperationException("Medium reflection budget");
                qualityProfile.quality=RiverEffectsQuality.Low;Shot("two-height-low",new Vector3(62,53,-18),new Vector3(6,5,22));if(rivers.Reflections.RenderedSlots!=0)throw new InvalidOperationException("Low reflection budget");
                File.WriteAllText(folder+"/reflections.txt",reflection+"\nCamera move refresh PASS; Medium1 / Low0 PASS");
                qualityProfile.quality=originalQuality;camera.orthographic=false;
                // Real project lighting at night, with the exact clock state restored.
                var atmosphere=Object.FindAnyObjectByType<DayNightLightingController>();
                var globals=Object.FindAnyObjectByType<StylizedLightingGlobals>();
                if(atmosphere&&atmosphere.TimeSystem)
                {
                    var clock=atmosphere.TimeSystem;var state=clock.CaptureState();bool automatic=clock.RunAutomatically;
                    try{clock.RunAutomatically=false;clock.RestoreState(new WorldTimeState(1,60));atmosphere.ApplyLighting();if(globals)globals.ApplyGlobals();Shot("meander-night",mc+new Vector3(7,.7f,-9),mc);Shot("landing-night",join+new Vector3(4,1.7f,-4),join+new Vector3(-.8f,.15f,0));}
                    finally{clock.RestoreState(state);clock.RunAutomatically=automatic;atmosphere.ApplyLighting();if(globals)globals.ApplyGlobals();}
                }
                var renderers=rivers.Waterfalls.Renderers.Concat(rivers.WaterRenderers).Where(r=>r.sharedMaterial&&r.sharedMaterial.HasProperty("_PreviewTime")).ToArray();var block=new MaterialPropertyBlock();Directory.CreateDirectory(folder+"/motion");
                var eye=mc+new Vector3(7,.7f,-9);camera.transform.SetPositionAndRotation(eye,Quaternion.LookRotation(mc-eye));
                foreach(var renderer in renderers){renderer.GetPropertyBlock(block);block.SetFloat("_PreviewTime",2.75f);renderer.SetPropertyBlock(block);}Capture(camera,folder+"/waves-on.png");
                foreach(var renderer in renderers){renderer.GetPropertyBlock(block);block.SetFloat("_RiverWaveStrength",0);renderer.SetPropertyBlock(block);}Capture(camera,folder+"/waves-off.png");
                foreach(var renderer in renderers){renderer.GetPropertyBlock(block);block.SetFloat("_RiverWaveStrength",1);renderer.SetPropertyBlock(block);}
                for(int frame=0;frame<=120;frame++){float time=frame/12f;foreach(var renderer in renderers){renderer.GetPropertyBlock(block);block.SetFloat("_PreviewTime",time);renderer.SetPropertyBlock(block);}Capture(camera,folder+"/motion/"+frame.ToString("D4")+".png",960,600);}
                var errors=renderers.Select(r=>r.sharedMaterial.shader).Distinct().SelectMany(ShaderUtil.GetShaderMessages).Where(m=>m.severity==UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error).Select(m=>m.message).ToArray();File.WriteAllLines(folder+"/shader-errors.txt",errors);if(errors.Length>0)throw new InvalidOperationException("Water shader errors");
                var loaded=new MapEditSession(s.ExportMap());loaded.LoadJson(s.ExportJson());if(loaded.ExportJson()!=s.ExportJson())throw new InvalidOperationException("Save roundtrip mismatch");
                File.WriteAllText(folder+"/checks.txt","v10 roundtrip PASS; real Unity 121 frames / 10 seconds; shaderErrors=0");
            }
            finally{if(qualityProfile)qualityProfile.quality=originalQuality;rivers?.Dispose();terrain?.Dispose();Object.DestroyImmediate(cameraRoot);typeof(MapAuthoringWindow).GetMethod("BindScenePreview",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(w,null);bool same=before==JsonUtility.ToJson(s.ExportMap())&&draft==RiverState(w).ExportDraftJson();File.WriteAllText(folder+"/restored.txt",same?"Exact live map/draft match":"MISMATCH");if(!same)throw new InvalidOperationException("Map/draft changed");}
        }
        static void SaveTexture(RenderTexture rt,string path)
        {var old=RenderTexture.active;Texture2D pixels=null;try{RenderTexture.active=rt;pixels=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);pixels.Apply();File.WriteAllBytes(path,pixels.EncodeToPNG());}finally{RenderTexture.active=old;if(pixels)Object.DestroyImmediate(pixels);}}
        static void CheckOffscreenObject(MapRiverReflections reflections,Camera main,string folder)
        {
            var slots=(Array)typeof(MapRiverReflections).GetField("slots",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(reflections);
            var first=slots.GetValue(0);var reflected=(Camera)first.GetType().GetField("camera").GetValue(first);
            var frustum=GeometryUtility.CalculateFrustumPlanes(main);Vector3 position=default,moved=default;bool found=false;
            foreach(float y in new[]{40f,60,25,15})
            {for(int x=-80;x<=80&&!found;x+=10)for(int z=-80;z<=120&&!found;z+=10)
                {
                    var point=new Vector3(x,y,z);var uv=reflected.WorldToViewportPoint(point);var next=point+reflected.transform.right*6;
                    if(uv.z<=0||uv.x<.15||uv.x>.85||uv.y<.15||uv.y>.85)continue;
                    if(GeometryUtility.TestPlanesAABB(frustum,new Bounds(point,Vector3.one*4))||GeometryUtility.TestPlanesAABB(frustum,new Bounds(next,Vector3.one*4)))continue;
                    position=point;moved=next;found=true;
                }
                if(found)break;
            }
            if(!found)throw new InvalidOperationException("No offscreen reflection probe location");
            var marker=GameObject.CreatePrimitive(PrimitiveType.Cube);marker.hideFlags=HideFlags.HideAndDontSave;marker.name="Temporary offscreen reflection probe";marker.transform.position=position;marker.transform.localScale=Vector3.one*4;
            var material=new Material(Shader.Find("Universal Render Pipeline/Unlit")){hideFlags=HideFlags.HideAndDontSave};material.SetColor("_BaseColor",Color.magenta);marker.GetComponent<Renderer>().sharedMaterial=material;
            try
            {
                Capture(main,folder+"/offscreen-warmup.png");
                Capture(main,folder+"/offscreen-main-before.png");SaveTexture(reflections.Textures[0],folder+"/offscreen-reflection-before.png");
                marker.transform.position=moved;Capture(main,folder+"/offscreen-main-after.png");SaveTexture(reflections.Textures[0],folder+"/offscreen-reflection-after.png");
                if(File.ReadAllBytes(folder+"/offscreen-reflection-before.png").SequenceEqual(File.ReadAllBytes(folder+"/offscreen-reflection-after.png")))throw new InvalidOperationException("Offscreen object did not update reflection");
                File.WriteAllText(folder+"/offscreen-check.txt","Object bounds outside primary frustum at both positions: "+position+" -> "+moved+". Reflection RT changed after move.");
            }
            finally{Object.DestroyImmediate(marker);Object.DestroyImmediate(material);}
        }
        static void Capture(Camera camera,string path,int width=1440,int height=900)
        {
            var rt=RenderTexture.GetTemporary(width,height,24,RenderTextureFormat.ARGB32);var old=RenderTexture.active;Texture2D pixels=null;
            try{CameraCaptureCompatibility.Render(camera,rt);RenderTexture.active=rt;pixels=new Texture2D(width,height,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,width,height),0,0);pixels.Apply();File.WriteAllBytes(path,pixels.EncodeToPNG());}
            finally{RenderTexture.active=old;RenderTexture.ReleaseTemporary(rt);if(pixels)Object.DestroyImmediate(pixels);}
        }
    }
}
