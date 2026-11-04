using System;
using UnityEngine;

namespace LittleCastle.World
{
    public static class MapRiverNodeEditing
    {
        public static MapRiverRecord Copy(MapRiverRecord river)=>JsonUtility.FromJson<MapRiverRecord>(JsonUtility.ToJson(river));
        public static int Insert(MapRiverRecord river,int segment,float t,long id)
        {
            if(segment<0||segment>=river.nodes.Count-1||id==0||river.nodes.Exists(n=>n.id==id))throw new ArgumentException("Неверный сегмент или ID точки.");
            t=Mathf.Clamp(t,.05f,.95f);var a=river.nodes[segment];var d=river.nodes[segment+1];
            Vector2 p=MapRiverSpline.HasShape(river)?MapRiverSpline.Evaluate(river,segment,t):Cubic(river,segment,t);
            if(Vector2.Distance(a.position,p)<2||Vector2.Distance(p,d.position)<2)throw new InvalidOperationException("Для новой точки нужно хотя бы 2 м до соседних узлов.");
            var n=new MapRiverNode{id=id,position=p,width=Mathf.Lerp(a.width,d.width,t),depth=Mathf.Lerp(a.depth,d.depth,t),waterHeight=Mathf.Lerp(a.waterHeight,d.waterHeight,t),speed=Mathf.Lerp(a.speed,d.speed,t),leftTransition=Mathf.Lerp(a.leftTransition,d.leftTransition,t),rightTransition=Mathf.Lerp(a.rightTransition,d.rightTransition,t),leftBank=t<.5f?a.leftBank:d.leftBank,rightBank=t<.5f?a.rightBank:d.rightBank};
            // Bake the effective cubic first, then split it with de Casteljau.
            // Both adjacent spans and every other span retain the same curve.
            if(river.layoutVersion==3)
            {
                var corridor=MapRiverSpline.HasShape(river)?river.shape.corridor:null;
                var newScale=Vector2.Lerp(MapRiverSpline.CorridorScale(river,segment),MapRiverSpline.CorridorScale(river,segment+1),t);
                float smooth=MapRiverSpline.HasShape(river)?river.shape.smoothing:1;
                var incoming=new Vector2[river.nodes.Count];var outgoing=new Vector2[river.nodes.Count];
                for(int i=0;i<river.nodes.Count;i++)
                {
                    incoming[i]=MapRiverSpline.Handle(river,i,false);outgoing[i]=MapRiverSpline.Handle(river,i,true);
                    if(i>0)incoming[i]=Vector2.Lerp((river.nodes[i-1].position-river.nodes[i].position)/3,incoming[i],smooth);
                    if(i+1<river.nodes.Count)outgoing[i]=Vector2.Lerp((river.nodes[i+1].position-river.nodes[i].position)/3,outgoing[i],smooth);
                }
                river.shape=new MapRiverShape{enabled=true};
                if(corridor!=null)foreach(var adjustment in corridor)river.shape.corridor.Add(new MapRiverCorridorAdjustment{nodeId=adjustment.nodeId,widthScale=adjustment.widthScale,transitionScale=adjustment.transitionScale});
                if(newScale!=Vector2.one)river.shape.corridor.Add(new MapRiverCorridorAdjustment{nodeId=id,widthScale=newScale.x,transitionScale=newScale.y});
                for(int i=0;i<river.nodes.Count;i++){MapRiverSpline.SetHandle(river,i,false,incoming[i]);MapRiverSpline.SetHandle(river,i,true,outgoing[i]);}
                var b=a.position+outgoing[segment];var c=d.position+incoming[segment+1];
                var ab=Vector2.Lerp(a.position,b,t);var bc=Vector2.Lerp(b,c,t);var cd=Vector2.Lerp(c,d.position,t);
                var abc=Vector2.Lerp(ab,bc,t);var bcd=Vector2.Lerp(bc,cd,t);n.position=Vector2.Lerp(abc,bcd,t);
                MapRiverSpline.SetHandle(river,segment,true,ab-a.position);MapRiverSpline.SetHandle(river,segment+1,false,cd-d.position);
                river.nodes.Insert(segment+1,n);MapRiverSpline.SetHandle(river,segment+1,false,abc-n.position);MapRiverSpline.SetHandle(river,segment+1,true,bcd-n.position);
            }
            else river.nodes.Insert(segment+1,n);
            return segment+1;
        }
        static Vector2 Cubic(MapRiverRecord r,int i,float t)
        {
            var a=r.nodes[i].position;var d=r.nodes[i+1].position;var b=a+MapRiverSpline.DefaultHandle(r,i,true);var c=d+MapRiverSpline.DefaultHandle(r,i+1,false);float u=1-t;
            return u*u*u*a+3*u*u*t*b+3*u*t*t*c+t*t*t*d;
        }
        public static void Remove(MapRiverRecord river,int index)
        {
            if(river.nodes.Count<=2)throw new InvalidOperationException("У реки должны остаться минимум две точки.");
            long id=river.nodes[index].id;river.nodes.RemoveAt(index);river.shape?.handles.RemoveAll(h=>h.nodeId==id);river.shape?.corridor.RemoveAll(h=>h.nodeId==id);river.shape?.spans.RemoveAll(h=>h.startNodeId==id);
        }
    }
}
