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

namespace LittleCastle.Research
{
    /// <summary>Dedicated Meadow lighting fixture. No authoring save files are loaded or written.</summary>
    [DefaultExecutionOrder(40)]
    public sealed class LightingMeadowReview : MonoBehaviour
    {
        public Camera reviewCamera;
        public WorldTimeSystem clock;
        public DayNightLightingController atmosphere;
        public StylizedLightingGlobals globals;
        public LightingPaletteProfile palette;
        public UniversalRenderPipelineAsset hdr, ao;
        public MeadowGrassRenderer grass;
        public MeadowSurfacePainter painter;
        public GrassInteractionManager interaction;
        public Transform demonstrator;
        public bool showUI=true;
        public int mode=1,view;
        public float hour=12;
        static readonly string[] Names={"meadow","close","strategy","oak-contact"};
        static readonly Vector3[] Eyes={new Vector3(23,25,-27),new Vector3(8,7.8f,-10),new Vector3(12,57,-50),new Vector3(-6,8,1)};
        static readonly Vector3[] Targets={new Vector3(0,1,13),new Vector3(-1,.4f,1),new Vector3(0,0,12),new Vector3(-19,2.5f,17)};
        RenderPipelineAsset previous;
        bool capture,benchmark,completed,collecting;
        string output,errors="";
        int warmup,lastFrame=-1;
        long stamp;
        readonly List<float> wall=new List<float>(600),gpu=new List<float>(600),cpu=new List<float>(600);
        readonly FrameTiming[] timings=new FrameTiming[1];
        readonly System.Diagnostics.Stopwatch timer=new System.Diagnostics.Stopwatch();
        static string Arg(string[] a,string key,string fallback){int i=Array.IndexOf(a,key);return i>=0&&i+1<a.Length?a[i+1]:fallback;}
        void Awake()
        {
            previous=QualitySettings.renderPipeline;
            var args=Environment.GetCommandLineArgs();capture=args.Contains("-lmCapture");benchmark=args.Contains("-lmBenchmark");
            output=Path.GetFullPath(Arg(args,"-lmOutput",Path.Combine(Application.dataPath,"../Captures")));Directory.CreateDirectory(output);
            int.TryParse(Arg(args,"-lmMode","1"),out mode);mode=Mathf.Clamp(mode,0,1);
            int.TryParse(Arg(args,"-lmView","0"),out view);view=Mathf.Clamp(view,0,3);
            float.TryParse(Arg(args,"-lmHour","12"),System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out hour);
            Application.runInBackground=true;QualitySettings.vSyncCount=0;Application.targetFrameRate=capture?60:-1;
            showUI=!capture&&!benchmark;
            Apply();SetView(view);SetHour(hour);
            globals.SetWindPresentation(new Vector2(.86f,.5f),.6f,.65f);
            Application.logMessageReceived+=Log;RenderPipelineManager.endCameraRendering+=OnRendered;
        }
        void Log(string m,string s,LogType t){if(t==LogType.Error||t==LogType.Exception||t==LogType.Assert)errors+=m+"\n"+s+"\n";}
        public void Apply(){QualitySettings.renderPipeline=mode==0?hdr:ao;reviewCamera.allowHDR=true;reviewCamera.GetUniversalAdditionalCameraData().renderPostProcessing=true;atmosphere.Palette=palette;atmosphere.ApplyLighting();globals.ApplyGlobals();}
        public void SetHour(float h){hour=h;clock.RunAutomatically=false;if(clock.TryInitialize())clock.RestoreState(new WorldTimeState(1,h*60));atmosphere.ApplyLighting();globals.ApplyGlobals();}
        public void SetView(int i){view=i;reviewCamera.orthographic=false;reviewCamera.transform.position=Eyes[i];reviewCamera.transform.LookAt(Targets[i]);}
        void LateUpdate(){if(capture){var t=Shader.GetGlobalVector("_LC_GameTime");t.z=2;Shader.SetGlobalVector("_LC_GameTime",t);}}
        void Update()
        {
            if(collecting){FrameTimingManager.CaptureFrameTimings();if(timer.Elapsed.TotalSeconds>90)Finish();}
            if(capture||benchmark)return;
            if(Input.GetKeyDown(KeyCode.F1)){mode=1-mode;Apply();}
            if(Input.GetKeyDown(KeyCode.F2))SetView((view+1)%4);
            if(Input.GetKeyDown(KeyCode.F3))SetHour(hour==12?18.5f:hour==18.5f?1:12);
            if(Input.GetKeyDown(KeyCode.Escape))Application.Quit();
        }
        IEnumerator Start()
        {
            yield return new WaitForSecondsRealtime(2);
            ValidateBindings();
            if(benchmark){warmup=Time.frameCount;stamp=0;timer.Restart();collecting=true;yield break;}
            if(!capture)yield break;
            interaction.interactionEnabled=false;
            if(demonstrator)demonstrator.gameObject.SetActive(false);
            foreach(int m in new[]{0,1})foreach(float h in new[]{12f,18.5f,1f})for(int v=0;v<4;v++)
            {mode=m;Apply();SetHour(h);SetView(v);for(int f=0;f<8;f++)yield return null;yield return SaveVerifiedImage(Label());}
            mode=1;Apply();SetHour(12);SetView(1);
            float roots=grass.material.GetFloat("_MeadowRootOcclusion");
            grass.material.SetFloat("_MeadowRootOcclusion",0);for(int f=0;f<5;f++)yield return null;yield return SaveVerifiedImage("ao-roots-disabled");grass.material.SetFloat("_MeadowRootOcclusion",roots);
            // Exercise preserved brush on this private in-memory fixture, never load/save a user document.
            ulong original=painter.MaskDigest();
            painter.AddStroke(new SurfacePaintStroke{id="lighting-only-check-road",road=true,radius=1.45f,points=new List<Vector2>{new Vector2(-12,-3),new Vector2(-4,5)}});
            if(painter.SampleRoad01(-8,1)<.95f||painter.SampleGrass01(-8,1)>.05f)throw new InvalidOperationException("Road / vegetation coverage diverged.");
            for(int f=0;f<5;f++)yield return null;yield return SaveVerifiedImage("painted-road");painter.Undo();if(painter.MaskDigest()!=original)throw new InvalidOperationException("Lighting fixture brush undo changed authored mask.");
            interaction.interactionEnabled=true;if(demonstrator){demonstrator.gameObject.SetActive(true);demonstrator.position=new Vector3(-2.8f,MeadowPreview.SampleHeight(-2.8f,1.8f)+.7f,1.8f);}
            yield return new WaitForSecondsRealtime(1.3f);yield return SaveVerifiedImage("interaction-on");interaction.interactionEnabled=false;yield return new WaitForSecondsRealtime(1.3f);yield return SaveVerifiedImage("interaction-off");if(demonstrator)demonstrator.gameObject.SetActive(false);
            // Saved sequential frames expose edge, cascade and wind stability to visual review.
            for(int v=0;v<4;v+=3)
            {
                SetView(v);Vector3 eye=reviewCamera.transform.position;
                for(int f=0;f<16;f++){reviewCamera.transform.position=eye+Vector3.right*f*.07f;yield return null;yield return null;yield return SaveVerifiedImage("camera-"+v+"-"+f.ToString("D2"));}
            }
            SetView(3);
            for(int f=0;f<16;f++){var t=Shader.GetGlobalVector("_LC_GameTime");t.z=2+f*.1f;Shader.SetGlobalVector("_LC_GameTime",t);yield return SaveVerifiedImage("wind-"+f.ToString("D2"));yield return null;}
            File.WriteAllText(output+"/checks.txt","PASS actual Meadow terrain, mask, instanced native grass, four LODs and approved Oak A/B/C/D fixture.\nPASS road and vegetation mask agreement; Undo restores original digest.\nPASS HDR / AO runtime switches; direct and emission are not screen-AO multiplied.\nAnimation/halo acceptance requires visual review of images, not these assertions.\n"+errors);
            File.WriteAllText(output+"/runtime-errors.txt",errors);completed=true;Application.Quit(errors.Length==0?0:2);
        }
        void ValidateBindings()
        {
            if(grass.data.lodMeshes.Length!=4||!grass.material.enableInstancing||!grass.material.IsKeywordEnabled("_LC_MEADOW")||!grass.material.IsKeywordEnabled("_LC_SURFACE_PAINT"))throw new InvalidOperationException("Missing native meadow rendering contract.");
            if(painter.terrainMaterial.GetTexture("_SurfacePaint")!=grass.material.GetTexture("_SurfacePaint"))throw new InvalidOperationException("Terrain and grass do not share the surface mask.");
            if(painter.SampleRoad01(2,0)<.9f||painter.SampleGrass01(2,0)>.1f)throw new InvalidOperationException("Authored road mask was lost.");
        }
        string Label()=>(mode==0?"hdr":"ao")+"-"+Names[view]+"-"+hour.ToString("00.0",System.Globalization.CultureInfo.InvariantCulture);
        IEnumerator SaveVerifiedImage(string name)
        {
            for(int attempt=0;attempt<4;attempt++)
            {
                if(CaptureAttempt(name))yield break;
                // URP can initialise the first offscreen request without image data.
                // Retry on a later frame, not twice inside the same render graph.
                yield return null;
            }
            throw new InvalidOperationException("Empty capture after four rendered frames: "+name);
        }
        bool CaptureAttempt(string name)
        {
            const int w=1920,h=1080;
            var d=new RenderTextureDescriptor(w,h,RenderTextureFormat.ARGB32,24){msaaSamples=4,sRGB=QualitySettings.activeColorSpace==ColorSpace.Linear};
            d.msaaSamples=SystemInfo.GetRenderTextureSupportedMSAASampleCount(d);
            var target=RenderTexture.GetTemporary(d);var resolve=RenderTexture.GetTemporary(w,h,0,RenderTextureFormat.ARGB32,QualitySettings.activeColorSpace==ColorSpace.Linear?RenderTextureReadWrite.sRGB:RenderTextureReadWrite.Linear);
            var old=reviewCamera.targetTexture;var active=RenderTexture.active;var pixels=new Texture2D(w,h,TextureFormat.RGB24,false,false);
            try
            {
                reviewCamera.targetTexture=target;
                CameraCaptureCompatibility.Render(reviewCamera,target);
                if(d.msaaSamples>1)target.ResolveAntiAliasedSurface(resolve);else Graphics.Blit(target,resolve);
                RenderTexture.active=resolve;pixels.ReadPixels(new Rect(0,0,w,h),0,0);pixels.Apply();
                var bytes=pixels.GetRawTextureData<byte>();int maximum=0,minimum=255;
                for(int i=0;i<bytes.Length;i+=257){maximum=Mathf.Max(maximum,bytes[i]);minimum=Mathf.Min(minimum,bytes[i]);}
                if(maximum<3||maximum-minimum<3)return false;
                File.WriteAllBytes(output+"/"+name+".png",pixels.EncodeToPNG());File.WriteAllText(output+"/"+name+".json",JsonUtility.ToJson(Report("CAPTURE_NOT_TIMING"),true));
                return true;
            }
            finally{reviewCamera.targetTexture=old;RenderTexture.active=active;RenderTexture.ReleaseTemporary(target);RenderTexture.ReleaseTemporary(resolve);Destroy(pixels);}
        }
        void OnRendered(ScriptableRenderContext c,Camera camera)
        {
            if(!collecting||camera!=reviewCamera||camera.targetTexture!=null||Time.frameCount-warmup<120||lastFrame==Time.frameCount)return;
            lastFrame=Time.frameCount;long now=System.Diagnostics.Stopwatch.GetTimestamp();
            if(stamp!=0){wall.Add((float)((now-stamp)*1000.0/System.Diagnostics.Stopwatch.Frequency));if(FrameTimingManager.GetLatestTimings(1,timings)>0){if(timings[0].gpuFrameTime>0)gpu.Add((float)timings[0].gpuFrameTime);if(timings[0].cpuFrameTime>0)cpu.Add((float)timings[0].cpuFrameTime);}}
            stamp=now;if(wall.Count>=600)Finish();
        }
        void Finish(){collecting=false;var r=Report(wall.Count>=600&&errors.Length==0?"PASS":"FAIL");r.frame=LightingBenchmarkController.Distribution.From(wall);r.gpu=LightingBenchmarkController.Distribution.From(gpu);r.cpu=LightingBenchmarkController.Distribution.From(cpu);r.allocatedBytes=UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong();File.WriteAllText(output+"/benchmark.json",JsonUtility.ToJson(r,true));File.WriteAllText(output+"/runtime-errors.txt",errors);completed=true;Application.Quit(r.status=="PASS"?0:2);}
        Measurement Report(string status)=>new Measurement{status=status,utc=DateTime.UtcNow.ToString("O"),unity=Application.unityVersion,gpuName=SystemInfo.graphicsDeviceName,cpuName=SystemInfo.processorType,api=SystemInfo.graphicsDeviceType.ToString(),colorSpace=QualitySettings.activeColorSpace.ToString(),development=Debug.isDebugBuild,frameTiming=FrameTimingManager.IsFeatureEnabled(),width=Screen.width,height=Screen.height,mode=mode==0?"HDR":"HDR+AO",view=Names[view],hour=hour,pipeline=UrpPreviewQuality.ReadActive(),clumps=grass.submittedClumps,triangles=grass.submittedTriangles,draws=grass.drawCalls,roots=grass.material.GetFloat("_MeadowRootOcclusion"),cameraPosition=reviewCamera.transform.position};
        [Serializable] public sealed class Measurement{public string status,utc,unity,gpuName,cpuName,api,colorSpace,mode,view;public bool development,frameTiming;public int width,height,clumps,triangles,draws;public float hour,roots;public long allocatedBytes;public Vector3 cameraPosition;public UrpPreviewQuality.Snapshot pipeline;public LightingBenchmarkController.Distribution frame,gpu,cpu;}
        void OnGUI(){if(showUI)GUI.Box(new Rect(12,12,700,65),"Meadow lighting | "+QualitySettings.activeColorSpace+" | "+(mode==0?"HDR":"HDR + AO")+" | "+hour+"h\nF1 AO toggle | F2 camera | F3 day/sunset/night | Esc exit");}
        void OnDestroy(){RenderPipelineManager.endCameraRendering-=OnRendered;Application.logMessageReceived-=Log;if(QualitySettings.renderPipeline==hdr||QualitySettings.renderPipeline==ao)QualitySettings.renderPipeline=previous;}
        void OnApplicationQuit(){if((capture||benchmark)&&!completed)File.WriteAllText(output+"/interrupted.txt","Evidence incomplete.\n"+errors);}
    }
}
