using LittleCastle.Research;
using NUnit.Framework;
using UnityEngine;

namespace LittleCastle.Tests
{
    public sealed class SpatialIndirectTransportTests
    {
        static SpatialIndirectTrace.Lighting Light(Vector3 sunDirection,float intensity=1)=>new SpatialIndirectTrace.Lighting(new Vector3(.18f,.24f,.31f),new Vector3(.12f,.14f,.17f),new Vector3(.09f,.07f,.05f),sunDirection,new Vector3(.8f,.7f,.5f)*intensity,new Vector3(-.3f,.4f,.2f),new Vector3(.03f,.05f,.1f));
        static float Error(SpatialIndirectTrace.Probe a,SpatialIndirectTrace.Probe b)=>Vector4.Distance(a.c0,b.c0)+Vector4.Distance(a.cx,b.cx)+Vector4.Distance(a.cy,b.cy)+Vector4.Distance(a.cz,b.cz);
        static SpatialIndirectTrace.Proxy Ground()=>new SpatialIndirectTrace.Proxy(new Bounds(new Vector3(0,-.5f,0),new Vector3(40,1,40)),new Vector3(.16f,.25f,.1f),0);
        static void Compare(SpatialIndirectTransport transport,SpatialIndirectTrace reference,SpatialIndirectCache grid,SpatialIndirectTrace.Lighting light,float range)
        {
            transport.SetLighting(light,range);
            for(int i=0;i<grid.ProbeCount;i++)Assert.That(Error(transport.Solve(i,grid.Position(i)),reference.Solve(grid.Position(i),light,range)),Is.LessThan(.00001f),"Probe "+i);
        }
        [Test] public void CandidateMaskPreservesNearestHitWithThinBoxesParallelRaysAndNegativeCoordinates()
        {
            var trace=new SpatialIndirectTrace(new[]{Ground(),new SpatialIndirectTrace.Proxy(new Bounds(new Vector3(-2,2,1),new Vector3(.12f,3,4)),Vector3.one,3)},128);
            foreach(var p in new[]{new Vector3(-4,1,-1),new Vector3(-1,2,0),new Vector3(10,7,10),new Vector3(-2,2,1)})
            for(int ray=0;ray<trace.RayCount;ray++)
            {
                var d=trace.Direction(ray);bool a=trace.Trace(p,d,4,out var hit),b=trace.TraceCandidates(p,d,trace.InverseDirection(ray),4,trace.CandidateMask(p,4),out var fast);
                Assert.That(b,Is.EqualTo(a));if(a){Assert.That(fast.index,Is.EqualTo(hit.index));Assert.That(fast.distance,Is.EqualTo(hit.distance));Assert.That(fast.normal,Is.EqualTo(hit.normal));}
            }
        }
        [Test] public void ReusedTransportMatchesReferenceForAlbedoSkyAndMovingSunWithoutPrimaryRetracing()
        {
            var panel=new Bounds(new Vector3(-1,1.5f,1),new Vector3(.12f,3,3));
            var first=new SpatialIndirectTrace(new[]{Ground(),new SpatialIndirectTrace.Proxy(panel,new Vector3(.7f,.1f,.02f),5)},64);
            var grid=new SpatialIndirectCache(new Vector3(-4,.1f,-4),new Vector3Int(9,5,9),1);var cached=new SpatialIndirectTransport(grid,first);var light=Light(new Vector3(.3f,.8f,-.2f));
            Compare(cached,first,grid,light,3);long builds=cached.GeometryBuilds,reweights=cached.DirectReweights;int sky=cached.SurfaceEntries;
            var grey=new SpatialIndirectTrace(new[]{Ground(),new SpatialIndirectTrace.Proxy(panel,Vector3.one*.4f,5)},64);cached.Bind(grey);
            Compare(cached,grey,grid,light,3);Assert.That(cached.GeometryBuilds,Is.EqualTo(builds));Assert.That(cached.DirectReweights,Is.EqualTo(reweights));Assert.That(cached.SurfaceEntries,Is.EqualTo(sky));
            Compare(cached,grey,grid,Light(new Vector3(-.5f,.3f,.2f),0),3);
            Compare(cached,grey,grid,Light(new Vector3(-.5f,.3f,.2f),.7f),3);
            Assert.That(cached.GeometryBuilds,Is.EqualTo(builds));Assert.That(cached.SurfaceEntries,Is.EqualTo(sky));
        }
        [Test] public void LocalRoofRemovalAndUndoMatchFullRebuildWhileStableIdsSurviveCompaction()
        {
            var roof=new Bounds(new Vector3(-2,2.6f,-1),new Vector3(2,.12f,2));var wall=new SpatialIndirectTrace.Proxy(new Bounds(new Vector3(-.8f,1.5f,-1),new Vector3(.12f,3,3)),Vector3.one*.7f,5);
            var before=new SpatialIndirectTrace(new[]{Ground(),new SpatialIndirectTrace.Proxy(roof,Vector3.one*.4f,3),wall},64);
            var after=new SpatialIndirectTrace(new[]{Ground(),wall},64);var grid=new SpatialIndirectCache(new Vector3(-10,.1f,-10),new Vector3Int(21,5,21),1);var cached=new SpatialIndirectTransport(grid,before);var light=Light(new Vector3(.3f,.8f,-.2f));
            Compare(cached,before,grid,light,3);long builds=cached.GeometryBuilds;int sky=cached.SurfaceEntries;
            cached.Bind(after);cached.InvalidateGeometry(roof,roof,3);
            Assert.That(cached.SurfaceEntries,Is.GreaterThan(0).And.LessThan(sky));
            Compare(cached,after,grid,light,3);Assert.That(cached.GeometryBuilds-builds,Is.GreaterThan(0).And.LessThan(grid.ProbeCount));
            cached.Bind(before);cached.InvalidateGeometry(roof,roof,3);Compare(cached,before,grid,light,3);
        }
        [Test] public void ChangedRangeRebuildsTransportRatherThanKeepingOutOfRangeOcclusion()
        {
            var trace=new SpatialIndirectTrace(new[]{Ground(),new SpatialIndirectTrace.Proxy(new Bounds(new Vector3(0,3,0),new Vector3(6,.12f,6)),Vector3.zero,4)},64);
            var grid=new SpatialIndirectCache(new Vector3(-2,.1f,-2),new Vector3Int(5,4,5),1);var cached=new SpatialIndirectTransport(grid,trace);var light=Light(Vector3.up);
            Compare(cached,trace,grid,light,4);long builds=cached.GeometryBuilds;Compare(cached,trace,grid,light,1);
            Assert.That(cached.GeometryBuilds-builds,Is.EqualTo(grid.ProbeCount));
        }
        [Test] public void WarmInjectionAndDirectReweightDoNotAllocateManagedMemory()
        {
            var first=new SpatialIndirectTrace(new[]{Ground()},64);
            var second=new SpatialIndirectTrace(new[]{new SpatialIndirectTrace.Proxy(Ground().bounds,Vector3.one*.6f,0)},64);
            var grid=new SpatialIndirectCache(new Vector3(-2,.1f,-2),new Vector3Int(5,4,5),1);var cached=new SpatialIndirectTransport(grid,first);
            var day=Light(Vector3.up);var sunset=Light(new Vector3(.7f,.2f,.1f));Compare(cached,first,grid,day,3);
            TestDelegate allocating=()=>System.GC.KeepAlive(new byte[4096]);
            Assert.That(allocating,new UnityEngine.TestTools.Constraints.AllocatingGCMemoryConstraint(),"Positive control must detect an actual allocation.");
            TestDelegate hot=()=>{for(int i=0;i<grid.ProbeCount;i++){cached.Bind((i&1)==0?first:second);cached.SetLighting((i&1)==0?day:sunset,3);cached.Solve(i,grid.Position(i));}};
            hot();Assert.That(hot,new NUnit.Framework.Constraints.NotConstraint(new UnityEngine.TestTools.Constraints.AllocatingGCMemoryConstraint()));
        }
        [Test] public void MicroscopicCommittedMoveInvalidatesProbeOnOldBoxBoundary()
        {
            var old=new Bounds(new Vector3(-.5f,1.5f,0),new Vector3(1,3,3));var moved=old;moved.center+=Vector3.right*.000001f;
            Assert.That(old==moved,Is.True,"Control: tolerant Bounds operator regards this movement as equal.");
            Assert.That(SpatialIndirectTrace.SameGeometryBounds(old,moved),Is.False);
            var before=new SpatialIndirectTrace(new[]{new SpatialIndirectTrace.Proxy(old,Vector3.one*.4f,0)},64);
            var after=new SpatialIndirectTrace(new[]{new SpatialIndirectTrace.Proxy(moved,Vector3.one*.4f,0)},64);
            var grid=new SpatialIndirectCache(new Vector3(0,1.1f,0),new Vector3Int(2,2,2),1);var transport=new SpatialIndirectTransport(grid,before);var light=Light(Vector3.up);
            Compare(transport,before,grid,light,3);Assert.That(transport.Solve(0,grid.Position(0)).c0.w,Is.EqualTo(1));
            transport.Bind(after);if(!SpatialIndirectTrace.SameGeometryBounds(old,moved))transport.InvalidateGeometry(old,moved,0);
            Compare(transport,after,grid,light,3);Assert.That(transport.Solve(0,grid.Position(0)).c0.w,Is.EqualTo(-1));
        }
        [Test] public void BroadPhaseRetainsFiniteEndpointHitForFloatNormalisationRoundoff()
        {
            var bounds=new Bounds(new Vector3(6.000001f,0,0),new Vector3(.0000002f,1,1));
            var trace=new SpatialIndirectTrace(new[]{new SpatialIndirectTrace.Proxy(bounds,Vector3.one,0)},64);
            var direction=new Vector3(1.00000024f,0,0);
            Assert.That(trace.Trace(Vector3.zero,direction,6,out var reference),Is.True);
            Assert.That(trace.TraceCandidates(Vector3.zero,direction,SpatialIndirectTrace.InverseRay(direction),6,trace.CandidateMask(Vector3.zero,6),out var cached),Is.True);
            Assert.That(cached.distance,Is.EqualTo(reference.distance));
        }
        [Test] public void OptimisedNearestAndAnyHitMatchReferenceAcrossDeterministicFiniteRays()
        {
            var random=new System.Random(670261);float Next(float span)=>(float)(random.NextDouble()*2-1)*span;
            var boxes=new SpatialIndirectTrace.Proxy[8];
            for(int i=0;i<boxes.Length;i++)boxes[i]=new SpatialIndirectTrace.Proxy(new Bounds(new Vector3(Next(5),Next(3),Next(5)),new Vector3(i==0?.0001f:Mathf.Abs(Next(4)),Mathf.Abs(Next(5)),Mathf.Abs(Next(4)))),Vector3.one,i);
            var trace=new SpatialIndirectTrace(boxes,64);
            for(int i=0;i<24000;i++)
            {
                var p=new Vector3(Next(9),Next(6),Next(9));var d=new Vector3(Next(1),Next(1),Next(1)).normalized;
                if(i%7==0)d[i%3]=0;if(i%11==0)d[i%3]=-.000000001f;
                float range=i%13==0?.0005f:Mathf.Abs(Next(12));int ignore=i%9-1;int mask=trace.CandidateMask(p,range);
                bool found=trace.Trace(p,d,range,out var expected,ignore);var inv=SpatialIndirectTrace.InverseRay(d);
                bool nearest=trace.TraceCandidates(p,d,inv,range,mask,out var actual,ignore),any=trace.OccludedCandidates(p,inv,range,mask,ignore);
                if(found!=nearest||found!=any||(found&&(expected.index!=actual.index||expected.distance!=actual.distance||!expected.normal.Equals(actual.normal)||!expected.position.Equals(actual.position))))Assert.Fail("Kernel mismatch at deterministic ray "+i);
            }
        }
        [Test] public void OptimisedSlabPreservesInsideExitFaceTiesAndStrictFiniteEndpoints()
        {
            var box=new Bounds(Vector3.zero,Vector3.one*2);var trace=new SpatialIndirectTrace(new[]{new SpatialIndirectTrace.Proxy(box,Vector3.one,0),new SpatialIndirectTrace.Proxy(box,Vector3.zero,1)},64);
            foreach(var p in new[]{Vector3.zero,Vector3.one,new Vector3(-2,-2,-2),new Vector3(-2,1,0),new Vector3(-1,0,0)})
            foreach(var d in new[]{Vector3.right,Vector3.left,Vector3.up,new Vector3(1,1,1).normalized,new Vector3(1,0,1).normalized,new Vector3(0,-1e-9f,1)})
            foreach(float limit in new[]{.0005f,.001f,1f,2f,6f})
            {
                bool a=trace.Trace(p,d,limit,out var before),b=trace.TraceCandidates(p,d,SpatialIndirectTrace.InverseRay(d),limit,3,out var after);
                Assert.That(b,Is.EqualTo(a));Assert.That(trace.OccludedCandidates(p,SpatialIndirectTrace.InverseRay(d),limit,3),Is.EqualTo(a));
                if(a){Assert.That(after.index,Is.EqualTo(before.index));Assert.That(after.distance,Is.EqualTo(before.distance));Assert.That(after.normal,Is.EqualTo(before.normal));}
            }
        }
        [Test] public void InactiveSourcesRegainVisibilityAtSameDirectionAfterGeometryChange()
        {
            var roof=new Bounds(new Vector3(0,2.6f,0),new Vector3(3,.12f,3));
            var before=new SpatialIndirectTrace(new[]{Ground(),new SpatialIndirectTrace.Proxy(roof,Vector3.one*.5f,3)},64);var after=new SpatialIndirectTrace(new[]{Ground()},64);
            var grid=new SpatialIndirectCache(new Vector3(-3,.1f,-3),new Vector3Int(7,4,7),1);var cached=new SpatialIndirectTransport(grid,before);
            SpatialIndirectTrace.Lighting Mode(float sun,float moon)=>new SpatialIndirectTrace.Lighting(Vector3.one*.2f,Vector3.one*.1f,Vector3.one*.05f,new Vector3(.3f,.8f,.2f),Vector3.one*sun,new Vector3(-.4f,.6f,.1f),Vector3.one*moon);
            Compare(cached,before,grid,Mode(0,0),3);Compare(cached,before,grid,Mode(.8f,0),3);Compare(cached,before,grid,Mode(0,.1f),3);Compare(cached,before,grid,Mode(0,0),3);
            cached.Bind(after);cached.InvalidateGeometry(roof,roof,3);Compare(cached,after,grid,Mode(0,0),3);Compare(cached,after,grid,Mode(.8f,.1f),3);
        }
        [Test] public void PrecomputedSurfaceHemisphereMatchesReferenceOnAllSixFaces()
        {
            var trace=new SpatialIndirectTrace(new[]{new SpatialIndirectTrace.Proxy(new Bounds(Vector3.zero,new Vector3(2,3,4)),Vector3.one*.6f,2),new SpatialIndirectTrace.Proxy(new Bounds(new Vector3(3,1,0),new Vector3(.12f,4,4)),Vector3.one*.3f,5)},64);
            var grid=new SpatialIndirectCache(new Vector3(-3,-3,-3),new Vector3Int(7,7,7),1);var cached=new SpatialIndirectTransport(grid,trace);Compare(cached,trace,grid,Light(new Vector3(.4f,.7f,.3f)),4);
        }
    }
}
