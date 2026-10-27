using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LittleCastle.Rendering;
using LittleCastle.World;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace LittleCastle.Research
{
    [DefaultExecutionOrder(100)]
    public sealed class SpatialIndirectStudyReview : MonoBehaviour
    {
        public SpatialIndirectVolume field;
        public Camera camera;
        public WorldTimeSystem clock;
        public DayNightLightingController atmosphere;
        public StylizedLightingGlobals globals;
        public UniversalRenderPipelineAsset hdr,ao;
        public Material greyReflector;
        bool automated,regression,complete,freezeWind,showUI=true;
        string output,errors="";
        int view;
        float hour=12;
        RenderPipelineAsset previous;
        readonly List<UpdateReport> updates=new List<UpdateReport>();
        readonly List<BenchmarkReport> benchmarks=new List<BenchmarkReport>();
        readonly FrameTiming[] timing=new FrameTiming[1];
        readonly List<float> intervals=new List<float>(600),gpu=new List<float>(600),cpu=new List<float>(600);
        bool measuring;int warmup,lastFrame=-1;long stamp;
        bool latencyActive;
        long latencyStart,latencyChanges;
        double firstSupportMs=-1,visibleSupportMs=-1;
        int latencyRenderFrames;
        static readonly Vector3[] Eyes={new Vector3(8,8,-9),new Vector3(-6,3.2f,.1f),new Vector3(-.1f,2.6f,1.6f)};
        static readonly Vector3[] Targets={new Vector3(-5,1,4),new Vector3(-6,1.8f,6.7f),new Vector3(-1.65f,1.5f,5)};
        static readonly string[] Names={"overview","under-roof","thin-wall"};
        static string Arg(string[] args,string name,string fallback){int i=Array.IndexOf(args,name);return i>=0&&i+1<args.Length?args[i+1]:fallback;}
        void Awake()
        {
            var args=Environment.GetCommandLineArgs();regression=args.Contains("-giMeadowRegression");
            if(regression){SceneManager.LoadScene("LightingMeadow_20261025");return;}
            automated=args.Contains("-giStudy");showUI=!automated;output=Path.GetFullPath(Arg(args,"-giOutput",Path.Combine(Application.dataPath,"../SpatialIndirectCaptures")));Directory.CreateDirectory(output);
            field.priorityCamera=camera;
            previous=QualitySettings.renderPipeline;QualitySettings.renderPipeline=ao;camera.allowHDR=true;camera.GetUniversalAdditionalCameraData().renderPostProcessing=true;
            Application.runInBackground=true;QualitySettings.vSyncCount=0;Application.targetFrameRate=automated?60:60;
            SetHour(12);SetView(0);Application.logMessageReceived+=Log;RenderPipelineManager.endCameraRendering+=Rendered;
        }
        void Log(string m,string s,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors+=m+"\n"+s+"\n";}
        public void SetView(int i){view=i;camera.orthographic=false;camera.fieldOfView=i==1?64:44;camera.transform.position=Eyes[i];camera.transform.LookAt(Targets[i]);}
        void SetHour(float h)
        {hour=h;clock.RunAutomatically=false;if(clock.TryInitialize())clock.RestoreState(new WorldTimeState(1,h*60));atmosphere.ApplyLighting();globals.ApplyGlobals();if(field.Cache!=null)field.RefreshLighting();}
        void LateUpdate(){if(freezeWind){var t=Shader.GetGlobalVector("_LC_GameTime");t.z=2;Shader.SetGlobalVector("_LC_GameTime",t);}}
        void Update()
        {
            if(measuring)FrameTimingManager.CaptureFrameTimings();
            if(automated||regression)return;
            if(Input.GetKeyDown(KeyCode.F1))field.applyIndirect=!field.applyIndirect;
            if(Input.GetKeyDown(KeyCode.F2))SetView((view+1)%3);
            if(Input.GetKeyDown(KeyCode.F3))SetHour(hour==12?18.5f:hour==18.5f?1:12);
            if(Input.GetKeyDown(KeyCode.F4)){var p=field.proxies[1];field.CommitProxy(1,!p.active,p.bounds,p.albedo);}
            if(Input.GetKeyDown(KeyCode.F5))field.visibilityGuard=!field.visibilityGuard;
            if(Input.GetKeyDown(KeyCode.Escape))Application.Quit();
        }
        IEnumerator Start()
        {
            if(regression)yield break;
            yield return WaitCache("initial-field");
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-giMotionStudy")>=0)
            {
                var motionBaseline=(SpatialIndirectTrace.Probe[])field.Cache.Data.Clone();int refreshes=field.PriorityRefreshes,frames=0;
                field.RefreshLighting();var motionEye=camera.transform.position;float start=Time.realtimeSinceStartup;
                while(!field.Ready&&frames<240)
                {camera.transform.position=motionEye+Vector3.right*(Mathf.Sin(frames*.4f)*1.5f);frames++;yield return null;}
                bool identical=field.Ready;
                for(int i=0;i<motionBaseline.Length&&identical;i++){var a=motionBaseline[i];var b=field.Cache.Data[i];identical=a.c0.Equals(b.c0)&&a.cx.Equals(b.cx)&&a.cy.Equals(b.cy)&&a.cz.Equals(b.cz);}
                int count=field.PriorityRefreshes-refreshes;
                if(!identical||count>(frames+3)/4+1)throw new Exception("Moving-camera queue failed progress, exactness or refresh throttle.");
                File.WriteAllText(output+"/motion-checks.json",JsonUtility.ToJson(new MotionReport{pass=true,frames=frames,priorityRefreshes=count,wallMs=(Time.realtimeSinceStartup-start)*1000,solveBudgetMs=field.workBudgetMs,exactField=true},true));
                File.WriteAllText(output+"/result.txt","PASS moving camera: bounded refreshes, full progress and exact unchanged field.\n");File.WriteAllText(output+"/runtime-errors.txt",errors);complete=true;Application.Quit(errors.Length==0?0:2);yield break;
            }
            if(!automated)yield break;
            freezeWind=true;
            for(int mode=0;mode<2;mode++)for(int i=0;i<3;i++)
            {field.applyIndirect=mode==1;SetView(i);for(int f=0;f<6;f++)yield return null;yield return Capture((mode==0?"legacy-":"gi-")+Names[i]+"-day");}
            field.applyIndirect=true;SetView(1);
            bool reflectorVisible=GeometryUtility.TestPlanesAABB(GeometryUtility.CalculateFrustumPlanes(camera),field.proxies[4].bounds);
            if(reflectorVisible)throw new Exception("The declared offscreen reflector is actually inside the close camera frustum.");
            Vector3 wallPoint=new Vector3(-6,2.2f,2.93f);
            Vector3 red=field.Sample(wallPoint,Vector3.back,out bool outside);
            if(outside)throw new Exception("Offscreen diagnostic point fell outside the solved field.");
            var source=field.proxies[4];Color original=source.albedo;
            var originalMaterial=source.visual.sharedMaterial;source.visual.sharedMaterial=greyReflector;
            field.CommitProxy(4,true,source.bounds,new Color(.45f,.45f,.45f));int dirty=field.LastInvalidated;
            if(dirty<=0||dirty>=field.Cache.ProbeCount)throw new Exception("A local reflector edit must not invalidate the complete field.");
            yield return WaitCache("offscreen-reflector-grey");
            Vector3 grey=field.Sample(wallPoint,Vector3.back,out _);yield return Capture("gi-under-roof-offscreen-grey");
            source.visual.sharedMaterial=originalMaterial;field.CommitProxy(4,true,source.bounds,original);yield return WaitCache("offscreen-reflector-restored");yield return Capture("gi-under-roof-offscreen-red");
            if(Vector3.Distance(red,grey)<.0001f)throw new Exception("An out-of-frame reflector did not affect the wall irradiance.");
            var roof=field.proxies[1];Vector3 point=new Vector3(-6,.5f,4);
            Vector3 covered=field.Sample(point,Vector3.up,out _);
            int before=field.Cache.PendingCount;field.CommitProxy(1,false,roof.bounds,roof.albedo,true);if(field.Cache.PendingCount!=before||!roof.active)throw new Exception("Preview mutated committed lighting geometry.");
            field.CommitProxy(1,false,roof.bounds,roof.albedo);int roofDirty=field.LastInvalidated;
            if(roofDirty<=0||roofDirty>=field.Cache.ProbeCount)throw new Exception("Roof removal invalidated the full bounded field.");
            yield return WaitCache("roof-removal");Vector3 uncovered=field.Sample(point,Vector3.up,out _);yield return Capture("gi-under-roof-removed");
            if(uncovered.sqrMagnitude<=covered.sqrMagnitude*1.05f)throw new Exception("Removing the roof failed to increase indirect sky light.");
            field.CommitProxy(1,true,roof.bounds,roof.albedo);yield return WaitCache("roof-undo");Vector3 undo=field.Sample(point,Vector3.up,out _);
            if(Vector3.Distance(covered,undo)>.00001f)throw new Exception("Undo did not reproduce deterministic probe transport.");
            SetView(2);field.visibilityGuard=false;for(int f=0;f<6;f++)yield return null;yield return Capture("gi-thin-wall-guard-off");
            Vector3 leftPoint=new Vector3(-1.711f,1.5f,4),rightPoint=new Vector3(-1.589f,1.5f,4);
            Vector3 noGuard=field.Sample(leftPoint,Vector3.left,out _);field.visibilityGuard=true;for(int f=0;f<6;f++)yield return null;yield return Capture("gi-thin-wall-guard-on");Vector3 guard=field.Sample(leftPoint,Vector3.left,out _);
            Vector3 right=field.Sample(rightPoint,Vector3.right,out _);
            SetView(1);Vector3 unchanged=field.Sample(wallPoint,Vector3.back,out _);var eye=camera.transform.position;
            for(int i=0;i<12;i++){camera.transform.position=eye+Vector3.right*(i*.06f);yield return null;yield return null;yield return Capture("camera-"+i.ToString("D2"));}
            if(Vector3.Distance(unchanged,field.Sample(wallPoint,Vector3.back,out _))>.000001f)throw new Exception("Camera motion changed a world-space probe field.");
            SetHour(18.5f);yield return WaitCache("sunset-light-refresh");SetView(0);yield return Capture("gi-overview-sunset");SetView(1);yield return Capture("gi-under-roof-sunset");
            SetHour(1);yield return WaitCache("night-light-refresh");SetView(0);yield return Capture("gi-overview-night");SetView(1);yield return Capture("gi-under-roof-night");
            SetHour(12);yield return WaitCache("day-light-refresh");freezeWind=false;SetView(0);
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-giPriorityStudy")>=0)
            {
                // Explicit 0.8m cube, 0.5m committed move. Original six-proxy
                // fixture and its first eight comparisons are unchanged.
                SetView(1);for(int f=0;f<6;f++)yield return null;
                var cube=GameObject.CreatePrimitive(PrimitiveType.Cube);cube.name="Small edit latency reference cube";Destroy(cube.GetComponent<Collider>());
                var visual=cube.GetComponent<Renderer>();visual.sharedMaterial=field.proxies[5].visual.sharedMaterial;visual.enabled=false;
                var box=new Bounds(new Vector3(-8.3f,.65f,3.2f),Vector3.one*.8f);cube.transform.position=box.center;cube.transform.localScale=box.size;
                var expanded=new SpatialIndirectVolume.ProxyObject[7];Array.Copy(field.proxies,expanded,6);
                expanded[6]=new SpatialIndirectVolume.ProxyObject{id="small-edit-cube",bounds=box,albedo=field.proxies[5].albedo,visual=visual,active=false};field.proxies=expanded;
                field.CommitProxy(6,true,box,expanded[6].albedo);yield return WaitCache("small-cube-add");yield return Capture("gi-small-cube-added");
                var moved=box;moved.center+=Vector3.back*.5f;cube.transform.position=moved.center;
                field.CommitProxy(6,true,moved,expanded[6].albedo);yield return WaitCache("small-cube-move");yield return Capture("gi-small-cube-moved");
                cube.transform.position=box.center;field.CommitProxy(6,true,box,expanded[6].albedo);yield return WaitCache("small-cube-undo");
                field.CommitProxy(6,false,box,expanded[6].albedo);yield return WaitCache("small-cube-remove");Destroy(cube);SetView(0);
            }
            File.WriteAllText(output+"/transport-checks.json",JsonUtility.ToJson(new TransportChecks{reflectorVisible=reflectorVisible,red=red,grey=grey,covered=covered,uncovered=uncovered,undo=undo,guardOff=noGuard,guardOn=guard,otherWallSide=right,localReflectorProbes=dirty,localRoofProbes=roofDirty,totalProbes=field.Cache.ProbeCount},true));
            // Timings contain normal visible camera renders only. No readback/capture/update work.
            QualitySettings.vSyncCount=0;Application.targetFrameRate=-1;
            yield return Measure(false);yield return Measure(true);
            File.WriteAllText(output+"/updates.json",JsonUtility.ToJson(new UpdateReports{reports=updates.ToArray()},true));
            File.WriteAllText(output+"/benchmarks.json",JsonUtility.ToJson(new BenchmarkReports{reports=benchmarks.ToArray()},true));
            File.WriteAllText(output+"/runtime-errors.txt",errors);File.WriteAllText(output+"/result.txt",errors.Length==0?"PASS bounded proxy transport, offscreen contribution, roof removal/undo, preview isolation, local invalidation, camera independence.\nThin-wall interpolation and images require visual review.\n":"FAIL\n"+errors);
            complete=true;Application.Quit(errors.Length==0?0:2);
        }
        IEnumerator WaitCache(string reason)
        {
            int pending=field.Cache.PendingCount,startSolved=field.TotalSolved;double solve=field.TotalSolveMs,upload=field.TotalUploadMs;long bytes=field.TotalUploadBytes;float start=Time.realtimeSinceStartup;double max=0,maxUpload=0,maxTotal=0;int frames=0,maxProbes=0,maxBytes=0;
            long allocations=field.TotalSolverAllocatedBytes,geometry=field.Transport?.GeometryBuilds??0,injection=field.Transport?.Injections??0,reweight=field.Transport?.DirectReweights??0;
            int gc0=GC.CollectionCount(0),gc1=GC.CollectionCount(1),gc2=GC.CollectionCount(2);
            long replay=field.Transport?.EnvironmentReplays??0,allocationCalls=field.TotalSolverAllocationCalls;
            double priorityMs=field.TotalPriorityMs;int priorityRefreshes=field.PriorityRefreshes;long prioritySolved=field.TotalPrioritySolved;
            latencyStart=SpatialIndirectProfile.Stamp();latencyChanges=field.TotalPriorityChanges;firstSupportMs=visibleSupportMs=-1;latencyRenderFrames=0;latencyActive=true;
            while(!field.Ready)
            {
                max=Math.Max(max,field.LastSolveMs);maxUpload=Math.Max(maxUpload,field.LastUploadMs);maxTotal=Math.Max(maxTotal,field.LastSolveMs+field.LastUploadMs);maxProbes=Math.Max(maxProbes,field.LastProcessed);maxBytes=Math.Max(maxBytes,field.LastUploadedBytes);frames++;
                if(Time.realtimeSinceStartup-start>180)throw new Exception("Probe update did not converge in 180s: "+reason);
                yield return null;
            }
            max=Math.Max(max,field.LastSolveMs);maxUpload=Math.Max(maxUpload,field.LastUploadMs);maxTotal=Math.Max(maxTotal,field.LastSolveMs+field.LastUploadMs);maxProbes=Math.Max(maxProbes,field.LastProcessed);maxBytes=Math.Max(maxBytes,field.LastUploadedBytes);
            double fullWall=(Time.realtimeSinceStartup-start)*1000;
            // Allow the final upload to reach one ordinary camera render BEFORE
            // any diagnostic PNG/bin readback. This frame is outside fullWall.
            yield return null;latencyActive=false;
            updates.Add(new UpdateReport{reason=reason,queued=pending,solved=field.TotalSolved-startSolved,totalProbes=field.Cache.ProbeCount,frames=frames,wallMs=fullWall,solveMs=field.TotalSolveMs-solve,uploadMs=field.TotalUploadMs-upload,maxSolveFrameMs=max,maxUploadFrameMs=maxUpload,maxTotalUpdateFrameMs=maxTotal,maxProbesFrame=maxProbes,maxUploadFrameBytes=maxBytes,uploadedBytes=field.TotalUploadBytes-bytes,bufferBytes=field.BufferBytes,skyTransportEntries=field.SurfaceEntries,mode=field.UseTransport?"cached-transport":"reference",solveBudgetMs=field.workBudgetMs,probeLimit=field.EffectiveProbeLimit,solverAllocatedBytes=field.TotalSolverAllocatedBytes-allocations,commitMs=reason=="initial-field"?0:field.LastCommitMs,commitAllocatedBytes=reason=="initial-field"?0:field.LastCommitAllocatedBytes,geometryBuilds=(field.Transport?.GeometryBuilds??0)-geometry,injections=(field.Transport?.Injections??0)-injection,directReweights=(field.Transport?.DirectReweights??0)-reweight,transportPayloadBytes=field.Transport?.PayloadBytes??0,activeHitLinks=field.Transport?.ActiveHitLinks??0,allocatedLinkSlots=field.Transport?.AllocatedLinkSlots??0,allocatedBlockSlots=field.Transport?.AllocatedBlockSlots??0,prunedSurfaceCells=field.Transport?.LastPrunedSurfaceCells??0,managedUsedBytes=UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong(),gc0=GC.CollectionCount(0)-gc0,gc1=GC.CollectionCount(1)-gc1,gc2=GC.CollectionCount(2)-gc2});
            Debug.Log("LC_GI_UPDATE "+reason+" solved="+(field.TotalSolved-startSolved)+" wallMs="+fullWall.ToString("F1")+" cpuMs="+(field.TotalSolveMs-solve).ToString("F1"));
            var latest=updates[updates.Count-1];latest.environmentReplays=(field.Transport?.EnvironmentReplays??0)-replay;latest.allocationCounterSupported=field.AllocationCounterSupported;latest.allocationCalibrationBytes=field.AllocationCalibrationBytes;
            latest.priorityMode=field.UsePriority;latest.priorityClassifyMs=field.TotalPriorityMs-priorityMs;latest.priorityRefreshes=field.PriorityRefreshes-priorityRefreshes;latest.visibleSupportSolved=field.TotalPrioritySolved-prioritySolved;latest.firstChangedSupportRenderedMs=firstSupportMs;latest.visibleSupportRenderedMs=visibleSupportMs;latest.latencyCameraFrames=latencyRenderFrames;latest.queuePayloadBytes=field.Cache.QueuePayloadBytes;
            if(!field.AllocationCounterSupported){latest.solverAllocatedBytes=-1;latest.commitAllocatedBytes=-1;}
            latest.allocationRecorderSupported=field.AllocationRecorderSupported;latest.solverAllocationCalls=field.AllocationRecorderSupported?field.TotalSolverAllocationCalls-allocationCalls:-1;latest.commandAllocationCalls=field.AllocationRecorderSupported?field.LastCommandAllocationCalls:-1;latest.setupAllocationCalls=field.SetupAllocationCalls;
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-giDump")>=0)
            {
                using(var writer=new BinaryWriter(File.Create(output+"/"+reason+"-probes.bin")))
                foreach(var p in field.Cache.Data){Write(writer,p.c0);Write(writer,p.cx);Write(writer,p.cy);Write(writer,p.cz);}
            }
        }
        IEnumerator Measure(bool indirect)
        {
            field.applyIndirect=indirect;intervals.Clear();gpu.Clear();cpu.Clear();stamp=0;lastFrame=-1;warmup=Time.frameCount;measuring=true;float start=Time.realtimeSinceStartup;
            while(intervals.Count<600&&Time.realtimeSinceStartup-start<90)yield return null;
            measuring=false;
            if(intervals.Count!=600)throw new Exception("Insufficient ordinary camera frames.");
            benchmarks.Add(new BenchmarkReport{indirect=indirect,frame=LightingBenchmarkController.Distribution.From(intervals),gpu=LightingBenchmarkController.Distribution.From(gpu),cpu=LightingBenchmarkController.Distribution.From(cpu),device=SystemInfo.graphicsDeviceName,processor=SystemInfo.processorType,unity=Application.unityVersion,colorSpace=QualitySettings.activeColorSpace.ToString(),api=SystemInfo.graphicsDeviceType.ToString(),width=Screen.width,height=Screen.height,development=Debug.isDebugBuild,timingAvailable=FrameTimingManager.IsFeatureEnabled(),pipeline=UrpPreviewQuality.ReadActive(),allocatedBytes=UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong()});
        }
        void Rendered(ScriptableRenderContext c,Camera cam)
        {
            if(latencyActive&&cam==camera&&!cam.targetTexture)
            {
                latencyRenderFrames++;double elapsed=SpatialIndirectProfile.Ms(SpatialIndirectProfile.Stamp()-latencyStart);
                if(firstSupportMs<0&&field.TotalPriorityChanges>latencyChanges)firstSupportMs=elapsed;
                if(visibleSupportMs<0&&field.Cache.PriorityPendingCount==0)visibleSupportMs=elapsed;
            }
            if(!measuring||cam!=camera||cam.targetTexture||Time.frameCount-warmup<120||lastFrame==Time.frameCount||intervals.Count>=600)return;
            lastFrame=Time.frameCount;long now=System.Diagnostics.Stopwatch.GetTimestamp();
            if(stamp!=0){intervals.Add((float)((now-stamp)*1000.0/System.Diagnostics.Stopwatch.Frequency));if(FrameTimingManager.GetLatestTimings(1,timing)>0){if(timing[0].gpuFrameTime>0)gpu.Add((float)timing[0].gpuFrameTime);if(timing[0].cpuFrameTime>0)cpu.Add((float)timing[0].cpuFrameTime);}}
            stamp=now;
        }
        IEnumerator Capture(string name)
        {if(Array.IndexOf(Environment.GetCommandLineArgs(),"-giNoCapture")>=0)yield break;for(int attempt=0;attempt<4;attempt++){if(CaptureAttempt(name))yield break;yield return null;}throw new Exception("Empty screenshot: "+name);}
        static void Write(BinaryWriter writer,Vector4 v){writer.Write(v.x);writer.Write(v.y);writer.Write(v.z);writer.Write(v.w);}
        bool CaptureAttempt(string name)
        {
            int width=1920,height=1080;var descriptor=new RenderTextureDescriptor(width,height,RenderTextureFormat.ARGB32,24){msaaSamples=4,sRGB=true};descriptor.msaaSamples=SystemInfo.GetRenderTextureSupportedMSAASampleCount(descriptor);
            var rt=RenderTexture.GetTemporary(descriptor);var resolve=RenderTexture.GetTemporary(width,height,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);var old=camera.targetTexture;var active=RenderTexture.active;var pixels=new Texture2D(width,height,TextureFormat.RGB24,false,false);
            try
            {
                camera.targetTexture=rt;CameraCaptureCompatibility.Render(camera,rt);if(descriptor.msaaSamples>1)rt.ResolveAntiAliasedSurface(resolve);else Graphics.Blit(rt,resolve);RenderTexture.active=resolve;pixels.ReadPixels(new Rect(0,0,width,height),0,0);pixels.Apply();
                var bytes=pixels.GetRawTextureData<byte>();int hi=0,lo=255;for(int i=0;i<bytes.Length;i+=257){hi=Mathf.Max(hi,bytes[i]);lo=Mathf.Min(lo,bytes[i]);}if(hi-lo<3)return false;
                File.WriteAllBytes(output+"/"+name+".png",pixels.EncodeToPNG());File.WriteAllText(output+"/"+name+".json",JsonUtility.ToJson(new CaptureMetadata{view=Names[view],hour=hour,indirect=field.applyIndirect,visibilityGuard=field.visibilityGuard,ready=field.Ready,cameraPosition=camera.transform.position,probeCount=field.Cache.ProbeCount,rayCount=field.raysPerProbe,rayDistance=field.rayDistance,proxyCount=field.Snapshot.ProxyCount,bufferBytes=field.BufferBytes},true));return true;
            }
            finally{camera.targetTexture=old;RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);RenderTexture.ReleaseTemporary(resolve);Destroy(pixels);}
        }
        [Serializable] public sealed class CaptureMetadata{public string view;public float hour,rayDistance;public bool indirect,visibilityGuard,ready;public int probeCount,rayCount,proxyCount,bufferBytes;public Vector3 cameraPosition;}
        [Serializable] public sealed class TransportChecks{public bool reflectorVisible;public Vector3 red,grey,covered,uncovered,undo,guardOff,guardOn,otherWallSide;public int localReflectorProbes,localRoofProbes,totalProbes;}
        [Serializable] public sealed class UpdateReport
        {
            public string reason,mode;
            public int queued,solved,totalProbes,frames,maxProbesFrame,maxUploadFrameBytes,bufferBytes,skyTransportEntries,probeLimit,prunedSurfaceCells,gc0,gc1,gc2;
            public long uploadedBytes,solverAllocatedBytes,commitAllocatedBytes,geometryBuilds,injections,directReweights,environmentReplays,transportPayloadBytes,activeHitLinks,allocatedLinkSlots,allocatedBlockSlots,managedUsedBytes;
            public long allocationCalibrationBytes,solverAllocationCalls,commandAllocationCalls,setupAllocationCalls;
            public bool allocationCounterSupported,allocationRecorderSupported;
            public bool priorityMode;
            public int priorityRefreshes,latencyCameraFrames;
            public long visibleSupportSolved,queuePayloadBytes;
            public double priorityClassifyMs,firstChangedSupportRenderedMs,visibleSupportRenderedMs;
            public double wallMs,solveMs,uploadMs,maxSolveFrameMs,maxUploadFrameMs,maxTotalUpdateFrameMs,commitMs;
            public float solveBudgetMs;
        }
        [Serializable] public sealed class UpdateReports{public UpdateReport[] reports;}
        [Serializable] public sealed class MotionReport{public bool pass,exactField;public int frames,priorityRefreshes;public float wallMs,solveBudgetMs;}
        [Serializable] public sealed class BenchmarkReport{public bool indirect,development,timingAvailable;public string device,processor,unity,colorSpace,api;public int width,height;public long allocatedBytes;public UrpPreviewQuality.Snapshot pipeline;public LightingBenchmarkController.Distribution frame,gpu,cpu;}
        [Serializable] public sealed class BenchmarkReports{public BenchmarkReport[] reports;}
        void OnGUI(){if(showUI&&!regression)GUI.Box(new Rect(12,12,760,85),"Spatial indirect prototype | "+(field.applyIndirect?"ON":"legacy")+" | "+hour+"h | pending "+field.Cache.PendingCount+"\nF1 indirect | F2 camera | F3 time | F4 roof | F5 wall guard\nCPU solve "+field.LastSolveMs.ToString("F2")+" ms | "+field.LastProcessed+" probes | upload "+field.LastUploadedBytes+" B");}
        void OnDestroy(){RenderPipelineManager.endCameraRendering-=Rendered;Application.logMessageReceived-=Log;if(!regression&&(QualitySettings.renderPipeline==ao||QualitySettings.renderPipeline==hdr))QualitySettings.renderPipeline=previous;}
        void OnApplicationQuit(){if(automated&&!complete)File.WriteAllText(output+"/interrupted.txt","Evidence incomplete.\n"+errors);}
    }
}
