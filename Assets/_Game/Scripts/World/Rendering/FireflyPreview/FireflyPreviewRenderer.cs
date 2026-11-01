using System;
using System.Collections.Generic;
using LittleCastle.Research;
using LittleCastle.World;
using LittleCastle.World.Environment;
using UnityEngine.SceneManagement;
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

        sealed class ResidentCell
        {
            public readonly Matrix4x4[] matrices=new Matrix4x4[InstanceLimit];
            public readonly Vector4[] colors=new Vector4[InstanceLimit];
            public readonly MaterialPropertyBlock properties=new MaterialPropertyBlock();
            public int desired, count;
            public Bounds bounds;
        }

        [SerializeField] FireflyPreviewProfile profile;
        [SerializeField] Shader explicitPreviewShader;
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

        Material generatedMaterial;
        Mesh quad;
        bool foundReferences;
        MapEditSession observedSession;
        float nextReferenceProbe;
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
        public FireflyPreviewProfile Profile=>profile;
        public bool HasActiveMap=>observedSession!=null;
        public string ActiveMapId=>observedSession?.MapId;
        public bool AutomaticPlacementActive=>profile && profile.automaticWorldPlacement &&
            observedSession!=null;
        public void UseShader(Shader value)
        {
            if(explicitPreviewShader==value)return;
            explicitPreviewShader=value;
            if(generatedMaterial)
            {
                if(Application.isPlaying)Destroy(generatedMaterial);
                else DestroyImmediate(generatedMaterial);
                generatedMaterial=null;
            }
        }
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

        public void Invalidate()
        {
            cells.Clear();
            lastSignature=int.MinValue;
        }

        void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering+=Render;
            SceneManager.activeSceneChanged+=OnActiveSceneChanged;
            Invalidate();
            adaptiveScale=1f;
            smoothedFrameSeconds=1f/60f;
            lastAdaptiveCheck=0f;
            lastSampledFrame=-1;
        }

        void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering-=Render;
            SceneManager.activeSceneChanged-=OnActiveSceneChanged;
            BindSession(null);
            DisposeResources();
        }
        void OnDestroy()=>DisposeResources();

        void DisposeResources()
        {
            cells.Clear();
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

        void OnActiveSceneChanged(Scene oldScene,Scene newScene)
        {
            BindSession(null);
            optionalPainter=null;
            existingLighting=null;
            foundReferences=false;
            nextReferenceProbe=0f;
            Invalidate();
        }

        void BindSession(MapEditSession next)
        {
            if(ReferenceEquals(observedSession,next))return;
            if(observedSession!=null)observedSession.Changed-=OnMapChanged;
            observedSession=next;
            if(observedSession!=null)observedSession.Changed+=OnMapChanged;
            Invalidate();
        }

        void OnMapChanged(MapRegionChange change)
        {
            if(profile==null || !profile.automaticWorldPlacement)return;
            // Include proximity queries (trees, paths and nearby riverbanks).
            float radius=Mathf.Max(profile.automaticTreeRadius,
                Mathf.Max(profile.automaticWaterRadius,profile.automaticPathRadius))+2f;
            Rect area=change.bounds;
            area.xMin-=radius;area.xMax+=radius;
            area.yMin-=radius;area.yMax+=radius;
            toEvict.Clear();
            foreach(var entry in cells)
            {
                Rect cellRect=new Rect(entry.Key.x*CellWidth,entry.Key.y*CellWidth,
                    CellWidth,CellWidth);
                if(area.Overlaps(cellRect))toEvict.Add(entry.Key);
            }
            foreach(var key in toEvict)cells.Remove(key);
        }

        void ResolveExistingReferences()
        {
            if(optionalPainter && optionalPainter.Session!=null)
            {
                BindSession(optionalPainter.Session);
                if(!existingLighting && Time.realtimeSinceStartup>=nextReferenceProbe)
                    existingLighting=FindAnyObjectByType<StylizedLightingGlobals>();
                return;
            }
            if(Time.realtimeSinceStartup<nextReferenceProbe && foundReferences)return;
            foundReferences=true;
            nextReferenceProbe=Time.realtimeSinceStartup+1.5f;
            optionalPainter=FindAnyObjectByType<MeadowSurfacePainter>();
            if(!existingLighting)existingLighting=FindAnyObjectByType<StylizedLightingGlobals>();
            BindSession(optionalPainter ? optionalPainter.Session : null);
        }

        void EnsureDrawing()
        {
            if(!generatedMaterial)
            {
                var shader=explicitPreviewShader ?
                    explicitPreviewShader : Shader.Find("Little Castle/Effects/Firefly Preview URP");
                if(shader==null || !shader.isSupported)return;
                generatedMaterial=new Material(shader) {
                    name="Firefly preview (runtime only)",hideFlags=HideFlags.HideAndDontSave,enableInstancing=true
                };
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
            // Don't fake ecology outside an actual map. Keep the opt-in dormant
            // on loading/menu scenes until the painter has an initialized session.
            if(profile.automaticWorldPlacement && observedSession==null)
            {
                if(cells.Count>0)cells.Clear();
                DesiredWorldCount=SubmittedWorldCount=SubmittedNearCount=0;
                MissingCells=DrawCalls=BudgetRejected=0;
                return;
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

            int profileId=profile.GetInstanceID();
            int signature=ProfileSignature(profile);
            if(lastCacheProfileId!=profileId || lastSignature!=signature)
            {
                Invalidate();
                lastSignature=signature;lastCacheProfileId=profileId;
            }
            DrawCells(camera,cap,range);
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
                s=(s*397)^p.automaticWorldPlacement.GetHashCode();
                s=(s*397)^p.automaticUseMapBounds.GetHashCode();
                s=(s*397)^p.automaticIncludeForests.GetHashCode();
                s=(s*397)^p.automaticIncludeWater.GetHashCode();
                s=(s*397)^p.automaticIncludePaths.GetHashCode();
                s=(s*397)^p.automaticIncludeMeadows.GetHashCode();
                s=(s*397)^p.automaticTreeRadius.GetHashCode();
                s=(s*397)^p.automaticWaterRadius.GetHashCode();
                s=(s*397)^p.automaticPathRadius.GetHashCode();
                s=(s*397)^p.automaticForestMultiplier.GetHashCode();
                s=(s*397)^p.automaticWaterMultiplier.GetHashCode();
                s=(s*397)^p.automaticPathMultiplier.GetHashCode();
                s=(s*397)^p.automaticMeadowMultiplier.GetHashCode();
                s=(s*397)^p.automaticWinterMultiplier.GetHashCode();
                s=(s*397)^p.automaticSnowMultiplier.GetHashCode();
                s=(s*397)^p.automaticExcludeReservedGround.GetHashCode();
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

        void DrawCells(Camera camera,int cap,float range)
        {
            Vector3 eye=camera.transform.position;
            int minX=Mathf.FloorToInt((eye.x-range)/CellWidth);
            int maxX=Mathf.FloorToInt((eye.x+range)/CellWidth);
            int minZ=Mathf.FloorToInt((eye.z-range)/CellWidth);
            int maxZ=Mathf.FloorToInt((eye.z+range)/CellWidth);
            float rangeSq=(range+CellWidth)*(range+CellWidth);
            GeometryUtility.CalculateFrustumPlanes(camera,planes);
            SubmittedWorldCount=DesiredWorldCount=DrawCalls=MissingCells=BudgetRejected=0;
            int built=0;
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
                var key=new Vector2Int(x,z);
                if(!cells.TryGetValue(key,out ResidentCell cell))
                {
                    if(built>=maxCellBuildsPerCamera || cells.Count>=maxResidentCells)
                    {MissingCells++;continue;}
                    cell=BuildCell(x,z);
                    cells.Add(key,cell);built++;
                }
                DesiredWorldCount+=cell.desired;
                if(cell.count==0)continue;
                if(!GeometryUtility.TestPlanesAABB(planes,cell.bounds))continue;
                int allowed=Mathf.Min(cell.count,cap-SubmittedWorldCount);
                if(allowed<=0){BudgetRejected+=cell.count;continue;}
                Graphics.DrawMeshInstanced(quad,0,generatedMaterial,cell.matrices,allowed,
                    cell.properties,ShadowCastingMode.Off,false,gameObject.layer,camera,LightProbeUsage.Off);
                SubmittedWorldCount+=allowed;
                BudgetRejected+=cell.count-allowed;
                DrawCalls++;
            }
            toEvict.Clear();
            foreach(var entry in cells)
                if(entry.Key.x<minX-1 || entry.Key.x>maxX+1 ||
                   entry.Key.y<minZ-1 || entry.Key.y>maxZ+1)toEvict.Add(entry.Key);
            foreach(var key in toEvict)cells.Remove(key);
        }

        Vector4 CurrentBounds=>
            profile.automaticWorldPlacement && profile.automaticUseMapBounds &&
            observedSession!=null ? observedSession.Bounds : profile.mapBounds;

        static bool InBounds(Vector4 b,float x,float z)=>x>=b.x && z>=b.y &&
            x<=b.x+b.z && z<=b.y+b.w;

        float AutomaticFactor(float px,float pz,MapEditSession session,
            List<MapTreeInstance> nearbyTrees)
        {
            if(session==null)return 0f;
            Vector2 at=new Vector2(px,pz);
            float water=0f;
            bool wet=false;
            if(profile.automaticIncludeWater)
            {
                wet=session.QueryWater(at,out _,out _,out _);
                if(wet)water=1f;
                else
                {
                    float radius=profile.automaticWaterRadius;
                    if(session.QueryWater(at+Vector2.right*radius,out _,out _,out _) ||
                       session.QueryWater(at-Vector2.right*radius,out _,out _,out _) ||
                       session.QueryWater(at+Vector2.up*radius,out _,out _,out _) ||
                       session.QueryWater(at-Vector2.up*radius,out _,out _,out _))
                        water=.68f;
                }
            }
            float forest=0f;
            if(profile.automaticIncludeForests && nearbyTrees!=null)
            {
                float sq=profile.automaticTreeRadius*profile.automaticTreeRadius;
                for(int i=0;i<nearbyTrees.Count;i++)
                {
                    float d=(nearbyTrees[i].position-at).sqrMagnitude;
                    if(d<sq)forest=Mathf.Max(forest,1f-d/sq);
                }
            }
            float path=0f,meadow=0f;
            if(optionalPainter)
            {
                float road=optionalPainter.SampleRoad01(px,pz);
                float radius=profile.automaticPathRadius;
                if(profile.automaticIncludePaths)
                {
                    float neighbour=Mathf.Max(
                        Mathf.Max(optionalPainter.SampleRoad01(px+radius,pz),
                            optionalPainter.SampleRoad01(px-radius,pz)),
                        Mathf.Max(optionalPainter.SampleRoad01(px,pz+radius),
                            optionalPainter.SampleRoad01(px,pz-radius)));
                    path=Mathf.Clamp01(neighbour*(1f-road)+road*.15f);
                }
                if(profile.automaticIncludeMeadows)
                    meadow=Mathf.Clamp01(optionalPainter.SampleGrass01(px,pz))*(1f-road);
            }
            float winter=session.EnvironmentThemes!=null
                ?session.EnvironmentThemes.Sample(EnvironmentThemeIds.Winter,px,pz):0f;
            float snow=session.SampleEnvironmentSnow(px,pz);
            bool blocked=profile.automaticExcludeReservedGround && !wet &&
                !session.VegetationAllowed(px,pz,0f);
            return FireflyPreviewDistribution.AutomaticFactor(profile,
                forest,water,path,meadow,winter,snow,blocked);
        }

        ResidentCell BuildCell(int x,int z)
        {
            var cell=new ResidentCell();
            float baseX=x*CellWidth,baseZ=z*CellWidth;
            bool automatic=profile.automaticWorldPlacement;
            var map=observedSession;
            Vector4 bounds=CurrentBounds;
            // Exactly one spatial tree query per built cell, never per insect/frame.
            List<MapTreeInstance> trees=automatic && map!=null &&
                profile.automaticIncludeForests ?
                map.QueryTrees(new Rect(baseX-profile.automaticTreeRadius,
                    baseZ-profile.automaticTreeRadius,
                    CellWidth+profile.automaticTreeRadius*2f,
                    CellWidth+profile.automaticTreeRadius*2f)) : null;
            float lowest=float.MaxValue,highest=float.MinValue;
            int localCap=Mathf.Min(InstanceLimit,profile.desiredCellCap);
            const float spacing=CellWidth/CellSamples;
            for(int iz=0;iz<CellSamples;iz++)for(int ix=0;ix<CellSamples;ix++)
            {
                int gx=x*CellSamples+ix,gz=z*CellSamples+iz;
                float px=baseX+(ix+FireflyPreviewDistribution.Random01(gx,gz,profile.seed,1))*spacing;
                float pz=baseZ+(iz+FireflyPreviewDistribution.Random01(gx,gz,profile.seed,2))*spacing;
                if(!InBounds(bounds,px,pz))continue;
                Vector2 at=new Vector2(px,pz);
                float baseDensity=profile.densityPer100SquareMetres;
                if(automatic)baseDensity*=AutomaticFactor(px,pz,map,trees);
                float density=FireflyPreviewDistribution.Density(
                    profile,at,out var selectedArea,bounds,baseDensity);
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
            if(cell.count>0)cell.properties.SetVectorArray(InstanceColorId,cell.colors);
            if(lowest>highest){lowest=fallbackGroundY;highest=lowest+4f;}
            float pad=Mathf.Max(3f,profile.movementAmplitude*2f+profile.worldDiameter*4f);
            cell.bounds=new Bounds(new Vector3(baseX+CellWidth*.5f,(lowest+highest)*.5f,baseZ+CellWidth*.5f),
                new Vector3(CellWidth+pad*2f,highest-lowest+pad*2f,CellWidth+pad*2f));
            return cell;
        }

        bool TrySampleGround(float x,float z,out float height,out float normalUp)
        {
            if(optionalPainter && optionalPainter.Session!=null)
            {
                var session=optionalPainter.Session;
                Vector4 map=session.Bounds;
                if(x<map.x||z<map.y||x>map.x+map.z||z>map.y+map.w)
                {height=0;normalUp=1;return false;}
                bool onWater=session.QueryWater(new Vector2(x,z),out float water,out _,out _);
                height=onWater ? water : session.SampleHeight(x,z);
                normalUp=onWater ? 1f : session.SampleNormal(x,z).y;
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

        int DrawNearby(Camera camera,int limit)
        {
            if(limit<=0)return 0;
            Vector3 eye=camera.transform.position;
            if(!TrySampleGround(eye.x,eye.z,out float ground,out _))return 0;
            if(eye.y-ground>profile.maximumCameraHeightForNear || eye.y<ground)return 0;
            if(profile.nearMode==FireflyPreviewNearMode.NaturalZones)
            {
                if(profile.automaticWorldPlacement)
                {
                    if(observedSession==null)return 0;
                    var key=new Vector2Int(Mathf.FloorToInt(eye.x/CellWidth),
                        Mathf.FloorToInt(eye.z/CellWidth));
                    if(!cells.TryGetValue(key,out var c) || c.count==0)return 0;
                }
                else if(FireflyPreviewDistribution.Density(profile,
                    new Vector2(eye.x,eye.z),out _)<=.01f)return 0;
            }
            Vector4 activeBounds=CurrentBounds;
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
                   !InBounds(activeBounds,px,pz))continue;
                if(FireflyPreviewDistribution.Random01(x,z,profile.seed,143)>.45f)continue;
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
