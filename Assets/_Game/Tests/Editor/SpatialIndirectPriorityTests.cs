using System;
using LittleCastle.Research;
using NUnit.Framework;
using UnityEngine;

namespace LittleCastle.Tests
{
    public sealed class SpatialIndirectPriorityTests
    {
        static Plane[] Planes(Bounds b)=>new[]{new Plane(Vector3.right,-b.min.x),new Plane(Vector3.left,b.max.x),new Plane(Vector3.up,-b.min.y),new Plane(Vector3.down,b.max.y),new Plane(Vector3.forward,-b.min.z),new Plane(Vector3.back,b.max.z)};
        static SpatialIndirectCache Grid()=>new SpatialIndirectCache(new Vector3(-6,.1f,-6),new Vector3Int(13,5,13),1);
        [Test] public void FrustumPaddingConservativelyIncludesInterpolationNeighboursAtNegativeCoordinates()
        {
            var c=Grid();var bounds=new Bounds(new Vector3(-2,1.1f,-1),new Vector3(2,1,2));c.InvalidateAll();c.SetPriority(Planes(bounds),1.8f+.08f);
            int expected=0;while(c.TryBegin(out var w,false))
            {var b=bounds;b.Expand(2*(1.8f+.08f));bool inside=b.Contains(c.Position(w.index));Assert.That(c.IsPriority(w.index),Is.EqualTo(inside));if(inside)expected++;}
            Assert.That(expected,Is.GreaterThan(0).And.LessThan(c.ProbeCount));Assert.That(c.PriorityPendingCount,Is.Zero);
        }
        [Test] public void ThreeVisibleRequestsThenOldestFifoGuaranteeBackgroundProgressDespiteCameraChanges()
        {
            var c=Grid();var visible=Planes(new Bounds(new Vector3(3,2,3),new Vector3(4,6,4)));c.InvalidateAll();c.SetPriority(visible,0);
            for(int i=0;i<SpatialIndirectCache.PriorityBurst;i++){Assert.That(c.TryBegin(out var w),Is.True);Assert.That(c.IsPriority(w.index),Is.True);c.Invalidate(new Bounds(c.Position(w.index),Vector3.zero),0);c.SetPriority(visible,0);}
            Assert.That(c.TryBegin(out var oldest),Is.True);Assert.That(oldest.index,Is.Zero,"Camera refresh cannot reset the fairness counter.");
            var seen=new bool[c.ProbeCount];int remaining=c.ProbeCount;
            for(int i=0;i<c.ProbeCount*4&&remaining>0;i++)
            {
                Assert.That(c.TryBegin(out var w),Is.True);if(!seen[w.index]){seen[w.index]=true;remaining--;}
                if(c.IsPriority(w.index))c.Invalidate(new Bounds(c.Position(w.index),Vector3.zero),0);
                c.SetPriority(visible,0);
            }
            // index 0 was served immediately before this loop and not re-enqueued.
            Assert.That(remaining,Is.EqualTo(1));Assert.That(seen[0],Is.False);
        }
        [Test] public void PromotionInvalidationAndInFlightRevisionKeepExactlyOnePendingEntry()
        {
            var c=Grid();c.InvalidateAll();c.SetPriority(Planes(new Bounds(new Vector3(5,2,5),Vector3.one*2)),0);int count=c.PendingCount;
            Assert.That(c.TryBegin(out var old),Is.True);c.Invalidate(new Bounds(c.Position(old.index),Vector3.zero),0);c.InvalidateAll();Assert.That(c.PendingCount,Is.EqualTo(count));
            Assert.That(c.Commit(old,new SpatialIndirectTrace.Probe{c0=Vector4.one}),Is.False);
            c.SetPriority(Planes(new Bounds(new Vector3(-5,2,-5),Vector3.one*2)),0);var seen=new bool[c.ProbeCount];int solved=0;
            while(c.TryBegin(out var w)){Assert.That(seen[w.index],Is.False);seen[w.index]=true;solved++;Assert.That(c.Commit(w,default),Is.True);}
            Assert.That(solved,Is.EqualTo(count));Assert.That(c.PriorityPendingCount,Is.Zero);
        }
        [Test] public void InvalidPriorityConfigurationDoesNotPartiallyChangeLiveFrustum()
        {
            var c=Grid();var p=Planes(new Bounds(new Vector3(5,2,5),Vector3.one*2));c.InvalidateAll();c.SetPriority(p,0);int n=c.PriorityPendingCount;
            var invalid=Planes(new Bounds(new Vector3(-5,2,-5),Vector3.one*2));invalid[5]=new Plane(Vector3.zero,float.NaN);
            Assert.Throws<ArgumentException>(()=>c.SetPriority(invalid,0));Assert.That(c.PriorityPendingCount,Is.EqualTo(n));
            var point=new Bounds(new Vector3(6,1.1f,6),Vector3.zero);c.Invalidate(point,0);Assert.That(c.TryBegin(out var w),Is.True);Assert.That(c.Position(w.index).x,Is.GreaterThan(3));
        }
        [Test] public void PriorityOrderLeavesTransportFieldIdenticalToFifoAndSerialReference()
        {
            var proxies=new[]{new SpatialIndirectTrace.Proxy(new Bounds(new Vector3(0,-.5f,0),new Vector3(30,1,30)),Vector3.one*.3f,0),new SpatialIndirectTrace.Proxy(new Bounds(new Vector3(1,2,1),new Vector3(2,.12f,2)),Vector3.one*.6f,3)};
            var light=new SpatialIndirectTrace.Lighting(Vector3.one*.2f,Vector3.one*.1f,Vector3.one*.05f,new Vector3(.3f,.8f,.2f),Vector3.one*.7f,Vector3.down,Vector3.zero);
            var a=Grid();var b=Grid();var traceA=new SpatialIndirectTrace(proxies,64);var traceB=new SpatialIndirectTrace(proxies,64);var reference=new SpatialIndirectTrace(proxies,64);
            var ta=new SpatialIndirectTransport(a,traceA);var tb=new SpatialIndirectTransport(b,traceB);ta.SetLighting(light,3);tb.SetLighting(light,3);
            a.InvalidateAll();b.InvalidateAll();b.SetPriority(Planes(new Bounds(new Vector3(3,2,3),Vector3.one*4)),1);
            while(a.TryBegin(out var w))a.Commit(w,ta.Solve(w.index,a.Position(w.index)));while(b.TryBegin(out var w))b.Commit(w,tb.Solve(w.index,b.Position(w.index)));
            for(int i=0;i<a.ProbeCount;i++){var x=a.Data[i];var y=b.Data[i];Assert.That(y.c0,Is.EqualTo(x.c0));Assert.That(y.cx,Is.EqualTo(x.cx));Assert.That(y.cy,Is.EqualTo(x.cy));Assert.That(y.cz,Is.EqualTo(x.cz));Assert.That(Vector4.Distance(y.c0,reference.Solve(b.Position(i),light,3).c0),Is.LessThan(.00001f));}
        }
        [Test] public void PriorityRefreshEditsAndDrainDoNotAllocateManagedMemoryAfterConstruction()
        {
            var c=Grid();var p=Planes(new Bounds(Vector3.one,Vector3.one*4));
            TestDelegate allocating=()=>GC.KeepAlive(new byte[4096]);Assert.That(allocating,new UnityEngine.TestTools.Constraints.AllocatingGCMemoryConstraint());
            TestDelegate hot=()=>{c.InvalidateAll();c.SetPriority(p,1.8f);while(c.TryBegin(out var w))c.Commit(w,default);};hot();
            Assert.That(hot,new NUnit.Framework.Constraints.NotConstraint(new UnityEngine.TestTools.Constraints.AllocatingGCMemoryConstraint()));
        }
    }
}
