using System.Collections.Generic;
using UnityEngine;

namespace LittleCastle.World
{
    public sealed partial class MapEditSession
    {
        readonly Dictionary<ChunkCoordinate,float> fineHeightCache=new Dictionary<ChunkCoordinate,float>();
        long fineRiverRevision=-1;
        public float RiverTerrainStep=>HeightSpacing/Mathf.Max(1,Mathf.CeilToInt(HeightSpacing/MapRiverSection.TerrainStep));
        public bool FineRiverTerrainRequired(Rect area)
        {
            foreach(var key in Regions(area))if(riverIndex.TryGetValue(key,out var group))
                foreach(long id in group)if(rivers[id].record.layoutVersion==3&&rivers[id].bounds.Overlaps(area))return true;
            if(riverNetwork!=null)foreach(var w in WetRoutes())if(SoftRoute(w)&&wetAreas.TryGetValue(w.id,out var bounds)&&bounds.Overlaps(area))return true;
            return false;
        }
        void InvalidateFineHeight(Rect area)
        {
            if(fineHeightCache.Count==0)return;
            float step=RiverTerrainStep;
            int x0=Mathf.FloorToInt((area.xMin-HeightOrigin.x)/step)-2,x1=Mathf.CeilToInt((area.xMax-HeightOrigin.x)/step)+2;
            int z0=Mathf.FloorToInt((area.yMin-HeightOrigin.y)/step)-2,z1=Mathf.CeilToInt((area.yMax-HeightOrigin.y)/step)+2;
            for(int z=z0;z<=z1;z++)for(int x=x0;x<=x1;x++)fineHeightCache.Remove(new ChunkCoordinate(x,z));
        }
        float FineHeight(int x,int z)
        {
            if(fineRiverRevision!=RiverRevision){fineHeightCache.Clear();fineRiverRevision=RiverRevision;}
            var key=new ChunkCoordinate(x,z);if(fineHeightCache.TryGetValue(key,out var result))return result;
            var p=HeightOrigin+new Vector2(x,z)*RiverTerrainStep;
            float h=LegacyComposedHeight(p.x,p.y),maximum=MaximumHeight;
            foreach(var path in RiversAt(p))if(path.record.layoutVersion==3&&!BeyondRoutedTerminal(path,p)&&path.Nearest(p,out var f,out float d))
            {
                float radial=Vector2.Distance(p,f.center);if(radial>Mathf.Abs(d)+.001f)d=(d<0?-1:1)*radial;
                h=MapRiverSection.Bed(f,d,h);
                maximum=Mathf.Max(maximum,MaximumHeight+f.sectionProfile.bankRise);
            }
            h=SoftWaterfallTerrain(p,h);
            result=Mathf.Clamp(h,MinimumHeight,maximum);fineHeightCache[key]=result;return result;
        }
        float ComposedHeight(float x,float z)
        {
            float step=RiverTerrainStep;
            if(!FineRiverTerrainRequired(new Rect(x-step,z-step,step*2,step*2)))return LegacyComposedHeight(x,z);
            var g=(new Vector2(x,z)-HeightOrigin)/step;int ix=Mathf.FloorToInt(g.x),iz=Mathf.FloorToInt(g.y);float u=g.x-ix,v=g.y-iz;
            float a=FineHeight(ix,iz),b=FineHeight(ix+1,iz),c=FineHeight(ix,iz+1),d=FineHeight(ix+1,iz+1);
            return u+v<=1?a+(b-a)*u+(c-a)*v:d+(c-d)*(1-u)+(b-d)*(1-v);
        }
        long wetRouteRevision=-1;
        List<MapWaterfallRecord> wetRoutes=new List<MapWaterfallRecord>();
        readonly Dictionary<long,Bounds> wetBounds=new Dictionary<long,Bounds>();
        readonly Dictionary<long,Rect> wetAreas=new Dictionary<long,Rect>();
        List<MapWaterfallRecord> WetRoutes()
        {
            if(wetRouteRevision!=RiverRevision)
            {
                wetRoutes=riverNetwork==null?new List<MapWaterfallRecord>():ExportRiverNetwork().waterfalls;wetRouteRevision=RiverRevision;wetBounds.Clear();wetAreas.Clear();
                foreach(var w in wetRoutes)if(w.route!=null&&!w.route.IsEmpty){wetBounds[w.id]=w.route.Bounds;wetAreas[w.id]=WaterfallBounds(w);}
            }
            return wetRoutes;
        }
        bool SoftRoute(MapWaterfallRecord w)=>rivers.TryGetValue(w.upstreamRiverId,out var a)&&a.record.layoutVersion==3&&rivers.TryGetValue(w.downstreamRiverId,out var b)&&b.record.layoutVersion==3;
        bool WaterfallVegetationAllowed(Vector2 p,float clearance)
        {
            if(riverNetwork==null)return true;
            foreach(var w in WetRoutes())
            {
                if(w.route==null||w.route.IsEmpty)continue;
                var bounds=wetBounds[w.id];
                if(p.x+clearance<bounds.min.x||p.x-clearance>bounds.max.x||p.y+clearance<bounds.min.z||p.y-clearance>bounds.max.z)continue;
                var sections=w.route.sections;
                for(int i=1;i<sections.Count;i++)
                {
                    var a=sections[i-1];var b=sections[i];var ac=a.FrontCenter;var bc=b.FrontCenter;
                    Vector2 start=new Vector2(ac.x,ac.z),end=new Vector2(bc.x,bc.z),delta=end-start;
                    float t=delta.sqrMagnitude<1e-10f?0:Mathf.Clamp01(Vector2.Dot(p-start,delta)/delta.sqrMagnitude);
                    float width=Mathf.Max(Vector3.Distance(a.points[0].position,a.points[a.points.Count-1].position),Vector3.Distance(b.points[0].position,b.points[b.points.Count-1].position));
                    if((p-Vector2.Lerp(start,end,t)).sqrMagnitude<Mathf.Pow(width*.5f+clearance+.1f,2))return false;
                }
            }
            return true;
        }
        float SoftWaterfallTerrain(Vector2 p,float ground)
        {
            if(riverNetwork==null)return ground;
            foreach(var w in WetRoutes())
            {
                if(!SoftRoute(w)||w.route==null||w.route.IsEmpty||!wetAreas[w.id].Contains(p))continue;
                var sections=w.route.sections;
                for(int i=1;i<sections.Count;i++)
                {
                    var a=sections[i-1];var b=sections[i];
                    // Never attach earth walls to a detached stream.
                    if(a.kind==MapWaterfallSectionKind.FreeFall||b.kind==MapWaterfallSectionKind.FreeFall)continue;
                    var ac=a.FrontCenter;var bc=b.FrontCenter;Vector2 start=new Vector2(ac.x,ac.z),end=new Vector2(bc.x,bc.z),delta=end-start;
                    if(delta.sqrMagnitude<1e-6f)continue;
                    float t=Vector2.Dot(p-start,delta)/delta.sqrMagnitude;if(t<0||t>1)continue;
                    var center=Vector2.Lerp(start,end,t);
                    Vector3 side3=Vector3.Lerp(a.points[a.points.Count-1].position-a.points[0].position,b.points[b.points.Count-1].position-b.points[0].position,t);
                    Vector2 side=new Vector2(side3.x,side3.z).normalized;
                    float signed=Vector2.Dot(p-center,side),distance=Mathf.Abs(signed);
                    float width=Mathf.Lerp(Vector3.Distance(a.points[0].position,a.points[a.points.Count-1].position),Vector3.Distance(b.points[0].position,b.points[b.points.Count-1].position),t);
                    if(distance>width*.5f+1.5f)continue;
                    float across=Mathf.Clamp01(signed/width+.5f);
                    void Section(MapWaterfallSection section,out float bed,out float water,out bool detached)
                    {
                        int col=0;
                        while(col<section.points.Count-2&&(section.points[col+1].across<0?(col+1)/(float)(section.points.Count-1):section.points[col+1].across)<across)col++;
                        var left=section.points[col];var right=section.points[col+1];
                        float u=Mathf.InverseLerp(left.across<0?col/(float)(section.points.Count-1):left.across,right.across<0?(col+1)/(float)(section.points.Count-1):right.across,across);
                        bool landing=(left.surfaceKey??"").StartsWith(MapWaterfallLanding.SurfacePrefix,System.StringComparison.Ordinal);
                        bed=Mathf.Lerp(left.position.y,right.position.y,u)-(landing?0:.012f);
                        water=Mathf.Lerp(left.Front(section.thickness).y,right.Front(section.thickness).y,u)-.018f;
                        detached=left.detached||right.detached;
                    }
                    Section(a,out float bedA,out float waterA,out bool detachedA);Section(b,out float bedB,out float waterB,out bool detachedB);
                    if(detachedA||detachedB)continue;
                    var profile=rivers[w.upstreamRiverId].record.sectionProfile;
                    var f=new MapRiverFrame{width=width,water=Mathf.Lerp(waterA,waterB,t),depth=Mathf.Lerp(a.thickness,b.thickness,t),leftTransition=1.5f,rightTransition=1.5f,sectionProfile=profile};
                    float target=distance<=width*.5f?Mathf.Lerp(bedA,bedB,t):MapRiverSection.Bed(f,distance,ground);
                    // The same bounded earthwork contract applies to contact strips.
                    ground=Mathf.Clamp(target,ground-profile.maximumCut,ground+profile.maximumFill);
                }
            }
            return ground;
        }
    }
}
