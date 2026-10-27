using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using LittleCastle.CameraSystem;
using LittleCastle.Rendering;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace LittleCastle.Research
{
    public sealed partial class MeadowPreview : MonoBehaviour
    {
        public UniversalRenderPipelineAsset profile;
        public Camera studyCamera;
        public Light sun;
        public StylizedLightingGlobals globals;
        public MeadowGrassRenderer grass;
        public GrassInteractionManager interaction;
        public Transform demonstrator;
        public MeadowSurfacePainter painter;
        public MeadowGrassData baselineGrass;
        RenderPipelineAsset previous;
        bool wind=true,showUI=true,automated,free,moveProxy=true;
        bool automationRequested;
        int view;
        Vector2 panelScroll;
        string errors="";
        readonly FrameTiming[] timings=new FrameTiming[1];
        static readonly string[] ProfileNames=MeadowScatterProfiles.Names;
        float originalSoftness;
        struct Sample {public int mode,frame,patches,clumps,triangles,draws,sources;public double cpu,main,render,gpu,wall;public long gc;}
        public void SetView(int i)
        {
            view=i;free=false;studyCamera.GetComponent<StrategyCameraController>().enabled=false;
            Vector3[] eye={new Vector3(23,25,-27),new Vector3(8,7.8f,-10),new Vector3(12,57,-50),new Vector3(24,10,6)};
            Vector3[] target={new Vector3(0,1,13),new Vector3(-1,.4f,1),new Vector3(0,0,12),new Vector3(16,0,22)};
            studyCamera.transform.position=eye[i];studyCamera.transform.LookAt(target[i]);
        }
        void Awake(){automationRequested=Array.IndexOf(Environment.GetCommandLineArgs(),"-meadowCapture")>=0;previous=QualitySettings.renderPipeline;QualitySettings.renderPipeline=profile;originalSoftness=grass.material.GetFloat("_MeadowSoftness");Application.runInBackground=true;Application.targetFrameRate=60;Application.logMessageReceived+=Log;}
        void OnDestroy(){if(QualitySettings.renderPipeline==profile)QualitySettings.renderPipeline=previous;if(grass&&grass.material)grass.material.SetFloat("_MeadowSoftness",originalSoftness);Application.logMessageReceived-=Log;}
        void Log(string message,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors+=message+"\n"+stack+"\n";if(automationRequested&&(type==LogType.Exception||type==LogType.Assert))Application.Quit(2);}
        void ApplyWind(){globals.SetWindPresentation(new Vector2(.86f,.5f),wind?.6f:0,.65f);}
        public static float SampleHeight(float x,float z)=>.25f+.22f*Mathf.Sin(x*.085f)*Mathf.Cos(z*.068f)+3*Mathf.Exp(-((x+22)*(x+22)/240+(z-25)*(z-25)/380))+2.2f*Mathf.Exp(-((x-25)*(x-25)/310+(z-43)*(z-43)/260));
        void Update()
        {
            if(moveProxy){float t=Time.unscaledTime*.5f;float x=-2.8f+3.2f*Mathf.Sin(t),z=1.8f+2.0f*Mathf.Cos(t);demonstrator.position=new Vector3(x,(painter&&painter.Session!=null?painter.Session.SampleHeight(x,z):SampleHeight(x,z))+.7f,z);}
            if(!automated){for(int i=0;i<4;i++)if(Input.GetKeyDown(KeyCode.Alpha1+i))SetView(i);if(Input.GetKeyDown(KeyCode.H))showUI=!showUI;if(Input.GetKeyDown(KeyCode.Escape)&&(!painter||painter.ShouldQuitOnEscape))Application.Quit();}
        }
        IEnumerator Start()
        {
            SetView(0);ApplyWind();var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"-meadowCapture");if(at<0||at+1>=args.Length)yield break;
            automated=true;showUI=false;string dir=args[at+1];Directory.CreateDirectory(dir);yield return new WaitForSecondsRealtime(4);
            if(Array.IndexOf(args,"-riverChecks")>=0){yield return CaptureRivers(dir);Application.Quit(string.IsNullOrEmpty(errors)?0:2);yield break;}
            if(Array.IndexOf(args,"-treeChecks")>=0&&painter.treeCatalog)MapTreeThumbnailCapture.Capture(painter.treeCatalog,studyCamera,Path.GetFullPath(Path.Combine(dir,"../TreePreviews")));
            for(int i=0;i<4;i++){SetView(i);yield return new WaitForSecondsRealtime(1);yield return new WaitForEndOfFrame();Capture(Path.Combine(dir,new[]{"01_meadow","02_close","03_strategy","04_shadow"}[i]+".png"));}
            SetView(1);wind=false;moveProxy=false;interaction.interactionEnabled=false;ApplyWind();yield return new WaitForSecondsRealtime(.7f);yield return CaptureCoverage(dir);
            SetView(2);moveProxy=false;wind=false;ApplyWind();grass.material.SetFloat("_MeadowSoftness",0);yield return new WaitForSecondsRealtime(.3f);yield return new WaitForEndOfFrame();Capture(Path.Combine(dir,"07_distance_soft_off.png"));grass.material.SetFloat("_MeadowSoftness",originalSoftness);yield return new WaitForSecondsRealtime(.3f);yield return new WaitForEndOfFrame();Capture(Path.Combine(dir,"08_distance_soft_on.png"));
            Vector3 stableEye=studyCamera.transform.position;
            for(int i=0;i<16;i++){studyCamera.transform.position=stableEye+Vector3.right*(i*.1f);yield return new WaitForSecondsRealtime(.07f);yield return new WaitForEndOfFrame();Capture(Path.Combine(dir,"camera_move_"+i.ToString("D3")+".png"));}
            SetView(1);wind=false;ApplyWind();moveProxy=false;
            var checks=new List<string>();ulong initial=painter.MaskDigest();var authored=MeadowSurfacePainter.BuildAuthoredPixels(painter.source);ulong expected=14695981039346656037UL;foreach(byte value in authored){expected^=value;expected=unchecked(expected*1099511628211UL);}if(expected!=initial)throw new Exception("Editor authored mask differs from runtime mask.");checks.Add("PASS saved editor preview and runtime authored mask share identical rasterization.");float px=-8,pz=1;
            var stroke=new SurfacePaintStroke{id="verification-road-negative-coordinates",road=true,radius=1.45f,points=new List<Vector2>{new Vector2(-12,-3),new Vector2(-4,5)}};
            painter.AddStroke(stroke);if(painter.SampleRoad01(px,pz)<.95f||painter.SampleGrass01(px,pz)>.05f)throw new Exception("Road brush mask mismatch.");checks.Add("PASS road raises coverage and suppresses grass in negative world coordinates.");
            string json=painter.ExportJson();ulong painted=painter.MaskDigest();File.WriteAllText(Path.Combine(dir,"surface_edits.json"),json);yield return new WaitForSecondsRealtime(.5f);yield return new WaitForEndOfFrame();Capture(Path.Combine(dir,"05_brush_road.png"));
            painter.ResetEdits();if(painter.MaskDigest()!=initial)throw new Exception("Reset changed authored surface.");painter.LoadJson(json);if(painter.MaskDigest()!=painted)throw new Exception("Stroke serialization replay differs.");checks.Add("PASS reset and JSON roundtrip reproduce mask byte-for-byte.");
            painter.AddStroke(new SurfacePaintStroke{id="verification-meadow",road=false,scatter=true,radius=2,points=new List<Vector2>{new Vector2(px,pz)}});if(painter.SampleRoad01(px,pz)>.05f||painter.SampleGrass01(px,pz)<.95f)throw new Exception("Meadow brush did not restore grass.");checks.Add("PASS meadow restores grass and removes painted road.");yield return new WaitForSecondsRealtime(.5f);yield return new WaitForEndOfFrame();Capture(Path.Combine(dir,"06_brush_meadow.png"));
            int countBefore=painter.scatterRenderer.authoredInstances;ulong digestBefore=painter.scatterRenderer.placementDigest;painter.AddStroke(new SurfacePaintStroke{id="verification-repeat-meadow",road=false,scatter=true,radius=2,points=new List<Vector2>{new Vector2(px,pz)}});if(painter.scatterRenderer.authoredInstances!=countBefore||painter.scatterRenderer.placementDigest!=digestBefore)throw new Exception("Repeated meadow paint duplicated or moved props.");checks.Add("PASS repeated meadow stroke keeps identical scatter placements and count.");
            painter.AddStroke(new SurfacePaintStroke{id="verification-erase",road=false,erase=true,radius=1.2f,points=new List<Vector2>{new Vector2(px,pz)}});if(painter.SampleGrass01(px,pz)>.05f)throw new Exception("Erase failed.");painter.Undo();if(painter.SampleGrass01(px,pz)<.95f)throw new Exception("Undo failed.");checks.Add("PASS erase and undo restore vegetation coverage.");ulong undoRestoredDigest=painter.MaskDigest();painter.Redo();if(painter.SampleGrass01(px,pz)>.05f)throw new Exception("Redo failed.");painter.Undo();if(painter.MaskDigest()!=undoRestoredDigest)throw new Exception("Undo/redo changed the restored mask.");if(painter.lastUpdatedPixels>=MeadowSurfacePainter.Resolution*MeadowSurfacePainter.Resolution/8||painter.scatterRenderer.lastRebuiltRegions>4)throw new Exception("Small stroke invalidated the whole Meadow.");checks.Add("PASS redo restores erase; second undo restores identical mask; small edit updates local pixels and at most4 regions.");painter.ResetEdits();File.WriteAllLines(Path.Combine(dir,"brush_checks.txt"),checks);
            var legacy=JsonUtility.FromJson<SurfacePaintDocument>("{\"version\":1,\"strokes\":[{\"id\":\"old-json\",\"road\":false,\"scatter\":true,\"radius\":2,\"points\":[{\"x\":-8,\"y\":1}]}]}");if(legacy.strokes[0].scatterProfile!=MeadowScatterProfile.Mixed)throw new Exception("Old stroke JSON profile changed.");
            var center=new Vector2(px,pz);painter.AddStroke(new SurfacePaintStroke{id="verification-forest-first",road=false,scatter=true,scatterProfile=MeadowScatterProfile.ForestFloor,radius=6,points=new List<Vector2>{center}});if(painter.scatterRenderer.CountCategoryInCircle(MeadowScatterCategory.ForestDebris,center,4.5f)==0)throw new Exception("Forest profile test area has no debris.");painter.AddStroke(new SurfacePaintStroke{id="verification-flowers-after",road=false,scatter=true,scatterProfile=MeadowScatterProfile.Flowers,radius=6,points=new List<Vector2>{center}});if(painter.scatterRenderer.CountCategoryInCircle(MeadowScatterCategory.ForestDebris,center,4.5f)!=0)throw new Exception("Flower profile left forest debris in its area.");
            painter.AddStroke(new SurfacePaintStroke{id="verification-profile",road=false,scatter=true,scatterProfile=MeadowScatterProfile.ForestFloor,radius=2,points=new List<Vector2>{new Vector2(px,pz)}});string profiled=painter.ExportJson();ulong profileDigest=painter.scatterRenderer.placementDigest;int profileCount=painter.scatterRenderer.authoredInstances;painter.ResetEdits();painter.LoadJson(profiled);if(painter.scatterRenderer.placementDigest!=profileDigest||painter.scatterRenderer.authoredInstances!=profileCount)throw new Exception("Profile replay changed scatter.");painter.ResetEdits();File.AppendAllText(Path.Combine(dir,"brush_checks.txt"),"PASS old JSON defaults to Mixed; flower profile excludes debris; profile JSON replays identical placements.\n");
            if(Array.IndexOf(args,"-treeChecks")>=0)yield return CaptureTrees(dir);
            if(Array.IndexOf(args,"-terrainChecks")>=0)yield return CaptureTerrain(dir);
            SetView(1);moveProxy=false;demonstrator.position=new Vector3(-2.8f,SampleHeight(-2.8f,1.8f)+.7f,1.8f);wind=false;ApplyWind();interaction.interactionEnabled=false;yield return new WaitForSecondsRealtime(1.3f);yield return new WaitForEndOfFrame();Capture(Path.Combine(dir,"interaction_off.png"));
            interaction.interactionEnabled=true;yield return new WaitForSecondsRealtime(1.3f);yield return new WaitForEndOfFrame();Capture(Path.Combine(dir,"interaction_on.png"));
            moveProxy=true;wind=true;ApplyWind();var times=new List<string>();for(int i=0;i<24;i++){yield return new WaitForSecondsRealtime(.075f);yield return new WaitForEndOfFrame();Capture(Path.Combine(dir,"motion_"+i.ToString("D3")+".png"));times.Add(i+","+Time.unscaledTime.ToString(CultureInfo.InvariantCulture));}File.WriteAllLines(Path.Combine(dir,"motion_times.csv"),times);
            // Frame statistics are collected after warmup, without image readback or IMGUI.
            QualitySettings.vSyncCount=0;Application.targetFrameRate=-1;SetView(1);moveProxy=false;
            var samples=new Sample[6*180];bool gcAvailable=false;
            using(var gc=ProfilerRecorder.StartNew(ProfilerCategory.Memory,"GC Allocated In Frame",1))
            for(int mode=0;mode<6;mode++)
            {
                gcAvailable|=gc.Valid;grass.enabled=mode!=0;wind=mode>=2;interaction.interactionEnabled=mode>=3;grass.forceFullDetail=mode==4;ApplyWind();
                if(mode==5){SetView(1);studyCamera.transform.rotation=Quaternion.LookRotation(new Vector3(0,1,-1));}
                float ready=Time.unscaledTime+.65f;for(int f=0;f<45||Time.unscaledTime<ready;f++)yield return null;
                for(int f=0;f<180;f++)
                {
                    FrameTimingManager.CaptureFrameTimings();yield return null;uint count=FrameTimingManager.GetLatestTimings(1,timings);var t=count>0?timings[0]:default;
                    samples[mode*180+f]=new Sample{mode=mode,frame=f,cpu=t.cpuFrameTime,main=t.cpuMainThreadFrameTime,render=t.cpuRenderThreadFrameTime,gpu=t.gpuFrameTime,wall=Time.unscaledDeltaTime*1000,gc=gc.Valid?gc.LastValue:0,patches=grass.enabled?grass.visiblePatches:0,clumps=grass.enabled?grass.submittedClumps:0,triangles=grass.enabled?grass.submittedTriangles:0,draws=grass.enabled?grass.drawCalls:0,sources=interaction.activeSources};
                }
            }
            var lines=new List<string>{"mode,frame,cpuFrameMs,cpuMainMs,cpuRenderMs,gpuFrameMs,wallFrameMs,gcBytes,patches,clumps,triangles,draws,interactionSources"};
            foreach(var s in samples)lines.Add(string.Join(",",s.mode,s.frame,s.cpu.ToString("F4",CultureInfo.InvariantCulture),s.main.ToString("F4",CultureInfo.InvariantCulture),s.render.ToString("F4",CultureInfo.InvariantCulture),s.gpu.ToString("F4",CultureInfo.InvariantCulture),s.wall.ToString("F4",CultureInfo.InvariantCulture),s.gc,s.patches,s.clumps,s.triangles,s.draws,s.sources));
            File.WriteAllLines(Path.Combine(dir,"performance.csv"),lines);
            var restoredMask=painter.authoredPreviewMask;var groundMaterial=painter.terrainMaterial;var bladeMaterial=painter.grassMaterial;var accentMaterial=painter.accentMaterial;Destroy(painter);yield return null;
            if(groundMaterial.GetTexture("_SurfacePaint")!=restoredMask||bladeMaterial.GetTexture("_SurfacePaint")!=restoredMask||accentMaterial.GetTexture("_SurfacePaint")!=restoredMask)throw new Exception("Painter teardown did not restore authored preview bindings.");File.AppendAllText(Path.Combine(dir,"brush_checks.txt"),"PASS painter teardown restores persistent authored mask on all materials.\n");
            File.WriteAllText(Path.Combine(dir,"player_result.txt"),$"Unity={Application.unityVersion}\nGPU={SystemInfo.graphicsDeviceName}\nGraphicsAPI={SystemInfo.graphicsDeviceType}\nResolution={Screen.width}x{Screen.height}\nCPU={SystemInfo.processorType}\nMSAA={profile.msaaSampleCount}\nDevelopment={Debug.isDebugBuild}; FrameTimingStats={FrameTimingManager.IsFeatureEnabled()}; GCcounterAvailable={gcAvailable}\nVSync off during timing; no PNG readback or CSV formatting during timing; 180 frames/mode after45frames and>=.65s warmup. GC includes entire scene, not an isolated renderer assertion.\nModes:0=meadow renderer off (existing accents and scene retained),1=grass no wind,2=wind,3=interaction,4=forceLOD0,5=look away.\nErrors:\n"+errors);Application.Quit(string.IsNullOrEmpty(errors)?0:2);
        }
        [Serializable] sealed class TreeSnapshot {public List<LittleCastle.World.MapTreeInstance> records;}
        string TreeDigest()=>JsonUtility.ToJson(new TreeSnapshot{records=painter.Session.ExportTrees()});
        IEnumerator CaptureTrees(string dir)
        {
            var session=painter.Session;var view=painter.TreeView;var catalog=painter.treeCatalog;
            if(view==null||catalog==null)throw new Exception("Tree catalog/presenter missing.");
            var checks=new List<string>();string original=painter.ExportJson();int baseCount=session.TreeCount;
            var positions=new[]{new Vector2(-14,-8),new Vector2(-6,-12),new Vector2(13,-4),new Vector2(23,4)};
            var ids=new long[4];for(int i=0;i<4;i++)ids[i]=session.PlaceTree(catalog.entries[i].definition.archetypeId,positions[i],i*47,1,LittleCastle.World.MapObjectIds.FromKey("verified.single."+i));
            for(int i=0;i<4;i++){view.Tick(studyCamera.transform.position,true);yield return null;}
            var root=view.Root(ids[0]);if(!root||root.GetComponent<LODGroup>().GetLODs().Length!=3)throw new Exception("Approved tree lost its authored LODs.");
            var entry=catalog.Resolve(session.GetTree(ids[0]).archetypeId);var sourceRenderers=entry.prefab.GetComponentsInChildren<Renderer>(true);var actualRenderers=root.GetComponentsInChildren<Renderer>(true);
            for(int i=0;i<sourceRenderers.Length;i++)for(int m=0;m<sourceRenderers[i].sharedMaterials.Length;m++)if(sourceRenderers[i].sharedMaterials[m]!=actualRenderers[i].sharedMaterials[m])throw new Exception("Tree presenter cloned/replaced shared materials.");
            session.TransformTree(ids[0],new Vector2(-15,-6),135,1.1f);if(view.Root(ids[0])!=root)throw new Exception("Transform recreated the tree.");
            var point=view.Position(ids[0])+Vector3.up*3;
            if(!view.Pick(new Ray(studyCamera.transform.position,(point-studyCamera.transform.position).normalized),out long picked)||picked!=ids[0])throw new Exception("Tree crown selection picked the wrong instance.");
            checks.Add("PASS Oak A/B/C/D single placement; original 3 LODs/shared materials retained; move/rotate/scale keep stable ID and view; crown pick matches instance.");
            session.Access=LittleCastle.World.MapEditAccess.Player;bool denied=false;
            try{session.BeginTreeBrush(catalog.packs[0].id,10,1,6,7);}catch(UnauthorizedAccessException){denied=true;}if(!denied)throw new Exception("Player was allowed to use the author grove pack.");
            long temporary=session.PlaceTree(catalog.entries[0].definition.archetypeId,new Vector2(-28,-10));session.DeleteTree(temporary);session.Access=painter.allowedTools;
            checks.Add("PASS player can place/edit an allowed individual Oak; author-only area pack is denied by the shared command layer.");
            session.BeginTreeBrush(catalog.packs[0].id,14,.8f,6,2387,"verified.grove");session.AppendTreeBrush(new Vector2(-25,22));session.CommitStroke();
            int count=session.TreeCount;if(count<=baseCount+8)throw new Exception("Grove brush did not create a useful area.");string grove=TreeDigest();session.Undo();if(session.TreeCount!=baseCount+4)throw new Exception("Grove did not undo as one command.");session.Redo();if(TreeDigest()!=grove)throw new Exception("Grove redo changed final instances.");
            session.DeleteTree(ids[1]);session.Undo();if(TreeDigest()!=grove)throw new Exception("Tree delete/undo changed the instance.");
            for(int i=0;i<16;i++){view.Tick(studyCamera.transform.position,true);yield return null;}
            checks.Add("PASS grove brush creates final instances, undo/redo and delete/undo preserve exact IDs/composition; total trees="+session.TreeCount+"; resident views="+view.ResidentCount+"; max resident="+view.maximumResident+"; spawn budget/frame="+view.creationBudget);
            float before=view.Position(ids[0]).y;session.BeginHeightStroke(5,.7f);session.AppendHeightPoint(new Vector2(-15,-6));session.CommitStroke();
            if(view.Position(ids[0]).y<before+.3f)throw new Exception("Placed tree root failed to follow height edit.");session.Undo();if(Mathf.Abs(view.Position(ids[0]).y-before)>.0001f)throw new Exception("Tree root did not follow height undo.");
            checks.Add("PASS placed tree follows authoritative height edits and undo without changing its placement record/identity.");
            string saved=painter.ExportJson();grove=TreeDigest();File.WriteAllText(Path.Combine(dir,"tree_edits_v4.json"),saved);
            painter.ResetEdits();painter.LoadJson(saved);if(TreeDigest()!=grove)throw new Exception("V4 tree save/load changed final composition.");
            for(int i=0;i<16;i++){view.Tick(studyCamera.transform.position,true);yield return null;}
            // Authoring views deliberately use DontSave; FindObjectsByType omits those objects.
            int tagged=0;var seenTrees=new HashSet<long>();
            foreach(var tag in Resources.FindObjectsOfTypeAll<MapTreeInstanceView>())
            {
                if(!tag.gameObject.scene.IsValid())continue;tagged++;
                if(!seenTrees.Add(tag.stableId))throw new Exception("Tree load left duplicate stable instance IDs.");
            }
            if(tagged!=view.ResidentCount)throw new Exception($"Tree view count mismatch: tags={tagged}, resident={view.ResidentCount}.");
            checks.Add("PASS v4 reload restores exact catalog IDs, positions, scales, yaw and stable IDs; no duplicate views.");
            studyCamera.transform.position=new Vector3(24,20,-28);studyCamera.transform.LookAt(new Vector3(-8,2,10));yield return new WaitForSecondsRealtime(.35f);yield return new WaitForEndOfFrame();Capture(Path.Combine(dir,"12_oak_gallery_world.png"));
            studyCamera.transform.position=new Vector3(18,24,-22);studyCamera.transform.LookAt(new Vector3(-20,2,18));yield return new WaitForSecondsRealtime(.35f);yield return new WaitForEndOfFrame();Capture(Path.Combine(dir,"13_oak_grove.png"));
            studyCamera.transform.position=new Vector3(-10,10,0);studyCamera.transform.LookAt(new Vector3(-23,2,18));yield return new WaitForSecondsRealtime(.35f);yield return new WaitForEndOfFrame();Capture(Path.Combine(dir,"14_oak_grove_close.png"));
            File.WriteAllLines(Path.Combine(dir,"tree_checks.txt"),checks);painter.LoadJson(original);for(int i=0;i<3;i++)yield return null;SetView(1);
        }
        IEnumerator CaptureTerrain(string dir)
        {
            var session=painter.Session;var view=painter.TerrainView;
            if(view==null)throw new Exception("Terrain presenter missing.");
            var checks=new List<string>();float baseline=session.SampleHeight(4,12);
            painter.AddStroke(new SurfacePaintStroke{id="terrain-flowers",road=false,scatter=true,scatterProfile=MeadowScatterProfile.Flowers,radius=8,points=new List<Vector2>{new Vector2(-2,11)}});
            painter.AddStroke(new SurfacePaintStroke{id="terrain-road",road=true,radius=1.5f,points=new List<Vector2>{new Vector2(2,2),new Vector2(4,12),new Vector2(7,24)}});
            var watch=System.Diagnostics.Stopwatch.StartNew();session.BeginHeightStroke(9,.7f);for(int i=0;i<5;i++)session.AppendHeightPoint(new Vector2(4,12));session.CommitStroke();watch.Stop();
            int localChunks=view.LastTerrainChunks,localGrass=view.LastGrassPatches;
            if(localChunks>=view.ChunkCount/2||session.SampleHeight(4,12)<baseline+2)throw new Exception("Raise/local terrain invalidation failed.");
            checks.Add("PASS raised hill; touched terrain chunks="+localChunks+"/"+view.ChunkCount+"; grass patches="+localGrass+"; five-stamp transaction CPU ms="+watch.Elapsed.TotalMilliseconds.ToString("F3",CultureInfo.InvariantCulture));
            session.BeginHeightStroke(6,-.7f);for(int i=0;i<4;i++)session.AppendHeightPoint(new Vector2(-10,6));session.CommitStroke();float pit=session.SampleHeight(-10,6);
            session.Undo();if(session.SampleHeight(-10,6)<=pit+1)throw new Exception("Height Undo failed.");session.Redo();if(Mathf.Abs(session.SampleHeight(-10,6)-pit)>.00001f)throw new Exception("Height Redo failed.");
            checks.Add("PASS pit, undo and redo preserve final height samples.");
            while(view.PendingColliders>0){view.FlushColliderBudget(2);yield return null;}Physics.SyncTransforms();
            if(!view.Raycast(new Ray(new Vector3(4,50,12),Vector3.down),out var hit)||Mathf.Abs(hit.point.y-session.SampleHeight(4,12))>.002f)throw new Exception("Terrain collision differs from authoritative triangles.");
            if(painter.SampleRoad01(4,12)<.9f)throw new Exception("Road coverage lost while raising terrain.");checks.Add("PASS local collision matches height surface; road coverage follows raised mesh.");
            foreach(var group in FindObjectsByType<LODGroup>())
            {
                Vector3 p=group.transform.position;var b=session.Bounds;if(p.x<b.x||p.z<b.y||p.x>b.x+b.z||p.z>b.y+b.w)continue;
                float before=p.y;session.BeginHeightStroke(5,.7f);session.AppendHeightPoint(new Vector2(p.x,p.z));session.CommitStroke();
                if(group.transform.position.y<before+.3f)throw new Exception("Existing Oak did not follow terrain.");session.Undo();if(Mathf.Abs(group.transform.position.y-before)>.0001f)throw new Exception("Oak Undo did not restore its root.");
                checks.Add("PASS existing Oak anchor follows terrain and undo restores its root.");break;
            }
            string saved=painter.ExportJson();ulong scatterDigest=painter.scatterRenderer.placementDigest;float hill=session.SampleHeight(4,12);
            File.WriteAllText(Path.Combine(dir,"terrain_edits.json"),saved);painter.ResetEdits();painter.LoadJson(saved);
            if(Mathf.Abs(session.SampleHeight(4,12)-hill)>.00001f||painter.scatterRenderer.placementDigest!=scatterDigest)throw new Exception("Height/scatter save replay mismatch.");
            checks.Add("PASS current-format save/load restores terrain and exact scatter composition.");
            studyCamera.transform.position=new Vector3(21,18,-16);studyCamera.transform.LookAt(new Vector3(-1,1,12));
            yield return new WaitForSecondsRealtime(.4f);yield return new WaitForEndOfFrame();Capture(Path.Combine(dir,"09_height_hill_pit.png"));
            studyCamera.transform.position=new Vector3(13,36,-20);studyCamera.transform.LookAt(new Vector3(-2,1,12));
            yield return new WaitForSecondsRealtime(.3f);yield return new WaitForEndOfFrame();Capture(Path.Combine(dir,"10_height_strategy.png"));
            studyCamera.transform.position=new Vector3(12,9,0);studyCamera.transform.LookAt(new Vector3(3,2,12));
            yield return new WaitForSecondsRealtime(.3f);yield return new WaitForEndOfFrame();Capture(Path.Combine(dir,"11_height_roots.png"));
            File.WriteAllLines(Path.Combine(dir,"terrain_checks.txt"),checks);painter.ResetEdits();SetView(1);
        }
        void Capture(string path)
        {
            // Render the actual Player camera, including URP grading, into a readable target.
            // This also works when the OS has occluded the preview window.
            var descriptor=new RenderTextureDescriptor(Screen.width,Screen.height,RenderTextureFormat.ARGB32,24){msaaSamples=profile.msaaSampleCount,bindMS=false};
            var target=RenderTexture.GetTemporary(descriptor);
            var resolved=RenderTexture.GetTemporary(Screen.width,Screen.height,0,RenderTextureFormat.ARGB32);
            var previousTarget=RenderTexture.active;
            try
            {
                RenderPipeline.SubmitRenderRequest(studyCamera,new RenderPipeline.StandardRequest{destination=target});
                Graphics.Blit(target,resolved);RenderTexture.active=resolved;var texture=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,target.width,target.height),0,0);texture.Apply();File.WriteAllBytes(path,texture.EncodeToPNG());Destroy(texture);
            }
            finally{RenderTexture.active=previousTarget;RenderTexture.ReleaseTemporary(target);RenderTexture.ReleaseTemporary(resolved);}
        }
        IEnumerator CaptureCoverage(string dir)
        {
            if(!baselineGrass)throw new Exception("Coverage baseline missing.");
            MeshRenderer ground=null;foreach(var renderer in UnityEngine.Object.FindObjectsByType<MeshRenderer>())if(renderer.sharedMaterial==painter.terrainMaterial){ground=renderer;break;}
            if(!ground)throw new Exception("Coverage terrain renderer missing.");
            var currentData=grass.data;var currentMaterial=grass.material;var groundMaterial=ground.sharedMaterial;int grassLayer=grass.gameObject.layer,groundLayer=ground.gameObject.layer,culling=studyCamera.cullingMask;var clear=studyCamera.clearFlags;var background=studyCamera.backgroundColor;bool fog=RenderSettings.fog;
            var additional=studyCamera.GetUniversalAdditionalCameraData();bool post=additional.renderPostProcessing;
            var white=new Material(currentMaterial);foreach(string property in new[]{"_Color","_RootColor","_TipColor","_DryColor"})white.SetColor(property,Color.white*1000);white.SetFloat("_MeadowPaletteStrength",0);white.SetFloat("_ColorVariation",0);
            var black=new Material(painter.brushMaterial);black.SetColor("_BaseColor",Color.black);
            var stats=new List<string>{"version,clumps,triangles,draws"};
            try
            {
                grass.gameObject.layer=30;ground.gameObject.layer=30;studyCamera.cullingMask=1<<30;studyCamera.clearFlags=CameraClearFlags.SolidColor;studyCamera.backgroundColor=Color.black;RenderSettings.fog=false;additional.renderPostProcessing=false;ground.sharedMaterial=black;grass.material=white;
                for(int i=0;i<2;i++)
                {
                    grass.data=i==0?baselineGrass:currentData;yield return new WaitForSecondsRealtime(.15f);yield return new WaitForEndOfFrame();string label=i==0?"before":"after";Capture(Path.Combine(dir,"coverage_"+label+".png"));stats.Add($"{label},{grass.submittedClumps},{grass.submittedTriangles},{grass.drawCalls}");
                }
                File.WriteAllLines(Path.Combine(dir,"coverage_counts.csv"),stats);
            }
            finally
            {
                grass.data=currentData;grass.material=currentMaterial;ground.sharedMaterial=groundMaterial;grass.gameObject.layer=grassLayer;ground.gameObject.layer=groundLayer;studyCamera.cullingMask=culling;studyCamera.clearFlags=clear;studyCamera.backgroundColor=background;RenderSettings.fog=fog;additional.renderPostProcessing=post;Destroy(white);Destroy(black);
            }
        }
        void OnGUI()
        {
            if(!showUI)return;float scale=Mathf.Clamp(Screen.width/1600f,.85f,1.3f);GUI.matrix=Matrix4x4.Scale(Vector3.one*scale);
            GUILayout.BeginArea(new Rect(16,16,300,Mathf.Min(900,Screen.height/scale-32)),GUI.skin.box);panelScroll=GUILayout.BeginScrollView(panelScroll);GUILayout.Label("ЗЕЛЁНЫЙ ЛУГ · трава и свет");string[] names={"1  Луг","2  Трава вблизи и взаимодействие","3  Игровая высота","4  Трава в тени"};for(int i=0;i<4;i++)if(GUILayout.Button(names[i],GUILayout.Height(28)))SetView(i);
            bool w=GUILayout.Toggle(wind,"Ветер" );if(w!=wind){wind=w;ApplyWind();}interaction.interactionEnabled=GUILayout.Toggle(interaction.interactionEnabled,"Пригибание рядом с объектом");moveProxy=GUILayout.Toggle(moveProxy,"Движение тестового объекта");grass.enabled=GUILayout.Toggle(grass.enabled,"Объёмная трава");bool soft=GUILayout.Toggle(grass.material.GetFloat("_MeadowSoftness")>.01f,"Мягкая трава вдали");grass.material.SetFloat("_MeadowSoftness",soft?originalSoftness:0);
            GUILayout.Label("Дальность травы: "+grass.maximumDistance.ToString("F0")+" м");float distance=GUILayout.HorizontalSlider(grass.maximumDistance,40,128);if(!Mathf.Approximately(distance,grass.maximumDistance)){grass.maximumDistance=distance;grass.material.SetFloat("_FadeStart",distance*.84375f);grass.material.SetFloat("_FadeEnd",distance);}
            GUILayout.Label("Дальность взаимодействия: "+interaction.activeDistance.ToString("F0")+" м");interaction.activeDistance=GUILayout.HorizontalSlider(interaction.activeDistance,12,60);grass.material.SetFloat("_InteractionDistance",interaction.activeDistance);
            if(GUILayout.Button(free?"Вернуться к ракурсу":"Свободная камера")){if(free)SetView(view);else{free=true;studyCamera.GetComponent<StrategyCameraController>().enabled=true;}}
            if(painter)
            {
                GUILayout.Space(7);GUILayout.Label("КИСТЬ МЕСТНОСТИ · ЛКМ по земле");bool brush=GUILayout.Toggle(painter.painting,"Включить рисование");if(brush!=painter.painting){if(brush)painter.SetTreeTool(0);painter.painting=brush;if(brush){free=true;studyCamera.GetComponent<StrategyCameraController>().enabled=true;}}
                GUILayout.BeginHorizontal();if(GUILayout.Button("Луг")){painter.EndStroke();painter.sculpting=false;painter.road=false;painter.erase=false;}if(GUILayout.Button("Дорожка")){painter.EndStroke();painter.sculpting=false;painter.road=true;painter.erase=false;}if(GUILayout.Button("Убрать траву")){painter.EndStroke();painter.sculpting=false;painter.road=false;painter.erase=true;}GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();if(GUILayout.Button("Поднять землю")){painter.EndStroke();painter.sculpting=true;painter.lowerTerrain=false;}if(GUILayout.Button("Опустить землю")){painter.EndStroke();painter.sculpting=true;painter.lowerTerrain=true;}GUILayout.EndHorizontal();
                if(painter.sculpting){GUILayout.Label("Рельеф: "+(painter.lowerTerrain?"опустить":"поднять")+" · пределы "+painter.Session.MinimumHeight.ToString("F1")+"…"+painter.Session.MaximumHeight.ToString("F1")+" м");GUILayout.Label("Скорость: "+painter.terrainMetresPerSecond.ToString("F1")+" м/с");painter.terrainMetresPerSecond=GUILayout.HorizontalSlider(painter.terrainMetresPerSecond,.1f,4);}
                GUILayout.Label("Режим: "+(painter.road?"дорожка":painter.erase?"удаление растительности":"трава, цветы и мелочь"));painter.populatePlants=GUILayout.Toggle(painter.populatePlants,"Добавлять цветы и мелкие предметы");GUILayout.Label("Радиус кисти: "+painter.radius.ToString("F1")+" м");painter.radius=GUILayout.HorizontalSlider(painter.radius,.5f,painter.sculpting?Mathf.Min(128,Mathf.Max(32,painter.Session.HeightSpacing*64)):128);
                GUILayout.Label("Сила: "+painter.strength.ToString("F2"));painter.strength=GUILayout.HorizontalSlider(painter.strength,0,1);
                GUILayout.Label("Мягкость края: "+painter.brushSoftness.ToString("F2"));painter.brushSoftness=GUILayout.HorizontalSlider(painter.brushSoftness,0,1);
                if(painter.populatePlants&&!painter.road&&!painter.erase){GUILayout.Label("Плотность мелочи: "+painter.density.ToString("F2"));painter.density=GUILayout.HorizontalSlider(painter.density,0,1);}
                if(painter.populatePlants&&!painter.road&&!painter.erase){GUILayout.Label("Профиль: "+(painter.currentProfile==MeadowScatterProfile.ForestFloor?"Лесная мелочь":ProfileNames[(int)painter.currentProfile]));painter.currentProfile=(MeadowScatterProfile)GUILayout.SelectionGrid((int)painter.currentProfile,ProfileNames,3);}
                if(painter.treeCatalog&&painter.treeCatalog.entries.Length>0)
                {
                    GUILayout.Space(6);GUILayout.Label("ДЕРЕВЬЯ · группы");
                    bool player=GUILayout.Toggle(painter.playerMode,"Доступ игрока");if(player!=painter.playerMode){painter.EndStroke();painter.playerMode=player;painter.Session.Access=player?painter.allowedTools&LittleCastle.World.MapEditAccess.Player:painter.allowedTools;if(player&&painter.treeTool==2)painter.SetTreeTool(1);}
                    var entries=painter.treeCatalog.entries;
                    painter.selectedTreeGroup=GUILayout.SelectionGrid(painter.selectedTreeGroup,MapTreeCatalog.GroupNames,2);
                    var groupIndices=painter.treeCatalog.GroupIndices(painter.selectedTreeGroup);
                    for(int row=0;row<(groupIndices.Count+1)/2;row++)
                    {
                        GUILayout.BeginHorizontal();for(int col=0;col<2;col++)
                        {
                            int cell=row*2+col;if(cell>=groupIndices.Count)break;int index=groupIndices[cell];var entry=entries[index];if(!painter.Session.CanPlaceTree(entry.definition.archetypeId))continue;
                            if(GUILayout.Button(new GUIContent(entry.title,entry.thumbnail),GUILayout.Width(126),GUILayout.Height(106))){painter.selectedTreeSource=index;painter.treeScale=Mathf.Clamp(1,entry.definition.minimumScale,entry.definition.maximumScale);painter.SetTreeTool(1);}
                        }
                        GUILayout.EndHorizontal();
                    }
                    GUILayout.Label("Выбрано: "+entries[painter.selectedTreeSource].title);
                    if(!painter.playerMode&&painter.treeCatalog.packs.Length>0)
                    {
                        for(int pack=0;pack<painter.treeCatalog.packs.Length;pack++)if(GUILayout.Button("Кисть: "+MapTreeCatalog.PackTitle(painter.treeCatalog.packs[pack]))){painter.selectedTreePack=pack;painter.SetTreeTool(2);}
                        if(painter.treeTool==2)
                        {
                        GUILayout.Label("Радиус рощи: "+painter.radius.ToString("F1")+" м");painter.radius=GUILayout.HorizontalSlider(painter.radius,2,32);
                        GUILayout.Label("Плотность рощи");painter.treeBrushDensity=GUILayout.HorizontalSlider(painter.treeBrushDensity,0,1);
                        GUILayout.Label("Интервал: "+painter.treeBrushSpacing.ToString("F1")+" м");painter.treeBrushSpacing=GUILayout.HorizontalSlider(painter.treeBrushSpacing,3,12);
                        }
                    }
                    if(GUILayout.Button("Выбрать дерево в сцене"))painter.SetTreeTool(3);
                    painter.treeYaw=GUILayout.HorizontalSlider(painter.treeYaw,0,359);GUILayout.Label("Поворот: "+painter.treeYaw.ToString("F0")+"°");
                    painter.treeScale=GUILayout.HorizontalSlider(painter.treeScale,entries[painter.selectedTreeSource].definition.minimumScale,entries[painter.selectedTreeSource].definition.maximumScale);GUILayout.Label("Масштаб: "+painter.treeScale.ToString("F2"));
                    if(painter.selectedTree!=0&&painter.Session.HasTree(painter.selectedTree))
                    {
                        var selected=painter.Session.GetTree(painter.selectedTree);GUILayout.Label("Экземпляр: "+selected.archetypeId);
                        GUILayout.BeginHorizontal();if(GUILayout.Button("Переместить"))painter.SetTreeTool(4);if(GUILayout.Button("Повернуть/масштаб"))painter.ApplySelectedTreeTransform();if(GUILayout.Button("Удалить"))painter.DeleteSelectedTree();GUILayout.EndHorizontal();
                    }
                    if(painter.treeTool!=0&&GUILayout.Button("Закончить работу с деревьями"))painter.SetTreeTool(0);
                    GUILayout.Label("ЛКМ по кроне — выбор · ПКМ/Esc — отмена");
                }
                GUILayout.BeginHorizontal();if(GUILayout.Button("Отменить штрих"))painter.Undo();if(GUILayout.Button("Повторить"))painter.Redo();if(GUILayout.Button("Сбросить правки"))painter.ResetEdits();GUILayout.EndHorizontal();GUILayout.BeginHorizontal();if(GUILayout.Button("Сохранить"))painter.SaveEdits();if(GUILayout.Button("Загрузить"))painter.LoadEdits();GUILayout.EndHorizontal();GUILayout.Label(painter.status);
            }
            GUILayout.Label($"Участки {grass.visiblePatches} · вызовы {grass.drawCalls}\nПучки {grass.submittedClumps} · треугольники {grass.submittedTriangles}\nИсточники пригибания {interaction.activeSources}/4");GUILayout.Label("H: скрыть панель · Esc: выход");GUILayout.EndScrollView();GUILayout.EndArea();
        }
    }
}
