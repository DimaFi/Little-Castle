using System;
using System.Collections.Generic;
using LittleCastle.World;
using UnityEngine;
using UnityEngine.Rendering;

namespace LittleCastle.Rendering
{
    /// <summary>
    /// Scene presentation only. One instanced quad mesh and one material are shared
    /// by all fireflies. No per-firefly MonoBehaviour, collider, Update or Light.
    /// Editor and Game cameras are rendered separately; reflection cameras are skipped.
    /// </summary>
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class FireflyWorldRenderer : MonoBehaviour
    {
        const float CellSize = 32f;
        const int BatchLimit = 1023;
        static readonly int NowId = Shader.PropertyToID("_LC_FireflyTime");
        static readonly int SizeId = Shader.PropertyToID("_LC_FireflySize");
        static readonly int ColorId = Shader.PropertyToID("_LC_FireflyColor");
        static readonly int GlowId = Shader.PropertyToID("_LC_FireflyGlow");
        static readonly int MotionId = Shader.PropertyToID("_LC_FireflyMotion");
        static readonly int FlickerId = Shader.PropertyToID("_LC_FireflyFlicker");
        static readonly int ViewRangeId = Shader.PropertyToID("_LC_FireflyViewRange");
        static readonly int NightMinId = Shader.PropertyToID("_LC_FireflyNightMin");
        static readonly int InstanceColorId = Shader.PropertyToID("_LC_FireflyInstanceColor");

        sealed class Cell
        {
            public readonly Matrix4x4[] instances;
            public readonly Vector4[] colors;
            public Cell(int capacity)
            {
                instances=new Matrix4x4[Mathf.Max(1,capacity)];
                colors=new Vector4[Mathf.Max(1,capacity)];
            }
            public readonly MaterialPropertyBlock properties = new MaterialPropertyBlock();
            public int count, desired;
            public Matrix4x4 groundPatch;
            public Bounds bounds;
        }

        [SerializeField] public FireflyRuntimeQuality quality = new FireflyRuntimeQuality();
        [SerializeField] bool useMapDefaultPreset = true;
        [SerializeField] bool autoAdjustToFrameTime;
        [SerializeField, Range(30, 144)] int targetFramesPerSecond = 60;
        [SerializeField, Range(.3f, 1f)] float minimumAdaptiveScale = .55f;
        [SerializeField] bool allowReflectionCameras;
        [SerializeField, Range(0, 32)] int reflectionCap;
        [SerializeField] bool softDepthFade = true;
        [SerializeField] bool enableCheapGroundGlow = true;
        [SerializeField, Range(0, 24)] int maxGroundPatches = 12;
        [SerializeField, Range(0, 1)] float cheapGroundGlowIntensity = .13f;
        [SerializeField, Range(1, 8)] float cheapGroundPatchDiameter = 3.2f;
        [SerializeField, Range(1, 24)] int maxCellsBuiltPerFrame = 4;
        [SerializeField] StylizedLightingGlobals lighting;
        [SerializeField] Material overrideMaterial;

        readonly Dictionary<Vector2Int,Cell> cells=new Dictionary<Vector2Int,Cell>(128);
        readonly List<Vector2Int> eviction=new List<Vector2Int>(128);
        readonly Plane[] planes=new Plane[6];
        readonly Matrix4x4[] nearby=new Matrix4x4[48];
        readonly Vector4[] nearbyColors=new Vector4[48];
        readonly MaterialPropertyBlock nearProperties=new MaterialPropertyBlock();
        readonly Matrix4x4[] groundPatches=new Matrix4x4[24];
        MapEditSession session;
        bool ownsMaterial;
        Material material,groundMaterial;
        Mesh mesh;
        long revision=-1;
        int seed,lastRefreshFrame=-1,desiredWorld,shownWorld,shownNear,missingCells;
        float lastRange;
        float adaptiveScale=1f, smoothedFrameSeconds=1f/60f;
        float nextAdaptiveSample;
        int lastAdaptiveFrame=-1;
        public float CurrentAdaptiveScale=>adaptiveScale;
        public int DesiredWorldCount=>desiredWorld;
        public int VisibleWorldCount=>shownWorld;
        public int VisibleNearCount=>shownNear;
        public int ResidentCellCount=>cells.Count;
        public int WaitingCellCount=>missingCells;
        public int GlobalCapHitCount { get; private set; }

        public void Connect(MapEditSession map)
        {
            if(session==map)return;
            if(session!=null)session.Changed-=MapChanged;
            session=map;
            if(session!=null && useMapDefaultPreset && quality!=null)
                quality.preset=session.FirefliesForRendering.defaultQuality;
            if(session!=null&&isActiveAndEnabled)session.Changed+=MapChanged;
            cells.Clear();
            revision=-1;
        }

        void MapChanged(MapRegionChange change)
        {
            if((change.kind & (MapChangeKind.Height|MapChangeKind.Objects|MapChangeKind.Rivers|MapChangeKind.Biomes|MapChangeKind.Fireflies))==0)return;
            if((change.kind&MapChangeKind.Fireflies)!=0){cells.Clear();revision=-1;return;}
            eviction.Clear();
            foreach(var pair in cells)
            {
                var region=new Rect(pair.Key.x*CellSize,pair.Key.y*CellSize,CellSize,CellSize);
                if(region.Overlaps(change.bounds))eviction.Add(pair.Key);
            }
            foreach(var key in eviction)cells.Remove(key);
        }

        void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering+=Render;
            if(session!=null){session.Changed-=MapChanged;session.Changed+=MapChanged;}
        }
        void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering-=Render;
            if(session!=null)session.Changed-=MapChanged;
            Cleanup();
        }
        void OnDestroy()=>Cleanup();

        void Cleanup()
        {
            cells.Clear();
            if(material&&ownsMaterial){if(Application.isPlaying)Destroy(material);else DestroyImmediate(material);}material=null;ownsMaterial=false;
            if(groundMaterial){if(Application.isPlaying)Destroy(groundMaterial);else DestroyImmediate(groundMaterial);groundMaterial=null;}
            if(mesh){if(Application.isPlaying)Destroy(mesh);else DestroyImmediate(mesh);mesh=null;}
            desiredWorld=shownWorld=shownNear=missingCells=0;
        }

        void EnsureResources()
        {
            if(overrideMaterial!=null){material=overrideMaterial;ownsMaterial=false;}
            if(!material)
            {
                Shader shader=Shader.Find("Little Castle/Effects/LC Fireflies");
                if(shader==null || !shader.isSupported)return;
                material=new Material(shader){name="LC Fireflies (generated, runtime only)",hideFlags=HideFlags.HideAndDontSave,enableInstancing=true};ownsMaterial=true;
            }
            if(enableCheapGroundGlow && !groundMaterial)
            {
                Shader glow=Shader.Find("Little Castle/Effects/LC Night Light Pool");
                if(glow!=null&&glow.isSupported)
                {
                    groundMaterial=new Material(glow){name="Firefly faint ground pools (runtime only)",
                        enableInstancing=true,hideFlags=HideFlags.HideAndDontSave};
                    groundMaterial.SetColor("_Color",new Color(.72f,.83f,.34f,1));
                    groundMaterial.SetFloat("_InnerRadius",.04f);
                    groundMaterial.SetFloat("_EdgeSoftness",.9f);
                    groundMaterial.SetFloat("_FlickerStrength",.01f);
                }
            }
            if(mesh!=null)return;
            mesh=new Mesh{name="Firefly unit quad (shared)",hideFlags=HideFlags.HideAndDontSave};
            mesh.vertices=new[]{new Vector3(-.5f,-.5f,0),new Vector3(.5f,-.5f,0),
                new Vector3(.5f,.5f,0),new Vector3(-.5f,.5f,0)};
            mesh.uv=new[]{Vector2.zero,Vector2.right,Vector2.one,Vector2.up};
            mesh.triangles=new[]{0,2,1,0,3,2};
            mesh.bounds=new Bounds(Vector3.zero,Vector3.one*10);
        }

        void UpdateAutoQuality()
        {
            if(!autoAdjustToFrameTime || !Application.isPlaying)
            {adaptiveScale=1f;return;}
            if(lastAdaptiveFrame==Time.frameCount)return;
            lastAdaptiveFrame=Time.frameCount;
            float sample=Mathf.Clamp(Time.unscaledDeltaTime,.001f,.15f);
            smoothedFrameSeconds=Mathf.Lerp(smoothedFrameSeconds,sample,.045f);
            if(Time.unscaledTime<nextAdaptiveSample)return;
            nextAdaptiveSample=Time.unscaledTime+1.25f;
            float target=1f/Mathf.Max(30,targetFramesPerSecond);
            // Hysteresis makes temporary hitches less visually distracting.
            if(smoothedFrameSeconds>target*1.17f)
                adaptiveScale=Mathf.Max(minimumAdaptiveScale,adaptiveScale-.075f);
            else if(smoothedFrameSeconds<target*.88f)
                adaptiveScale=Mathf.Min(1f,adaptiveScale+.025f);
        }

        void Render(ScriptableRenderContext _, Camera cam)
        {
            if(!isActiveAndEnabled||session==null||cam==null||quality==null)return;
            if(cam.cameraType!=CameraType.Game && cam.cameraType!=CameraType.SceneView &&
               !(allowReflectionCameras&&cam.cameraType==CameraType.Reflection))return;
            if(cam.cameraType==CameraType.Reflection && reflectionCap<=0)return;

            UpdateAutoQuality();
            quality.ResolveAdaptive(out int cap,out int nearCap,out float range);
            cap=Mathf.RoundToInt(cap*adaptiveScale);
            nearCap=Mathf.RoundToInt(nearCap*adaptiveScale);
            range*=Mathf.Lerp(.75f,1f,adaptiveScale);
            if(cap==0 || range<=0){Cleanup();return;}
            FireflyMapData data=session.FirefliesForRendering;
            if(data==null||!data.enabled){Cleanup();return;}
            nearCap=Mathf.Min(nearCap,data.desiredNearCount);
            EnsureResources();
            if(!mesh||!material)return;
            material.enableInstancing=true;
            material.SetColor(ColorId,data.color);
            material.SetFloat(SizeId,data.worldSize);
            material.SetFloat(GlowId,data.glow);
            material.SetVector(MotionId,new Vector4(data.motionAmplitude,data.motionSpeed,data.windInfluence,data.colorVariation));
            material.SetFloat(FlickerId,data.flickerStrength);
            material.SetFloat(NightMinId,data.minimumNightAmount);
            material.SetFloat(NowId,Now());
            material.SetFloat(ViewRangeId,range);
            if(softDepthFade)material.EnableKeyword("_LC_FIREFLY_SOFT_DEPTH");
            else material.DisableKeyword("_LC_FIREFLY_SOFT_DEPTH");

            int budget=Mathf.Min(cap,data.globalDesiredCap);
            if(cam.cameraType==CameraType.Reflection)budget=Mathf.Min(budget,reflectionCap);
            if(budget<=0)return;
            Vector3 eye=cam.transform.position;
            if(revision!=session.FireflyRevision || seed!=data.seed || Mathf.Abs(lastRange-range)>1f)
            {cells.Clear();revision=session.FireflyRevision;seed=data.seed;lastRange=range;}
            int minX=Mathf.FloorToInt((eye.x-range)/CellSize),maxX=Mathf.FloorToInt((eye.x+range)/CellSize);
            int minZ=Mathf.FloorToInt((eye.z-range)/CellSize),maxZ=Mathf.FloorToInt((eye.z+range)/CellSize);
            int built=0;missingCells=0;desiredWorld=0;shownWorld=0;
            int groundCount=0;
            int allowedGroundPatches=enableCheapGroundGlow && quality.preset!=FireflyQuality.Low
                ?Mathf.Min(groundPatches.Length,maxGroundPatches):0;
            GeometryUtility.CalculateFrustumPlanes(cam,planes);

            // Deterministic X/Z iteration and a bounded number of rebuilds per render.
            for(int z=minZ;z<=maxZ;z++)
            for(int x=minX;x<=maxX;x++)
            {
                var key=new Vector2Int(x,z);
                float px=(x+.5f)*CellSize,pz=(z+.5f)*CellSize;
                if((new Vector2(px-eye.x,pz-eye.z)).sqrMagnitude>(range+CellSize)* (range+CellSize))continue;
                if(!cells.TryGetValue(key,out var cell))
                {
                    if(built>=maxCellsBuiltPerFrame){missingCells++;continue;}
                    cell=BuildCell(x,z,data);cells.Add(key,cell);built++;
                }
                desiredWorld+=cell.desired;
                if(shownWorld>=budget||cell.count==0)continue;
                if(!GeometryUtility.TestPlanesAABB(planes,cell.bounds))continue;
                int count=Mathf.Min(cell.count,budget-shownWorld);
                Graphics.DrawMeshInstanced(mesh,0,material,cell.instances,count,cell.properties,
                    ShadowCastingMode.Off,false,gameObject.layer,cam,LightProbeUsage.Off);
                shownWorld+=count;
                if(groundCount<allowedGroundPatches && cell.groundPatch!=Matrix4x4.zero)
                    groundPatches[groundCount++]=cell.groundPatch;
            }
            GlobalCapHitCount=Mathf.Max(0,desiredWorld-shownWorld);
            if(groundMaterial && groundCount>0)
            {
                groundMaterial.SetFloat("_Intensity",cheapGroundGlowIntensity);
                Graphics.DrawMeshInstanced(mesh,0,groundMaterial,groundPatches,groundCount,null,
                    ShadowCastingMode.Off,false,gameObject.layer,cam,LightProbeUsage.Off);
            }
            // Evict only outside the resident envelope; no global map materialization.
            eviction.Clear();
            foreach(var pair in cells)
                if(pair.Key.x<minX-1||pair.Key.x>maxX+1||
                    pair.Key.y<minZ-1||pair.Key.y>maxZ+1)eviction.Add(pair.Key);
            foreach(var key in eviction)cells.Remove(key);
            shownNear=0;
            if(data.allowNear && nearCap>0 && cam.cameraType!=CameraType.Reflection)
            {
                int count=BuildNear(cam,data,nearCap);
                if(count>0)
                {
                    nearProperties.SetVectorArray(InstanceColorId,nearbyColors);
                    Graphics.DrawMeshInstanced(mesh,0,material,nearby,count,nearProperties,
                        ShadowCastingMode.Off,false,gameObject.layer,cam,LightProbeUsage.Off);
                    shownNear=count;
                }
            }
        }

        Cell BuildCell(int cx,int cz,FireflyMapData data)
        {
            int capacity=Mathf.Clamp(Mathf.Min(data.cellDesiredCap,data.perAreaDesiredCap),0,BatchLimit);
            var cell=new Cell(capacity);
            float baseX=cx*CellSize,baseZ=cz*CellSize;
            float minY=float.MaxValue,maxY=float.MinValue;
            
            for(int z=0;z<13;z++)for(int x=0;x<13;x++)
            {
                int gx=cx*13+x,gz=cz*13+z;
                float wx=baseX+(x+FireflyDistribution.Random01(gx,gz,data.seed,1))*CellSize/13;
                float wz=baseZ+(z+FireflyDistribution.Random01(gx,gz,data.seed,2))*CellSize/13;
                var b=session.Bounds;
                if(wx<b.x||wx>b.x+b.z||wz<b.y||wz>b.y+b.w)continue;
                float areaDensity=FireflyDistribution.Density(data,new Vector2(wx,wz));
                if(areaDensity<=0)continue;
                areaDensity*=session.FireflyBiomeWeight(data,wx,wz);
                int localSeed=FireflyDistribution.SeedAt(data,new Vector2(wx,wz));
                float expected=Mathf.Clamp(areaDensity*CellSize*CellSize/(100f*169f),0,8);
                int copies=Mathf.FloorToInt(expected);
                if(FireflyDistribution.Random01(gx,gz,localSeed,3)<expected-copies)copies++;
                if(!session.FireflySurfaceAllowed(wx,wz))continue;
                float terrain=session.FireflySurfaceHeight(wx,wz);
                Vector3 normal=session.QueryWater(new Vector2(wx,wz),out _,out _,out _)
                    ?Vector3.up:session.SampleNormal(wx,wz);
                if(normal.y<Mathf.Cos(data.maximumSlopeDegrees*Mathf.Deg2Rad))
                    continue;
                for(int k=0;k<copies;k++)
                {
                    cell.desired++;
                    if(cell.count>=capacity)continue;
                    float h=Mathf.Lerp(data.minimumHeight,data.maximumHeight,FireflyDistribution.Random01(gx,gz,localSeed,k+10));
                    float y=terrain+h;
                    minY=Mathf.Min(minY,y);maxY=Mathf.Max(maxY,y);
                    Color tint=FireflyDistribution.ColorAt(data,new Vector2(wx,wz));
                    cell.colors[cell.count]=new Vector4(tint.r,tint.g,tint.b,tint.a);
                    cell.instances[cell.count++]=Matrix4x4.TRS(new Vector3(wx,y,wz),
                        Quaternion.identity,Vector3.one);
                }
            }
            if(minY>maxY){minY=session.SampleHeight(baseX+16,baseZ+16);maxY=minY+4;}
            if(cell.count>0)
            {
                Vector3 p=cell.instances[0].GetColumn(3);
                Vector2 at=new Vector2(p.x,p.z);
                if(session.FireflySurfaceAllowed(p.x,p.z))
                {
                    Vector3 normal=session.QueryWater(at,out _,out _,out _)?Vector3.up:
                        session.SampleNormal(p.x,p.z);
                    float y=session.FireflySurfaceHeight(p.x,p.z)+.065f;
                    cell.groundPatch=Matrix4x4.TRS(new Vector3(p.x,y,p.z),
                        Quaternion.FromToRotation(Vector3.back,normal),
                        new Vector3(cheapGroundPatchDiameter,cheapGroundPatchDiameter,1));
                }
            }
            if(cell.count>0)cell.properties.SetVectorArray(InstanceColorId,cell.colors);
            cell.bounds=new Bounds(new Vector3(baseX+16,(minY+maxY)*.5f,baseZ+16),
                new Vector3(CellSize+8,maxY-minY+10,CellSize+8));
            return cell;
        }

        int BuildNear(Camera cam,FireflyMapData data,int limit)
        {
            Vector3 eye=cam.transform.position;
            float ground=session.SampleHeight(eye.x,eye.z);
            float altitude=eye.y-ground;
            if(altitude<data.nearCameraMinHeight||altitude>data.nearCameraMaxHeight)return 0;
            if(data.nearMode==FireflyNearMode.NaturalAreas &&
               FireflyDistribution.Density(data,new Vector2(eye.x,eye.z))<=.01f)return 0;
            int count=0;
            float radius=Mathf.Clamp(data.nearRadius,1,50);
            int baseX=Mathf.FloorToInt(eye.x/4),baseZ=Mathf.FloorToInt(eye.z/4);
            for(int dz=-3;dz<=3&&count<limit;dz++)
            for(int dx=-3;dx<=3&&count<limit;dx++)
            {
                int gx=baseX+dx,gz=baseZ+dz;
                float wx=(gx+FireflyDistribution.Random01(gx,gz,seed,83))*4;
                float wz=(gz+FireflyDistribution.Random01(gx,gz,seed,84))*4;
                float dist=Vector2.Distance(new Vector2(wx,wz),new Vector2(eye.x,eye.z));
                if(dist>radius||dist<1.2f)continue;
                if(FireflyDistribution.Random01(gx,gz,seed,85)>.28f)continue;
                if(!session.FireflySurfaceAllowed(wx,wz))continue;
                float y=session.FireflySurfaceHeight(wx,wz)+
                    Mathf.Lerp(.7f,2.5f,FireflyDistribution.Random01(gx,gz,seed,86));
                Color tint=FireflyDistribution.ColorAt(data,new Vector2(wx,wz));
                nearbyColors[count]=new Vector4(tint.r,tint.g,tint.b,tint.a);
                nearby[count++]=Matrix4x4.TRS(new Vector3(wx,y,wz),Quaternion.identity,Vector3.one);
            }
            return count;
        }

        /// <summary>Copies up to two currently resident cluster targets without allocations.</summary>
        public int CopyClusterTargets(Vector3 viewer,Vector3[] output)
        {
            if(output==null||output.Length==0)return 0;
            int count=0;float first=-1,second=-1;
            foreach(var pair in cells)
            {
                var cell=pair.Value;
                if(cell.desired<=0||cell.count<=0)continue;
                float sq=(viewer-cell.bounds.center).sqrMagnitude;
                if(sq>140*140)continue;
                float score=cell.desired/(1f+sq*.001f);
                if(score>first)
                {
                    if(output.Length>1&&count>0){output[1]=output[0];second=first;}
                    output[0]=cell.bounds.center;first=score;count=Mathf.Min(output.Length,Mathf.Max(1,count+1));
                }
                else if(output.Length>1&&score>second&&pair.Value.bounds.center!=output[0])
                {output[1]=cell.bounds.center;second=score;count=2;}
            }
            return count;
        }

        void OnValidate()
        {
            maxCellsBuiltPerFrame=Mathf.Clamp(maxCellsBuiltPerFrame,1,24);
            maxGroundPatches=Mathf.Clamp(maxGroundPatches,0,24);
            cheapGroundPatchDiameter=Mathf.Clamp(cheapGroundPatchDiameter,1,8);
            minimumAdaptiveScale=Mathf.Clamp(minimumAdaptiveScale,.3f,1f);
            if(cells!=null)cells.Clear();
        }

        static float Now()
        {
#if UNITY_EDITOR
            if(!Application.isPlaying)return (float)UnityEditor.EditorApplication.timeSinceStartup;
#endif
            return Time.time;
        }
    }
}
