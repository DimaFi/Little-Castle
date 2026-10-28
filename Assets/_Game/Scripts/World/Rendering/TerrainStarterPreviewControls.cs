using System;
using System.IO;
using UnityEngine;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using System.Collections.Generic;
using UnityEngine.Rendering;

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
        private int lastSampleFrame = -1;
        private bool captureInProgress, oldRunInBackground;
        private Camera previewCamera;
        private UrpPreviewQualityOverride urpQuality;
        private TerrainStarterSoftScene softScene;
        private bool oldSoftEnabled;
        private float oldBlur, oldContact, oldFilter;
        private readonly System.Diagnostics.Stopwatch qaClock=new System.Diagnostics.Stopwatch();
        private void OnEnable()
        {
            previewCamera=GetComponent<Camera>();
            oldRunInBackground=Application.runInBackground;
            softScene=GetComponent<TerrainStarterSoftScene>();
            if(softScene) { oldSoftEnabled=softScene.enabled; oldBlur=softScene.blurStrength;
                oldContact=softScene.contactOcclusion; oldFilter=softScene.shadowFilterRadius; }
            RenderPipelineManager.endCameraRendering+=OnEndCameraRendering;
            oldVsync=QualitySettings.vSyncCount; oldTarget=Application.targetFrameRate;
            oldTerrainDetail=Shader.GetGlobalFloat("_LC_LowTerrainDetail");
            oldMip=QualitySettings.globalTextureMipmapLimit; oldLod=QualitySettings.lodBias;
            oldShadow=QualitySettings.shadowDistance;
            oldAA=QualitySettings.antiAliasing;
            oldShadows=QualitySettings.shadows; oldShadowResolution=QualitySettings.shadowResolution;
            oldPixelLights=QualitySettings.pixelLightCount; oldCascades=QualitySettings.shadowCascades;
            qa=Array.IndexOf(System.Environment.GetCommandLineArgs(),"-starterQA")>=0;
            visualQA=Array.IndexOf(System.Environment.GetCommandLineArgs(),"-starterVisual")>=0;
            low=Array.IndexOf(System.Environment.GetCommandLineArgs(),"-starterLow")>=0;
            quality=low ? PreviewQuality.Low : Array.IndexOf(System.Environment.GetCommandLineArgs(),"-starterUltra")>=0 ? PreviewQuality.Ultra :
                Array.IndexOf(System.Environment.GetCommandLineArgs(),"-starterHigh")>=0 ? PreviewQuality.High : PreviewQuality.Medium;
            urpQuality=UrpPreviewQualityOverride.Create(quality);
            stackOff=Array.IndexOf(System.Environment.GetCommandLineArgs(),"-starterStackOff")>=0;
            aoOff=Array.IndexOf(System.Environment.GetCommandLineArgs(),"-starterAOOff")>=0;
            dofOff=Array.IndexOf(System.Environment.GetCommandLineArgs(),"-starterDOFOff")>=0;
            filterOff=Array.IndexOf(System.Environment.GetCommandLineArgs(),"-starterFilterOff")>=0;
            terrainOff=Array.IndexOf(System.Environment.GetCommandLineArgs(),"-starterTerrainOff")>=0;
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
            RenderPipelineManager.endCameraRendering-=OnEndCameraRendering;
            urpQuality?.Dispose(); urpQuality=null;
            Application.runInBackground=oldRunInBackground;
            if(softScene) { softScene.enabled=oldSoftEnabled; softScene.blurStrength=oldBlur;
                softScene.contactOcclusion=oldContact; softScene.shadowFilterRadius=oldFilter; }
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
            if(urpQuality!=null) urpQuality.SetQuality(quality);
            else if(GraphicsSettings.currentRenderPipeline==null)
            {
                // Keep preview/capture tooling usable in an explicit Built-in baseline.
                QualitySettings.antiAliasing=low ? 2 : quality==PreviewQuality.Ultra ? 8 : 4;
                QualitySettings.pixelLightCount=1;
                QualitySettings.shadows=low ? ShadowQuality.HardOnly : ShadowQuality.All;
                QualitySettings.shadowResolution=low ? ShadowResolution.Low : quality==PreviewQuality.Ultra ? ShadowResolution.VeryHigh : ShadowResolution.High;
                QualitySettings.shadowCascades=low ? 0 : quality==PreviewQuality.Ultra ? 4 : 2;
                QualitySettings.shadowDistance=low ? 45 : quality==PreviewQuality.High ? 80 : quality==PreviewQuality.Ultra ? 100 : 65;
            }
            QualitySettings.globalTextureMipmapLimit=low ? 1 : 0;
            QualitySettings.lodBias=low ? .65f : quality==PreviewQuality.High ? 1.2f : quality==PreviewQuality.Ultra ? 1.4f : 1;
            var soft=GetComponent<TerrainStarterSoftScene>();
            if(soft) { soft.enabled=!low && !stackOff; soft.blurStrength=dofOff ? 0 : quality==PreviewQuality.Medium ? .65f : .85f;
                soft.contactOcclusion=aoOff ? 0 : .28f; soft.shadowFilterRadius=filterOff ? 0 : quality==PreviewQuality.Medium ? 3 : 4; }
            if(qa) { startFrame=Time.frameCount; count=0; lastRenderStamp=0; lastSampleFrame=-1;
                gpuSum=cpuSum=mainThreadSum=renderThreadSum=0; gpuSamples=cpuSamples=0; qaClock.Restart(); }
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
            if(GraphicsSettings.currentRenderPipeline==null) SampleRenderedFrame();
        }
        private void OnEndCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            if(camera==previewCamera && GraphicsSettings.currentRenderPipeline!=null) SampleRenderedFrame();
        }
        private void SampleRenderedFrame()
        {
            // A StandardRequest also invokes endCameraRendering. Count only one
            // ordinary render of this camera per frame; exclude synchronous captures.
            // URP temporarily assigns request.destination to camera.targetTexture,
            // so this also excludes requests issued by other capture tools.
            if(previewCamera && previewCamera.targetTexture!=null) { lastRenderStamp=0; return; }
            if(!qa || captureInProgress || Time.frameCount==lastSampleFrame || Time.frameCount-startFrame < 120) return;
            lastSampleFrame=Time.frameCount;
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
                pipelineSettings=UrpPreviewQuality.ReadActive(),
                renderCallback=GraphicsSettings.currentRenderPipeline ? "endCameraRendering" : "OnPostRender",
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
            captureInProgress=true;
            var camera=GetComponent<Camera>();
            var previous=camera.targetTexture;
            var active=RenderTexture.active;
            var target=RenderTexture.GetTemporary(1600,1000,24,RenderTextureFormat.Default,RenderTextureReadWrite.Default,low ? 2 : 4);
            var pixels=new Texture2D(1600,1000,TextureFormat.RGB24,false);
            try {
                camera.targetTexture=target; LittleCastle.Rendering.CameraCaptureCompatibility.Render(camera,target); RenderTexture.active=target;
                pixels.ReadPixels(new Rect(0,0,1600,1000),0,0); pixels.Apply();
                Directory.CreateDirectory("Logs/TerrainStarter");
                File.WriteAllBytes("Logs/TerrainStarter/player-"+CaptureLabel+(suffix.Length>0 ? "-"+suffix : "")+".png",pixels.EncodeToPNG());
            }
            finally { camera.targetTexture=previous; RenderTexture.active=active;
                RenderTexture.ReleaseTemporary(target); Destroy(pixels);
                captureInProgress=false; lastRenderStamp=0; }
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
        [Serializable] private class Report { public string device,cpu,quality,renderCallback; public bool visualStack;
            public UrpPreviewQuality.Snapshot pipelineSettings;
            public int width,height,samples,gpuSamples,cpuSamples; public float meanMs,p95Ms,seconds;
            public double gpuMeanMs,cpuMeanMs,mainThreadMeanMs,renderThreadMeanMs;
            public long drawCalls,batches,setPassCalls,triangles,vertices,allocatedBytes; }
    }
}
