using System;
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
        // Intrusive global FIFO + visible subset: promotion/removal never leaves
        // duplicate/stale queue entries, even during repeated edits/camera motion.
        readonly int[] next,previous,priorityNext,priorityPrevious;
        readonly bool[] priority;
        readonly Vector4[] planes=new Vector4[6];
        int head=-1,tail=-1,priorityHead=-1,priorityTail=-1,pending,priorityPending,priorityRun;
        bool hasPriority;
        float priorityPadding;
        public int PendingCount=>pending;
        public int PriorityPendingCount=>priorityPending;
        public const int PriorityBurst=3;
        public long QueuePayloadBytes=>ProbeCount*22L+6*16; // 4int + 2bool + revision int
        public bool IsPriority(int index)=>priority[index];
        public int ProbeCount=>Data.Length;
        public SpatialIndirectCache(Vector3 origin,Vector3Int size,float spacing)
        {
            if(size.x<2||size.y<2||size.z<2||(long)size.x*size.y*size.z>262144||spacing<.1f||float.IsNaN(spacing)||float.IsInfinity(spacing))throw new ArgumentException("Invalid bounded probe grid.");
            Origin=origin;Size=size;Spacing=spacing;
            var extent=new Vector3(size.x-1,size.y-1,size.z-1)*spacing;
            Bounds=new Bounds(origin+extent*.5f,extent);
            int count=size.x*size.y*size.z;Data=new SpatialIndirectTrace.Probe[count];queued=new bool[count];revisions=new int[count];priority=new bool[count];
            next=new int[count];previous=new int[count];priorityNext=new int[count];priorityPrevious=new int[count];
        }
        public int Index(int x,int y,int z)=>(z*Size.y+y)*Size.x+x;
        public Vector3 Position(int index)
        {int x=index%Size.x,y=index/Size.x%Size.y,z=index/(Size.x*Size.y);return Origin+new Vector3(x,y,z)*Spacing;}
        void Mark(int i)
        {
            revisions[i]++;if(queued[i])return;queued[i]=true;pending++;
            previous[i]=tail;next[i]=-1;if(tail<0)head=i;else next[tail]=i;tail=i;
            priority[i]=Visible(i);if(priority[i])AppendPriority(i);
        }
        void AppendPriority(int i)
        {priorityPrevious[i]=priorityTail;priorityNext[i]=-1;if(priorityTail<0)priorityHead=i;else priorityNext[priorityTail]=i;priorityTail=i;priorityPending++;}
        bool Visible(int index)
        {
            if(!hasPriority)return false;var p=Position(index);
            for(int i=0;i<6;i++){var plane=planes[i];if(plane.x*p.x+plane.y*p.y+plane.z*p.z+plane.w < -priorityPadding)return false;}return true;
        }
        /// <summary>Conservative probe support of a camera frustum; no camera/authority dependency.</summary>
        public void SetPriority(Plane[] frustum,float padding)
        {
            if(frustum==null||frustum.Length!=6||padding<0||float.IsNaN(padding)||float.IsInfinity(padding))throw new ArgumentException("Six finite frustum planes and nonnegative padding required.");
            for(int i=0;i<6;i++)
            {
                var n=frustum[i].normal;float d=frustum[i].distance;
                if(!float.IsFinite(n.x)||!float.IsFinite(n.y)||!float.IsFinite(n.z)||!float.IsFinite(d)||Mathf.Abs(n.sqrMagnitude-1)>.001f)throw new ArgumentException("Normalised finite planes required.");
            }
            for(int i=0;i<6;i++){var n=frustum[i].normal;planes[i]=new Vector4(n.x,n.y,n.z,frustum[i].distance);}
            hasPriority=true;priorityPadding=padding;priorityHead=priorityTail=-1;priorityPending=0;
            for(int i=head;i>=0;i=next[i]){priority[i]=Visible(i);if(priority[i])AppendPriority(i);}
            // Keep priorityRun across camera changes: motion cannot reset fairness.
        }
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
        public bool TryBegin(out Work work,bool prioritize=true)
        {
            if(pending==0){work=default;return false;}
            bool preferred=prioritize&&priorityHead>=0&&priorityRun<PriorityBurst;
            int i=preferred?priorityHead:head;priorityRun=preferred?priorityRun+1:0;
            if(previous[i]<0)head=next[i];else next[previous[i]]=next[i];
            if(next[i]<0)tail=previous[i];else previous[next[i]]=previous[i];
            if(priority[i])
            {
                if(priorityPrevious[i]<0)priorityHead=priorityNext[i];else priorityNext[priorityPrevious[i]]=priorityNext[i];
                if(priorityNext[i]<0)priorityTail=priorityPrevious[i];else priorityPrevious[priorityNext[i]]=priorityPrevious[i];priorityPending--;
            }
            queued[i]=false;pending--;work=new Work(i,revisions[i]);return true;
        }
        public bool Commit(Work work,SpatialIndirectTrace.Probe result)
        {if(revisions[work.index]!=work.revision)return false;Data[work.index]=result;return true;}
    }
}
