using System;
using System.IO;
using UnityEngine;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using System.Collections.Generic;

namespace LittleCastle.World
{
    /// <summary>Local preview quality controls and an explicitly requested standalone QA run.</summary>
    public sealed class TerrainStarterPreviewControls : MonoBehaviour
    {
        private bool low;
        public enum PreviewQuality { Low, Medium, High, Ultra }
        private PreviewQuality quality=PreviewQuality.Medium;
        private bool stackOff, aoOff, dofOff, filterOff, terrainOff;
        private float oldTerrainDetail;
        private ProfilerRecorder draws,batches,setPass,triangles,vertices;
        private readonly FrameTiming[] gpuTiming=new FrameTiming[1];
        private double gpuSum; private int gpuSamples;
        private double cpuSum,mainThreadSum,renderThreadSum; private int cpuSamples;
        private bool qa;
        private int count;
        private bool visualQA;
        private int visualStage;
        private readonly float[] frames = new float[600];
        private int oldVsync, oldTarget, oldMip, oldAA;
        private float oldLod, oldShadow;
        private ShadowQuality oldShadows;
        private ShadowResolution oldShadowResolution;
        private int oldPixelLights, oldCascades;
        private int startFrame;
        private long lastRenderStamp;
        private readonly System.Diagnostics.Stopwatch qaClock=new System.Diagnostics.Stopwatch();
        private void OnEnable()
        {
            oldVsync=QualitySettings.vSyncCount; oldTarget=Application.targetFrameRate;
            oldTerrainDetail=Shader.GetGlobalFloat("_LC_LowTerrainDetail");
            oldMip=QualitySettings.globalTextureMipmapLimit; oldLod=QualitySettings.lodBias;
            oldShadow=QualitySettings.shadowDistance;
            oldAA=QualitySettings.antiAliasing; QualitySettings.antiAliasing=4;
            oldShadows=QualitySettings.shadows; oldShadowResolution=QualitySettings.shadowResolution;
            oldPixelLights=QualitySettings.pixelLightCount; oldCascades=QualitySettings.shadowCascades;
            qa=Array.IndexOf(Environment.GetCommandLineArgs(),"-starterQA")>=0;
            visualQA=Array.IndexOf(Environment.GetCommandLineArgs(),"-starterVisual")>=0;
            low=Array.IndexOf(Environment.GetCommandLineArgs(),"-starterLow")>=0;
            quality=low ? PreviewQuality.Low : Array.IndexOf(Environment.GetCommandLineArgs(),"-starterUltra")>=0 ? PreviewQuality.Ultra :
                Array.IndexOf(Environment.GetCommandLineArgs(),"-starterHigh")>=0 ? PreviewQuality.High : PreviewQuality.Medium;
            stackOff=Array.IndexOf(Environment.GetCommandLineArgs(),"-starterStackOff")>=0;
            aoOff=Array.IndexOf(Environment.GetCommandLineArgs(),"-starterAOOff")>=0;
            dofOff=Array.IndexOf(Environment.GetCommandLineArgs(),"-starterDOFOff")>=0;
            filterOff=Array.IndexOf(Environment.GetCommandLineArgs(),"-starterFilterOff")>=0;
            terrainOff=Array.IndexOf(Environment.GetCommandLineArgs(),"-starterTerrainOff")>=0;
            if(qa) {
                draws=RenderCounter("Draw Calls Count","Draw Calls");
                batches=RenderCounter("Batches Count","Batches");
                setPass=ProfilerRecorder.StartNew(ProfilerCategory.Render,"SetPass Calls Count");
                triangles=ProfilerRecorder.StartNew(ProfilerCategory.Render,"Triangles Count");
                vertices=ProfilerRecorder.StartNew(ProfilerCategory.Render,"Vertices Count");
            }
            if(qa || visualQA) Application.runInBackground=true;
            if(visualQA)
            {
                GetComponent<LittleCastle.CameraSystem.StrategyCameraController>().enabled=false;
                transform.position=new Vector3(-19,12,-16); transform.LookAt(new Vector3(-10,2,-3));
                GetComponent<Camera>().orthographicSize=10;
            }
            QualitySettings.vSyncCount=qa ? 0 : 1; Application.targetFrameRate=qa ? -1 : 60;
            startFrame=Time.frameCount; ApplyQuality();
            qaClock.Restart();
        }
        private static ProfilerRecorder RenderCounter(string name,string alternate)
        {
            var handles=new List<ProfilerRecorderHandle>();
            ProfilerRecorderHandle.GetAvailable(handles);
            foreach(var handle in handles) {
                var description=ProfilerRecorderHandle.GetDescription(handle);
                if(description.Name==name || description.Name==alternate)
                    return ProfilerRecorder.StartNew(description.Category,description.Name);
            }
            foreach(var handle in handles) {
                var description=ProfilerRecorderHandle.GetDescription(handle);
                if(description.Name.IndexOf("Draw",StringComparison.OrdinalIgnoreCase)>=0 ||
                    description.Name.IndexOf("Batch",StringComparison.OrdinalIgnoreCase)>=0)
                    Debug.Log("[Art QA counter] "+description.Name+" / "+description.Category);
            }
            return default;
        }
        private void OnDisable()
        {
            QualitySettings.vSyncCount=oldVsync; Application.targetFrameRate=oldTarget;
            QualitySettings.globalTextureMipmapLimit=oldMip; QualitySettings.lodBias=oldLod;
            QualitySettings.shadowDistance=oldShadow;
            QualitySettings.antiAliasing=oldAA;
            QualitySettings.shadows=oldShadows; QualitySettings.shadowResolution=oldShadowResolution;
            QualitySettings.pixelLightCount=oldPixelLights; QualitySettings.shadowCascades=oldCascades;
            Shader.SetGlobalFloat("_LC_LowTerrainDetail",oldTerrainDetail);
            draws.Dispose(); batches.Dispose(); setPass.Dispose(); triangles.Dispose(); vertices.Dispose();
        }
        private void ApplyQuality()
        {
            low=quality==PreviewQuality.Low;
            Shader.SetGlobalFloat("_LC_LowTerrainDetail",low || terrainOff ? 1 : 0);
            QualitySettings.antiAliasing=low ? 2 : quality==PreviewQuality.Ultra ? 8 : 4;
            QualitySettings.pixelLightCount=1;
            QualitySettings.shadows=low ? ShadowQuality.HardOnly : ShadowQuality.All;
            QualitySettings.shadowResolution=low ? ShadowResolution.Low : quality==PreviewQuality.Ultra ? ShadowResolution.VeryHigh : ShadowResolution.High;
            QualitySettings.shadowCascades=low ? 0 : quality==PreviewQuality.Ultra ? 4 : 2;
            QualitySettings.globalTextureMipmapLimit=low ? 1 : 0;
            QualitySettings.lodBias=low ? .65f : quality==PreviewQuality.High ? 1.2f : quality==PreviewQuality.Ultra ? 1.4f : 1;
            QualitySettings.shadowDistance=low ? 45 : quality==PreviewQuality.High ? 80 : quality==PreviewQuality.Ultra ? 100 : 65;
            var soft=GetComponent<TerrainStarterSoftScene>();
            if(soft) { soft.enabled=!low && !stackOff; soft.blurStrength=dofOff ? 0 : quality==PreviewQuality.Medium ? .65f : .85f;
                soft.contactOcclusion=aoOff ? 0 : .28f; soft.shadowFilterRadius=filterOff ? 0 : quality==PreviewQuality.Medium ? 3 : 4; }
        }
        private void Update()
        {
            if(Input.GetKeyDown(KeyCode.F1)) { quality=(PreviewQuality)(((int)quality+1)%4); ApplyQuality(); }
            if(Input.GetKeyDown(KeyCode.F2)) { stackOff=!stackOff; ApplyQuality(); }
            if(Input.GetKeyDown(KeyCode.F3)) { aoOff=!aoOff; ApplyQuality(); }
            if(Input.GetKeyDown(KeyCode.F4)) { dofOff=!dofOff; ApplyQuality(); }
            if(Input.GetKeyDown(KeyCode.F5)) { filterOff=!filterOff; ApplyQuality(); }
            if(Input.GetKeyDown(KeyCode.F6)) { terrainOff=!terrainOff; ApplyQuality(); }
            if(qa) FrameTimingManager.CaptureFrameTimings();
            if(visualQA)
            {
                double seconds=qaClock.Elapsed.TotalSeconds;
                if(visualStage==0 && seconds>1) { CapturePreview("close-a"); visualStage++; }
                else if(visualStage==1 && seconds>2.5) { CapturePreview("close-b"); visualStage++;
                    transform.position=new Vector3(-43,38,-48); transform.LookAt(new Vector3(-8,0,-3));
                    GetComponent<Camera>().orthographicSize=28; }
                else if(visualStage==2 && seconds>4) { CapturePreview("far"); visualStage++;
                    transform.position=new Vector3(-14,7,-18); transform.LookAt(new Vector3(-9.8f,0,-10));
                    GetComponent<Camera>().orthographicSize=4.5f; }
                else if(visualStage==3 && seconds>5.5) { CapturePreview("ground"); visualStage++; Application.Quit(); }
                return;
            }
            if(qa && Time.frameCount-startFrame==60) CapturePreview();
            if(qa && qaClock.Elapsed.TotalSeconds>30) FinishQA();
        }
        private void OnPostRender()
        {
            if(!qa || Time.frameCount-startFrame < 120) return;
            long stamp=System.Diagnostics.Stopwatch.GetTimestamp();
            if(lastRenderStamp==0) { lastRenderStamp=stamp; return; }
            frames[count++]=(float)((stamp-lastRenderStamp)*1000.0/System.Diagnostics.Stopwatch.Frequency);
            if(FrameTimingManager.GetLatestTimings(1,gpuTiming)>0) {
                if(gpuTiming[0].gpuFrameTime>0) { gpuSum+=gpuTiming[0].gpuFrameTime; gpuSamples++; }
                if(gpuTiming[0].cpuFrameTime>0) { cpuSum+=gpuTiming[0].cpuFrameTime;
                    mainThreadSum+=gpuTiming[0].cpuMainThreadFrameTime;
                    renderThreadSum+=gpuTiming[0].cpuRenderThreadFrameTime; cpuSamples++; }
            }
            lastRenderStamp=stamp;
            if(count < frames.Length) return;
            FinishQA();
        }
        private void FinishQA()
        {
            Array.Sort(frames,0,count);
            float sum=0; for(int i=0;i<count;i++) sum+=frames[i];
            Directory.CreateDirectory("Logs/TerrainStarter");
            File.WriteAllText("Logs/TerrainStarter/player-performance-"+CaptureLabel+".json",JsonUtility.ToJson(new Report {
                device=SystemInfo.graphicsDeviceName,cpu=SystemInfo.processorType,
                width=Screen.width,height=Screen.height,meanMs=count>0 ? sum/count : 0,
                p95Ms=count>0 ? frames[(int)(count*.95f)] : 0,
                samples=count, seconds=(float)qaClock.Elapsed.TotalSeconds,
                quality=quality.ToString(),visualStack=!stackOff && !low,
                gpuMeanMs=gpuSamples>0 ? gpuSum/gpuSamples : 0,gpuSamples=gpuSamples,
                cpuMeanMs=cpuSamples>0 ? cpuSum/cpuSamples : 0,cpuSamples=cpuSamples,
                mainThreadMeanMs=cpuSamples>0 ? mainThreadSum/cpuSamples : 0,
                renderThreadMeanMs=cpuSamples>0 ? renderThreadSum/cpuSamples : 0,
                drawCalls=draws.Valid ? draws.LastValue : -1,batches=batches.Valid ? batches.LastValue : -1,
                setPassCalls=setPass.Valid ? setPass.LastValue : -1,triangles=triangles.Valid ? triangles.LastValue : -1,
                vertices=vertices.Valid ? vertices.LastValue : -1,
                allocatedBytes=UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong() },true));
            qa=false;
            Application.Quit();
        }
        private void CapturePreview(string suffix="")
        {
            var camera=GetComponent<Camera>();
            var previous=camera.targetTexture;
            var active=RenderTexture.active;
            var target=RenderTexture.GetTemporary(1600,1000,24,RenderTextureFormat.Default,RenderTextureReadWrite.Default,low ? 2 : 4);
            var pixels=new Texture2D(1600,1000,TextureFormat.RGB24,false);
            try {
                camera.targetTexture=target; camera.Render(); RenderTexture.active=target;
                pixels.ReadPixels(new Rect(0,0,1600,1000),0,0); pixels.Apply();
                Directory.CreateDirectory("Logs/TerrainStarter");
                File.WriteAllBytes("Logs/TerrainStarter/player-"+CaptureLabel+(suffix.Length>0 ? "-"+suffix : "")+".png",pixels.EncodeToPNG());
            }
            finally { camera.targetTexture=previous; RenderTexture.active=active;
                RenderTexture.ReleaseTemporary(target); Destroy(pixels); }
        }
        private void LateUpdate()
        {
            var camera=GetComponent<Camera>();
            if(camera.orthographic && !visualQA)
                camera.orthographicSize=Mathf.Clamp(transform.position.y*.8f,5,48);
        }
        private string CaptureLabel => quality.ToString().ToLowerInvariant()+
            (stackOff ? "-stack-off" : "")+(aoOff ? "-ao-off" : "")+
            (dofOff ? "-dof-off" : "")+(filterOff ? "-filter-off" : "")+(terrainOff ? "-terrain-off" : "");
        private void OnGUI()
        {
            GUI.Label(new Rect(16,16,1250,30),gameObject.scene.name+" | WASD / Wheel / RMB | F1: " +quality+" | F2: stack "+(!stackOff)+" | F3: AO | F4: DOF | F5: soft shadows | F6: ground detail");
        }
        [Serializable] private class Report { public string device,cpu,quality; public bool visualStack;
            public int width,height,samples,gpuSamples,cpuSamples; public float meanMs,p95Ms,seconds;
            public double gpuMeanMs,cpuMeanMs,mainThreadMeanMs,renderThreadMeanMs;
            public long drawCalls,batches,setPassCalls,triangles,vertices,allocatedBytes; }
    }
}
