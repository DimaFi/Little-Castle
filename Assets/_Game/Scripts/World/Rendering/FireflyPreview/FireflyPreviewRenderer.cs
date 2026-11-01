using System;
using System.Collections.Generic;
using System.Collections;
using LittleCastle.Research;
using LittleCastle.World;
using LittleCastle.World.Environment;
using LittleCastle.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace LittleCastle.Rendering.Fireflies
{
    /// <summary>
    /// Optional stand-alone visual-only component: reads existing world data but never
    /// changes the map, light budget, river systems, materials or scene settings.
    /// No MonoBehaviours/Update/Light/Collider per insect.
    /// </summary>
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class FireflyPreviewRenderer : MonoBehaviour
    {
        const int CellSamples = 14;
        const float CellWidth = 32f;
        const int InstanceLimit = 1023;

        static readonly int ColorId=Shader.PropertyToID("_FireflyColor");
        static readonly int DiameterId=Shader.PropertyToID("_FireflyDiameter");
        static readonly int GlowId=Shader.PropertyToID("_FireflyGlow");
        static readonly int MotionId=Shader.PropertyToID("_FireflyMotion");
        static readonly int NightId=Shader.PropertyToID("_FireflyNight");
        static readonly int TimeId=Shader.PropertyToID("_FireflyTime");
        static readonly int RangeId=Shader.PropertyToID("_FireflyRange");
        static readonly int FogId=Shader.PropertyToID("_FireflyFog");
        static readonly int FadesId=Shader.PropertyToID("_FireflyFades");
        static readonly int InstanceColorId=Shader.PropertyToID("_FireflyInstanceColor");
        static readonly int BehaviorId=Shader.PropertyToID("_FireflyBehavior");
        static readonly int LookId=Shader.PropertyToID("_FireflyLook");
        static readonly int LodId=Shader.PropertyToID("_FireflyLod");

        static readonly int CellFadeId=Shader.PropertyToID("_FireflyCellFade");
        static readonly int LodCountId=Shader.PropertyToID("_FireflyLodCount");
        sealed class ResidentCell
        {
            public readonly Matrix4x4[] matrices=new Matrix4x4[InstanceLimit];
            public readonly Vector4[] colors=new Vector4[InstanceLimit];
            public readonly MaterialPropertyBlock properties=new MaterialPropertyBlock();
            public int desired, count;
            public float createdAt;
            public IEnumerator builder;
            public bool complete;
            public Bounds bounds;
        }

        [SerializeField] FireflyPreviewProfile profile;
        [SerializeField] public FireflyPreviewBudget quality=new FireflyPreviewBudget();
        [SerializeField] Camera onlyCamera;
        [SerializeField] MeadowSurfacePainter optionalPainter;
        [SerializeField] StylizedLightingGlobals existingLighting;
        [SerializeField] LayerMask groundLayers = -1;
        [SerializeField] float rayStartY=400f;
        [SerializeField] float fallbackGroundY;
        [SerializeField] bool previewNightInEditMode=true;
        [SerializeField, Range(0f,1f)] float editModeNightAmount=1f;
        [SerializeField, Range(1,12)] int maxCellBuildsPerCamera=3;
        [SerializeField, Range(16,600)] int maxResidentCells=200;
        [SerializeField] bool allowReflectionCamera;
        [SerializeField, Range(0,128)] int reflectionPointLimit;
        [SerializeField] bool softDepthFade;
        [SerializeField, Range(0f,5f)] float depthFadeMetres=.6f;
        [SerializeField, Range(0f,3f)] float nearFadeMetres=.75f;
        [SerializeField, Range(0f,1f)] float fogMultiplier=.8f;

        readonly Dictionary<Vector2Int,ResidentCell> cells=new Dictionary<Vector2Int,ResidentCell>(200);
        readonly List<Vector2Int> toEvict=new List<Vector2Int>(200);
        readonly Matrix4x4[] nearby=new Matrix4x4[48];
        readonly Plane[] planes=new Plane[6];
        readonly RaycastHit[] groundHits=new RaycastHit[16];

        int buildFrame=-1,buildSteps,buildStarts;
        static int editorBuildFrame;
        /// <summary>The Editor adapter advances this once per update; runtime has no Editor dependency.</summary>
        public static void AdvanceEditorBuildFrame(){unchecked{editorBuildFrame++;}}
        long buildTicks;
        public int BuildStepsThisFrame=>buildSteps;
        public double BuildMillisecondsThisFrame=>buildTicks*1000d/System.Diagnostics.Stopwatch.Frequency;
        public int PendingCellCount
        {
            get{int n=0;foreach(var cell in cells.Values)if(!cell.complete)n++;return n;}
        }
        void BeginBuildFrame()
        {
            int frame=Application.isPlaying?Time.frameCount:editorBuildFrame;
            if(buildFrame==frame)return;
            buildFrame=frame;buildSteps=buildStarts=0;buildTicks=0;
        }
        bool HasBuildBudget=>buildSteps<Mathf.Clamp(quality.buildSamplesPerFrame,1,256)&&
            BuildMillisecondsThisFrame<Mathf.Clamp(quality.buildMillisecondsPerFrame,.05f,4f);
        void AdvanceCell(ResidentCell cell)
        {
            while(!cell.complete&&HasBuildBudget)
            {
                long start=System.Diagnostics.Stopwatch.GetTimestamp();
                try
                {
                    if(!cell.builder.MoveNext())
                    {cell.complete=true;cell.builder=null;cell.createdAt=Time.realtimeSinceStartup;}
                }
                finally{buildTicks+=System.Diagnostics.Stopwatch.GetTimestamp()-start;buildSteps++;}
            }
        }

        Material generatedMaterial;
        Mesh quad;
        MapEditSession activeWorldSession;
        MapEditSession explicitReadOnlySession;
        /// <summary>Presentation reads the live editor document, including unsaved rivers.</summary>
        public void ConnectReadOnlyWorld(MapEditSession value)
        {
            if(ReferenceEquals(explicitReadOnlySession,value))return;
            explicitReadOnlySession=value;
            BindSession(value);
        }
        MapEditSession previewReadOnlySession;
        SurfacePaintData previewReadOnlySource;
        bool snapshotAttempted;
        public bool AutomaticWorldReady => profile && profile.automaticWorld != null &&
            profile.automaticWorld.enabled && activeWorldSession != null;
        public string AutomaticWorldStatus => !profile || profile.automaticWorld == null ||
            !profile.automaticWorld.enabled ? "Ручное распределение" :
            activeWorldSession != null ? "Карта подключена: "+activeWorldSession.MapId :
            "Не найдены данные карты: автоматическое размещение остановлено";
        int lastSignature=int.MinValue;
        int lastCacheProfileId;
        float adaptiveScale=1f;
        float smoothedFrameSeconds=1f/60f;
        float lastAdaptiveCheck;
        int lastSampledFrame=-1;
        bool simulateEditorFps;
        float editorSimulatedFps=60f;
        public int DesiredWorldCount { get; private set; }
        public int SubmittedWorldCount { get; private set; }
        public int SubmittedNearCount { get; private set; }
        public int ResidentCellCount=>cells.Count;
        public int MissingCells { get; private set; }
        public int DrawCalls { get; private set; }
        public int BudgetRejected { get; private set; }
        public int LodRejected { get; private set; }
        public bool CollectDiagnostics { get; set; }
        bool diagnosticsCounterTested,diagnosticsCounterAvailable;
        public double LastRenderCpuMilliseconds { get; private set; }
        public long LastRenderAllocatedBytes { get; private set; }
        public FireflyPreviewProfile Profile=>profile;
        public float AdaptiveScale=>adaptiveScale;
        public float EstimatedFrameFps=>smoothedFrameSeconds>.0001f
            ?1f/smoothedFrameSeconds:0f;

        // Explicit editor demonstration only; never treats SceneView repaint as real FPS.
        public void ConfigureFpsSimulation(bool enabled,float fps)
        {
            simulateEditorFps=enabled;
            editorSimulatedFps=Mathf.Clamp(fps,5f,240f);
        }

        public void Connect(FireflyPreviewProfile value)
        {
            if(profile==value)return;
            profile=value; Invalidate();
        }

        /// <summary>Optional explicit runtime activation, never writes map/scene assets.</summary>
        public void ConfigureRuntime(FireflyPreviewProfile value)
        {
            Connect(value);
            quality=value!=null && value.runtimeBudget!=null
                ?value.runtimeBudget:new FireflyPreviewBudget();
        }

        public void Invalidate()
        {
            cells.Clear();
            lastSignature=int.MinValue;
            lastNearHabitatCheck=0f;
        }

        void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering+=Render;
            Invalidate();
            adaptiveScale=1f;
            smoothedFrameSeconds=1f/60f;
            lastAdaptiveCheck=0f;
            lastSampledFrame=-1;
        }

        void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering-=Render;
            DisposeResources();
        }
        void OnDestroy()=>DisposeResources();

        void DisposeResources()
        {
            cells.Clear();
            if(activeWorldSession!=null)activeWorldSession.Changed-=OnWorldMapChanged;
            activeWorldSession=null;
            previewReadOnlySession=null;
            previewReadOnlySource=null;
            snapshotAttempted=false;
            if(generatedMaterial)
            {
                if(Application.isPlaying)Destroy(generatedMaterial);
                else DestroyImmediate(generatedMaterial);
                generatedMaterial=null;
            }
            if(quad)
            {
                if(Application.isPlaying)Destroy(quad);
                else DestroyImmediate(quad);
                quad=null;
            }
            DesiredWorldCount=SubmittedWorldCount=SubmittedNearCount=MissingCells=DrawCalls=BudgetRejected=0;
        }

        void ResolveExistingReferences()
        {
            if(!optionalPainter)optionalPainter=FindAnyObjectByType<MeadowSurfacePainter>();
            if(!existingLighting)existingLighting=FindAnyObjectByType<StylizedLightingGlobals>();
        }

        void OnWorldMapChanged(MapRegionChange change)
        {
            // Only the affected cached cells are rebuilt, so continuous map
            // authoring does not reload the whole visible forest every brush tick.
            var a=profile?profile.automaticWorld:null;
            float margin=a!=null?Mathf.Max(a.treeInfluenceRadius,
                Mathf.Max(a.shoreSearchRadius,a.roadsideSearchRadius))+2f:2f;
            Rect dirty=new Rect(change.bounds.xMin-margin,
                change.bounds.yMin-margin,change.bounds.width+margin*2,
                change.bounds.height+margin*2);
            toEvict.Clear();
            foreach(var item in cells)
            {
                Rect cell=new Rect(item.Key.x*CellWidth,item.Key.y*CellWidth,
                    CellWidth,CellWidth);
                if(dirty.Overlaps(cell))toEvict.Add(item.Key);
            }
            foreach(var key in toEvict)cells.Remove(key);
            lastNearHabitatCheck=0f;
        }

        void BindSession(MapEditSession session)
        {
            if(ReferenceEquals(session,activeWorldSession))return;
            if(activeWorldSession!=null)activeWorldSession.Changed-=OnWorldMapChanged;
            activeWorldSession=session;
            if(activeWorldSession!=null)activeWorldSession.Changed+=OnWorldMapChanged;
            Invalidate();
        }

        MapEditSession ResolveAutomaticSession()
        {
            if(explicitReadOnlySession!=null)return explicitReadOnlySession;
            if(!optionalPainter)return null;
            if(optionalPainter.Session!=null)return optionalPainter.Session;
            if(Application.isPlaying || !optionalPainter.source)return null;
            if(previewReadOnlySource!=optionalPainter.source)
            {
                previewReadOnlySource=optionalPainter.source;
                previewReadOnlySession=null;
                snapshotAttempted=false;
            }
            if(!snapshotAttempted)
            {
                snapshotAttempted=true;
                try
                {
                    var mesh=optionalPainter.terrainMaterial
                        ?MeadowSurfacePainter.FindGround(optionalPainter.terrainMaterial):null;
                    var document=MeadowSurfacePainter.CreateDocument(
                        optionalPainter.source,mesh,optionalPainter.treeCatalog);
                    previewReadOnlySession=new MapEditSession(document,MapEditAccess.None);
                }
                catch(Exception e)
                {
                    Debug.LogWarning("Firefly auto-world preview could not read map: "+e.Message,this);
                }
            }
            return previewReadOnlySession;
        }

        void EnsureDrawing()
        {
            if(!generatedMaterial)
            {
                var shader=profile.materialForBuild
                    ?profile.materialForBuild.shader
                    :Shader.Find("Little Castle/Effects/Firefly Preview URP");
                if(shader==null || !shader.isSupported)return;
                generatedMaterial=profile.materialForBuild
                    ?new Material(profile.materialForBuild):new Material(shader);
                generatedMaterial.hideFlags=HideFlags.HideAndDontSave;
                generatedMaterial.enableInstancing=true;
                generatedMaterial.name="Firefly preview (runtime only)";
            }
            if(quad || !generatedMaterial)return;
            quad=new Mesh{name="Firefly preview quad",hideFlags=HideFlags.HideAndDontSave};
            quad.vertices=new[]{new Vector3(-.5f,-.5f,0),new Vector3(.5f,-.5f,0),
                new Vector3(.5f,.5f,0),new Vector3(-.5f,.5f,0)};
            quad.uv=new[]{Vector2.zero,Vector2.right,Vector2.one,Vector2.up};
            quad.triangles=new[]{0,1,2,0,2,3};
            quad.bounds=new Bounds(Vector3.zero,Vector3.one*4);
        }

        void Render(ScriptableRenderContext _,Camera camera)
        {
            if(!CollectDiagnostics){RenderCore(_,camera);return;}
            if(!diagnosticsCounterTested)
            {
                diagnosticsCounterTested=true;
                long counter=GC.GetAllocatedBytesForCurrentThread();
                var probe=new byte[1024];GC.KeepAlive(probe);
                diagnosticsCounterAvailable=GC.GetAllocatedBytesForCurrentThread()>counter;
            }
            long start=System.Diagnostics.Stopwatch.GetTimestamp();
            long allocated=GC.GetAllocatedBytesForCurrentThread();
            try{RenderCore(_,camera);}
            finally
            {
                LastRenderCpuMilliseconds=(System.Diagnostics.Stopwatch.GetTimestamp()-start)*1000d/
                    System.Diagnostics.Stopwatch.Frequency;
                LastRenderAllocatedBytes=diagnosticsCounterAvailable?
                    GC.GetAllocatedBytesForCurrentThread()-allocated:-1;
            }
        }

        void RenderCore(ScriptableRenderContext _,Camera camera)
        {
            if(!isActiveAndEnabled || !profile || camera==null || quality==null)return;
            if(!profile.enabled || quality.quality==FireflyPreviewQuality.Off)
            {
                if(cells.Count>0 || generatedMaterial || quad)DisposeResources();
                return;
            }
            if(!SystemInfo.supportsInstancing)return;
            if(onlyCamera && onlyCamera!=camera)return;
            bool reflection=camera.cameraType==CameraType.Reflection;
            if(reflection && (!allowReflectionCamera || reflectionPointLimit==0))return;
            if(camera.cameraType!=CameraType.SceneView && camera.cameraType!=CameraType.Game && !reflection)return;
            quality.Resolve(out int cap,out int nearCap,out float range);
            if(reflection){cap=Mathf.Min(cap,reflectionPointLimit);nearCap=0;}
            else ApplyAdaptiveBudget(camera,ref cap,ref nearCap,ref range);
            if(cap<=0 || range<=0 || profile.desiredGlobalCap==0)
            {
                if(cells.Count>0)cells.Clear();
                DesiredWorldCount=SubmittedWorldCount=SubmittedNearCount=0;
                DrawCalls=BudgetRejected=MissingCells=0;
                return;
            }
            ResolveExistingReferences();
            bool auto=profile.automaticWorld!=null && profile.automaticWorld.enabled;
            BindSession(ResolveAutomaticSession());
            if(auto && activeWorldSession==null)
            {
                DesiredWorldCount=SubmittedWorldCount=SubmittedNearCount=0;
                DrawCalls=BudgetRejected=MissingCells=0;
                return; // Fail closed, never fill unsupported scenes blindly.
            }
            EnsureDrawing();
            if(!generatedMaterial || !quad)return;
            cap=Mathf.Min(cap,profile.desiredGlobalCap);
            // Never alter shared shaders, URP assets or global game lighting.
            float night=!Application.isPlaying && previewNightInEditMode
                ? editModeNightAmount : existingLighting ? existingLighting.NightAmount : 0f;
            float visibleNight=Mathf.SmoothStep(0f,1f,
                Mathf.InverseLerp(profile.minimumNightAmount,1f,night));
            if(visibleNight<=.0001f)
            {
                SubmittedWorldCount=SubmittedNearCount=DrawCalls=0;
                return;
            }
            generatedMaterial.SetColor(ColorId,profile.coreColor);
            generatedMaterial.SetFloat(DiameterId,profile.worldDiameter);
            generatedMaterial.SetFloat(GlowId,profile.emission);
            generatedMaterial.SetVector(MotionId,new Vector4(profile.movementAmplitude,
                profile.movementSpeed,profile.windResponse,profile.flickerAmount));
            generatedMaterial.SetFloat(NightId,visibleNight);
            generatedMaterial.SetFloat(TimeId,Time.realtimeSinceStartup);
            generatedMaterial.SetFloat(RangeId,range);
            generatedMaterial.SetFloat(FogId,fogMultiplier);
            generatedMaterial.SetVector(BehaviorId,new Vector4(
                profile.motionPauseFraction,profile.motionCycleSeconds,0f,0f));
            generatedMaterial.SetVector(LookId,new Vector4(
                profile.brightCoreRadius,profile.haloStrength,profile.maxDiameterPixels,0f));
            generatedMaterial.SetVector(LodId,new Vector4(
                profile.farLodStartFraction,profile.farLodSurvivalFraction,
                profile.farBrightnessFraction,0f));
            if(softDepthFade)generatedMaterial.EnableKeyword("_LC_FIREFLY_SOFT_DEPTH");
            else generatedMaterial.DisableKeyword("_LC_FIREFLY_SOFT_DEPTH");
            generatedMaterial.SetVector(FadesId,new Vector4(nearFadeMetres,softDepthFade?depthFadeMetres:0f,
                profile.colorVariance,0f));

            int profileId=profile.GetEntityId().GetHashCode();
            int signature=ProfileSignature(profile);
            // Session.Changed updates only relevant spatial cells. Revisions here
            // would force an expensive full-cache reload on every tree brush tick.
            if(lastCacheProfileId!=profileId || lastSignature!=signature)
            {
                Invalidate();
                lastSignature=signature;lastCacheProfileId=profileId;
            }
            DrawCells(camera,cap,range,!reflection);
            SubmittedNearCount=nearCap>0 && profile.nearEnabled && !reflection
                ? DrawNearby(camera,Mathf.Min(nearCap,profile.desiredNearCount)) : 0;
        }

        // A real game frame is sampled once; in edit mode only deliberate simulated FPS
        // can modify this preview budget. This controller does not measure GPU time.
        void ApplyAdaptiveBudget(Camera camera,ref int world,ref int near,ref float range)
        {
            if(!quality.adaptiveEnabled)
            {
                adaptiveScale=1f;
                return;
            }
            bool simulated=!Application.isPlaying && simulateEditorFps;
            if(!Application.isPlaying && !simulated)
            {
                adaptiveScale=1f;
                return;
            }
            if(!simulated && camera.cameraType==CameraType.Game &&
               lastSampledFrame!=Time.frameCount)
            {
                lastSampledFrame=Time.frameCount;
                float delta=Time.unscaledDeltaTime;
                if(delta>.001f && delta<.25f)
                    smoothedFrameSeconds=Mathf.Lerp(smoothedFrameSeconds,delta,.08f);
            }
            float now=Time.realtimeSinceStartup;
            if(now-lastAdaptiveCheck>=.8f)
            {
                lastAdaptiveCheck=now;
                float fps=simulated ? editorSimulatedFps :
                    1f/Mathf.Max(.001f,smoothedFrameSeconds);
                adaptiveScale=FireflyPreviewAdaptation.NextScale(
                    adaptiveScale,fps,quality.targetFps,quality.minimumAdaptiveScale);
            }
            FireflyPreviewAdaptation.ApplyScale(adaptiveScale,ref world,ref near,ref range);
        }

        // Detect changes outside the preview window too, e.g. direct asset Inspector edits.
        static int ProfileSignature(FireflyPreviewProfile p)
        {
            unchecked
            {
                int s=p.seed*397 ^ (p.areas==null ? 0 : p.areas.Count);
                s=(s*397)^p.densityPer100SquareMetres.GetHashCode();
                s=(s*397)^p.desiredCellCap;
                s=(s*397)^p.mapBounds.GetHashCode();
                s=(s*397)^p.minimumHeight.GetHashCode();
                s=(s*397)^p.maximumHeight.GetHashCode();
                s=(s*397)^p.maximumSlopeDegrees.GetHashCode();
                s=(s*397)^p.movementAmplitude.GetHashCode();
                s=(s*397)^p.worldDiameter.GetHashCode();
                s=(s*397)^p.coreColor.GetHashCode();
                if(p.automaticWorld!=null)
                {
                    var a=p.automaticWorld;
                    s=s*397 ^ a.enabled.GetHashCode();
                    s=s*397 ^ a.meadowWeight.GetHashCode();
                    s=s*397 ^ a.forestWeight.GetHashCode();
                    s=s*397 ^ a.treeInfluenceRadius.GetHashCode();
                    s=s*397 ^ a.shoreWeight.GetHashCode();
                    s=s*397 ^ a.shoreSearchRadius.GetHashCode();
                    s=s*397 ^ a.waterSurfaceWeight.GetHashCode();
                    s=s*397 ^ a.roadsideWeight.GetHashCode();
                    s=s*397 ^ a.roadsideSearchRadius.GetHashCode();
                    s=s*397 ^ a.pavedRoadMultiplier.GetHashCode();
                    s=s*397 ^ a.enchantedForestMultiplier.GetHashCode();
                    s=s*397 ^ a.winterMultiplier.GetHashCode();
                    s=s*397 ^ a.snowMultiplier.GetHashCode();
                    s=s*397 ^ a.maximumHabitatMultiplier.GetHashCode();
                    s=s*397 ^ a.avoidReservedGround.GetHashCode();
                }
                if(p.areas!=null)for(int i=0;i<p.areas.Count;i++)
                {
                    var a=p.areas[i];
                    if(a==null){s=s*397+1;continue;}
                    s=s*397 ^ a.enabled.GetHashCode();
                    s=s*397 ^ a.center.GetHashCode();
                    s=s*397 ^ a.radii.GetHashCode();
                    s=s*397 ^ a.rotationDegrees.GetHashCode();
                    s=s*397 ^ a.falloff.GetHashCode();
                    s=s*397 ^ a.priority;
                    s=s*397 ^ a.seed;
                    s=s*397 ^ (int)a.shape;
                    s=s*397 ^ (int)a.densityMode;
                    s=s*397 ^ a.densityValue.GetHashCode();
                    s=s*397 ^ a.color.GetHashCode();
                    s=s*397 ^ a.overrideColor.GetHashCode();
                    if(a.polygon!=null)
                        for(int j=0;j<a.polygon.Count;j++)
                            s=s*397 ^ a.polygon[j].GetHashCode();
                }
                return s;
            }
        }

        void DrawCells(Camera camera,int cap,float range,bool canBuild)
        {
            Vector3 eye=camera.transform.position;
            int minX=Mathf.FloorToInt((eye.x-range)/CellWidth);
            int maxX=Mathf.FloorToInt((eye.x+range)/CellWidth);
            int minZ=Mathf.FloorToInt((eye.z-range)/CellWidth);
            int maxZ=Mathf.FloorToInt((eye.z+range)/CellWidth);
            float rangeSq=(range+CellWidth)*(range+CellWidth);
            GeometryUtility.CalculateFrustumPlanes(camera,planes);
            SubmittedWorldCount=DesiredWorldCount=DrawCalls=MissingCells=BudgetRejected=LodRejected=0;
            BeginBuildFrame();
            // Center-first rings: nearby light survives the world budget instead
            // of wasting all slots on the distant corner of a strategic view.
            int centerX=Mathf.FloorToInt(eye.x/CellWidth);
            int centerZ=Mathf.FloorToInt(eye.z/CellWidth);
            int rings=Mathf.CeilToInt(range/CellWidth)+2;
            for(int ring=0;ring<=rings;ring++)
            for(int dz=-ring;dz<=ring;dz++)
            for(int dx=-ring;dx<=ring;dx++)
            {
                if(Mathf.Max(Mathf.Abs(dx),Mathf.Abs(dz))!=ring)continue;
                int x=centerX+dx,z=centerZ+dz;
                if(x<minX||x>maxX||z<minZ||z>maxZ)continue;
                float cx=(x+.5f)*CellWidth,cz=(z+.5f)*CellWidth;
                if((new Vector2(cx-eye.x,cz-eye.z)).sqrMagnitude>rangeSq)continue;
                if(activeWorldSession!=null)
                {
                    var b=activeWorldSession.Bounds;
                    if(cx+CellWidth*.5f<b.x || cx-CellWidth*.5f>b.x+b.z ||
                       cz+CellWidth*.5f<b.y || cz-CellWidth*.5f>b.y+b.w)continue;
                }
                else if(cx+CellWidth*.5f<profile.mapBounds.x ||
                    cx-CellWidth*.5f>profile.mapBounds.x+profile.mapBounds.z ||
                    cz+CellWidth*.5f<profile.mapBounds.y ||
                    cz-CellWidth*.5f>profile.mapBounds.y+profile.mapBounds.w)continue;
                var key=new Vector2Int(x,z);
                if(!cells.TryGetValue(key,out ResidentCell cell))
                {
                    if(!canBuild||!HasBuildBudget||buildStarts>=maxCellBuildsPerCamera||cells.Count>=maxResidentCells)
                    {MissingCells++;continue;}
                    cell=new ResidentCell();cell.builder=BuildCell(cell,x,z);
                    cells.Add(key,cell);buildStarts++;
                }
                if(!cell.complete&&canBuild)AdvanceCell(cell);
                if(!cell.complete){MissingCells++;continue;}
                DesiredWorldCount+=cell.desired;
                if(cell.count==0)continue;
                if(!GeometryUtility.TestPlanesAABB(planes,cell.bounds))continue;
                float fraction=Vector2.Distance(new Vector2(cx,cz),
                    new Vector2(eye.x,eye.z))/Mathf.Max(1,range);
                float target=FireflyPreviewDistribution.LodPopulation(cell.count,fraction,
                    profile.lodStartFraction,profile.lodEndFraction,profile.lodMinimumFraction);
                int lodCount=Mathf.CeilToInt(target);
                LodRejected+=cell.count-lodCount;
                int allowed=Mathf.Min(lodCount,cap-SubmittedWorldCount);
                if(allowed<=0){BudgetRejected+=lodCount;continue;}
                float life=Mathf.Clamp01((Time.realtimeSinceStartup-cell.createdAt)/
                    Mathf.Max(.05f,profile.cellFadeSeconds));
                cell.properties.SetFloat(CellFadeId,life*life*(3-2*life));
                cell.properties.SetFloat(LodCountId,target);
                Graphics.DrawMeshInstanced(quad,0,generatedMaterial,cell.matrices,allowed,
                    cell.properties,ShadowCastingMode.Off,false,gameObject.layer,camera,LightProbeUsage.Off);
                SubmittedWorldCount+=allowed;
                BudgetRejected+=lodCount-allowed;
                DrawCalls++;
            }
            toEvict.Clear();
            foreach(var entry in cells)
                if(entry.Key.x<minX-1 || entry.Key.x>maxX+1 ||
                   entry.Key.y<minZ-1 || entry.Key.y>maxZ+1)toEvict.Add(entry.Key);
            foreach(var key in toEvict)cells.Remove(key);
        }

        IEnumerator BuildCell(ResidentCell cell,int x,int z)
        {
            float baseX=x*CellWidth,baseZ=z*CellWidth;
            float lowest=float.MaxValue,highest=float.MinValue;
            bool automatic=activeWorldSession!=null && profile.automaticWorld!=null &&
                profile.automaticWorld.enabled;
            var autoSettings=profile.automaticWorld;
            float margin=automatic?Mathf.Max(autoSettings.treeInfluenceRadius,
                autoSettings.roadsideSearchRadius)+1f:0f;
            Rect area=new Rect(baseX-margin,baseZ-margin,
                CellWidth+margin*2,CellWidth+margin*2);
            var nearbyTrees=automatic?activeWorldSession.QueryTrees(area):null;
            var nearbyRoads=automatic?activeWorldSession.QuerySurface(area):null;
            int localCap=Mathf.Min(InstanceLimit,profile.desiredCellCap);
            const float spacing=CellWidth/CellSamples;
            for(int iz=0;iz<CellSamples;iz++)for(int ix=0;ix<CellSamples;ix++)
            {
                // One bounded sample per MoveNext, including all early-out paths.
                yield return null;
                int gx=x*CellSamples+ix,gz=z*CellSamples+iz;
                float px=baseX+(ix+FireflyPreviewDistribution.Random01(gx,gz,profile.seed,1))*spacing;
                float pz=baseZ+(iz+FireflyPreviewDistribution.Random01(gx,gz,profile.seed,2))*spacing;
                if(!automatic && !profile.Includes(px,pz))continue;
                if(automatic && !WithinBounds(activeWorldSession.Bounds,px,pz))continue;
                Vector2 at=new Vector2(px,pz);
                float density;
                FireflyPreviewZone selectedArea;
                if(automatic)
                {
                    bool waterHere=activeWorldSession.QueryWater(at,out _,out _,out _);
                    float treeDistance=NearestTreeMetres(nearbyTrees,at,
                        autoSettings.treeInfluenceRadius);
                    RoadAffinity(nearbyRoads,at,autoSettings.roadsideSearchRadius,
                        out float onRoad,out float roadside);
                    // The live, already-built road mask accounts for erased paths.
                    if(optionalPainter && ReferenceEquals(optionalPainter.Session,
                        activeWorldSession))onRoad=optionalPainter.SampleRoad01(px,pz);
                    float shore=autoSettings.shoreWeight>0f
                        ?ShoreInfluence(activeWorldSession,at,waterHere,
                            autoSettings.shoreSearchRadius):0f;
                    float winter=activeWorldSession.EnvironmentThemes.Sample(
                        EnvironmentThemeIds.Winter,px,pz);
                    float magic=Mathf.Max(
                        activeWorldSession.EnvironmentThemes.Sample(
                            EnvironmentThemeIds.EnchantedForest,px,pz),
                        activeWorldSession.EnvironmentThemes.Sample(
                            EnvironmentThemeIds.ElvenForest,px,pz));
                    float snow=activeWorldSession.SampleEnvironmentSnow(px,pz);
                    if(!waterHere && autoSettings.avoidReservedGround &&
                        !activeWorldSession.VegetationAllowed(px,pz,.35f))
                        continue;
                    float habitat=FireflyPreviewHabitats.Multiplier(autoSettings,
                        treeDistance,waterHere,shore,onRoad,roadside,winter,snow,magic);
                    density=FireflyPreviewDistribution.Density(profile,at,out selectedArea,
                        profile.densityPer100SquareMetres*habitat,true);
                }
                else density=FireflyPreviewDistribution.Density(profile,at,out selectedArea);
                int areaSeed=profile.seed ^ (selectedArea!=null?selectedArea.seed:0);
                float expectation=Mathf.Clamp(density*CellWidth*CellWidth/
                    (100f*CellSamples*CellSamples),0f,8f);
                int count=Mathf.FloorToInt(expectation);
                if(FireflyPreviewDistribution.Random01(gx,gz,areaSeed,3)<expectation-count)count++;
                if(count<=0)continue;
                if(!TrySampleGround(px,pz,out float ground,out float up))continue;
                if(up<Mathf.Cos(profile.maximumSlopeDegrees*Mathf.Deg2Rad))continue;
                for(int k=0;k<count;k++)
                {
                    cell.desired++;
                    if(cell.count>=localCap)continue;
                    float ox=(FireflyPreviewDistribution.Random01(gx,gz,areaSeed,10+k*3)-.5f)*spacing*.6f;
                    float oz=(FireflyPreviewDistribution.Random01(gx,gz,areaSeed,11+k*3)-.5f)*spacing*.6f;
                    float height=Mathf.Lerp(profile.minimumHeight,profile.maximumHeight,
                        FireflyPreviewDistribution.Random01(gx,gz,areaSeed,12+k*3));
                    float wy=ground+height;
                    lowest=Mathf.Min(lowest,wy);highest=Mathf.Max(highest,wy);
                    Color areaColor=profile.coreColor;
                    bool tinted=selectedArea!=null && selectedArea.overrideColor;
                    if(tinted)areaColor=Color.Lerp(profile.coreColor,selectedArea.color,
                        FireflyPreviewDistribution.Influence(selectedArea,at));
                    cell.colors[cell.count]=new Vector4(
                        areaColor.r,areaColor.g,areaColor.b,tinted?1f:0f);
                    cell.matrices[cell.count++]=Matrix4x4.TRS(
                        new Vector3(px+ox,wy,pz+oz),Quaternion.identity,Vector3.one);
                }
            }
            // Imported from the parallel preview polish: stable Fisher-Yates ordering.
            for(int i=cell.count-1;i>0;i--)
            {
                int j=(int)(FireflyPreviewDistribution.Hash(x,z,profile.seed,i)%(uint)(i+1));
                var matrix=cell.matrices[i];cell.matrices[i]=cell.matrices[j];cell.matrices[j]=matrix;
                var tint=cell.colors[i];cell.colors[i]=cell.colors[j];cell.colors[j]=tint;
            }
            for(int i=0;i<cell.count;i++)
            {
                var tint=cell.colors[i];tint.w=(tint.w>.5f?1:0)+(i+1)/2048f;cell.colors[i]=tint;
            }
            if(cell.count>0)cell.properties.SetVectorArray(InstanceColorId,cell.colors);
            if(lowest>highest){lowest=fallbackGroundY;highest=lowest+4f;}
            float pad=Mathf.Max(3f,profile.movementAmplitude*2f+profile.worldDiameter*4f);
            cell.bounds=new Bounds(new Vector3(baseX+CellWidth*.5f,(lowest+highest)*.5f,baseZ+CellWidth*.5f),
                new Vector3(CellWidth+pad*2f,highest-lowest+pad*2f,CellWidth+pad*2f));
        }

        static bool WithinBounds(Vector4 b,float x,float z) =>
            x>=b.x && z>=b.y && x<=b.x+b.z && z<=b.y+b.w;

        static float NearestTreeMetres(List<MapTreeInstance> trees,Vector2 point,
            float radius)
        {
            float square=radius*radius;
            if(trees!=null)for(int i=0;i<trees.Count;i++)
            {
                if(trees[i]==null)continue;
                float d=(trees[i].position-point).sqrMagnitude;
                if(d<square)square=d;
            }
            return Mathf.Sqrt(square);
        }

        static float PointSegmentDistance(Vector2 p,Vector2 a,Vector2 b)
        {
            Vector2 ab=b-a;
            float t=Mathf.Clamp01(Vector2.Dot(p-a,ab)/
                Mathf.Max(.00001f,ab.sqrMagnitude));
            return Vector2.Distance(p,a+ab*t);
        }

        static void RoadAffinity(List<SurfacePaintStroke> roads,Vector2 at,
            float nearMetres,out float onRoad,out float nearRoad)
        {
            float closest=float.PositiveInfinity;
            if(roads!=null)for(int i=0;i<roads.Count;i++)
            {
                var stroke=roads[i];
                if(stroke==null || !stroke.road || stroke.erase ||
                    stroke.points==null || stroke.points.Count==0)continue;
                float d=float.PositiveInfinity;
                if(stroke.points.Count==1)
                    d=Vector2.Distance(at,stroke.points[0]);
                else for(int j=1;j<stroke.points.Count;j++)
                    d=Mathf.Min(d,PointSegmentDistance(at,
                        stroke.points[j-1],stroke.points[j]));
                d-=Mathf.Max(.05f,stroke.radius);
                if(d<closest)closest=d;
            }
            onRoad=closest<=0f?1f:0f;
            nearRoad=closest>0f && closest<nearMetres
                ?1f-Mathf.SmoothStep(0f,1f,closest/nearMetres):0f;
        }

        static float ShoreInfluence(MapEditSession session,Vector2 at,
            bool onWater,float radius)
        {
            if(onWater)return 0f;
            // Coarse, bounded offset probes. Called only as cells enter the cache.
            // Avoids scanning all authored rivers or generating any objects.
            for(int scale=1;scale<=2;scale++)
            {
                float d=radius*(scale==1?.5f:1f);
                if(session.QueryWater(at+new Vector2(d,0),out _,out _,out _) ||
                   session.QueryWater(at+new Vector2(-d,0),out _,out _,out _) ||
                   session.QueryWater(at+new Vector2(0,d),out _,out _,out _) ||
                   session.QueryWater(at+new Vector2(0,-d),out _,out _,out _))
                    return scale==1?1f:.45f;
            }
            return 0f;
        }

        bool TrySampleGround(float x,float z,out float height,out float normalUp)
        {
            var session=activeWorldSession??(optionalPainter?optionalPainter.Session:null);
            if(session!=null)
            {
                Vector4 map=session.Bounds;
                if(x<map.x||z<map.y||x>map.x+map.z||z>map.y+map.w)
                {height=0;normalUp=1;return false;}
                bool wet=session.QueryWater(new Vector2(x,z),
                    out float water,out _,out _);
                height=wet?water:session.SampleHeight(x,z);
                normalUp=wet?1f:session.SampleNormal(x,z).y;
                return true;
            }
            int hits=Physics.RaycastNonAlloc(new Vector3(x,rayStartY,z),Vector3.down,
                groundHits,Mathf.Max(1f,rayStartY-fallbackGroundY+100f),
                groundLayers,QueryTriggerInteraction.Ignore);
            float bestDistance=float.MaxValue;int best=-1;
            for(int i=0;i<hits;i++)
                if(groundHits[i].collider && groundHits[i].distance<bestDistance)
                {bestDistance=groundHits[i].distance;best=i;}
            if(best>=0)
            {
                height=groundHits[best].point.y;
                normalUp=groundHits[best].normal.y;
                return true;
            }
            height=fallbackGroundY;normalUp=1f;
            return true;
        }

        float lastNearHabitatCheck;
        Vector2 cachedNearHabitatPoint;
        bool cachedNearHabitat;

        bool HasNaturalNearHabitat(Vector3 eye)
        {
            Vector2 at=new Vector2(eye.x,eye.z);
            if(activeWorldSession==null || profile.automaticWorld==null ||
                !profile.automaticWorld.enabled)
                return FireflyPreviewDistribution.Density(profile,at,out _)>.01f;
            // Avoid QueryTrees allocations in every render: refresh at most twice
            // per second, or after meaningful movement in world space.
            if(Time.realtimeSinceStartup-lastNearHabitatCheck<.5f &&
                (cachedNearHabitatPoint-at).sqrMagnitude<4f)
                return cachedNearHabitat;
            lastNearHabitatCheck=Time.realtimeSinceStartup;
            cachedNearHabitatPoint=at;
            // In auto mode, suppress near-camera flights in inappropriate regions.
            var auto=profile.automaticWorld;
            var nearTrees=activeWorldSession.QueryTrees(new Rect(
                eye.x-auto.treeInfluenceRadius,eye.z-auto.treeInfluenceRadius,
                auto.treeInfluenceRadius*2,auto.treeInfluenceRadius*2));
            bool water=activeWorldSession.QueryWater(at,out _,out _,out _);
            float tree=NearestTreeMetres(nearTrees,at,auto.treeInfluenceRadius);
            float shore=auto.shoreWeight>0f
                ?ShoreInfluence(activeWorldSession,at,water,auto.shoreSearchRadius):0f;
            float snow=activeWorldSession.SampleEnvironmentSnow(eye.x,eye.z);
            float winter=activeWorldSession.EnvironmentThemes.Sample(
                EnvironmentThemeIds.Winter,eye.x,eye.z);
            float m=FireflyPreviewHabitats.Multiplier(auto,tree,water,shore,0f,0f,winter,snow,0f);
            float density=FireflyPreviewDistribution.Density(profile,at,
                out var selected,profile.densityPer100SquareMetres*m,true);
            cachedNearHabitat=density>.01f && (m>.3f || selected!=null);
            return cachedNearHabitat;
        }

        int DrawNearby(Camera camera,int limit)
        {
            if(limit<=0)return 0;
            Vector3 eye=camera.transform.position;
            if(!TrySampleGround(eye.x,eye.z,out float ground,out _))return 0;
            if(eye.y-ground>profile.maximumCameraHeightForNear || eye.y<ground)return 0;
            if(profile.nearMode==FireflyPreviewNearMode.NaturalZones &&
               !HasNaturalNearHabitat(eye))
                return 0;
            float radius=profile.nearRadius;
            const float spacing=2.6f;
            int fromX=Mathf.FloorToInt((eye.x-radius)/spacing),toX=Mathf.CeilToInt((eye.x+radius)/spacing);
            int fromZ=Mathf.FloorToInt((eye.z-radius)/spacing),toZ=Mathf.CeilToInt((eye.z+radius)/spacing);
            int placed=0;
            for(int z=fromZ;z<=toZ && placed<limit;z++)
            for(int x=fromX;x<=toX && placed<limit;x++)
            {
                float px=(x+FireflyPreviewDistribution.Random01(x,z,profile.seed,141))*spacing;
                float pz=(z+FireflyPreviewDistribution.Random01(x,z,profile.seed,142))*spacing;
                float dist=Vector2.Distance(new Vector2(px,pz),new Vector2(eye.x,eye.z));
                if(dist>radius || dist<1.35f ||
                   (activeWorldSession==null && !profile.Includes(px,pz)))continue;
                if(FireflyPreviewDistribution.Random01(x,z,profile.seed,143)>.45f)continue;
                if(activeWorldSession!=null)
                {
                    if(!WithinBounds(activeWorldSession.Bounds,px,pz))continue;
                    if(activeWorldSession.EnvironmentThemes.Sample(
                        EnvironmentThemeIds.Winter,px,pz)>.8f)continue;
                    if(FireflyPreviewDistribution.Density(profile,
                        new Vector2(px,pz),out _,1f,true)<=.01f)continue;
                }
                if(!TrySampleGround(px,pz,out float y,out _))continue;
                y+=Mathf.Lerp(profile.minimumHeight,profile.maximumHeight,
                    FireflyPreviewDistribution.Random01(x,z,profile.seed,144));
                nearby[placed++]=Matrix4x4.TRS(new Vector3(px,y,pz),Quaternion.identity,Vector3.one*1.12f);
            }
            if(placed>0)
            {
                Graphics.DrawMeshInstanced(quad,0,generatedMaterial,nearby,placed,
                    null,ShadowCastingMode.Off,false,gameObject.layer,camera,LightProbeUsage.Off);
                DrawCalls++;
            }
            return placed;
        }
    }
}
