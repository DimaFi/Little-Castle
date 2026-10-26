using System;
using System.Collections.Generic;
using UnityEngine;

namespace LittleCastle.Research
{
    /// <summary>Finite presentation cache. Commit-only dirty regions; per-probe stale work rejection.</summary>
    public sealed class SpatialIndirectCache
    {
        public readonly struct Work
        {
            public readonly int index,revision;
            public Work(int index,int revision){this.index=index;this.revision=revision;}
        }
        public Vector3 Origin {get;}
        public Vector3Int Size {get;}
        public float Spacing {get;}
        public Bounds Bounds {get;}
        public SpatialIndirectTrace.Probe[] Data {get;}
        readonly bool[] queued;
        readonly int[] revisions;
        readonly Queue<int> queue;
        public int PendingCount=>queue.Count;
        public int ProbeCount=>Data.Length;
        public SpatialIndirectCache(Vector3 origin,Vector3Int size,float spacing)
        {
            if(size.x<2||size.y<2||size.z<2||(long)size.x*size.y*size.z>262144||spacing<.1f||float.IsNaN(spacing)||float.IsInfinity(spacing))throw new ArgumentException("Invalid bounded probe grid.");
            Origin=origin;Size=size;Spacing=spacing;
            var extent=new Vector3(size.x-1,size.y-1,size.z-1)*spacing;
            Bounds=new Bounds(origin+extent*.5f,extent);
            int count=size.x*size.y*size.z;Data=new SpatialIndirectTrace.Probe[count];queued=new bool[count];revisions=new int[count];queue=new Queue<int>(count);
        }
        public int Index(int x,int y,int z)=>(z*Size.y+y)*Size.x+x;
        public Vector3 Position(int index)
        {int x=index%Size.x,y=index/Size.x%Size.y,z=index/(Size.x*Size.y);return Origin+new Vector3(x,y,z)*Spacing;}
        void Mark(int i)
        {revisions[i]++;if(queued[i])return;queued[i]=true;queue.Enqueue(i);}
        public void InvalidateAll(){for(int i=0;i<Data.Length;i++)Mark(i);}
        public int Invalidate(Bounds changed,float influenceRadius,bool preview=false)
        {
            if(preview)return 0;
            if(influenceRadius<0||float.IsNaN(influenceRadius)||float.IsInfinity(influenceRadius))throw new ArgumentOutOfRangeException(nameof(influenceRadius));
            changed.Expand(influenceRadius*2);
            if(!Bounds.Intersects(changed))return 0;
            var min=(changed.min-Origin)/Spacing;var max=(changed.max-Origin)/Spacing;
            int x0=Mathf.Max(0,Mathf.CeilToInt(min.x)),y0=Mathf.Max(0,Mathf.CeilToInt(min.y)),z0=Mathf.Max(0,Mathf.CeilToInt(min.z));
            int x1=Mathf.Min(Size.x-1,Mathf.FloorToInt(max.x)),y1=Mathf.Min(Size.y-1,Mathf.FloorToInt(max.y)),z1=Mathf.Min(Size.z-1,Mathf.FloorToInt(max.z));
            int touched=0;
            for(int z=z0;z<=z1;z++)for(int y=y0;y<=y1;y++)for(int x=x0;x<=x1;x++){Mark(Index(x,y,z));touched++;}
            return touched;
        }
        public bool TryBegin(out Work work)
        {
            if(queue.Count==0){work=default;return false;}
            int i=queue.Dequeue();queued[i]=false;work=new Work(i,revisions[i]);return true;
        }
        public bool Commit(Work work,SpatialIndirectTrace.Probe result)
        {if(revisions[work.index]!=work.revision)return false;Data[work.index]=result;return true;}
    }
}
