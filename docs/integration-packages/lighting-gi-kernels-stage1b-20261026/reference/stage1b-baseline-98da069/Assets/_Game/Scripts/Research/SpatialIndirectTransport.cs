using System;
using System.Collections.Generic;
using UnityEngine;

namespace LittleCastle.Research
{
    /// <summary>
    /// Serial research cache. Exact primary hit distances/face/ray indices are retained;
    /// linear light/albedo changes use aggregated SH weights without retracing geometry.
    /// Not thread-safe: scratch storage, surface dictionary and cells have one owner.
    /// </summary>
    public sealed class SpatialIndirectTransport
    {
        // Eight bytes per retained hit, rather than position+normal+direction records.
        struct Link {public float distance;public int encoded;}
        struct Block {public int id,uniformFace;public Vector4 sky,equator,ground,sun,moon,faceSum;}
        sealed class Cell
        {
            public bool valid,solid,environmentDirty;
            public Vector4 sky,equator,ground;
            public Link[] links;
            public Block[] blocks;
            public int linkCount,blockCount,directEpoch=-1;
        }
        readonly struct Key : IEquatable<Key>
        {
            public readonly int id,face,x,y,z;
            public Key(int id,int face,Vector3 p){this.id=id;this.face=face;x=Mathf.FloorToInt(p.x*2);y=Mathf.FloorToInt(p.y*2);z=Mathf.FloorToInt(p.z*2);}
            public bool Equals(Key b)=>id==b.id&&face==b.face&&x==b.x&&y==b.y&&z==b.z;
            public override bool Equals(object obj)=>obj is Key b&&Equals(b);
            public override int GetHashCode(){unchecked{return (((id*31+face)*31+x)*31+y)*31+z;}}
        }
        struct Surface {public Vector3 weights,position;}
        readonly Cell[] cells;
        readonly SpatialIndirectCache grid;
        readonly Vector3[] directions,inverses;
        readonly Vector4[] projection;
        readonly Link[] scratchLinks;
        readonly Block[] scratchBlocks=new Block[8];
        readonly int[] scratchIds=new int[8],activeIndex=new int[8];
        readonly Vector3[] albedo=new Vector3[8];
        readonly Dictionary<Key,Surface> surfaces=new Dictionary<Key,Surface>(16384);
        readonly List<Key> removed=new List<Key>(16384);
        static readonly Vector3[] Normals={Vector3.left,Vector3.right,Vector3.down,Vector3.up,Vector3.back,Vector3.forward};
        SpatialIndirectTrace snapshot;
        SpatialIndirectTrace.Lighting light;
        Vector4 openSky,openEquator,openGround;
        Vector3 inverseSun,inverseMoon;
        float range=-1;
        int lightEpoch;
        public long GeometryBuilds {get;private set;}
        public long Injections {get;private set;}
        public long DirectReweights {get;private set;}
        public long EnvironmentReplays {get;private set;}
        public long AllocatedLinkSlots {get;private set;}
        public long AllocatedBlockSlots {get;private set;}
        public long ActiveHitLinks {get;private set;}
        public int SurfaceEntries=>surfaces.Count;
        public int LastPrunedSurfaceCells {get;private set;}
        public void ResetInvalidationMetrics()=>LastPrunedSurfaceCells=0;
        // Payload only; managed object/array/dictionary overhead is measured separately.
        public long PayloadBytes=>cells.Length*48L+AllocatedLinkSlots*8+AllocatedBlockSlots*104+surfaces.Count*44L;
        public SpatialIndirectTransport(SpatialIndirectCache grid,SpatialIndirectTrace snapshot)
        {
            this.grid=grid;cells=new Cell[grid.ProbeCount];for(int i=0;i<cells.Length;i++)cells[i]=new Cell();
            directions=new Vector3[snapshot.RayCount];inverses=new Vector3[directions.Length];projection=new Vector4[directions.Length];scratchLinks=new Link[directions.Length];
            for(int i=0;i<directions.Length;i++)
            {
                var d=snapshot.Direction(i);directions[i]=d;inverses[i]=snapshot.InverseDirection(i);projection[i]=new Vector4(1,2*d.x,2*d.y,2*d.z)/directions.Length;
                float up=Mathf.Max(0,d.y),down=Mathf.Max(0,-d.y);openSky+=projection[i]*up;openEquator+=projection[i]*(1-up-down);openGround+=projection[i]*down;
            }
            Bind(snapshot);
        }
        public void Bind(SpatialIndirectTrace value)
        {
            if(value.RayCount!=directions.Length)throw new ArgumentException("Ray count changes require a new transport cache.");
            snapshot=value;LastPrunedSurfaceCells=0;Array.Fill(activeIndex,-1);Array.Clear(albedo,0,albedo.Length);
            for(int i=0;i<value.ProxyCount;i++){int id=value.StableId(i);if(id<0||id>=8||activeIndex[id]>=0)throw new ArgumentException("Unique stable proxy IDs 0..7 required.");activeIndex[id]=i;albedo[id]=value.GetProxy(i).albedo;}
        }
        public void SetLighting(SpatialIndirectTrace.Lighting value,float maximumDistance)
        {
            if(range!=maximumDistance){range=maximumDistance;surfaces.Clear();foreach(var c in cells)c.valid=false;}
            if(!light.sunDirection.Equals(value.sunDirection)||!light.moonDirection.Equals(value.moonDirection))lightEpoch++;
            light=value;inverseSun=SpatialIndirectTrace.InverseRay(light.sunDirection);inverseMoon=SpatialIndirectTrace.InverseRay(light.moonDirection);
        }
        public void InvalidateGeometry(Bounds oldBounds,Bounds newBounds,int changedId)
        {
            float radius=SpatialIndirectTrace.InfluenceRadius(range);
            InvalidateCells(oldBounds,radius);if(!SpatialIndirectTrace.SameGeometryBounds(oldBounds,newBounds))InvalidateCells(newBounds,radius);
            var a=oldBounds;var b=newBounds;a.Expand(2*(range+.52f));b.Expand(2*(range+.52f));removed.Clear();
            foreach(var pair in surfaces)if(pair.Key.id==changedId||a.Contains(pair.Value.position)||b.Contains(pair.Value.position))removed.Add(pair.Key);
            foreach(var key in removed)surfaces.Remove(key);LastPrunedSurfaceCells=removed.Count;
        }
        void InvalidateCells(Bounds area,float halo)
        {
            var changed=area;area.Expand(2*halo);var lo=(area.min-grid.Origin)/grid.Spacing;var hi=(area.max-grid.Origin)/grid.Spacing;var s=grid.Size;
            for(int z=Mathf.Max(0,Mathf.CeilToInt(lo.z));z<=Mathf.Min(s.z-1,Mathf.FloorToInt(hi.z));z++)
            for(int y=Mathf.Max(0,Mathf.CeilToInt(lo.y));y<=Mathf.Min(s.y-1,Mathf.FloorToInt(hi.y));y++)
            for(int x=Mathf.Max(0,Mathf.CeilToInt(lo.x));x<=Mathf.Min(s.x-1,Mathf.FloorToInt(hi.x));x++)
            {
                int index=grid.Index(x,y,z);var c=cells[index];
                // Only a primary hit within R can change. Outside that support keep
                // exact links and replay secondary environment/shadow visibility.
                if(changed.SqrDistance(grid.Position(index))<=(range+.0001f)*(range+.0001f))c.valid=false;
                else{c.environmentDirty=true;c.directEpoch=-1;}
            }
        }
        public SpatialIndirectTrace.Probe Solve(int index,Vector3 position,bool bounce=true)
        {
            var c=cells[index];if(!c.valid)Build(c,position);else if(c.environmentDirty&&!c.solid)ReplayEnvironment(c,position);
            if(c.solid)return new SpatialIndirectTrace.Probe{c0=new Vector4(0,0,0,-1)};
            if(bounce&&c.directEpoch!=lightEpoch)ReweightDirect(c,position);
            Injections++;
            var result=new SpatialIndirectTrace.Probe();Add(ref result,c.sky,light.sky);Add(ref result,c.equator,light.equator);Add(ref result,c.ground,light.ground);
            if(bounce)for(int i=0;i<c.blockCount;i++)
            {
                ref var b=ref c.blocks[i];var a=albedo[b.id];
                Add(ref result,b.sky,Vector3.Scale(a,light.sky));Add(ref result,b.equator,Vector3.Scale(a,light.equator));Add(ref result,b.ground,Vector3.Scale(a,light.ground));
                Add(ref result,b.sun,Vector3.Scale(a,light.sunColor));Add(ref result,b.moon,Vector3.Scale(a,light.moonColor));
            }
            result.c0.w=1;return result;
        }
        static void Add(ref SpatialIndirectTrace.Probe p,Vector4 weight,Vector3 color)
        {
            p.c0.x+=weight.x*color.x;p.c0.y+=weight.x*color.y;p.c0.z+=weight.x*color.z;
            p.cx.x+=weight.y*color.x;p.cx.y+=weight.y*color.y;p.cx.z+=weight.y*color.z;
            p.cy.x+=weight.z*color.x;p.cy.y+=weight.z*color.y;p.cy.z+=weight.z*color.z;
            p.cz.x+=weight.w*color.x;p.cz.y+=weight.w*color.y;p.cz.z+=weight.w*color.z;
        }
        void Build(Cell c,Vector3 position)
        {
            GeometryBuilds++;ActiveHitLinks-=c.linkCount;c.linkCount=c.blockCount=0;c.directEpoch=-1;c.environmentDirty=false;c.sky=c.equator=c.ground=Vector4.zero;c.solid=snapshot.Contains(position);c.valid=true;
            if(c.solid)return;
            int mask=snapshot.CandidateMask(position,range);
            if(mask==0){c.sky=openSky;c.equator=openEquator;c.ground=openGround;return;}
            Array.Fill(scratchIds,-1);Array.Clear(scratchBlocks,0,scratchBlocks.Length);
            var profile=snapshot.Profile;bool timed=profile!=null&&profile.enabled;
            for(int ray=0;ray<directions.Length;ray++)
            {
                long stamp=timed?SpatialIndirectProfile.Stamp():0;var d=directions[ray];var w=projection[ray];
                bool hit=snapshot.TraceCandidates(position,d,inverses[ray],range,mask,out var h);
                if(timed){profile.primaryRays++;profile.primaryTicks+=SpatialIndirectProfile.Stamp()-stamp;}
                if(hit)
                {
                    int id=snapshot.StableId(h.index),face=Face(h.normal),block=scratchIds[id];
                    if(block<0){block=c.blockCount++;scratchIds[id]=block;scratchBlocks[block].id=id;scratchBlocks[block].uniformFace=face;}
                    scratchLinks[c.linkCount++]=new Link{distance=h.distance,encoded=id|(face<<3)|(ray<<6)};
                    if(timed)stamp=SpatialIndirectProfile.Stamp();var sky=Sky(h,face,id);
                    if(timed){profile.skyTicks+=SpatialIndirectProfile.Stamp()-stamp;stamp=SpatialIndirectProfile.Stamp();}
                    ref var b=ref scratchBlocks[block];b.sky+=w*sky.x;b.equator+=w*sky.y;b.ground+=w*sky.z;b.faceSum+=w;if(b.uniformFace!=face)b.uniformFace=-1;
                    if(timed)profile.projectionTicks+=SpatialIndirectProfile.Stamp()-stamp;
                }
                else{float up=Mathf.Max(0,d.y),down=Mathf.Max(0,-d.y);c.sky+=w*up;c.equator+=w*(1-up-down);c.ground+=w*down;}
            }
            if(c.linkCount>0)
            {
                if(c.links==null||c.links.Length<c.linkCount){int count=Mathf.NextPowerOfTwo(c.linkCount);AllocatedLinkSlots+=count-(c.links?.Length??0);c.links=new Link[count];}
                Array.Copy(scratchLinks,c.links,c.linkCount);
            }
            if(c.blockCount>0)
            {
                if(c.blocks==null||c.blocks.Length<c.blockCount){int count=Mathf.NextPowerOfTwo(c.blockCount);AllocatedBlockSlots+=count-(c.blocks?.Length??0);c.blocks=new Block[count];}
                Array.Copy(scratchBlocks,c.blocks,c.blockCount);
            }
            ActiveHitLinks+=c.linkCount;
        }
        void ReplayEnvironment(Cell c,Vector3 position)
        {
            EnvironmentReplays++;Array.Fill(scratchIds,-1);
            for(int i=0;i<c.blockCount;i++){c.blocks[i].sky=c.blocks[i].equator=c.blocks[i].ground=Vector4.zero;scratchIds[c.blocks[i].id]=i;}
            for(int i=0;i<c.linkCount;i++)
            {
                var link=c.links[i];int id=link.encoded&7,face=(link.encoded>>3)&7,ray=link.encoded>>6,proxy=activeIndex[id];
                if(proxy<0)throw new InvalidOperationException("Removed primary hit must invalidate geometry, not only environment.");
                var hit=new SpatialIndirectTrace.Hit(proxy,link.distance,position+directions[ray]*link.distance,Normals[face]);
                var sky=Sky(hit,face,id);var w=projection[ray];ref var b=ref c.blocks[scratchIds[id]];
                b.sky+=w*sky.x;b.equator+=w*sky.y;b.ground+=w*sky.z;
            }
            c.environmentDirty=false;
        }
        void ReweightDirect(Cell c,Vector3 position)
        {
            DirectReweights++;Array.Fill(scratchIds,-1);
            int mask=snapshot.CandidateMask(position,range*2+.02f);var profile=snapshot.Profile;bool timed=profile!=null&&profile.enabled;long stamp=timed?SpatialIndirectProfile.Stamp():0;
            bool trace=false;
            for(int i=0;i<c.blockCount;i++)
            {
                ref var b=ref c.blocks[i];
                // A convex source cannot shadow its own outward-facing hemisphere.
                // With no other box in finite support, aggregate a uniform face exactly.
                if(b.uniformFace>=0&&(mask&~(1<<activeIndex[b.id]))==0)
                {var n=Normals[b.uniformFace];b.sun=b.faceSum*Mathf.Max(0,Vector3.Dot(n,light.sunDirection));b.moon=b.faceSum*Mathf.Max(0,Vector3.Dot(n,light.moonDirection));}
                else{trace=true;b.sun=b.moon=Vector4.zero;scratchIds[b.id]=i;}
            }
            for(int i=0;trace&&i<c.linkCount;i++)
            {
                var link=c.links[i];int id=link.encoded&7,face=(link.encoded>>3)&7,ray=link.encoded>>6,proxy=activeIndex[id];
                if(scratchIds[id]<0)continue;
                if(proxy<0)throw new InvalidOperationException("Stale transport used a removed proxy.");
                var n=Normals[face];var p=position+directions[ray]*link.distance+n*.01f;
                float sun=Mathf.Max(0,Vector3.Dot(n,light.sunDirection)),moon=Mathf.Max(0,Vector3.Dot(n,light.moonDirection));
                if(sun>0){if(timed)profile.shadowRays++;if(snapshot.TraceCandidates(p,light.sunDirection,inverseSun,range,mask,out _,proxy))sun=0;}
                if(moon>0){if(timed)profile.shadowRays++;if(snapshot.TraceCandidates(p,light.moonDirection,inverseMoon,range,mask,out _,proxy))moon=0;}
                ref var b=ref c.blocks[scratchIds[id]];b.sun+=projection[ray]*sun;b.moon+=projection[ray]*moon;
            }
            if(timed)profile.shadowTicks+=SpatialIndirectProfile.Stamp()-stamp;c.directEpoch=lightEpoch;
        }
        Vector3 Sky(SpatialIndirectTrace.Hit hit,int face,int id)
        {
            var key=new Key(id,face,hit.position);var profile=snapshot.Profile;bool count=profile!=null&&profile.enabled;
            if(surfaces.TryGetValue(key,out var found)){if(count)profile.skyHits++;return found.weights;}
            if(count)profile.skyMisses++;
            var p=new Vector3((key.x+.5f)*.5f,(key.y+.5f)*.5f,(key.z+.5f)*.5f);var g=snapshot.GetProxy(hit.index);int axis=face/2;
            p=Vector3.Min(g.max,Vector3.Max(g.min,p));p[axis]=(face&1)==1?g.max[axis]:g.min[axis];p+=hit.normal*.01f;
            Vector3 tangent=Vector3.Cross(Mathf.Abs(hit.normal.y)>.9f?Vector3.forward:Vector3.up,hit.normal).normalized,bitangent=Vector3.Cross(hit.normal,tangent),weights=Vector3.zero;
            int mask=snapshot.CandidateMask(p,range);
            for(int i=0;i<12;i++)
            {
                float r=Mathf.Sqrt((i+.5f)/12),angle=i*2.39996322972865332f;var d=tangent*(r*Mathf.Cos(angle))+bitangent*(r*Mathf.Sin(angle))+hit.normal*Mathf.Sqrt(1-r*r);
                if(count)profile.skyRays++;
                if(snapshot.TraceCandidates(p,d,SpatialIndirectTrace.InverseRay(d),range,mask,out _,hit.index))continue;
                float up=Mathf.Max(0,d.y),down=Mathf.Max(0,-d.y);weights+=new Vector3(up,1-up-down,down)/12;
            }
            surfaces.Add(key,new Surface{position=p,weights=weights});return weights;
        }
        static int Face(Vector3 n)=>Mathf.Abs(n.x)>.5f?(n.x>0?1:0):Mathf.Abs(n.y)>.5f?(n.y>0?3:2):(n.z>0?5:4);
    }
}
