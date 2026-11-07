using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace LittleCastle.World
{
    [Serializable] public sealed class MapRiverCorridorAdjustment
    {public long nodeId;public float widthScale=1,transitionScale=1;}
    [Serializable] public sealed class MapRiverSpanAdjustment
    {public long startNodeId;public Vector2 offset;}
    [Serializable] public sealed class MapRiverHandle
    {
        public long nodeId;
        public Vector2 incoming,outgoing;
    }
    [Serializable] public sealed class MapRiverShape
    {
        public bool enabled;
        public float smoothing=1;
        public List<MapRiverHandle> handles=new List<MapRiverHandle>();
        public List<MapRiverCorridorAdjustment> corridor=new List<MapRiverCorridorAdjustment>();
        public List<MapRiverSpanAdjustment> spans=new List<MapRiverSpanAdjustment>();
        public void Validate(MapRiverRecord river)
        {
            if(!enabled)return;
            if(river.layoutVersion!=3||!float.IsFinite(smoothing)||smoothing<0||smoothing>1||handles==null||handles.Count>river.nodes.Count)
                throw new InvalidDataException("Неверные параметры формы мягкой реки.");
            var ids=new HashSet<long>();
            foreach(var h in handles)
                if(h==null||!ids.Add(h.nodeId)||!river.nodes.Exists(n=>n.id==h.nodeId)||!Finite(h.incoming)||!Finite(h.outgoing)||h.incoming.magnitude>1024||h.outgoing.magnitude>1024)
                    throw new InvalidDataException("Неверная ручка сплайна реки.");
            if(corridor==null||spans==null||corridor.Count>river.nodes.Count||spans.Count>=river.nodes.Count)throw new InvalidDataException("Неверная автокоррекция реки.");
            ids.Clear();foreach(var a in corridor)if(a==null||!ids.Add(a.nodeId)||!river.nodes.Exists(n=>n.id==a.nodeId)||!float.IsFinite(a.widthScale)||!float.IsFinite(a.transitionScale)||a.widthScale<.85f||a.widthScale>1||a.transitionScale<.5f||a.transitionScale>1)throw new InvalidDataException("Автокоррекция сечения вне пределов.");
            ids.Clear();foreach(var a in spans)if(a==null||!ids.Add(a.startNodeId)||!river.nodes.Exists(n=>n.id==a.startNodeId)||!Finite(a.offset)||a.offset.magnitude>1)throw new InvalidDataException("Обход препятствия вне пределов.");
        }
        static bool Finite(Vector2 p)=>float.IsFinite(p.x)&&float.IsFinite(p.y);
    }
    public static class MapRiverSpline
    {
        public static bool HasShape(MapRiverRecord river)=>river.shape!=null&&river.shape.enabled;
        public static Vector2 DefaultHandle(MapRiverRecord river,int index,bool outgoing)
        {
            var n=river.nodes[index];var before=index>0?river.nodes[index-1].position:2*n.position-river.nodes[index+1].position;
            var after=index+1<river.nodes.Count?river.nodes[index+1].position:2*n.position-river.nodes[index-1].position;
            return (after-before)/6*(outgoing?1:-1);
        }
        public static Vector2 Handle(MapRiverRecord river,int index,bool outgoing)
        {
            var h=HasShape(river)?river.shape.handles.Find(v=>v.nodeId==river.nodes[index].id):null;
            var result=h==null?DefaultHandle(river,index,outgoing):(outgoing?h.outgoing:h.incoming);
            int segment=outgoing?index:index-1;
            if(HasShape(river)&&segment>=0&&segment<river.nodes.Count-1)
            {var span=river.shape.spans.Find(v=>v.startNodeId==river.nodes[segment].id);if(span!=null)result+=span.offset;}
            return result;
        }
        public static Vector2 CorridorScale(MapRiverRecord river,int node)
        {var a=HasShape(river)?river.shape.corridor.Find(v=>v.nodeId==river.nodes[node].id):null;return a==null?Vector2.one:new Vector2(a.widthScale,a.transitionScale);}
        public static void SetHandle(MapRiverRecord river,int index,bool outgoing,Vector2 offset)
        {
            if(river.layoutVersion!=3)throw new InvalidOperationException("Сначала перейдите на мягкое русло.");
            if(!HasShape(river))river.shape=new MapRiverShape{enabled=true};
            long id=river.nodes[index].id;var h=river.shape.handles.Find(v=>v.nodeId==id);
            if(h==null){h=new MapRiverHandle{nodeId=id,incoming=DefaultHandle(river,index,false),outgoing=DefaultHandle(river,index,true)};river.shape.handles.Add(h);}
            if(outgoing)h.outgoing=offset;else h.incoming=offset;
        }
        public static Vector2 Evaluate(MapRiverRecord river,int segment,float t)
        {
            var a=river.nodes[segment].position;var d=river.nodes[segment+1].position;
            var b=a+Handle(river,segment,true);var c=d+Handle(river,segment+1,false);float u=1-t;
            var curve=u*u*u*a+3*u*u*t*b+3*u*t*t*c+t*t*t*d;
            return Vector2.Lerp(Vector2.Lerp(a,d,t),curve,HasShape(river)?river.shape.smoothing:1);
        }
    }
    // Exact pre-B v10 field order, including the unexpanded section profile.
    [Serializable] public sealed class MapRiverLegacyV10
    {
        public long id;public int seed,layoutVersion;
        public List<MapRiverNode> nodes;public List<MapBankPatch> patches;public List<MapRockInstance> rocks;
        public MapRiverSectionProfile sectionProfile;
        public static List<MapRiverLegacyV10> Copy(List<MapRiverRecord> rivers)
        {var result=new List<MapRiverLegacyV10>();foreach(var r in rivers)result.Add(new MapRiverLegacyV10{id=r.id,seed=r.seed,layoutVersion=r.layoutVersion,nodes=r.nodes,patches=r.patches,rocks=r.rocks,sectionProfile=r.sectionProfile});return result;}
    }
}
