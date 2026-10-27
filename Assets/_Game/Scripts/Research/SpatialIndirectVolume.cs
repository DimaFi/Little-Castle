using System;
using System.Collections.Generic;
using LittleCastle.Rendering;
using LittleCastle.World;
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
        public bool UsePriority {get;private set;}
        public bool UseTemporal {get;private set;}
        public bool continuousLighting;
        public WorldTimeSystem presentationClock;
        public float blendSeconds=.2f;
        public SpatialIndirectPublication Publication {get;private set;}
        public double LastPublishMs {get;private set;}
        public double TotalPublishMs {get;private set;}
        public double LastCommitUploadMs {get;private set;}
        public long LastCommitUploadBytes {get;private set;}
        public double RequestedClockMinutes {get;private set;}
        public double PublishedClockMinutes {get;private set;}
        public long DisplayChangeSerial {get;private set;}
        public int PublicationCount=>Publication?.PublicationCount??0;
        public float DisplayBlend=>Publication?.Weight(Time.realtimeSinceStartupAsDouble)??1;
        public Camera priorityCamera;
        public double LastPriorityMs {get;private set;}
        public double TotalPriorityMs {get;private set;}
        public int PriorityRefreshes {get;private set;}
        public long TotalPrioritySolved {get;private set;}
        public long TotalPriorityChanges {get;private set;}
        readonly Plane[] priorityPlanes=new Plane[6];
        Matrix4x4 priorityView,priorityProjection;
        bool priorityPrepared;
        int priorityRefreshAfterFrame;
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
        public int BufferBytes=>Cache==null?0:Cache.ProbeCount*64*(UseTemporal?2:1);
        public bool Ready=>Cache!=null&&Cache.PendingCount==0&&(!UseTemporal||Publication.Settled(Time.realtimeSinceStartupAsDouble));
        SpatialIndirectTrace.Lighting lighting;
        ComputeBuffer buffer,previousBuffer;
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
            UsePriority=Array.IndexOf(args,"-giFifo")<0;if(!priorityCamera)priorityCamera=Camera.main;
            UseTemporal=Array.IndexOf(args,"-giImmediate")<0;continuousLighting=Array.IndexOf(args,"-giContinuous")>=0;
            Profile=new SpatialIndirectProfile{enabled=Array.IndexOf(args,"-giProfile")>=0};
            long calibration=GC.GetAllocatedBytesForCurrentThread();GC.KeepAlive(new byte[4096]);AllocationCalibrationBytes=GC.GetAllocatedBytesForCurrentThread()-calibration;AllocationCounterSupported=AllocationCalibrationBytes>=4096;
            allocationCounter=new SpatialIndirectAllocationCounter(Array.IndexOf(args,"-giAlloc")>=0);allocationCounter.Begin();
            if(UseTransport)changed.Capacity=maximumCachedProbesPerFrame;
            Cache=new SpatialIndirectCache(origin,gridSize,spacing);
            if(UseTemporal)Publication=new SpatialIndirectPublication(Cache);
            buffer=new ComputeBuffer(Cache.ProbeCount,64,ComputeBufferType.Structured);buffer.SetData(Cache.Data);
            previousBuffer=UseTemporal?new ComputeBuffer(Cache.ProbeCount,64,ComputeBufferType.Structured):buffer;
            if(UseTemporal)previousBuffer.SetData(Publication.Previous);
            Shader.SetGlobalBuffer("_LC_IndirectProbes",buffer);
            Shader.SetGlobalBuffer("_LC_IndirectPrevious",previousBuffer);
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
            Publication?.BeginLighting();
            if(presentationClock&&presentationClock.Clock!=null)RequestedClockMinutes=presentationClock.Clock.AbsoluteGameMinutes;
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
            LastCommitUploadMs=0;LastCommitUploadBytes=0;
            if(UseTemporal)
            {
                if(geometryChanged)
                {
                    int forbidden=Publication.BeginGeometry(old,bounds,halo);
                    if(forbidden>0)
                    {
                        // Clear BOTH histories before any render. Whole uploads avoid
                        // hundreds of small row calls for these large finite halos.
                        long uploadStart=SpatialIndirectProfile.Stamp();buffer.SetData(Publication.Published);previousBuffer.SetData(Publication.Previous);
                        LastCommitUploadMs=SpatialIndirectProfile.Ms(SpatialIndirectProfile.Stamp()-uploadStart);LastCommitUploadBytes=BufferBytes;
                        TotalUploadMs+=LastCommitUploadMs;TotalUploadBytes+=LastCommitUploadBytes;DisplayChangeSerial++;
                    }
                }
                else Publication.BeginLighting();
            }
            LastCommitMs=SpatialIndirectProfile.Ms(SpatialIndirectProfile.Stamp()-start);LastCommitAllocatedBytes=GC.GetAllocatedBytesForCurrentThread()-allocated;LastCommandAllocationCalls=allocationCounter.End();
        }
        void BindParameters()
        {
            Shader.SetGlobalVector("_LC_IndirectParameters",new Vector4(applyIndirect?1:0,spacing,.08f,visibilityGuard?1:0));
            Shader.SetGlobalVector("_LC_IndirectTemporal",new Vector4(UseTemporal?1:0,DisplayBlend,0,0));
        }
        void PublishIfReady()
        {
            if(!UseTemporal){if(Cache.PendingCount==0)PublishedClockMinutes=RequestedClockMinutes;return;}
            long start=SpatialIndirectProfile.Stamp();
            if(!Publication.TryPublish(Publication.RequestRevision,Cache.PendingCount==0,Cache.Data,Time.realtimeSinceStartupAsDouble,blendSeconds))return;
            previousBuffer.SetData(Publication.Previous);buffer.SetData(Publication.Published);
            LastPublishMs=SpatialIndirectProfile.Ms(SpatialIndirectProfile.Stamp()-start);TotalPublishMs+=LastPublishMs;
            LastUploadMs+=LastPublishMs;TotalUploadMs+=LastPublishMs;LastUploadedBytes+=BufferBytes;TotalUploadBytes+=BufferBytes;
            PublishedClockMinutes=RequestedClockMinutes;DisplayChangeSerial++;BindParameters();
        }
        void Update()
        {
            BindParameters();LastProcessed=LastUploadedBytes=0;LastSolveMs=LastUploadMs=LastPriorityMs=LastPublishMs=0;changed.Clear();
            // Coalesce live clock while busy; this presentation never advances time.
            if(continuousLighting&&presentationClock&&presentationClock.Clock!=null&&Ready&&presentationClock.Clock.AbsoluteGameMinutes!=RequestedClockMinutes)RefreshLighting();
            if(Cache.PendingCount==0){PublishIfReady();return;}
            var snapshot=Snapshot;var light=lighting;long allocated=GC.GetAllocatedBytesForCurrentThread();allocationCounter.Begin();timer.Restart();
            // Priority classification is inside the SAME cooperative CPU budget.
            if(priorityCamera&&(!priorityPrepared||(Time.frameCount>=priorityRefreshAfterFrame&&(!priorityView.Equals(priorityCamera.worldToCameraMatrix)||!priorityProjection.Equals(priorityCamera.projectionMatrix)))))
            {
                long start=SpatialIndirectProfile.Stamp();GeometryUtility.CalculateFrustumPlanes(priorityCamera,priorityPlanes);
                Cache.SetPriority(priorityPlanes,spacing*1.8f+.08f);priorityView=priorityCamera.worldToCameraMatrix;priorityProjection=priorityCamera.projectionMatrix;priorityPrepared=true;
                LastPriorityMs=SpatialIndirectProfile.Ms(SpatialIndirectProfile.Stamp()-start);TotalPriorityMs+=LastPriorityMs;PriorityRefreshes++;priorityRefreshAfterFrame=Time.frameCount+4;
                // At least three solver frames between moving-camera rebuilds.
                // Only queue preference may lag; geometry/revision correctness does not.
            }
            bool budgetRemaining=timer.Elapsed.TotalMilliseconds<workBudgetMs;
            while(budgetRemaining&&LastProcessed<EffectiveProbeLimit&&Cache.TryBegin(out var work,UsePriority))
            {
                var position=Cache.Position(work.index);
                var result=UseTransport?Transport.Solve(work.index,position,bounce):snapshot.Solve(position,light,rayDistance,bounce);
                var old=Cache.Data[work.index];
                if(Cache.Commit(work,result))
                {
                    changed.Add(work.index);LastProcessed++;TotalSolved++;
                    if(Cache.IsPriority(work.index)){TotalPrioritySolved++;if(result.c0.w>.5f&&(!old.c0.Equals(result.c0)||!old.cx.Equals(result.cx)||!old.cy.Equals(result.cy)||!old.cz.Equals(result.cz)))TotalPriorityChanges++;}
                }
                if(timer.Elapsed.TotalMilliseconds>=workBudgetMs)break;
            }
            LastSolveMs=timer.Elapsed.TotalMilliseconds;TotalSolveMs+=LastSolveMs;timer.Restart();
            TotalSolverAllocatedBytes+=GC.GetAllocatedBytesForCurrentThread()-allocated;
            if(AllocationRecorderSupported)TotalSolverAllocationCalls+=allocationCounter.End();
            if(UseTemporal){PublishIfReady();return;}
            changed.Sort();
            for(int at=0;at<changed.Count;)
            {
                int start=changed[at],count=1;at++;
                while(at<changed.Count&&changed[at]==start+count){count++;at++;}
                buffer.SetData(Cache.Data,start,start,count);LastUploadedBytes+=count*64;
            }
            LastUploadMs=timer.Elapsed.TotalMilliseconds;TotalUploadMs+=LastUploadMs;TotalUploadBytes+=LastUploadedBytes;
            if(changed.Count>0)DisplayChangeSerial++;PublishedClockMinutes=Cache.PendingCount==0?RequestedClockMinutes:PublishedClockMinutes;
        }
        /// <summary>Diagnostic reference for the same eight-probe visibility-weighted interpolation.</summary>
        public Vector3 Sample(Vector3 position,Vector3 normal,out bool fallback)
            =>SampleField(position,normal,out fallback,false);
        public Vector3 SampleDisplayed(Vector3 position,Vector3 normal,out bool fallback)
            =>SampleField(position,normal,out fallback,UseTemporal);
        Vector3 SampleField(Vector3 position,Vector3 normal,out bool fallback,bool displayed)
        {
            var q=(position+normal*.08f-origin)/spacing;var sz=Cache.Size;
            fallback=q.x<0||q.y<0||q.z<0||q.x>sz.x-1||q.y>sz.y-1||q.z>sz.z-1;
            if(fallback)return lighting.Environment(normal);
            int ix=Mathf.Min(Mathf.FloorToInt(q.x),sz.x-2),iy=Mathf.Min(Mathf.FloorToInt(q.y),sz.y-2),iz=Mathf.Min(Mathf.FloorToInt(q.z),sz.z-2);
            var f=q-new Vector3(ix,iy,iz);Vector3 value=default;float weight=0;bool unknown=false;
            for(int z=0;z<2;z++)for(int y=0;y<2;y++)for(int x=0;x<2;x++)
            {
                float w=(x==1?f.x:1-f.x)*(y==1?f.y:1-f.y)*(z==1?f.z:1-f.z);if(w<1e-5f)continue;
                int index=Cache.Index(ix+x,iy+y,iz+z);var p=displayed?Publication.Published[index]:Cache.Data[index];unknown|=p.c0.w==0;if(p.c0.w<.5f)continue;
                Vector3 delta=Cache.Position(index)-(position+normal*.08f);float distance=delta.magnitude;
                if(visibilityGuard&&distance>.001f&&Snapshot.Trace(position+normal*.08f,delta/distance,distance*.9999f,out _))continue;
                if(displayed)value+=Publication.Value(index,normal,lighting.Environment(normal),Time.realtimeSinceStartupAsDouble)*w;
                else{var v=p.c0+p.cx*normal.x+p.cy*normal.y+p.cz*normal.z;value+=new Vector3(v.x,v.y,v.z)*w;}weight+=w;
            }
            if(weight<1e-5f){fallback=unknown;return unknown?lighting.Environment(normal):Vector3.zero;}
            value/=weight;return new Vector3(Mathf.Max(0,value.x),Mathf.Max(0,value.y),Mathf.Max(0,value.z));
        }
        static Vector3 RGB(Color c)=>new Vector3(c.r,c.g,c.b);
        void OnDestroy()
        {
            if(owner==this){Shader.SetGlobalVector("_LC_IndirectParameters",Vector4.zero);Shader.SetGlobalVector("_LC_IndirectTemporal",Vector4.zero);Shader.SetGlobalBuffer("_LC_IndirectProbes",(ComputeBuffer)null);Shader.SetGlobalBuffer("_LC_IndirectPrevious",(ComputeBuffer)null);Shader.DisableKeyword(Keyword);owner=null;}
            if(previousBuffer!=buffer)previousBuffer?.Release();previousBuffer=null;buffer?.Release();buffer=null;
        }
    }
}
