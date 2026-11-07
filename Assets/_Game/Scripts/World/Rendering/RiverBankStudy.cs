using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using LittleCastle.CameraSystem;
using LittleCastle.Rendering;
using LittleCastle.World;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace LittleCastle.Research
{
    /// <summary>Repeatable, isolated acceptance fixture. It edits the shared map session, not a second river model.</summary>
    public sealed class RiverBankStudy : MonoBehaviour
    {
        [Tooltip("Opt in only inside the isolated study. Disabled by default, including Awake side effects.")]
        public bool enableStudyDriver;
        bool initialized;
        MapRockCatalog Catalog=>painter.riverCatalogOverride?painter.riverCatalogOverride:MapRockCatalog.Default;
        public MeadowSurfacePainter painter;
        public Camera studyCamera;
        public Light sun;
        public StylizedLightingGlobals globals;
        public UniversalRenderPipelineAsset profile;
        public GameObject bridge;
        public bool waterOnlyFixture;
        public static readonly Vector2 BridgeOrigin=new Vector2(-4,-4);
        public const float BridgeYaw=-71.56505f;
        public const float FordFraction=.88f;
        RenderPipelineAsset previous;
        string errors="";
        bool automated,evening;
        int view;
        readonly FrameTiming[] timings=new FrameTiming[1];
        public static float Height(float x,float z)
        {
            float h=3.5f+.18f*Mathf.Sin(x*.075f)*Mathf.Cos(z*.08f)+2.5f*Mathf.Exp(-((x+21)*(x+21)/190+(z-25)*(z-25)/250));
            float distance=Vector2.Distance(new Vector2(x,z),BridgeOrigin);
            float plateau=Mathf.SmoothStep(0,1,Mathf.InverseLerp(8,23,distance));
            h=Mathf.Lerp(0,h,plateau);
            float floodplain=Vector2.Distance(new Vector2(x,z),new Vector2(-4,35));
            return Mathf.Lerp(.2f,h,Mathf.SmoothStep(0,1,Mathf.InverseLerp(6,22,floodplain)));
        }
        public static MapRiverRecord River(int seed=1729)
        {
            var river=new MapRiverRecord{id=2026102602,seed=seed,layoutVersion=RiverBankGeneration.Version};
            Vector2[] points={new Vector2(-12,-26),BridgeOrigin,new Vector2(2,18),new Vector2(-6,43)};
            for(int i=0;i<points.Length;i++)river.nodes.Add(new MapRiverNode{id=i+1,position=points[i],width=new[]{9f,6,9,8}[i],depth=1.8f,waterHeight=-.85f,speed=.9f,leftTransition=8,rightTransition=8,leftBank=i<2?MapBankKind.Cliff:MapBankKind.Stepped,rightBank=i==3?MapBankKind.Sand:MapBankKind.Rocky});
            var path=new MapRiverPath(river);
            path.Nearest(BridgeOrigin,out var crossing,out _);river.patches.Add(new MapBankPatch{id=501,station=crossing.station,radius=5,left=true,right=true});
            river.patches.Add(new MapBankPatch{id=502,station=path.Length*FordFraction,radius=7,left=true,right=true,crossChannel=true,shallowDepth=.25f});
            return river;
        }
        public static MapRiverRecord WaterRiver(int seed=1729)
        {
            var river=River(seed);river.patches.Clear();
            foreach(var node in river.nodes){node.leftBank=node.rightBank=MapBankKind.Sand;node.depth=1.45f;}
            // Existing bridge deck ends at +/-5.4m: complete the bank rise before its road sockets.
            river.nodes[1].leftTransition=river.nodes[1].rightTransition=2;
            return river;
        }
        void Awake()
        {
            if (!enableStudyDriver) return;
            if (!waterOnlyFixture) throw new InvalidOperationException("Only the isolated water fixture is supported in Main.");
            initialized = true;
            previous=QualitySettings.renderPipeline;QualitySettings.renderPipeline=profile;Application.runInBackground=true;
            automated=Array.IndexOf(Environment.GetCommandLineArgs(),"-riverBankCapture")>=0;
            Application.logMessageReceived+=Log;Daylight(false);
        }
        void Log(string message,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors+=message+"\n"+stack+"\n";}
        void OnDestroy(){if(!initialized)return;Application.logMessageReceived-=Log;if(QualitySettings.renderPipeline==profile)QualitySettings.renderPipeline=previous;}
        IEnumerator Start()
        {
            if (!enableStudyDriver) yield break;
            yield return null;
            if(!automated){SetView(0);yield break;}
            var args=Environment.GetCommandLineArgs();int arg=Array.IndexOf(args,"-riverBankCapture");string folder=args[arg+1];Directory.CreateDirectory(folder);
            QualitySettings.vSyncCount=0;Application.targetFrameRate=60;
            var controller=studyCamera.GetComponent<StrategyCameraController>();if(controller){controller.SetInputEnabled(false);controller.enabled=false;}
            var riverTool=painter.GetComponent<MapRiverRuntimeTool>();if(riverTool)riverTool.enabled=false;
            // Warm presentation and bounded collider updates before taking or timing frames.
            for(int i=0;i<90;i++){painter.RiverView.Tick(studyCamera.transform.position,true);yield return null;}
            string[] names=waterOnlyFixture?new[]{"00_water_game_day","01_water_top","02_water_close","03_shore_transition","04_visible_bed","05_water_chunk_border","06_water_bridge","07_water_bend"}:new[]{"00_game_camera_day","01_top_day","02_bank_close","03_module_contacts","04_rock_grass","05_chunk_border","06_bridge","07_sand_ford"};
            for(int i=0;i<names.Length;i++){SetView(i);yield return new WaitForSecondsRealtime(.5f);yield return new WaitForEndOfFrame();Capture(Path.Combine(folder,names[i]+".png"));}
            if(waterOnlyFixture){SetView(8);yield return new WaitForSecondsRealtime(.5f);yield return new WaitForEndOfFrame();Capture(Path.Combine(folder,"10_water_sun_glints.png"));}
            yield return Guard(Checks(folder));
            Daylight(true);SetView(0);yield return new WaitForSecondsRealtime(.5f);yield return new WaitForEndOfFrame();Capture(Path.Combine(folder,"08_game_camera_evening.png"));
            SetView(2);yield return new WaitForSecondsRealtime(.35f);yield return new WaitForEndOfFrame();Capture(Path.Combine(folder,"09_bank_evening.png"));
            Daylight(false);SetView(waterOnlyFixture?8:6);
            for(int i=0;i<24;i++){yield return new WaitForSecondsRealtime(.2f);yield return new WaitForEndOfFrame();Capture(Path.Combine(folder,"flow_"+i.ToString("D2")+".png"));}
            File.WriteAllText(Path.Combine(folder,"capture-log-before-benchmark.txt"),$"Unity errors before timed rendering:\n{errors}");
            yield return Benchmark(folder);
            File.WriteAllText(Path.Combine(folder,"player_result.txt"),$"Unity={Application.unityVersion}\nGPU={SystemInfo.graphicsDeviceName}\nAPI={SystemInfo.graphicsDeviceType}\nResolution={Screen.width}x{Screen.height}\nWater={Catalog.waterMaterial.shader.name}\nWaterOnly={waterOnlyFixture}; resident cliff models={painter.RiverView.ResidentRocks}\nErrors:\n"+errors);
            Application.Quit(string.IsNullOrEmpty(errors)?0:2);
        }
        IEnumerator Checks(string folder)
        {
            var checks=new List<string>();var session=painter.Session;var record=session.GetRiver(2026102602);var path=new MapRiverPath(record);
            if(record.layoutVersion!=2||painter.RiverView.ResidentTiles==0)throw new Exception("Version 2 fixture did not materialize.");
            checks.Add($"PASS v2: {path.frames.Count} global frames; tiles={painter.RiverView.ResidentTiles}; catalog rocks={record.rocks.Count}; resident={painter.RiverView.ResidentRocks}; mesh update={painter.RiverView.LastMeshMilliseconds:F3}ms.");
            if(waterOnlyFixture)
            {
                if(record.rocks.Count!=0||painter.RiverView.ResidentRocks!=0)throw new Exception("Water-only fixture created cliff models.");
                foreach(var filter in Resources.FindObjectsOfTypeAll<MeshFilter>())if(filter.gameObject.scene.IsValid()&&filter.sharedMesh&&filter.sharedMesh.name=="River bank mass v2")throw new Exception("Water-only fixture created cliff geometry.");
                checks.Add("PASS water-only: existing Meadow heightfield, no cliff instances or added bank meshes; bed and banks are terrain.");
            }
            if(!waterOnlyFixture){
            var cliff=path.At(path.Length*.59f);float d=cliff.width*.5f+1.5f;var p=cliff.center+cliff.Left*d;
            if(Mathf.Abs(session.SampleHeight(p.x,p.y)-session.SampleBaseHeight(p.x,p.y))>.15f)throw new Exception("Cliff plateau is still cut into a wide berm.");
            checks.Add("PASS cliff plateau remains within .15m of the original land 1.5m behind the water edge.");
            float fordStation=path.Length*FordFraction;var f=path.At(fordStation);var a=f.center+f.Left*(f.width*.5f+f.leftTransition+1);var b=f.center-f.Left*(f.width*.5f+f.rightTransition+1);
            if(!session.CanTraverse(a,b))throw new Exception("Fixture ford is not connected at depth <=.45 and slope <=32 degrees.");checks.Add("PASS ford uses composed terrain, water depth and slope queries.");
            }
            string json=session.ExportJson();session.LoadJson(json);if(json!=session.ExportJson())throw new Exception("v5 saved river composition changed.");File.WriteAllText(Path.Combine(folder,"study-save-v5.json"),json);checks.Add("PASS v5 exact final composition round trip.");
            // Main uses terrain banks; the rejected source cliff-ribbon experiment is not installed.
            if(waterOnlyFixture)
            {
                int meshes=0,contacts=0;var shared=new Dictionary<Vector2,float>();
                foreach(var filter in Resources.FindObjectsOfTypeAll<MeshFilter>())if(filter.gameObject.scene.IsValid()&&filter.sharedMesh&&filter.sharedMesh.name=="Flow strip")
                {
                    meshes++;var vertices=filter.sharedMesh.vertices;var triangles=filter.sharedMesh.triangles;var tangents=filter.sharedMesh.tangents;
                    if(tangents.Length!=vertices.Length)throw new Exception("Water has no tangent basis for Uber normal/specular lighting.");
                    foreach(var tangent in tangents)if(Mathf.Abs(new Vector3(tangent.x,tangent.y,tangent.z).magnitude-1)>.001f||Mathf.Abs(tangent.w)!=1)throw new Exception("Invalid water tangent basis.");
                    for(int i=0;i<triangles.Length;i+=3)if(Vector3.Cross(vertices[triangles[i+1]]-vertices[triangles[i]],vertices[triangles[i+2]]-vertices[triangles[i]]).y<=0)throw new Exception("Water has a folded or degenerate triangle.");
                    foreach(var v in vertices){var key=new Vector2(v.x,v.z);if(shared.TryGetValue(key,out float y)){if(Mathf.Abs(v.y-y)>1e-5f)throw new Exception("Water chunk border height differs.");contacts++;}else shared.Add(key,v.y);}
                }
                if(meshes<3||contacts<60)throw new Exception("Insufficient resident water seam coverage.");checks.Add($"PASS real water meshes: {meshes} resident strips, {contacts} matching shared vertices, all triangles upward.");
                var material=Catalog.waterMaterial;
                foreach(string keyword in new[]{"_ENABLENORMAL","_ENABLECAUSTICS","_ENABLEREFRACTION","_ENABLESHORELINE","_ENABLEWAVE","_ENABLEPLANERREFLECTION"})if(!material.IsKeywordEnabled(keyword))throw new Exception("Uber fixture feature disabled: "+keyword);
                var reflection=Shader.GetGlobalTexture("_PlanarReflectionTexture") as RenderTexture;
                if(!reflection||!reflection.IsCreated())throw new Exception("Original Uber manager did not create a reflection render target.");
                checks.Add($"PASS Uber normal tangent basis; waves/shoreline/caustics/refraction/normals/planar-reflection keywords active; original reflection target={reflection.width}x{reflection.height}.");
                var f=path.At(path.Length*.45f);if(!session.QueryWater(f.center,out _,out float depth,out _)||depth<1)throw new Exception("Deep channel water query disagrees with visible bed.");
                double commit;var timer=System.Diagnostics.Stopwatch.StartNew();session.TranslateRiver(record.id,new Vector2(22,0));timer.Stop();commit=timer.Elapsed.TotalMilliseconds;session.Undo();session.DeleteRiver(record.id);if(Mathf.Abs(session.SampleHeight(f.center.x,f.center.y)-session.SampleBaseHeight(f.center.x,f.center.y))>.001f)throw new Exception("River removal did not restore terrain.");session.Undo();session.LoadJson(json);checks.Add($"PASS shared move/delete/undo/v5 reload restores terrain and exact river data; move commit={commit:F2}ms. Deep water remains nonwalkable.");
            }
            for(int i=0;i<90&&painter.TerrainView.PendingColliders>0;i++)yield return null;
            checks.Add($"Terrain collider queue after warmup={painter.TerrainView.PendingColliders}; total views={painter.TerrainView.ChunkCount}. Bridge uses approved v002 prefab, yaw only, scale 1.");
            File.WriteAllLines(Path.Combine(folder,"geometry-checks.txt"),checks);
            using(var navigation=new MapRiverNavigation())
            {
                navigation.Build(session,bridge,new Bounds(new Vector3(-4,0,9),new Vector3(70,24,95)));
                var from=FixedBridgeSiteProfile.LocalToWorld(new Vector2(0,-9),BridgeOrigin,BridgeYaw);var to=FixedBridgeSiteProfile.LocalToWorld(new Vector2(0,9),BridgeOrigin,BridgeYaw);
                var start=new Vector3(from.x,session.SampleHeight(from.x,from.y),from.y);var goal=new Vector3(to.x,session.SampleHeight(to.x,to.y),to.y);
                if(!navigation.Path(start,goal,out var bridgePath))throw new Exception("Actual bridge collider + bank terrain has no complete NavMesh route.");
                float length=0;for(int i=1;i<bridgePath.corners.Length;i++)length+=Vector3.Distance(bridgePath.corners[i-1],bridgePath.corners[i]);if(length>25)throw new Exception("Navigation detours around the bridge instead of using its deck.");
                if(!UnityEngine.AI.NavMesh.SamplePosition(start,out var spawn,1,UnityEngine.AI.NavMesh.AllAreas))throw new Exception("No bridge approach spawn.");
                var agentRoot=new GameObject("Navigation acceptance agent");agentRoot.transform.position=spawn.position;var agent=agentRoot.AddComponent<UnityEngine.AI.NavMeshAgent>();agent.radius=.3f;agent.height=1.7f;agent.speed=6;agent.acceleration=24;agent.Warp(spawn.position);agent.SetDestination(goal);
                float deadline=Time.realtimeSinceStartup+10;while((agent.pathPending||Vector3.Distance(agent.transform.position,goal)>1)&&Time.realtimeSinceStartup<deadline)yield return null;
                bool reached=Vector3.Distance(agent.transform.position,goal)<=1;Destroy(agentRoot);if(!reached)throw new Exception("NavMesh agent did not cross the actual bridge.");
                checks.Add($"PASS real Unity NavMesh: {navigation.SourceCount} sources, bake={navigation.BuildMilliseconds:F2}ms, bridge path={length:F2}m, agent reached opposite bank. Deep water triangles excluded at .45m depth.");
            }
            File.WriteAllLines(Path.Combine(folder,"geometry-checks.txt"),checks);
        }
        IEnumerator Guard(IEnumerator routine)
        {
            while(true)
            {
                object value;
                try{if(!routine.MoveNext())yield break;value=routine.Current;}
                catch(Exception error){Debug.LogException(error);yield break;}
                if(value is IEnumerator nested)yield return Guard(nested);else yield return value;
            }
        }
        IEnumerator Benchmark(string folder)
        {
            int vsync=QualitySettings.vSyncCount,target=Application.targetFrameRate;QualitySettings.vSyncCount=0;Application.targetFrameRate=-1;
            var lines=new List<string>{"mode,frames,cpu_counter_mean_ms,cpu_counter_p95_ms,gpu_mean_ms,wall_mean_ms,wall_p95_ms,total_allocated_bytes,reserved_bytes,tiles,rocks,grass_draws,grass_triangles"};
            var water=Catalog.waterMaterial;
            bool normals=water.IsKeywordEnabled("_ENABLENORMAL"),caustics=water.IsKeywordEnabled("_ENABLECAUSTICS"),refraction=water.IsKeywordEnabled("_ENABLEREFRACTION");
            var renderTarget=RenderTexture.GetTemporary(new RenderTextureDescriptor(Screen.width,Screen.height,RenderTextureFormat.ARGB32,24){msaaSamples=profile.msaaSampleCount});
            try
            {
                SetView(0);
                for(int mode=0;mode<2;mode++)
                {
                    if(mode==1){water.DisableKeyword("_ENABLENORMAL");water.DisableKeyword("_ENABLECAUSTICS");water.DisableKeyword("_ENABLEREFRACTION");}
                    for(int i=0;i<90;i++){RenderPipeline.SubmitRenderRequest(studyCamera,new RenderPipeline.StandardRequest{destination=renderTarget});FrameTimingManager.CaptureFrameTimings();yield return null;}
                    var cpu=new List<double>();var gpu=new List<double>();var wall=new List<double>();var timer=System.Diagnostics.Stopwatch.StartNew();double previousMs=timer.Elapsed.TotalMilliseconds;
                    for(int i=0;i<360;i++){RenderPipeline.SubmitRenderRequest(studyCamera,new RenderPipeline.StandardRequest{destination=renderTarget});FrameTimingManager.CaptureFrameTimings();yield return null;double now=timer.Elapsed.TotalMilliseconds;wall.Add(now-previousMs);previousMs=now;uint n=FrameTimingManager.GetLatestTimings(1,timings);if(n>0&&timings[0].cpuFrameTime>0)cpu.Add(timings[0].cpuFrameTime);if(n>0&&timings[0].gpuFrameTime>0)gpu.Add(timings[0].gpuFrameTime);}
                    cpu.Sort();wall.Sort();var grass=painter.GetComponentInChildren<MeadowGrassRenderer>();if(!grass)grass=FindAnyObjectByType<MeadowGrassRenderer>();
                    lines.Add(string.Format(CultureInfo.InvariantCulture,"{0},{1},{2:F3},{3:F3},{4:F3},{5:F3},{6:F3},{7},{8},{9},{10},{11},{12}",mode==0?"uber_full":"uber_depth_color_foam",cpu.Count,cpu.Count>0?cpu.Average():double.NaN,cpu.Count>0?cpu[(int)((cpu.Count-1)*.95)]:double.NaN,gpu.Count>0?gpu.Average():double.NaN,wall.Average(),wall[(int)((wall.Count-1)*.95)],Profiler.GetTotalAllocatedMemoryLong(),Profiler.GetTotalReservedMemoryLong(),painter.RiverView.ResidentTiles,painter.RiverView.ResidentRocks,grass?grass.drawCalls:0,grass?grass.submittedTriangles:0));
                }
                File.WriteAllLines(Path.Combine(folder,"frame-timing.csv"),lines);
                File.WriteAllText(Path.Combine(folder,"benchmark-scope.txt"),"360 forced URP camera render requests/mode after 90 warmup, 1600x900, existing MSAA. A hidden Player can skip normal camera rendering, so every timed iteration explicitly renders the actual camera into one reused target. No PNG readback during timing. VSync and FPS limit disabled. Same scene/camera; compares Uber normals+refraction+caustics against its depth/color/foam path. Wall includes CPU render submission/driver waits; it is not an interactive FPS or isolated GPU measurement. Whole-scene memory. GPU NaN means timing unavailable.\n");
            }
            finally{RenderTexture.ReleaseTemporary(renderTarget);if(normals)water.EnableKeyword("_ENABLENORMAL");if(caustics)water.EnableKeyword("_ENABLECAUSTICS");if(refraction)water.EnableKeyword("_ENABLEREFRACTION");QualitySettings.vSyncCount=vsync;Application.targetFrameRate=target;}
        }
        public void SetView(int index)
        {
            view=index;studyCamera.orthographic=false;studyCamera.fieldOfView=46;
            Vector3[] positions={new Vector3(32,29,-32),new Vector3(-4,83,9),new Vector3(9,8,9),new Vector3(8,7,24),new Vector3(-6,8,27),new Vector3(15,14,12),new Vector3(12,9,-14),new Vector3(15,17,32),new Vector3(-16,21,12)};
            Vector3[] targets={new Vector3(-4,1,10),new Vector3(-4,0,9),new Vector3(-3,2,22),new Vector3(-3,2.8f,30),new Vector3(-7,4,26),new Vector3(-1,2,18),new Vector3(-4,0,-4),new Vector3(-2,0,29),new Vector3(-4,-.8f,-4)};
            int i=Mathf.Clamp(index,0,positions.Length-1);studyCamera.transform.position=positions[i];studyCamera.transform.LookAt(targets[i],i==1?Vector3.forward:Vector3.up);if(i==1){studyCamera.orthographic=true;studyCamera.orthographicSize=43;}
            var controller=studyCamera.GetComponent<StrategyCameraController>();if(controller&&controller.enabled)controller.SyncFromCurrentView();
        }
        void Daylight(bool sunset)
        {
            evening=sunset;sun.transform.rotation=Quaternion.Euler(sunset?19:48,sunset?-65:-35,0);sun.color=sunset?new Color(1,.68f,.42f):new Color(1,.94f,.82f);sun.intensity=sunset?.82f:1.15f;
            RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=sunset?new Color(.30f,.34f,.46f):new Color(.42f,.49f,.58f);RenderSettings.ambientEquatorColor=sunset?new Color(.29f,.29f,.24f):new Color(.32f,.37f,.25f);RenderSettings.ambientGroundColor=new Color(.18f,.20f,.13f);if(globals)globals.ApplyGlobals();
        }
        void Update(){if(!enableStudyDriver||automated)return;if(Input.GetKeyDown(KeyCode.F1))SetView((view+1)%(waterOnlyFixture?9:8));if(Input.GetKeyDown(KeyCode.F2))Daylight(!evening);if(Input.GetKeyDown(KeyCode.Escape)&&painter.ShouldQuitOnEscape)Application.Quit();}
        void OnGUI(){if(!enableStudyDriver||automated)return;GUI.Label(new Rect(14,12,700,25),waterOnlyFixture?"Река · F1 ракурс · F2 день/вечер · WASD и колесо — камера":"River bank study · F1 camera · F2 day/evening");}
        void Capture(string path)
        {
            var descriptor=new RenderTextureDescriptor(Screen.width,Screen.height,RenderTextureFormat.ARGB32,24){msaaSamples=profile.msaaSampleCount};var target=RenderTexture.GetTemporary(descriptor);var resolved=RenderTexture.GetTemporary(Screen.width,Screen.height,0,RenderTextureFormat.ARGB32);var old=RenderTexture.active;
            try{RenderPipeline.SubmitRenderRequest(studyCamera,new RenderPipeline.StandardRequest{destination=target});Graphics.Blit(target,resolved);RenderTexture.active=resolved;var image=new Texture2D(Screen.width,Screen.height,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,Screen.width,Screen.height),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());Destroy(image);}
            finally{RenderTexture.active=old;RenderTexture.ReleaseTemporary(target);RenderTexture.ReleaseTemporary(resolved);}
        }
    }
}
