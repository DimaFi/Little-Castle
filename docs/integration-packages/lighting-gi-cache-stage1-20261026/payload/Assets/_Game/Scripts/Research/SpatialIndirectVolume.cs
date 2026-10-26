using System;
using System.Collections.Generic;
using LittleCastle.Rendering;
using UnityEngine;

namespace LittleCastle.Research
{
    /// <summary>Research-only bounded diffuse probe field. No map commands, authority or asset writes.</summary>
    [DefaultExecutionOrder(80)]
    public sealed class SpatialIndirectVolume : MonoBehaviour
    {
        [Serializable] public sealed class ProxyObject
        {
            public string id;
            public Bounds bounds;
            public Color albedo=Color.gray;
            public Renderer visual;
            public bool active=true;
        }
        public StylizedLightingGlobals globals;
        public ProxyObject[] proxies=Array.Empty<ProxyObject>();
        public Vector3 origin=new Vector3(-24,.05f,-24);
        public Vector3Int gridSize=new Vector3Int(49,9,49);
        public float spacing=1,rayDistance=6,workBudgetMs=2;
        public int raysPerProbe=128,maximumProbesPerFrame=256;
        public int maximumCachedProbesPerFrame=8192;
        public bool applyIndirect=true,visibilityGuard=true,bounce=true;
        public bool UseTransport {get;private set;}
        public SpatialIndirectTransport Transport {get;private set;}
        public SpatialIndirectProfile Profile {get;private set;}
        public long TotalSolverAllocatedBytes {get;private set;}
        public bool AllocationCounterSupported {get;private set;}
        public long AllocationCalibrationBytes {get;private set;}
        public bool AllocationRecorderSupported=>allocationCounter!=null&&allocationCounter.Supported;
        public long TotalSolverAllocationCalls {get;private set;}
        public long SetupAllocationCalls {get;private set;}
        public long LastCommandAllocationCalls {get;private set;}
        SpatialIndirectAllocationCounter allocationCounter;
        public double LastCommitMs {get;private set;}
        public long LastCommitAllocatedBytes {get;private set;}
        public int SurfaceEntries=>UseTransport?Transport.SurfaceEntries:Snapshot.SkyTransportEntries;
        public int EffectiveProbeLimit=>UseTransport?maximumCachedProbesPerFrame:maximumProbesPerFrame;
        public SpatialIndirectCache Cache {get;private set;}
        public SpatialIndirectTrace Snapshot {get;private set;}
        public int LastProcessed {get;private set;}
        public int LastUploadedBytes {get;private set;}
        public int LastInvalidated {get;private set;}
        public int TotalSolved {get;private set;}
        public double LastSolveMs {get;private set;}
        public double TotalSolveMs {get;private set;}
        public double LastUploadMs {get;private set;}
        public double TotalUploadMs {get;private set;}
        public long TotalUploadBytes {get;private set;}
        public int BufferBytes=>Cache==null?0:Cache.ProbeCount*64;
        public bool Ready=>Cache!=null&&Cache.PendingCount==0;
        SpatialIndirectTrace.Lighting lighting;
        ComputeBuffer buffer;
        readonly List<int> changed=new List<int>(256);
        readonly Vector4[] mins=new Vector4[8],maxs=new Vector4[8];
        readonly System.Diagnostics.Stopwatch timer=new System.Diagnostics.Stopwatch();
        static SpatialIndirectVolume owner;
        const string Keyword="_LC_SPATIAL_INDIRECT";
        void Awake()
        {
            if(owner||Shader.IsKeywordEnabled(Keyword))throw new InvalidOperationException("Only one research indirect field may own the view.");
            if(SystemInfo.graphicsShaderLevel<45)throw new NotSupportedException("Spatial indirect experiment requires shader model 4.5; baseline remains available.");
            owner=this;
            var args=Environment.GetCommandLineArgs();UseTransport=Array.IndexOf(args,"-giReference")<0;
            Profile=new SpatialIndirectProfile{enabled=Array.IndexOf(args,"-giProfile")>=0};
            long calibration=GC.GetAllocatedBytesForCurrentThread();GC.KeepAlive(new byte[4096]);AllocationCalibrationBytes=GC.GetAllocatedBytesForCurrentThread()-calibration;AllocationCounterSupported=AllocationCalibrationBytes>=4096;
            allocationCounter=new SpatialIndirectAllocationCounter(Array.IndexOf(args,"-giAlloc")>=0);allocationCounter.Begin();
            if(UseTransport)changed.Capacity=maximumCachedProbesPerFrame;
            Cache=new SpatialIndirectCache(origin,gridSize,spacing);
            buffer=new ComputeBuffer(Cache.ProbeCount,64,ComputeBufferType.Structured);buffer.SetData(Cache.Data);
            Shader.SetGlobalBuffer("_LC_IndirectProbes",buffer);
            Shader.SetGlobalVector("_LC_IndirectOrigin",origin);
            Shader.SetGlobalVector("_LC_IndirectSize",new Vector4(gridSize.x,gridSize.y,gridSize.z,0));
            SnapshotGeometry();if(UseTransport)Transport=new SpatialIndirectTransport(Cache,Snapshot);
            SetupAllocationCalls=allocationCounter.End();
            RefreshLighting();Cache.InvalidateAll();Shader.EnableKeyword(Keyword);BindParameters();
        }
        public void RefreshLighting()
        {
            allocationCounter?.Begin();
            long start=SpatialIndirectProfile.Stamp(),allocated=GC.GetAllocatedBytesForCurrentThread();
            globals.ApplyGlobals();
            lighting=new SpatialIndirectTrace.Lighting(RGB(globals.CurrentAmbientSkyColor),RGB(globals.CurrentAmbientEquatorColor),RGB(globals.CurrentAmbientGroundColor),globals.SunDirection,RGB(globals.CurrentSunColor),globals.MoonDirection,RGB(globals.CurrentMoonColor));
            Transport?.ResetInvalidationMetrics();Transport?.SetLighting(lighting,rayDistance);
            if(Cache!=null){Cache.InvalidateAll();LastInvalidated=Cache.ProbeCount;}
            LastCommitMs=SpatialIndirectProfile.Ms(SpatialIndirectProfile.Stamp()-start);LastCommitAllocatedBytes=GC.GetAllocatedBytesForCurrentThread()-allocated;
            LastCommandAllocationCalls=allocationCounter?.End()??-1;
        }
        void SnapshotGeometry()
        {
            var list=new List<SpatialIndirectTrace.Proxy>(proxies.Length);
            for(int i=0;i<proxies.Length;i++){var p=proxies[i];if(p.active)list.Add(new SpatialIndirectTrace.Proxy(p.bounds,RGB(StylizedLightingGlobals.ToWorkingColor(p.albedo)),i));}
            Snapshot=new SpatialIndirectTrace(list.ToArray(),raysPerProbe,!UseTransport){Profile=Profile};
            Transport?.Bind(Snapshot);
            Array.Clear(mins,0,mins.Length);Array.Clear(maxs,0,maxs.Length);
            for(int i=0;i<Snapshot.ProxyCount;i++){mins[i]=Snapshot.GetProxy(i).bounds.min;maxs[i]=Snapshot.GetProxy(i).bounds.max;}
            Shader.SetGlobalVectorArray("_LC_IndirectBoxMin",mins);Shader.SetGlobalVectorArray("_LC_IndirectBoxMax",maxs);Shader.SetGlobalInt("_LC_IndirectBoxCount",Snapshot.ProxyCount);
        }
        /// <summary>Commit/undo/load adapter boundary. Preview must never enqueue the expensive cache.</summary>
        public void CommitProxy(int index,bool active,Bounds bounds,Color albedo,bool preview=false)
        {
            if(preview){LastInvalidated=0;return;}
            allocationCounter.Begin();
            long start=SpatialIndirectProfile.Stamp(),allocated=GC.GetAllocatedBytesForCurrentThread();
            var p=proxies[index];var old=p.bounds;bool geometryChanged=p.active!=active||!SpatialIndirectTrace.SameGeometryBounds(old,bounds);p.active=active;p.bounds=bounds;p.albedo=albedo;
            if(p.visual)p.visual.enabled=active;
            SnapshotGeometry();int before=Cache.PendingCount;
            if(geometryChanged)Transport?.InvalidateGeometry(old,bounds,index);
            // A changed occluder can affect a probe ray AND the sun visibility at its
            // bounce hit, including the quantised surface-cache footprint.
            float halo=SpatialIndirectTrace.InfluenceRadius(rayDistance);
            Cache.Invalidate(old,halo);
            if(!SpatialIndirectTrace.SameGeometryBounds(old,bounds))Cache.Invalidate(bounds,halo);
            LastInvalidated=Cache.PendingCount-before;
            LastCommitMs=SpatialIndirectProfile.Ms(SpatialIndirectProfile.Stamp()-start);LastCommitAllocatedBytes=GC.GetAllocatedBytesForCurrentThread()-allocated;
            LastCommandAllocationCalls=allocationCounter.End();
        }
        void BindParameters()=>Shader.SetGlobalVector("_LC_IndirectParameters",new Vector4(applyIndirect?1:0,spacing,.08f,visibilityGuard?1:0));
        void Update()
        {
            BindParameters();LastProcessed=LastUploadedBytes=0;LastSolveMs=LastUploadMs=0;changed.Clear();
            if(Cache.PendingCount==0)return;
            var snapshot=Snapshot;var light=lighting;long allocated=GC.GetAllocatedBytesForCurrentThread();allocationCounter.Begin();timer.Restart();
            while(LastProcessed<EffectiveProbeLimit&&Cache.TryBegin(out var work))
            {
                var position=Cache.Position(work.index);
                var result=UseTransport?Transport.Solve(work.index,position,bounce):snapshot.Solve(position,light,rayDistance,bounce);
                if(Cache.Commit(work,result)){changed.Add(work.index);LastProcessed++;TotalSolved++;}
                if(timer.Elapsed.TotalMilliseconds>=workBudgetMs)break;
            }
            LastSolveMs=timer.Elapsed.TotalMilliseconds;TotalSolveMs+=LastSolveMs;timer.Restart();
            TotalSolverAllocatedBytes+=GC.GetAllocatedBytesForCurrentThread()-allocated;
            if(AllocationRecorderSupported)TotalSolverAllocationCalls+=allocationCounter.End();
            changed.Sort();
            for(int at=0;at<changed.Count;)
            {
                int start=changed[at],count=1;at++;
                while(at<changed.Count&&changed[at]==start+count){count++;at++;}
                buffer.SetData(Cache.Data,start,start,count);LastUploadedBytes+=count*64;
            }
            LastUploadMs=timer.Elapsed.TotalMilliseconds;TotalUploadMs+=LastUploadMs;TotalUploadBytes+=LastUploadedBytes;
        }
        /// <summary>Diagnostic reference for the same eight-probe visibility-weighted interpolation.</summary>
        public Vector3 Sample(Vector3 position,Vector3 normal,out bool fallback)
        {
            var q=(position+normal*.08f-origin)/spacing;var sz=Cache.Size;
            fallback=q.x<0||q.y<0||q.z<0||q.x>sz.x-1||q.y>sz.y-1||q.z>sz.z-1;
            if(fallback)return lighting.Environment(normal);
            int ix=Mathf.Min(Mathf.FloorToInt(q.x),sz.x-2),iy=Mathf.Min(Mathf.FloorToInt(q.y),sz.y-2),iz=Mathf.Min(Mathf.FloorToInt(q.z),sz.z-2);
            var f=q-new Vector3(ix,iy,iz);Vector3 value=default;float weight=0;bool unknown=false;
            for(int z=0;z<2;z++)for(int y=0;y<2;y++)for(int x=0;x<2;x++)
            {
                float w=(x==1?f.x:1-f.x)*(y==1?f.y:1-f.y)*(z==1?f.z:1-f.z);if(w<1e-5f)continue;
                int index=Cache.Index(ix+x,iy+y,iz+z);var p=Cache.Data[index];unknown|=p.c0.w==0;if(p.c0.w<.5f)continue;
                Vector3 delta=Cache.Position(index)-(position+normal*.08f);float distance=delta.magnitude;
                if(visibilityGuard&&distance>.001f&&Snapshot.Trace(position+normal*.08f,delta/distance,distance*.9999f,out _))continue;
                var v=p.c0+p.cx*normal.x+p.cy*normal.y+p.cz*normal.z;value+=new Vector3(v.x,v.y,v.z)*w;weight+=w;
            }
            if(weight<1e-5f){fallback=unknown;return unknown?lighting.Environment(normal):Vector3.zero;}
            value/=weight;return new Vector3(Mathf.Max(0,value.x),Mathf.Max(0,value.y),Mathf.Max(0,value.z));
        }
        static Vector3 RGB(Color c)=>new Vector3(c.r,c.g,c.b);
        void OnDestroy()
        {
            if(owner==this){Shader.SetGlobalVector("_LC_IndirectParameters",Vector4.zero);Shader.SetGlobalBuffer("_LC_IndirectProbes",(ComputeBuffer)null);Shader.DisableKeyword(Keyword);owner=null;}
            buffer?.Release();buffer=null;
        }
    }
}
