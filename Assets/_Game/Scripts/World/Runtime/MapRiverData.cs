using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace LittleCastle.World
{
    public enum MapBankKind { Cliff, Sand }
    public enum MapRiverConnection { Open, Source, Outlet, Drop }
    [Serializable] public sealed class MapRiverNode
    {
        public long id;
        public Vector2 position;
        public float width=9,depth=2,waterHeight=-1,speed=1;
        public float leftTransition=5,rightTransition=5;
        public MapBankKind leftBank,rightBank;
        public MapRiverConnection connection;
    }
    [Serializable] public sealed class MapBankPatch
    {
        public long id;
        // Distance along the sampled river, in metres. Both sides + crossChannel is a ford.
        public float station,radius=5,shallowDepth=.25f;
        public bool left=true,right,crossChannel;
    }
    [Serializable] public sealed class MapRockInstance
    {
        public long id;
        public string archetypeId;
        public int catalogVersion=1;
        public Vector2 position;
        public float yaw,scale=1,topOffset,station,bankOffset;
        public int side;
        public bool manual,removed;
    }
    [Serializable] public sealed class MapRiverRecord
    {
        public long id;
        public int seed=1729,layoutVersion=1;
        public List<MapRiverNode> nodes=new List<MapRiverNode>();
        public List<MapBankPatch> patches=new List<MapBankPatch>();
        public List<MapRockInstance> rocks=new List<MapRockInstance>();
    }
    [Serializable] public sealed class MapRockSection
    {
        public long id;
        public int seed=1729;
        public List<Vector2> points=new List<Vector2>();
        public List<MapRockInstance> rocks=new List<MapRockInstance>();
    }
    [Serializable] public sealed class MapRockDefinition
    {
        public string id;
        public int version=1;
        // Normalized prefab: length +Z, base Y=0, front -X. Contact volumes, not guaranteed sockets.
        public float length=6,height=2.6f,thickness=2.6f,embed=.35f,overlap=.8f;
        public float minimumScale=.9f,maximumScale=1.1f,maxTurn=25,weight=1;
        public bool allowReverse=true;
    }
    public struct MapRiverFrame
    {
        public Vector2 center,tangent;
        public float station,width,depth,water,speed,leftTransition,rightTransition,leftSand,rightSand,ford,shallowDepth;
        public Vector2 Left=>new Vector2(-tangent.y,tangent.x);
        public Vector2 Edge(float side)=>center+Left*(width*.5f*side);
    }
    /// <summary>Deterministic Catmull-Rom strip with explicit validity checks. No scene dependencies.</summary>
    public sealed class MapRiverPath
    {
        public readonly MapRiverRecord record;
        public readonly List<MapRiverFrame> frames=new List<MapRiverFrame>();
        public readonly Rect bounds;
        readonly Dictionary<ChunkCoordinate,List<int>> segmentIndex=new Dictionary<ChunkCoordinate,List<int>>();
        public float Length=>frames[frames.Count-1].station;
        static Vector2 Curve(Vector2 a,Vector2 b,Vector2 c,Vector2 d,float t)=>.5f*((2*b)+(-a+c)*t+(2*a-5*b+4*c-d)*t*t+(-a+3*b-3*c+d)*t*t*t);
        public MapRiverPath(MapRiverRecord r)
        {
            record=r;
            if(r==null||r.id==0||r.layoutVersion!=1||r.nodes==null||r.nodes.Count<2||r.nodes.Count>1024||r.patches==null||r.rocks==null)throw new InvalidDataException("Река: нужны 2–1024 контрольные точки и версия профиля 1.");
            foreach(var node in r.nodes)if(node==null)throw new InvalidDataException("Река: отсутствует контрольная точка.");
            var ids=new HashSet<long>();Vector2 lo=r.nodes[0].position,hi=lo;
            float length=0;Vector2 previous=lo;
            for(int i=0;i<r.nodes.Count;i++)
            {
                var n=r.nodes[i];
                if(n==null||n.id==0||!ids.Add(n.id)||!Finite(n.position.x)||!Finite(n.position.y)||!Range(n.width,4,60)||!Range(n.depth,.15f,10)||!Finite(n.waterHeight)||!Range(n.speed,.05f,6)||!Range(n.leftTransition,2,30)||!Range(n.rightTransition,2,30)||(int)n.leftBank<0||(int)n.leftBank>1||(int)n.rightBank<0||(int)n.rightBank>1||(int)n.connection<0||(int)n.connection>3)throw new InvalidDataException("Река: неверная точка, ширина, глубина или переход берега.");
                if(i==r.nodes.Count-1)continue;
                var next=r.nodes[i+1];float distance=Vector2.Distance(n.position,next.position);
                if(distance<2||distance>1024)throw new InvalidDataException("Река: расстояние между точками должно быть 2–1024 м.");
                if(!Finite(next.waterHeight)||next.waterHeight>n.waterHeight+.001f||Mathf.Abs(next.waterHeight-n.waterHeight)/distance>.15f)throw new InvalidDataException("Вода должна течь вниз с уклоном до 15%. Для водопада нужны отдельные соединённые уровни.");
                int steps=Mathf.CeilToInt(distance/.75f);
                for(int j=0;j<steps;j++)
                {
                    float t=j/(float)steps;
                    Vector2 a=i>0?r.nodes[i-1].position:2*n.position-next.position;
                    Vector2 d=i+2<r.nodes.Count?r.nodes[i+2].position:2*next.position-n.position;
                    var p=Curve(a,n.position,next.position,d,t);
                    var tangent=(Curve(a,n.position,next.position,d,Mathf.Min(1,t+.001f))-Curve(a,n.position,next.position,d,Mathf.Max(0,t-.001f))).normalized;
                    if(tangent.sqrMagnitude<.9f)throw new InvalidDataException("Река: вырожденный изгиб.");
                    length+=Vector2.Distance(previous,p);previous=p;
                    if(frames.Count>=8192)throw new InvalidDataException("Река: более 8192 выборок. Разделите длинную реку на участки.");
                    var f=new MapRiverFrame{center=p,tangent=tangent,station=length,width=Mathf.Lerp(n.width,next.width,t),depth=Mathf.Lerp(n.depth,next.depth,t),water=Mathf.Lerp(n.waterHeight,next.waterHeight,t),speed=Mathf.Lerp(n.speed,next.speed,t),leftTransition=Mathf.Lerp(n.leftTransition,next.leftTransition,t),rightTransition=Mathf.Lerp(n.rightTransition,next.rightTransition,t),leftSand=Mathf.Lerp((float)n.leftBank,(float)next.leftBank,t),rightSand=Mathf.Lerp((float)n.rightBank,(float)next.rightBank,t),shallowDepth=.25f};frames.Add(f);
                }
            }
            var last=r.nodes[r.nodes.Count-1];var lf=frames[frames.Count-1];lf.station+=Vector2.Distance(lf.center,last.position);lf.center=last.position;lf.width=last.width;lf.depth=last.depth;lf.water=last.waterHeight;lf.speed=last.speed;lf.leftTransition=last.leftTransition;lf.rightTransition=last.rightTransition;lf.leftSand=(float)last.leftBank;lf.rightSand=(float)last.rightBank;frames.Add(lf);
            foreach(var patch in r.patches)
                if(patch==null||patch.id==0||!ids.Add(patch.id)||!Range(patch.station,0,Length)||!Range(patch.radius,2,30)||!Range(patch.shallowDepth,.1f,.5f)||(!patch.left&&!patch.right)||(patch.crossChannel&&(!patch.left||!patch.right)))throw new InvalidDataException("Берег/брод: неверный участок. Брод требует оба спуска.");
            for(int i=0;i<frames.Count;i++)
            {
                var f=frames[i];foreach(var patch in r.patches)
                {
                    float w=1-Mathf.SmoothStep(0,1,Mathf.Clamp01((Mathf.Abs(f.station-patch.station)-patch.radius*.6f)/(patch.radius*.4f)));
                    if(patch.left)f.leftSand=Mathf.Max(f.leftSand,w);if(patch.right)f.rightSand=Mathf.Max(f.rightSand,w);
                    if(patch.crossChannel&&w>f.ford){f.ford=w;f.shallowDepth=patch.shallowDepth;}
                }
                frames[i]=f;float extent=f.width*.5f+Mathf.Max(f.leftTransition,f.rightTransition)+3;
                lo=Vector2.Min(lo,f.center-Vector2.one*extent);hi=Vector2.Max(hi,f.center+Vector2.one*extent);
                if(i==0)continue;
                var p=frames[i-1];
                foreach(float side in new[]{-1f,1f})
                {
                    var edge=f.Edge(side)-p.Edge(side);
                    if(Vector2.Dot(edge,f.center-p.center)<=.00001f)throw new InvalidDataException("Река: изгиб слишком тесный для ширины. Раздвиньте точки или уменьшите ширину.");
                    float ft=side>0?f.leftTransition:f.rightTransition,pt=side>0?p.leftTransition:p.rightTransition;
                    var shoulder=f.Edge(side)+f.Left*ft*side-p.Edge(side)-p.Left*pt*side;
                    if(Vector2.Dot(shoulder,f.center-p.center)<=.00001f)throw new InvalidDataException("Берег складывается на изгибе. Уменьшите переход или расширьте поворот.");
                }
            }
            bounds=Rect.MinMaxRect(lo.x,lo.y,hi.x,hi.y);
            for(int i=0;i<frames.Count-1;i++)
            {
                var a=frames[i];var b=frames[i+1];float extent=Mathf.Max(a.width,b.width)*.5f+Mathf.Max(Mathf.Max(a.leftTransition,a.rightTransition),Mathf.Max(b.leftTransition,b.rightTransition))+3;
                var min=Vector2.Min(a.center,b.center)-Vector2.one*extent;var max=Vector2.Max(a.center,b.center)+Vector2.one*extent;
                for(int z=Mathf.FloorToInt(min.y/8);z<=Mathf.FloorToInt(max.y/8);z++)for(int x=Mathf.FloorToInt(min.x/8);x<=Mathf.FloorToInt(max.x/8);x++){var key=new ChunkCoordinate(x,z);if(!segmentIndex.TryGetValue(key,out var list))segmentIndex.Add(key,list=new List<int>());list.Add(i);}
            }
            // Nonlocal centre segments may not approach closer than the complete river corridor.
            var checkedPairs=new HashSet<long>();
            foreach(var candidates in segmentIndex.Values)foreach(int i in candidates)foreach(int j in candidates)
            {
                if(j<=i+1||!checkedPairs.Add(((long)i<<32)|(uint)j))continue;
                float clearance=(frames[i].width+frames[j].width)*.5f+2;
                if(frames[j].station-frames[i].station<clearance*2)continue;
                float distance=SegmentDistance(frames[i].center,frames[i+1].center,frames[j].center,frames[j+1].center);
                if(distance<clearance)throw new InvalidDataException("Река пересекает себя или соседний берег. Раздвиньте изгибы.");
            }
        }
        static bool Finite(float v)=>!float.IsNaN(v)&&!float.IsInfinity(v);
        static bool Range(float v,float lo,float hi)=>Finite(v)&&v>=lo&&v<=hi;
        static float Cross(Vector2 a,Vector2 b)=>a.x*b.y-a.y*b.x;
        public static float SegmentDistance(Vector2 a,Vector2 b,Vector2 c,Vector2 d)
        {
            var ab=b-a;var cd=d-c;float det=Cross(ab,cd);
            if(Mathf.Abs(det)>.00001f){float t=Cross(c-a,cd)/det,u=Cross(c-a,ab)/det;if(t>=0&&t<=1&&u>=0&&u<=1)return 0;}
            return Mathf.Min(Distance(a,c,d),Distance(b,c,d),Distance(c,a,b),Distance(d,a,b));
        }
        static float Distance(Vector2 p,Vector2 a,Vector2 b)=>Vector2.Distance(p,Vector2.Lerp(a,b,Mathf.Clamp01(Vector2.Dot(p-a,b-a)/(b-a).sqrMagnitude)));
        public MapRiverFrame At(float station)
        {
            int lo=0,hi=frames.Count-1;while(lo+1<hi){int m=(lo+hi)/2;if(frames[m].station<station)lo=m;else hi=m;}
            var a=frames[lo];var b=frames[hi];return Blend(a,b,Mathf.InverseLerp(a.station,b.station,station));
        }
        public bool Nearest(Vector2 p,out MapRiverFrame frame,out float signedDistance)
        {
            frame=default;signedDistance=0;if(!bounds.Contains(p))return false;float best=float.PositiveInfinity;
            if(!segmentIndex.TryGetValue(new ChunkCoordinate(Mathf.FloorToInt(p.x/8),Mathf.FloorToInt(p.y/8)),out var candidates))return false;
            foreach(int i in candidates)
            {
                var a=frames[i];var b=frames[i+1];var d=b.center-a.center;float t=Mathf.Clamp01(Vector2.Dot(p-a.center,d)/d.sqrMagnitude);var c=Vector2.Lerp(a.center,b.center,t);float sq=(p-c).sqrMagnitude;
                if(sq>=best)continue;best=sq;frame=Blend(a,b,t);signedDistance=Vector2.Dot(p-c,frame.Left);
            }
            // Round finite end caps; prevent an infinite lateral projection past endpoints.
            return best<Mathf.Pow(frame.width*.5f+Mathf.Max(frame.leftTransition,frame.rightTransition)+3,2);
        }
        static MapRiverFrame Blend(MapRiverFrame a,MapRiverFrame b,float t)=>new MapRiverFrame{center=Vector2.Lerp(a.center,b.center,t),tangent=Vector2.Lerp(a.tangent,b.tangent,t).normalized,station=Mathf.Lerp(a.station,b.station,t),width=Mathf.Lerp(a.width,b.width,t),depth=Mathf.Lerp(a.depth,b.depth,t),water=Mathf.Lerp(a.water,b.water,t),speed=Mathf.Lerp(a.speed,b.speed,t),leftTransition=Mathf.Lerp(a.leftTransition,b.leftTransition,t),rightTransition=Mathf.Lerp(a.rightTransition,b.rightTransition,t),leftSand=Mathf.Lerp(a.leftSand,b.leftSand,t),rightSand=Mathf.Lerp(a.rightSand,b.rightSand,t),ford=Mathf.Lerp(a.ford,b.ford,t),shallowDepth=Mathf.Lerp(a.shallowDepth,b.shallowDepth,t)};
        public static float Bed(MapRiverFrame f,float distance,float ground)
        {
            float half=f.width*.5f,d=Mathf.Abs(distance),sand=distance>=0?f.leftSand:f.rightSand,transition=distance>=0?f.leftTransition:f.rightTransition;
            float depth=Mathf.Lerp(f.depth,f.shallowDepth,f.ford);
            float bed=f.water-depth;float shelf=f.water-Mathf.Lerp(.7f,.12f,sand);
            if(d<half)return Mathf.Lerp(bed,shelf,Mathf.SmoothStep(0,1,Mathf.InverseLerp(half*.48f,half,d)))*(1-f.ford)+(f.water-f.shallowDepth)*f.ford;
            float u=Mathf.Clamp01((d-half)/transition);
            float rock=Mathf.SmoothStep(0,1,Mathf.Clamp01(u*3)),ramp=Mathf.SmoothStep(0,1,u);
            return Mathf.Lerp(shelf,ground,Mathf.Lerp(rock,ramp,sand));
        }
    }
}
