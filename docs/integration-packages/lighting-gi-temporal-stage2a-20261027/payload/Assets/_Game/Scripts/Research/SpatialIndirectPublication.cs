using System;
using UnityEngine;

namespace LittleCastle.Research
{
    /// <summary>Coherent presentation snapshots; no save/time authority or GPU calls.</summary>
    public sealed class SpatialIndirectPublication
    {
        public readonly SpatialIndirectTrace.Probe[] Published,Previous;
        readonly SpatialIndirectCache grid;
        double blendStart;
        float duration;
        bool hasPublication;
        public int RequestRevision {get;private set;}
        public int PublishedRevision {get;private set;}
        public int GeometryRevision {get;private set;}
        public int PublishedGeometryRevision {get;private set;}
        public int PublicationCount {get;private set;}
        public bool Pending=>RequestRevision!=PublishedRevision;
        public long PayloadBytes=>Published.Length*128L;
        public SpatialIndirectPublication(SpatialIndirectCache grid)
        {this.grid=grid;Published=new SpatialIndirectTrace.Probe[grid.ProbeCount];Previous=new SpatialIndirectTrace.Probe[grid.ProbeCount];}
        public float Blend(double now)=>!hasPublication||duration<=0?1:Mathf.Clamp01((float)((now-blendStart)/duration));
        public float Weight(double now){float a=Blend(now);return a*a*(3-2*a);}
        public bool Settled(double now)=>!Pending&&Blend(now)>=1;
        public int BeginLighting()=>++RequestRevision;
        public int BeginGeometry(Bounds oldBounds,Bounds newBounds,float halo)
        {
            if(!float.IsFinite(halo)||halo<0)throw new ArgumentOutOfRangeException(nameof(halo));
            RequestRevision++;GeometryRevision++;
            int count=Forbid(oldBounds,halo);
            if(!SpatialIndirectTrace.SameGeometryBounds(oldBounds,newBounds))count+=Forbid(newBounds,halo);
            return count;
        }
        int Forbid(Bounds area,float halo)
        {
            area.Expand(halo*2);var lo=(area.min-grid.Origin)/grid.Spacing;var hi=(area.max-grid.Origin)/grid.Spacing;var size=grid.Size;int count=0;
            for(int z=Mathf.Max(0,Mathf.CeilToInt(lo.z));z<=Mathf.Min(size.z-1,Mathf.FloorToInt(hi.z));z++)
            for(int y=Mathf.Max(0,Mathf.CeilToInt(lo.y));y<=Mathf.Min(size.y-1,Mathf.FloorToInt(hi.y));y++)
            for(int x=Mathf.Max(0,Mathf.CeilToInt(lo.x));x<=Mathf.Min(size.x-1,Mathf.FloorToInt(hi.x));x++)
            {
                int i=grid.Index(x,y,z);if(Published[i].c0.w==-2&&Previous[i].c0.w==-2)continue;
                // -2 is forbidden old transport, NOT an uninitialised sky fallback.
                Published[i]=Previous[i]=new SpatialIndirectTrace.Probe{c0=new Vector4(0,0,0,-2)};count++;
            }
            return count;
        }
        public bool TryPublish(int candidateRevision,bool queueReady,SpatialIndirectTrace.Probe[] computed,double now,float fadeSeconds)
        {
            if(computed==null||computed.Length!=Published.Length)throw new ArgumentException("Mismatched field.");
            if(!float.IsFinite(fadeSeconds)||fadeSeconds<0||!double.IsFinite(now))throw new ArgumentOutOfRangeException(nameof(fadeSeconds));
            if(candidateRevision!=RequestRevision||!Pending||!queueReady||Blend(now)<1)return false;
            Array.Copy(Published,Previous,Published.Length);Array.Copy(computed,Published,Published.Length);
            PublishedRevision=candidateRevision;PublishedGeometryRevision=GeometryRevision;PublicationCount++;hasPublication=true;blendStart=now;duration=fadeSeconds;return true;
        }
        public static Vector3 Value(SpatialIndirectTrace.Probe p,Vector3 normal,Vector3 fallback)
        {
            if(p.c0.w==0)return fallback;if(p.c0.w<.5f)return Vector3.zero;
            var v=p.c0+p.cx*normal.x+p.cy*normal.y+p.cz*normal.z;return new Vector3(v.x,v.y,v.z);
        }
        public Vector3 Value(int index,Vector3 normal,Vector3 fallback,double now)
        {
            var target=Value(Published[index],normal,fallback);float a=Weight(now);
            return a>=1?target:Vector3.LerpUnclamped(Value(Previous[index],normal,fallback),target,a);
        }
    }
}
