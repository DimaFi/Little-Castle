using System.Collections.Generic;
using UnityEngine;

namespace LittleCastle.World
{
    public static class MapRiverPicking
    {
        /// <summary>Pick the mean water ribbon itself, including sloped surfaces.</summary>
        public static bool Pick(IEnumerable<MapRiverPath> paths,Ray ray,out long id,out Vector3 hit,out float station)
        {
            id=0;hit=default;station=0;float closest=float.PositiveInfinity;
            foreach(var path in paths)for(int i=1;i<path.frames.Count;i++)
            {
                var a=path.frames[i-1];var b=path.frames[i];
                Vector3 Point(MapRiverFrame f,float side){var p=f.Edge(side);return new Vector3(p.x,f.water+.018f,p.y);}
                var al=Point(a,-1);var ar=Point(a,1);var bl=Point(b,-1);var br=Point(b,1);
                float t;
                if(!Triangle(ray,al,ar,bl,out t)&&!Triangle(ray,ar,br,bl,out t))continue;
                if(t>=closest)continue;closest=t;id=path.record.id;hit=ray.GetPoint(t);
                var v=new Vector2(hit.x,hit.z)-a.center;var delta=b.center-a.center;
                station=Mathf.Lerp(a.station,b.station,Mathf.Clamp01(Vector2.Dot(v,delta)/Mathf.Max(.000001f,delta.sqrMagnitude)));
            }
            return id!=0;
        }
        static bool Triangle(Ray ray,Vector3 a,Vector3 b,Vector3 c,out float t)
        {
            t=0;var e=b-a;var f=c-a;var p=Vector3.Cross(ray.direction,f);float determinant=Vector3.Dot(e,p);
            if(Mathf.Abs(determinant)<.0000001f)return false;
            var s=ray.origin-a;float u=Vector3.Dot(s,p)/determinant;if(u<0||u>1)return false;
            var q=Vector3.Cross(s,e);float v=Vector3.Dot(ray.direction,q)/determinant;if(v<0||u+v>1)return false;
            t=Vector3.Dot(f,q)/determinant;return t>=0;
        }
    }
}
