using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using LittleCastle.CameraSystem;
using LittleCastle.Rendering;
using LittleCastle.Research;
using LittleCastle.World;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace LittleCastle.Acceptance
{
    // Scene-owned adapter. Normal authoring does not execute acceptance or alter time.
    [DefaultExecutionOrder(500)]
    public sealed class WorldEditorAcceptance : MonoBehaviour
    {
        public Camera camera;
        public WorldTimeSystem clock;
        public DayNightLightingController atmosphere;
        public StylizedLightingGlobals globals;
        public MeadowSurfacePainter painter;
        public UniversalRenderPipelineAsset hdr, ao;
        string output, errors="";
        bool benchmark;
        StrategyCameraController rig;
        readonly List<string> rows=new List<string>();
        const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        void Set(string name,object value)
        {
            var field=typeof(StrategyCameraController).GetField(name,Private);
            if(field==null)throw new MissingFieldException(name);
            field.SetValue(rig,value);
        }
        void Pose(float distance)
        {
            var focus=new Vector3(-4,.25f,5);
            // Begin at the same settled ground focus for every AO pair. Otherwise
            // the rig's ground smoothing makes the first/second view differ.
            if(Physics.Raycast(new Vector3(focus.x,1000,focus.z),Vector3.down,out var hit,2000,~0,QueryTriggerInteraction.Ignore))
                focus.y=hit.point.y+(float)typeof(StrategyCameraController).GetField("focusHeightOffset",Private).GetValue(rig);
            Set("targetDistance",distance);Set("smoothedDistance",distance);Set("distanceVelocity",0f);
            Set("targetFocus",focus);Set("smoothedFocus",focus);Set("focusVelocity",Vector3.zero);
            Set("currentGroundY",focus.y);Set("yaw",35f);Set("pitch",distance<20?42f:distance<60?50f:62f);
        }
        void SetHour(float h)
        {
            clock.RunAutomatically=false;clock.RestoreState(new WorldTimeState(1,h*60));Apply();
        }
        void Apply(){atmosphere.ApplyLighting();globals.ApplyGlobals();}
        static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
        static string F(double n)=>n.ToString("R",CultureInfo.InvariantCulture);
        void Log(string message,string stack,LogType type)
        {if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors+=message+"\n"+stack+"\n";}
        IEnumerator Start()
        {
            var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"-weAccept");
            benchmark=Array.IndexOf(args,"-weBenchmark")>=0;if(benchmark)at=Array.IndexOf(args,"-weBenchmark");
            if(at<0)yield break;
            if(at+1>=args.Length)throw new ArgumentException("Explicit isolated acceptance output required.");
            output=Path.GetFullPath(args[at+1]);Directory.CreateDirectory(output);
            Application.logMessageReceived+=Log;
            // Drive nested iterators ourselves so every exception produces an explicit FAIL.
            var stack=new Stack<IEnumerator>();stack.Push(Run());
            while(stack.Count>0)
            {
                object next=null;bool moved=false;
                try{moved=stack.Peek().MoveNext();if(moved)next=stack.Peek().Current;}
                catch(Exception e){errors+=e+"\n";break;}
                if(!moved){(stack.Pop() as IDisposable)?.Dispose();continue;}
                if(next is IEnumerator nested)stack.Push(nested);else yield return next;
            }
            while(stack.Count>0)(stack.Pop() as IDisposable)?.Dispose();
            File.WriteAllText(Path.Combine(output,"runtime-errors.txt"),errors);
            File.WriteAllText(Path.Combine(output,"result.txt"),errors.Length==0?"PASS automated checks; visual acceptance remains manual.\n":"FAIL\n"+errors);
            Application.Quit(errors.Length==0?0:2);
        }
        IEnumerator Run()
        {
            Require(camera&&clock&&atmosphere&&globals&&painter&&hdr&&ao,"Scene references incomplete.");
            Require(clock.TryInitialize(),"Clock initialization failed.");
            Require(GraphicsSettings.currentRenderPipeline!=null,"URP required for render-request acceptance.");
            rig=camera.GetComponent<StrategyCameraController>();Require(rig,"Strategy camera missing.");
            yield return null;yield return null;
            Require(clock.RunAutomatically,"Authoring startup clock frozen before acceptance overrides.");
            rig.enabled=true;rig.SetInputEnabled(false);Application.runInBackground=true;
            Require(painter.Session!=null&&painter.Tools!=null,"Current map tools did not initialize.");
            Require(!Shader.IsKeywordEnabled("_LC_SPATIAL_INDIRECT"),"Spatial GI must be OFF.");
            foreach(var b in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
                Require(!(b is LightingMeadowReview||b is SpatialIndirectStudyReview),"Review driver present: disabled components still run Awake.");
            if(benchmark){yield return Benchmark();yield break;}
            BrushChecks();
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-weRiverChecks")>=0)yield return RiverChecks();
            rows.Add("name,hour,distance,ao,cameraX,cameraY,cameraZ,shadowDistance,fogStart,fogEnd");
            foreach(float hour in new[]{12f,18.5f,1f})foreach(float distance in new[]{11f,35f,115f})foreach(bool useAO in new[]{false,true})
            {
                QualitySettings.renderPipeline=useAO?ao:hdr;SetHour(hour);Pose(distance);
                for(int i=0;i<16;i++)yield return null;
                string name="h"+F(hour)+"-d"+F(distance)+"-ao"+(useAO?"on":"off");yield return Capture(name);
                var p=camera.transform.position;
                rows.Add(name+","+F(clock.CurrentHour)+","+F(distance)+","+useAO+","+F(p.x)+","+F(p.y)+","+F(p.z)+","+F((useAO?ao:hdr).shadowDistance)+","+F(RenderSettings.fogStartDistance)+","+F(RenderSettings.fogEndDistance));
            }
            File.WriteAllLines(Path.Combine(output,"views.csv"),rows);rows.Clear();
            QualitySettings.renderPipeline=ao;Pose(35);SetHour(19.8f);
            rows.Add("sample,absoluteMinutes,hour,sunR,sunG,sunB,sunIntensity,ambientR,ambientG,ambientB");
            // One coherent trajectory across 20:00, midnight, 06:00. No RestoreState per sample.
            double before=clock.Clock.AbsoluteGameMinutes;
            for(int i=0;i<240;i++)
            {
                clock.AdvanceSimulation(.5);Apply();yield return null;
                double now=clock.Clock.AbsoluteGameMinutes;Require(now>before,"Continuous clock stopped or reversed.");before=now;
                var sun=atmosphere.Sun;Color s=sun?sun.color:Color.black,a=RenderSettings.ambientSkyColor;
                rows.Add(i+","+F(now)+","+F(clock.CurrentHour)+","+F(s.r)+","+F(s.g)+","+F(s.b)+","+F(sun?sun.intensity:0)+","+F(a.r)+","+F(a.g)+","+F(a.b));
                if(i%12==0)yield return Capture("transition-"+i.ToString("D3"));
            }
            File.WriteAllLines(Path.Combine(output,"transition.csv"),rows);
            // Verify the production automatic Update path also advances without harness stepping.
            before=clock.Clock.AbsoluteGameMinutes;clock.RunAutomatically=true;
            for(int i=0;i<12;i++)yield return null;
            Require(clock.Clock.AbsoluteGameMinutes>before,"Automatic authoritative time did not advance.");
        }
        void BrushChecks()
        {
            // Exercise the live painter command boundary, restore baseline afterwards; never touch user save path.
            string original=painter.ExportJson();ulong baseline=painter.MaskDigest();bool enabled=painter.enabled;painter.enabled=false;
            try
            {
                var b=painter.Session.Bounds;var p=new Vector2(b.x+b.z*.5f,b.y+b.w*.5f);
                var tools=painter.Tools;tools.Select("surface.road");tools.context.settings.radius=2;tools.context.settings.strength=1;
                tools.Begin(new MapToolPointer(new Ray(new Vector3(p.x,100,p.y),Vector3.down),true,p),0);tools.Complete();
                string saved=painter.ExportJson();ulong painted=painter.MaskDigest();
                Require(saved!=original&&painter.Session.CanUndo,"Brush transaction was not committed.");
                painter.Session.Undo();Require(painter.ExportJson()==original&&painter.MaskDigest()==baseline,"Undo mismatch.");
                painter.Session.Redo();Require(painter.ExportJson()==saved&&painter.MaskDigest()==painted,"Redo mismatch.");
                string path=Path.Combine(output,"brush-save.json");File.WriteAllText(path,saved);
                painter.LoadJson(original);painter.LoadJson(File.ReadAllText(path));
                Require(painter.ExportJson()==saved&&painter.MaskDigest()==painted,"Save/load mismatch.");
                File.WriteAllText(Path.Combine(output,"brush-checks.txt"),"PASS live MapToolController stroke/undo/redo and isolated disk save/load; JSON + derived mask checks.\nHistory intentionally cleared when restoring baseline.\n");
            }
            finally{painter.LoadJson(original);painter.enabled=enabled;}
        }
        IEnumerator RiverChecks()
        {
            var river=painter.GetComponent<MapRiverRuntimeTool>();Require(river,"River adapter missing.");
            var state=(MapRiverToolState)typeof(MapRiverRuntimeTool).GetField("state",Private).GetValue(river);
            string original=painter.ExportJson();bool enabled=painter.enabled;painter.enabled=false;
            try
            {
                river.ConfigureWaterOnly(painter.waterOnlyRivers);river.BeginRiver();
                var controller=painter.Tools;controller.Select("river.points");
                foreach(var point in new[]{new Vector2(-14,-12),new Vector2(-14,24)})
                {controller.Begin(new MapToolPointer(new Ray(new Vector3(point.x,100,point.y),Vector3.down),true,point),Time.unscaledTimeAsDouble);controller.Complete();}
                Require(state.CanApplyRiver&&state.draft.nodes.Count==2,"Shared river tool did not collect both clicks.");
                Require(river.HasUnappliedDraft,"New river must be reported as unsaved draft.");
                Require(state.ApplyRiver(painter.Session),"River Apply failed: "+state.status);
                Require(!river.HasUnappliedDraft,"Applied river still marked as unpublished.");
                long id=state.draft.id;string saved=painter.ExportJson();
                Require(painter.Session.GetRiver(id).layoutVersion==2,"Integrated new river profile is not v2.");
                painter.Undo();Require(painter.ExportJson()==original,"River undo mismatch.");
                painter.Redo();Require(painter.ExportJson()==saved,"River redo mismatch.");
                File.WriteAllText(Path.Combine(output,"river-save.json"),saved);painter.LoadJson(original);painter.LoadJson(File.ReadAllText(Path.Combine(output,"river-save.json")));
                Require(painter.ExportJson()==saved,"River disk roundtrip mismatch.");
                painter.enabled=enabled;river.CloseTools();Pose(35);
                foreach(float hour in new[]{12f,1f})
                {
                    SetHour(hour);for(int frame=0;frame<24;frame++)yield return null;
                    Require(painter.RiverView.ResidentTiles>0,"Applied river has no resident water mesh.");
                    yield return Capture("river-h"+F(hour)+"-d35");
                }
                File.WriteAllText(Path.Combine(output,"river-checks.txt"),"PASS shared control-point clicks; draft guard; Apply v2; Undo/Redo; isolated disk save/load; resident water and day/night renders.\n");
            }
            finally{river.DiscardDraft();river.CloseTools();painter.LoadJson(original);painter.enabled=enabled;}
        }
        IEnumerator Capture(string name)
        {
            for(int attempt=0;attempt<4;attempt++)
            {
                if(CaptureAttempt(name))yield break;
                // URP may initialise its first render request without pixels. Retry on a later frame.
                yield return null;
            }
            throw new InvalidOperationException("Blank/flat render-request after four attempts: "+name);
        }
        bool CaptureAttempt(string name)
        {
            bool linear=QualitySettings.activeColorSpace==ColorSpace.Linear;
            var descriptor=new RenderTextureDescriptor(1600,900,RenderTextureFormat.ARGB32,24){msaaSamples=4,sRGB=linear};
            descriptor.msaaSamples=SystemInfo.GetRenderTextureSupportedMSAASampleCount(descriptor);
            var rt=RenderTexture.GetTemporary(descriptor);
            var resolve=RenderTexture.GetTemporary(1600,900,0,RenderTextureFormat.ARGB32,linear?RenderTextureReadWrite.sRGB:RenderTextureReadWrite.Linear);
            var oldTarget=camera.targetTexture;var oldActive=RenderTexture.active;Texture2D image=null;
            try
            {
                camera.targetTexture=rt;CameraCaptureCompatibility.Render(camera,rt);
                if(descriptor.msaaSamples>1)rt.ResolveAntiAliasedSurface(resolve);else Graphics.Blit(rt,resolve);
                RenderTexture.active=resolve;
                image=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);image.Apply();
                var pixels=image.GetPixels32();long sum=0;int low=765,high=0;
                foreach(var p in pixels){int v=p.r+p.g+p.b;sum+=v;low=Math.Min(low,v);high=Math.Max(high,v);}
                if(high-low<=8||sum<=pixels.Length*2L)return false;
                File.WriteAllBytes(Path.Combine(output,name+".png"),image.EncodeToPNG());
                return true;
            }
            finally{camera.targetTexture=oldTarget;RenderTexture.active=oldActive;if(image)Destroy(image);RenderTexture.ReleaseTemporary(rt);RenderTexture.ReleaseTemporary(resolve);}
        }
        IEnumerator Benchmark()
        {
            var args=Environment.GetCommandLineArgs();
            float distance=ReadFloat(args,"-weDistance",35),hour=ReadFloat(args,"-weHour",12);
            bool useAO=ReadFloat(args,"-weAO",1)>0;
            QualitySettings.renderPipeline=useAO?ao:hdr;SetHour(hour);Pose(distance);
            double riverCommitMs=0;bool withRiver=Array.IndexOf(args,"-weBenchmarkRiver")>=0;
            if(withRiver)
            {
                double started=Time.realtimeSinceStartupAsDouble;
                painter.Session.UpsertRiver(new MapRiverRecord{id=918273,layoutVersion=2,nodes=new List<MapRiverNode>{
                    new MapRiverNode{id=1,position=new Vector2(-14,-12),leftBank=MapBankKind.Sand,rightBank=MapBankKind.Sand},
                    new MapRiverNode{id=2,position=new Vector2(-14,24),leftBank=MapBankKind.Sand,rightBank=MapBankKind.Sand}}});
                riverCommitMs=(Time.realtimeSinceStartupAsDouble-started)*1000;
                // Derived tile work runs through the real budgeted presentation before warming up.
                for(int i=0;i<60;i++)yield return null;
                Require(painter.RiverView.ResidentTiles>0,"Benchmark river presentation missing.");
            }
            QualitySettings.vSyncCount=0;Application.targetFrameRate=-1;
            // Foreground measurements only. Hidden launch allows selecting the right
            // window before warming up; timeout is an explicit failed measurement.
            double deadline=Time.realtimeSinceStartupAsDouble+45;
            while(!Application.isFocused){Require(Time.realtimeSinceStartupAsDouble<deadline,"Benchmark never received focus.");yield return null;}
            for(int i=0;i<120;i++)yield return null;
            var samples=new double[600];var cpu=new double[600];var gpu=new double[600];var alloc=new long[600];var timing=new FrameTiming[1];
            var gc=ProfilerRecorder.StartNew(ProfilerCategory.Memory,"GC Allocated In Frame");
            bool gcAvailable=gc.Valid;int cpuCount=0,gpuCount=0;int rendered=0;
            Action<ScriptableRenderContext,Camera> onRender=(ctx,c)=>{if(c==camera)rendered++;};
            RenderPipelineManager.endCameraRendering+=onRender;
            try
            {
                double last=Time.realtimeSinceStartupAsDouble;
                for(int i=0;i<600;i++)
                {
                    FrameTimingManager.CaptureFrameTimings();int previous=rendered;yield return null;
                    Require(Application.isFocused,"Benchmark lost foreground focus.");
                    Require(rendered>previous,"No ordinary camera frame during benchmark.");
                    double now=Time.realtimeSinceStartupAsDouble;samples[i]=(now-last)*1000;last=now;
                    if(FrameTimingManager.GetLatestTimings(1,timing)>0){cpu[i]=timing[0].cpuFrameTime;gpu[i]=timing[0].gpuFrameTime;if(cpu[i]>0)cpuCount++;if(gpu[i]>0)gpuCount++;}
                    if(gcAvailable)alloc[i]=gc.LastValue;
                }
            }
            finally{RenderPipelineManager.endCameraRendering-=onRender;gc.Dispose();}
            rows.Clear();rows.Add("frame,wallMs,cpuMs,gpuMs,gcAllocatedBytes");
            for(int i=0;i<600;i++)rows.Add(i+","+F(samples[i])+","+F(cpu[i])+","+F(gpu[i])+","+(gcAvailable?alloc[i].ToString():"unavailable"));
            File.WriteAllLines(Path.Combine(output,"benchmark.csv"),rows);
            var grass=FindAnyObjectByType<MeadowGrassRenderer>();
            File.WriteAllText(Path.Combine(output,"benchmark.txt"),"600 ordinary rendered camera frames; 120 warmup; no PNG/readback during measurement.\nCPU timing samples="+cpuCount+"; GPU timing samples="+gpuCount+"; GC recorder="+gcAvailable+"\nBatchMode="+Application.isBatchMode+"; focused="+Application.isFocused+"; GPU="+SystemInfo.graphicsDeviceName+"; API="+SystemInfo.graphicsDeviceType+"\nDistance="+F(distance)+"; hour="+F(hour)+"; AO="+useAO+"; resolution="+Screen.width+"x"+Screen.height+"; grassPatches="+(grass?grass.visiblePatches:0)+"; clumps="+(grass?grass.submittedClumps:0)+"; triangles="+(grass?grass.submittedTriangles:0)+"; grassDraws="+(grass?grass.drawCalls:0)+"\nRiver="+withRiver+"; riverCommitMs="+F(riverCommitMs)+"; residentWaterTiles="+painter.RiverView.ResidentTiles+"; reflectionScale="+(painter.GetComponent<UberRiverReflectionAdapter>()?.reflectionScale.ToString()??"none")+"\nGPU/CPU zero timings mean unavailable, not zero cost.\n");
        }
        static float ReadFloat(string[] args,string key,float fallback)
        {int i=Array.IndexOf(args,key);return i>=0&&i+1<args.Length?float.Parse(args[i+1],CultureInfo.InvariantCulture):fallback;}
        void OnDestroy(){Application.logMessageReceived-=Log;}
    }
}
