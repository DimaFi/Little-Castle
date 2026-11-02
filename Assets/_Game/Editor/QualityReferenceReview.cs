using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using LittleCastle.Rendering;
using LittleCastle.Research;
using LittleCastle.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace LittleCastle.Editor
{
    // One-shot, read-only quality audit. No map restoration from an old snapshot.
    [InitializeOnLoad]
    static class QualityReferenceReview
    {
        const string Owner="01a18b72-7012-7de0-8a5f-9a5a1f5f5265";
        const string Request="Library/QualityReferenceReviewRequest.json";
        const string Output="E:/Games_Develop/Research/EnvironmentSoftness_20261031/quality-check";
        [Serializable] class Lease {public string ownerThreadId,status,purpose,updatedAtUtc;}
        [Serializable] class Command {public string id;}
        [Serializable] class Sample
        {
            public string file,quality,pipeline,cameraAA;
            public int qualityIndex,pipelineMSAA,targetMSAA,mipLimit,screenWidth,screenHeight;
            public float renderScale,lodBias,shadowDistance,blurStrength;
            public bool hdr,allowMSAA,softEnabled,atmosphereOverride;
            public string atmosphereQuality;
        }
        [Serializable] class Report
        {
            public string status,error,scene,gpu,api;
            public bool mapPreserved,assetsPreserved,dirtyPreserved,qualityRestored,cameraRestored,editMode;
            public Sample original;
            public List<Sample> samples=new List<Sample>();
            public List<string> errors=new List<string>(),grass=new List<string>();
        }
        static double next;
        static bool done;
        static QualityReferenceReview(){EditorApplication.update+=Poll;}
        static void Poll()
        {
            if(done||EditorApplication.timeSinceStartup<next)return;next=EditorApplication.timeSinceStartup+1;
            if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||!File.Exists(Request))return;
            var lease=JsonUtility.FromJson<Lease>(File.ReadAllText("Library/SharedPcControl.json"));
            if(lease==null||lease.ownerThreadId!=Owner||lease.status!="active")return;
            done=true;
            try{Run();}
            finally
            {
                EditorApplication.update-=Poll;File.Delete(Request);
                lease.status="released";lease.purpose="Quality A/B done; settings/map restored, one-shot callback disarmed";
                lease.updatedAtUtc=DateTime.UtcNow.ToString("o");
                const string leasePath="Library/SharedPcControl.json";
                string temporaryLease=leasePath+".quality-review.tmp";
                File.WriteAllText(temporaryLease,JsonUtility.ToJson(lease,true));
                if(File.Exists(leasePath))File.Replace(temporaryLease,leasePath,null);
                else File.Move(temporaryLease,leasePath);
                Directory.CreateDirectory(Output);File.WriteAllText(Output+"/disarmed.json","{\"disarmed\":true,\"playMode\":false}");
            }
        }
        static string MapHash()=>string.Join("|",Resources.FindObjectsOfTypeAll<MapAuthoringWindow>().Where(w=>w.VerificationSession!=null).Select(w=>JsonUtility.ToJson(w.VerificationSession.ExportMap())));
        static string Hash(string path){if(!File.Exists(path))return "missing";using(var h=SHA256.Create())return Convert.ToBase64String(h.ComputeHash(File.ReadAllBytes(path)));}
        static Sample Read(Camera camera,DayNightLightingController atmosphere,string file="",int samples=0)
        {
            var pipeline=(QualitySettings.renderPipeline??GraphicsSettings.defaultRenderPipeline) as UniversalRenderPipelineAsset;
            var soft=camera.GetComponent<TerrainStarterSoftScene>();
            return new Sample {file=file,qualityIndex=QualitySettings.GetQualityLevel(),quality=QualitySettings.names[QualitySettings.GetQualityLevel()],
                pipeline=pipeline?pipeline.name:"none",pipelineMSAA=pipeline?pipeline.msaaSampleCount:0,targetMSAA=samples,
                renderScale=pipeline?pipeline.renderScale:0,hdr=pipeline&&pipeline.supportsHDR,lodBias=QualitySettings.lodBias,
                shadowDistance=pipeline?pipeline.shadowDistance:0,mipLimit=QualitySettings.globalTextureMipmapLimit,
                allowMSAA=camera.allowMSAA,cameraAA=camera.TryGetComponent<UniversalAdditionalCameraData>(out var data)?data.antialiasing.ToString():"none",
                softEnabled=soft&&soft.enabled,blurStrength=soft?soft.blurStrength:0,screenWidth=camera.pixelWidth,screenHeight=camera.pixelHeight,
                atmosphereOverride=atmosphere&&atmosphere.HasAtmosphereQualityOverride,atmosphereQuality=atmosphere?atmosphere.AtmosphereQuality.ToString():"none"};
        }
        static void Run()
        {
            Directory.CreateDirectory(Output);
            var report=new Report {scene=SceneManager.GetActiveScene().path,gpu=SystemInfo.graphicsDeviceName,api=SystemInfo.graphicsDeviceType.ToString()};
            var scene=SceneManager.GetActiveScene();bool dirty=scene.isDirty;
            var painter=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<MeadowSurfacePainter>(true)).FirstOrDefault();
            if(!painter||!painter.studyCamera)throw new InvalidOperationException("Open current WorldEditor scene.");
            var camera=painter.studyCamera;
            var atmosphere=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<DayNightLightingController>(true)).FirstOrDefault();
            var globals=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<StylizedLightingGlobals>(true)).FirstOrDefault();
            int quality=QualitySettings.GetQualityLevel();var pipeline=QualitySettings.renderPipeline;
            var pose=camera.transform.localToWorldMatrix;float aspect=camera.aspect;
            var soft=camera.GetComponent<TerrainStarterSoftScene>();bool softEnabled=soft&&soft.enabled;
            float blur=soft?soft.blurStrength:0;string map=MapHash();
            var paths=new[]{scene.path,AssetDatabase.GetAssetPath(painter.source),"ProjectSettings/QualitySettings.asset","ProjectSettings/GraphicsSettings.asset"};
            var hashes=paths.Select(Hash).ToArray();
            void Log(string m,string s,LogType t){if(t==LogType.Error||t==LogType.Exception||t==LogType.Assert)report.errors.Add(m);}
            Application.logMessageReceived+=Log;
            try
            {
                report.original=Read(camera,atmosphere);Capture(camera,Output+"/00-current.png",Samples());
                foreach(string tier in new[]{"Medium","High"})
                {
                    int index=Array.IndexOf(QualitySettings.names,tier);if(index<0)throw new InvalidOperationException(tier);
                    QualitySettings.SetQualityLevel(index,false);atmosphere?.ApplyLighting();globals?.ApplyGlobals();
                    string name=tier=="Medium"?"01-medium":"02-high";
                    var sample=Read(camera,atmosphere,name+".png",Samples());Capture(camera,Output+"/"+sample.file,sample.targetMSAA);report.samples.Add(sample);
                }
                var one=Read(camera,atmosphere,"03-high-target-1x.png",1);Capture(camera,Output+"/"+one.file,1);report.samples.Add(one);
                if(soft)soft.enabled=false;
                var clean=Read(camera,atmosphere,"04-high-without-softscene.png",Samples());Capture(camera,Output+"/"+clean.file,clean.targetMSAA);report.samples.Add(clean);
                foreach(var grass in scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<MeadowGrassRenderer>(true)))
                    report.grass.Add(grass.name+"; clumps="+grass.submittedClumps+"; triangles="+grass.submittedTriangles+"; LOD distances="+grass.lodDistances+"; force="+grass.forceFullDetail);
            }
            catch(Exception e){report.error=e.ToString();}
            finally
            {
                QualitySettings.SetQualityLevel(quality,false);QualitySettings.renderPipeline=pipeline;
                camera.aspect=aspect;if(soft){soft.enabled=softEnabled;soft.blurStrength=blur;}
                atmosphere?.ApplyLighting();globals?.ApplyGlobals();
                Application.logMessageReceived-=Log;
                report.mapPreserved=MapHash()==map;report.assetsPreserved=paths.Select(Hash).SequenceEqual(hashes);
                report.dirtyPreserved=scene.isDirty==dirty;report.qualityRestored=QualitySettings.GetQualityLevel()==quality&&QualitySettings.renderPipeline==pipeline;
                report.cameraRestored=camera.transform.localToWorldMatrix==pose&&Mathf.Approximately(camera.aspect,aspect);
                report.editMode=!EditorApplication.isPlayingOrWillChangePlaymode;
                report.status=report.error==null&&report.errors.Count==0&&report.mapPreserved&&report.assetsPreserved&&report.dirtyPreserved&&report.qualityRestored&&report.cameraRestored&&report.editMode?"PASS":"FAIL";
                File.WriteAllText(Output+"/result.json",JsonUtility.ToJson(report,true));
            }
        }
        static int Samples(){var p=(QualitySettings.renderPipeline??GraphicsSettings.defaultRenderPipeline) as UniversalRenderPipelineAsset;return p?p.msaaSampleCount:1;}
        static void Capture(Camera camera,string path,int samples)
        {
            var descriptor=new RenderTextureDescriptor(1600,900,RenderTextureFormat.ARGB32,24){msaaSamples=samples};
            var target=RenderTexture.GetTemporary(descriptor);var resolved=RenderTexture.GetTemporary(1600,900,0,RenderTextureFormat.ARGB32);
            var previous=RenderTexture.active;float aspect=camera.aspect;Texture2D pixels=null;
            try
            {
                camera.aspect=1600f/900;CameraCaptureCompatibility.Render(camera,target);CameraCaptureCompatibility.Render(camera,target);
                Graphics.Blit(target,resolved);RenderTexture.active=resolved;
                pixels=new Texture2D(1600,900,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,1600,900),0,0);pixels.Apply();
                File.WriteAllBytes(path,pixels.EncodeToPNG());
            }
            finally{camera.aspect=aspect;RenderTexture.active=previous;if(pixels)Object.DestroyImmediate(pixels);RenderTexture.ReleaseTemporary(target);RenderTexture.ReleaseTemporary(resolved);}
        }
    }
}
