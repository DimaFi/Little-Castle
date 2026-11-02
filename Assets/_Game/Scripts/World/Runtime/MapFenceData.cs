using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace LittleCastle.World
{
    /// <summary>Authoritative authoring recipe. Meshes, posts and transforms are derived.</summary>
    [Serializable] public sealed class MapFenceRecord
    {
        public int version=1;
        public long id;
        public string variantId="fence.wood01";
        public float spacing=1.6f,smoothing=.85f;
        public float scale=1;
        public bool closed;
        public List<Vector2> points=new List<Vector2>();
    }

    public readonly struct MapFenceFrame
    {
        public readonly Vector2 position,tangent;
        public readonly float station;
        public MapFenceFrame(Vector2 position,Vector2 tangent,float station)
        {this.position=position;this.tangent=tangent;this.station=station;}
    }

    /// <summary>Deterministic smooth centreline, arc-length queries and equal post spacing.
    /// Straight inputs stay collinear. The same centreline drives both rail heights.</summary>
    public sealed class MapFencePath
    {
        public const float NativeRailLength=1.6f;
        public readonly MapFenceRecord record;
        public readonly List<MapFenceFrame> frames=new List<MapFenceFrame>();
        public readonly Rect bounds;
        public float Length=>frames[frames.Count-1].station;
        public int SpanCount=>Mathf.Max(1,Mathf.CeilToInt(Length/(record.spacing*record.scale)-.0001f));
        public int PostCount=>record.closed?SpanCount:SpanCount+1;
        public float ActualSpacing=>Length/SpanCount;
        static bool Finite(float v)=>!float.IsNaN(v)&&!float.IsInfinity(v);
        public static bool KnownVariant(string id)=>id=="fence.wood01"||id=="fence.wood02";

        public MapFencePath(MapFenceRecord value)
        {
            record=JsonUtility.FromJson<MapFenceRecord>(JsonUtility.ToJson(value));
            if(record==null||record.version!=1||record.id==0||!KnownVariant(record.variantId)||
                !Finite(record.spacing)||record.spacing<.8f||record.spacing>2.5f||
                !Finite(record.smoothing)||record.smoothing<.2f||record.smoothing>1||
                !Finite(record.scale)||record.scale<.5f||record.scale>1.5f||
                record.points==null||record.points.Count<(record.closed?3:2)||record.points.Count>512)
                throw new InvalidDataException("Неверная линия забора: материал, шаг, плавность или число точек.");
            foreach(var p in record.points)if(!Finite(p.x)||!Finite(p.y))throw new InvalidDataException("Неверная точка забора.");
            int count=record.points.Count,segments=record.closed?count:count-1;
            var tangents=new Vector2[count];
            for(int i=0;i<count;i++)
            {
                Vector2 a=record.points[(i+count-1)%count],b=record.points[(i+1)%count];
                if(!record.closed&&i==0)tangents[i]=(record.points[1]-record.points[0])*record.smoothing;
                else if(!record.closed&&i==count-1)tangents[i]=(record.points[i]-record.points[i-1])*record.smoothing;
                else tangents[i]=(b-a).normalized*Mathf.Min(Vector2.Distance(a,record.points[i]),Vector2.Distance(b,record.points[i]))*record.smoothing;
            }
            float length=0;Vector2 last=record.points[0];
            float minX=last.x,maxX=last.x,minY=last.y,maxY=last.y;
            for(int i=0;i<segments;i++)
            {
                int j=(i+1)%count;var a=record.points[i];var b=record.points[j];
                if((a-b).sqrMagnitude<.0001f)throw new InvalidDataException("Удалите повторяющиеся соседние точки забора.");
                int samples=Mathf.Max(2,Mathf.CeilToInt(Vector2.Distance(a,b)/.075f));
                if(samples>30000)throw new InvalidDataException("Слишком длинный пролёт забора.");
                for(int s=i==0?0:1;s<=samples;s++)
                {
                    float t=s/(float)samples,t2=t*t,t3=t2*t;
                    Vector2 p=(2*t3-3*t2+1)*a+(t3-2*t2+t)*tangents[i]+(-2*t3+3*t2)*b+(t3-t2)*tangents[j];
                    Vector2 d=(6*t2-6*t)*a+(3*t2-4*t+1)*tangents[i]+(-6*t2+6*t)*b+(3*t2-2*t)*tangents[j];
                    if(d.sqrMagnitude<1e-8f)d=b-a;
                    length+=Vector2.Distance(last,p);last=p;
                    if(length>2048||frames.Count>40000)throw new InvalidDataException("Разделите забор на линии длиной до 2048 м.");
                    frames.Add(new MapFenceFrame(p,d.normalized,length));
                    minX=Mathf.Min(minX,p.x);maxX=Mathf.Max(maxX,p.x);minY=Mathf.Min(minY,p.y);maxY=Mathf.Max(maxY,p.y);
                }
            }
            if(length<.25f)throw new InvalidDataException("Линия забора должна быть длиннее 25 см.");
            bounds=Rect.MinMaxRect(minX-.2f,minY-.2f,maxX+.2f,maxY+.2f);
        }
        public MapFenceFrame At(float station)
        {
            station=Mathf.Clamp(station,0,Length);int lo=0,hi=frames.Count-1;
            while(lo+1<hi){int m=(lo+hi)/2;if(frames[m].station<station)lo=m;else hi=m;}
            var a=frames[lo];var b=frames[hi];float u=(station-a.station)/Mathf.Max(.000001f,b.station-a.station);
            return new MapFenceFrame(Vector2.Lerp(a.position,b.position,u),Vector2.Lerp(a.tangent,b.tangent,u).normalized,station);
        }
        public float Distance(Vector2 point)
        {
            float nearest=float.PositiveInfinity;
            for(int i=1;i<frames.Count;i++)
            {
                var a=frames[i-1].position;var d=frames[i].position-a;
                var p=a+d*Mathf.Clamp01(Vector2.Dot(point-a,d)/Mathf.Max(1e-9f,d.sqrMagnitude));
                nearest=Mathf.Min(nearest,(point-p).magnitude);
            }
            return nearest;
        }
        /// <summary>Remove pointer oversampling before storing a recipe. No frame-rate-dependent meshes in saves.</summary>
        public static List<Vector2> Simplify(List<Vector2> points,float tolerance=.08f)
        {
            if(points.Count<3)return new List<Vector2>(points);
            var keep=new bool[points.Count];keep[0]=keep[points.Count-1]=true;
            var stack=new Stack<Vector2Int>();stack.Push(new Vector2Int(0,points.Count-1));
            while(stack.Count>0)
            {
                var range=stack.Pop();var a=points[range.x];var d=points[range.y]-a;
                float far=tolerance*tolerance;int index=-1;
                for(int i=range.x+1;i<range.y;i++)
                {
                    var p=a+d*Mathf.Clamp01(Vector2.Dot(points[i]-a,d)/Mathf.Max(1e-9f,d.sqrMagnitude));
                    float distance=(points[i]-p).sqrMagnitude;if(distance>far){far=distance;index=i;}
                }
                if(index<0)continue;keep[index]=true;stack.Push(new Vector2Int(range.x,index));stack.Push(new Vector2Int(index,range.y));
            }
            var result=new List<Vector2>();for(int i=0;i<points.Count;i++)if(keep[i])result.Add(points[i]);return result;
        }
    }
}
