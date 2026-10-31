using System;
using System.Collections.Generic;
using LittleCastle.Research;
using LittleCastle.World;
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

        sealed class ResidentCell
        {
            public readonly Matrix4x4[] matrices=new Matrix4x4[InstanceLimit];
            public readonly Vector4[] colors=new Vector4[InstanceLimit];
            public readonly MaterialPropertyBlock properties=new MaterialPropertyBlock();
            public int desired, count;
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

        Material generatedMaterial;
        Mesh quad;
        bool foundReferences;
        int lastSignature=int.MinValue;
        int lastCacheProfileId;
        float cachedRange;
        public int DesiredWorldCount { get; private set; }
        public int SubmittedWorldCount { get; private set; }
        public int SubmittedNearCount { get; private set; }
        public int ResidentCellCount=>cells.Count;
        public int MissingCells { get; private set; }
        public int DrawCalls { get; private set; }
        public int BudgetRejected { get; private set; }
        public FireflyPreviewProfile Profile=>profile;

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
            Invalidate();
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
            if(foundReferences)return;
            foundReferences=true;
            if(!optionalPainter)optionalPainter=FindAnyObjectByType<MeadowSurfacePainter>();
            if(!existingLighting)existingLighting=FindAnyObjectByType<StylizedLightingGlobals>();
        }

        void EnsureDrawing()
        {
            if(!generatedMaterial)
            {
                var shader=Shader.Find("Little Castle/Effects/Firefly Preview URP");
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
            if(cap<=0 || range<=0 || profile.desiredGlobalCap==0)
            {
                if(cells.Count>0)cells.Clear();
                DesiredWorldCount=SubmittedWorldCount=SubmittedNearCount=0;
                DrawCalls=BudgetRejected=MissingCells=0;
                return;
            }
            ResolveExistingReferences();
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
            if(softDepthFade)generatedMaterial.EnableKeyword("_LC_FIREFLY_SOFT_DEPTH");
            else generatedMaterial.DisableKeyword("_LC_FIREFLY_SOFT_DEPTH");
            generatedMaterial.SetVector(FadesId,new Vector4(nearFadeMetres,softDepthFade?depthFadeMetres:0f,
                profile.colorVariance,0f));

            int profileId=profile.GetInstanceID();
            int signature=ProfileSignature(profile);
            if(lastCacheProfileId!=profileId || lastSignature!=signature ||
               Mathf.Abs(cachedRange-range)>8f)
            {
                Invalidate();
                lastSignature=signature;lastCacheProfileId=profileId;cachedRange=range;
            }
            DrawCells(camera,cap,range);
            SubmittedNearCount=nearCap>0 && profile.nearEnabled && !reflection
                ? DrawNearby(camera,Mathf.Min(nearCap,profile.desiredNearCount)) : 0;
        }

        // Captures runtime profile changes; Undo and validation from Editor call Invalidate explicitly.
        static int ProfileSignature(FireflyPreviewProfile p)
        {
            unchecked
            {
                int s=p.seed*397 ^ p.areas.Count;
                s=(s*397)^p.densityPer100SquareMetres.GetHashCode();
                s=(s*397)^p.desiredCellCap;
                s=(s*397)^p.mapBounds.GetHashCode();
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

        ResidentCell BuildCell(int x,int z)
        {
            var cell=new ResidentCell();
            float baseX=x*CellWidth,baseZ=z*CellWidth;
            float lowest=float.MaxValue,highest=float.MinValue;
            int localCap=Mathf.Min(InstanceLimit,profile.desiredCellCap);
            const float spacing=CellWidth/CellSamples;
            for(int iz=0;iz<CellSamples;iz++)for(int ix=0;ix<CellSamples;ix++)
            {
                int gx=x*CellSamples+ix,gz=z*CellSamples+iz;
                float px=baseX+(ix+FireflyPreviewDistribution.Random01(gx,gz,profile.seed,1))*spacing;
                float pz=baseZ+(iz+FireflyPreviewDistribution.Random01(gx,gz,profile.seed,2))*spacing;
                if(!profile.Includes(px,pz))continue;
                Vector2 at=new Vector2(px,pz);
                float density=FireflyPreviewDistribution.Density(profile,at,out var selectedArea);
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
                height=session.QueryWater(new Vector2(x,z),out float water,out _,out _)
                    ?water:session.SampleHeight(x,z);
                normalUp=session.SampleNormal(x,z).y;
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
            if(profile.nearMode==FireflyPreviewNearMode.NaturalZones &&
               FireflyPreviewDistribution.Density(profile,new Vector2(eye.x,eye.z),out _)<=.01f)
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
                if(dist>radius || dist<1.35f || !profile.Includes(px,pz))continue;
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
