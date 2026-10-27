using System;
using LittleCastle.Research;
using NUnit.Framework;
using UnityEngine;

namespace LittleCastle.Tests
{
    public sealed class SpatialIndirectPublicationTests
    {
        static SpatialIndirectCache Grid()=>new SpatialIndirectCache(new Vector3(-3,.1f,-3),new Vector3Int(7,4,7),1);
        static SpatialIndirectTrace.Probe[] Field(SpatialIndirectCache c,float value)
        {var a=new SpatialIndirectTrace.Probe[c.ProbeCount];for(int i=0;i<a.Length;i++)a[i].c0=new Vector4(value,value*.5f,value*.2f,1);return a;}
        [Test] public void IncompleteAndSupersededCandidatesNeverBecomePublished()
        {
            var c=Grid();var p=new SpatialIndirectPublication(c);var data=Field(c,1);int a=p.BeginLighting();
            Assert.That(p.TryPublish(a,false,data,0,.2f),Is.False);int b=p.BeginLighting();Assert.That(p.TryPublish(a,true,data,0,.2f),Is.False);
            Assert.That(p.PublishedRevision,Is.Zero);Assert.That(p.TryPublish(b,true,data,0,.2f),Is.True);Assert.That(p.PublishedRevision,Is.EqualTo(b));Assert.That(p.TryPublish(b,true,data,1,.2f),Is.False);
        }
        [Test] public void WholeFieldChangesTogetherWithClampedMonotoneSmoothstep()
        {
            var c=Grid();var p=new SpatialIndirectPublication(c);var a=Field(c,.2f);p.BeginLighting();Assert.That(p.TryPublish(p.RequestRevision,true,a,0,0),Is.True);
            var b=Field(c,.8f);p.BeginLighting();Assert.That(p.TryPublish(p.RequestRevision,true,b,1,.2f),Is.True);
            for(int step=0;step<=20;step++)
            {
                double now=1+step*.01;float w=p.Weight(now);Assert.That(w,Is.InRange(0,1));if(step>0)Assert.That(w,Is.GreaterThanOrEqualTo(p.Weight(now-.01)));
                var expected=Vector3.Lerp(new Vector3(.2f,.1f,.04f),new Vector3(.8f,.4f,.16f),w);
                for(int i=0;i<c.ProbeCount;i++)Assert.That(Vector3.Distance(p.Value(i,Vector3.up,Vector3.one,now),expected),Is.LessThan(1e-6));
            }
            Assert.That(p.Settled(1.21),Is.True);
        }
        [Test] public void NewLightingWaitsForExistingBlendWithoutRebasingUnknownFallback()
        {
            var c=Grid();var p=new SpatialIndirectPublication(c);var a=Field(c,.2f);p.BeginLighting();p.TryPublish(p.RequestRevision,true,a,0,1);
            int b=p.BeginLighting();Assert.That(p.TryPublish(b,true,Field(c,.8f),.5,.2f),Is.False);Assert.That(p.PublishedRevision,Is.LessThan(b));
            Assert.That(p.TryPublish(b,true,Field(c,.8f),1.01,.2f),Is.True);Assert.That(p.Previous[0].c0,Is.EqualTo(a[0].c0));
        }
        [Test] public void GeometryForbidsBothHistoriesImmediatelyDuringBlendAndKeepsRemoteProbes()
        {
            var c=Grid();var p=new SpatialIndirectPublication(c);p.BeginLighting();p.TryPublish(p.RequestRevision,true,Field(c,1),0,1);
            int revision=p.RequestRevision;var region=new Bounds(new Vector3(-2,1.1f,-2),Vector3.one*.1f);int count=p.BeginGeometry(region,region,1);
            Assert.That(count,Is.GreaterThan(0).And.LessThan(c.ProbeCount));Assert.That(p.RequestRevision,Is.GreaterThan(revision));
            int affected=c.Index(1,1,1);Assert.That(p.Published[affected].c0.w,Is.EqualTo(-2));Assert.That(p.Previous[affected].c0.w,Is.EqualTo(-2));
            Assert.That(p.Value(affected,Vector3.up,Vector3.one,.3).magnitude,Is.Zero);Assert.That(p.Published[c.ProbeCount-1].c0.w,Is.EqualTo(1));
            Assert.That(p.TryPublish(revision,true,Field(c,1),2,.2f),Is.False);
        }
        [Test] public void RemoteMoveAndUndoNeverResurrectMaskedOldTransport()
        {
            var c=Grid();var p=new SpatialIndirectPublication(c);p.BeginLighting();p.TryPublish(p.RequestRevision,true,Field(c,1),0,0);
            var a=new Bounds(new Vector3(-2,1.1f,-2),Vector3.one*.1f);var b=new Bounds(new Vector3(2,1.1f,2),Vector3.one*.1f);
            p.BeginGeometry(a,b,.2f);int old=p.RequestRevision;p.BeginGeometry(b,a,.2f);
            Assert.That(p.Value(c.Index(1,1,1),Vector3.up,Vector3.one,1).magnitude,Is.Zero);Assert.That(p.Value(c.Index(5,1,5),Vector3.up,Vector3.one,1).magnitude,Is.Zero);
            Assert.That(p.TryPublish(old,true,Field(c,.5f),1,.2f),Is.False);Assert.That(p.TryPublish(p.RequestRevision,true,Field(c,.5f),1,.2f),Is.True);
            Assert.That(p.PublishedGeometryRevision,Is.EqualTo(2));
        }
        [Test] public void UnknownFallbackAndKnownInvalidAreDistinctAndSettledEndpointIsExact()
        {
            var c=Grid();var p=new SpatialIndirectPublication(c);var a=Field(c,.3f);p.BeginLighting();p.TryPublish(p.RequestRevision,true,a,0,1);
            var fallback=new Vector3(.1f,.2f,.4f);Assert.That(p.Value(0,Vector3.up,fallback,0),Is.EqualTo(fallback));
            Assert.That(p.Value(0,Vector3.up,fallback,1),Is.EqualTo(new Vector3(.3f,.15f,.06f)).Using(Vector3Comparer.Instance));
            Assert.That(SpatialIndirectPublication.Value(new SpatialIndirectTrace.Probe{c0=new Vector4(5,5,5,-2)},Vector3.up,fallback),Is.EqualTo(Vector3.zero));
        }
        sealed class Vector3Comparer:System.Collections.Generic.IEqualityComparer<Vector3>
        {public static readonly Vector3Comparer Instance=new Vector3Comparer();public bool Equals(Vector3 a,Vector3 b)=>Vector3.Distance(a,b)<1e-6;public int GetHashCode(Vector3 a)=>a.GetHashCode();}
        [Test] public void RequestsMasksAndPublicationReuseStorageWithoutManagedAllocations()
        {
            var c=Grid();var p=new SpatialIndirectPublication(c);var data=Field(c,.3f);var region=new Bounds(Vector3.one,Vector3.one);
            TestDelegate positive=()=>GC.KeepAlive(new byte[4096]);Assert.That(positive,new UnityEngine.TestTools.Constraints.AllocatingGCMemoryConstraint());
            TestDelegate hot=()=>{p.BeginLighting();p.TryPublish(p.RequestRevision,true,data,100,0);p.BeginGeometry(region,region,1);p.TryPublish(p.RequestRevision,true,data,100,0);};hot();
            Assert.That(hot,new NUnit.Framework.Constraints.NotConstraint(new UnityEngine.TestTools.Constraints.AllocatingGCMemoryConstraint()));
        }
    }
}
