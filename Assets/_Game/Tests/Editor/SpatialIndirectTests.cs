using LittleCastle.Research;
using NUnit.Framework;
using UnityEngine;

namespace LittleCastle.Tests
{
    public sealed class SpatialIndirectTests
    {
        static SpatialIndirectTrace.Lighting Light(Vector3 sky,Vector3 sun)=>new SpatialIndirectTrace.Lighting(sky,sky,sky,Vector3.up,sun,Vector3.down,Vector3.zero);
        [Test] public void SlabTraceHandlesParallelMissInsideAndThinWall()
        {
            var box=new Bounds(Vector3.zero,new Vector3(.1f,4,4));
            Assert.That(SpatialIndirectTrace.IntersectBox(new Vector3(-1,0,0),Vector3.right,box,3,out float t,out var n),Is.True);
            Assert.That(t,Is.EqualTo(.95f).Within(.0001f));Assert.That(n,Is.EqualTo(Vector3.left));
            Assert.That(SpatialIndirectTrace.IntersectBox(new Vector3(-1,0,3),Vector3.right,box,3,out _,out _),Is.False);
            Assert.That(SpatialIndirectTrace.IntersectBox(Vector3.zero,Vector3.right,box,3,out t,out n),Is.True);
            Assert.That(t,Is.EqualTo(.05f).Within(.0001f));Assert.That(n,Is.EqualTo(Vector3.right));
            Assert.That(SpatialIndirectTrace.IntersectBox(new Vector3(-1,0,0),Vector3.right,box,.5f,out _,out _),Is.False);
        }
        [Test] public void ConstantEnvironmentConservesDiffuseEnergyAndDoesNotDependOnCamera()
        {
            var trace=new SpatialIndirectTrace(System.Array.Empty<SpatialIndirectTrace.Proxy>(),512);var c=new Vector3(.2f,.4f,.7f);
            var p=trace.Solve(new Vector3(-3,1,-2),Light(c,Vector3.zero),6);
            foreach(var n in new[]{Vector3.up,Vector3.down,Vector3.left,Vector3.forward})Assert.That(Vector3.Distance(p.Evaluate(n),c),Is.LessThan(.004f));
            var again=trace.Solve(new Vector3(-3,1,-2),Light(c,Vector3.zero),6);Assert.That(again.c0,Is.EqualTo(p.c0));Assert.That(again.cy,Is.EqualTo(p.cy));
        }
        [Test] public void RoofBlocksSkyAndRemovingItRestoresTransport()
        {
            var roof=new SpatialIndirectTrace.Proxy(new Bounds(new Vector3(0,3,0),new Vector3(12,.12f,12)),Vector3.zero);
            var closed=new SpatialIndirectTrace(new[]{roof},512);var open=new SpatialIndirectTrace(System.Array.Empty<SpatialIndirectTrace.Proxy>(),512);var light=Light(Vector3.one*.3f,Vector3.zero);
            float a=closed.Solve(new Vector3(0,1,0),light,8).Evaluate(Vector3.up).x,b=open.Solve(new Vector3(0,1,0),light,8).Evaluate(Vector3.up).x;
            Assert.That(a,Is.LessThan(b*.5f));Assert.That(b,Is.EqualTo(.3f).Within(.004f));
        }
        [Test] public void BounceUsesLitReflectorAlbedoAndSunVisibility()
        {
            var floor=new SpatialIndirectTrace.Proxy(new Bounds(new Vector3(0,-.5f,0),new Vector3(30,1,30)),new Vector3(.1f,.7f,.1f));
            var trace=new SpatialIndirectTrace(new[]{floor},512);var sun=Light(Vector3.zero,Vector3.one);
            var bounce=trace.Solve(new Vector3(0,1,0),sun,6,true).Evaluate(Vector3.down);
            var none=trace.Solve(new Vector3(0,1,0),sun,6,false).Evaluate(Vector3.down);
            Assert.That(bounce.y,Is.GreaterThan(bounce.x*5));Assert.That(none.magnitude,Is.LessThan(.0001f));
            var roof=new SpatialIndirectTrace.Proxy(new Bounds(new Vector3(0,4,0),new Vector3(30,.2f,30)),Vector3.zero);
            var blocked=new SpatialIndirectTrace(new[]{floor,roof},512).Solve(new Vector3(0,1,0),sun,6,true).Evaluate(Vector3.down);
            Assert.That(blocked.magnitude,Is.LessThan(.0001f),"A reflector shadowed from the sun cannot inject constant coloured light.");
        }
        [Test] public void RegionInvalidationIsLocalDeduplicatedAndIgnoresPreview()
        {
            var cache=new SpatialIndirectCache(new Vector3(-20,0,-20),new Vector3Int(41,5,41),1);var area=new Bounds(new Vector3(-6,2,-7),new Vector3(2,.2f,2));
            Assert.That(cache.Invalidate(area,2,true),Is.Zero);Assert.That(cache.PendingCount,Is.Zero);
            int count=cache.Invalidate(area,2);Assert.That(count,Is.GreaterThan(0).And.LessThan(cache.ProbeCount/4));
            cache.Invalidate(area,2);Assert.That(cache.PendingCount,Is.EqualTo(count));
            while(cache.TryBegin(out var work))Assert.That(cache.Position(work.index).x,Is.InRange(-9,-3));
        }
        [Test] public void SkyReflectsWithoutSunAndTransportCacheSurvivesLightColourChanges()
        {
            var floor=new SpatialIndirectTrace.Proxy(new Bounds(new Vector3(0,-.5f,0),new Vector3(30,1,30)),new Vector3(.1f,.7f,.1f));
            var trace=new SpatialIndirectTrace(new[]{floor},512);var p=new Vector3(0,1,0);
            var a=trace.Solve(p,Light(Vector3.one*.3f,Vector3.zero),6,true).Evaluate(Vector3.down);int entries=trace.SkyTransportEntries;
            var noBounce=trace.Solve(p,Light(Vector3.one*.3f,Vector3.zero),6,false).Evaluate(Vector3.down);
            var reflected=a-noBounce; // Finite-distance rays also retain a neutral sky contribution.
            Assert.That(reflected.y,Is.GreaterThan(.15f));Assert.That(reflected.y,Is.GreaterThan(reflected.x*5));Assert.That(entries,Is.GreaterThan(0));
            var b=trace.Solve(p,Light(new Vector3(.03f,.03f,.3f),Vector3.zero),6,true).Evaluate(Vector3.down);
            Assert.That(trace.SkyTransportEntries,Is.EqualTo(entries));Assert.That(b.y,Is.LessThan(a.y*.2f));Assert.That(b.z,Is.EqualTo(a.z).Within(.001f));
        }
        [Test] public void InvalidatedInFlightResultCannotOverwriteNewRevision()
        {
            var cache=new SpatialIndirectCache(Vector3.zero,new Vector3Int(3,3,3),1);var region=new Bounds(Vector3.one,Vector3.one*.1f);
            cache.Invalidate(region,0);Assert.That(cache.TryBegin(out var old),Is.True);cache.Invalidate(region,0);
            Assert.That(cache.Commit(old,new SpatialIndirectTrace.Probe{c0=Vector4.one}),Is.False);
            Assert.That(cache.TryBegin(out var current),Is.True);Assert.That(cache.Commit(current,new SpatialIndirectTrace.Probe{c0=Vector4.one}),Is.True);
            Assert.That(cache.PendingCount,Is.Zero);
        }
        [Test] public void LocalGeometryUpdateMatchesFullRebuildWithSurfaceCacheFootprint()
        {
            const float range=2;
            var ground=new SpatialIndirectTrace.Proxy(new Bounds(new Vector3(0,-.5f,0),new Vector3(40,1,40)),Vector3.one*.35f);
            var oldBox=new Bounds(new Vector3(0,2.6f,0),new Vector3(1.2f,.12f,1.4f));
            var newBox=new Bounds(new Vector3(.8f,2.6f,-.2f),oldBox.size);
            var before=new SpatialIndirectTrace(new[]{ground,new SpatialIndirectTrace.Proxy(oldBox,Vector3.one*.7f)},64);
            var after=new SpatialIndirectTrace(new[]{ground,new SpatialIndirectTrace.Proxy(newBox,new Vector3(.6f,.2f,.1f))},64);
            var cache=new SpatialIndirectCache(new Vector3(-8,.1f,-8),new Vector3Int(17,5,17),1);
            var light=Light(Vector3.one*.2f,Vector3.one*.6f);
            cache.InvalidateAll();
            while(cache.TryBegin(out var work))cache.Commit(work,before.Solve(cache.Position(work.index),light,range));
            cache.Invalidate(oldBox,SpatialIndirectTrace.InfluenceRadius(range));
            cache.Invalidate(newBox,SpatialIndirectTrace.InfluenceRadius(range));
            Assert.That(cache.PendingCount,Is.GreaterThan(0).And.LessThan(cache.ProbeCount));
            while(cache.TryBegin(out var work))cache.Commit(work,after.Solve(cache.Position(work.index),light,range));
            for(int i=0;i<cache.ProbeCount;i++)
            {
                var full=after.Solve(cache.Position(i),light,range);var local=cache.Data[i];
                Assert.That(Vector4.Distance(full.c0,local.c0)+Vector4.Distance(full.cx,local.cx)+Vector4.Distance(full.cy,local.cy)+Vector4.Distance(full.cz,local.cz),Is.LessThan(.000001f),"Probe "+i);
            }
        }
    }
}
