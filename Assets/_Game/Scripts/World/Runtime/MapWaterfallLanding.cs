using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace LittleCastle.World
{
    /// <summary>Saved, bounded landing geometry. Every strip meets the receiving river's level and slope.</summary>
    public static class MapWaterfallLanding
    {
        public const string SurfacePrefix="river-landing:";
        public static MapWaterfallRouteData Blend(MapWaterfallRouteData source,MapRiverEndpoint lower)
        {
            if(source==null||source.IsEmpty||lower.layoutVersion!=3)return source?.Copy();
            var route=source.Copy();int last=route.sections.Count-1,columns=route.sections[0].points.Count;
            if(last<3)return route;
            var distances=new float[last+1];
            for(int i=last-1;i>=0;i--)distances[i]=distances[i+1]+Horizontal(route.sections[i].FrontCenter,route.sections[i+1].FrontCenter);
            int start=last-1;while(start>1&&distances[start]<2)start--;
            float length=distances[start];if(length<.15f)return route;
            for(int i=start;i<=last;i++)foreach(var point in route.sections[i].points)
            {
                string key=point.surfaceKey??"";
                // A rock asset cannot be carved by the map's terrain layer.
                if(point.detached||!key.StartsWith("terrain:",StringComparison.Ordinal)&&!key.StartsWith("river:",StringComparison.Ordinal)&&!key.StartsWith(SurfacePrefix,StringComparison.Ordinal))return route;
            }
            int triangles=(route.sections.Count-1)*(columns-1)*4+(route.sections.Count-1)*4+(columns-1)*4;
            int available=(11200-triangles)/((columns-1)*4+4);
            var samples=new List<(float distance,MapWaterfallSection section)>();
            for(int i=0;i<=last;i++)samples.Add((distances[i],route.sections[i]));
            // Short final faces approximate the common tangent without an oversized extra mesh.
            foreach(float distance in new[]{.1f,.025f})if(available>0&&distance<length&&!samples.Exists(s=>Mathf.Abs(s.distance-distance)<.002f))
            {
                int before=last-1;while(before>start&&distances[before]<distance)before--;
                float t=Mathf.InverseLerp(distances[before],distances[before+1],distance);
                var a=route.sections[before];var b=route.sections[before+1];var section=a.Copy();section.kind=MapWaterfallSectionKind.Contact;
                for(int c=0;c<columns;c++)
                {
                    var p=section.points[c];float depth=Mathf.Lerp(a.points[c].thickness>0?a.points[c].thickness:a.thickness,b.points[c].thickness>0?b.points[c].thickness:b.thickness,t);
                    var front=Vector3.Lerp(a.points[c].Front(a.thickness),b.points[c].Front(b.thickness),t);
                    p.normal=Vector3.Lerp(a.points[c].normal,b.points[c].normal,t).normalized;p.thickness=depth;p.position=front-p.normal*depth;
                }
                samples.Add((distance,section));available--;
            }
            var first=source.sections[start];var prior=source.sections[start-1];
            var startY=new float[columns];var initialSlope=new float[columns];
            float previousDistance=Mathf.Max(.0001f,Horizontal(first.FrontCenter,prior.FrontCenter));
            for(int c=0;c<columns;c++)
            {startY[c]=first.points[c].Front(first.thickness).y;initialSlope[c]=(startY[c]-prior.points[c].Front(prior.thickness).y)/previousDistance;}
            samples.Sort((a,b)=>b.distance.CompareTo(a.distance));
            foreach(var sample in samples)
            {
                if(sample.distance>length+.0001f)continue;
                var section=sample.section;float t=Mathf.Clamp01(1-sample.distance/length);float endpoint=lower.waterHeight+.018f;
                float join=Mathf.SmoothStep(0,1,t);section.speed=Mathf.Lerp(section.speed,lower.speed,join);
                for(int c=0;c<columns;c++)
                {
                    var p=section.points[c];Vector3 front=p.Front(section.thickness);float original=front.y;
                    float secant=(endpoint-startY[c])/length,m0=initialSlope[c],m1=lower.surfaceSlope;
                    if(secant>1e-5f)throw new InvalidDataException("Сопряжение чаши идёт вверх. Измените нижний уровень.");
                    if(Mathf.Abs(secant)<1e-5f)m0=m1=0;
                    else
                    {
                        m0=Mathf.Clamp(m0,3*secant,0);m1=Mathf.Clamp(m1,3*secant,0);
                        float magnitude=Mathf.Sqrt(Mathf.Pow(m0/secant,2)+Mathf.Pow(m1/secant,2));if(magnitude>3){m0*=3/magnitude;m1*=3/magnitude;}
                    }
                    float t2=t*t,t3=t2*t;
                    front.y=(2*t3-3*t2+1)*startY[c]+(t3-2*t2+t)*length*m0+(-2*t3+3*t2)*endpoint+(t3-t2)*length*m1;
                    if(original-front.y>lower.maximumCut+.001f||front.y-original>lower.maximumFill+.001f)throw new InvalidDataException("Плавное впадение превышает предел локальной правки русла.");
                    float slope=((6*t2-6*t)*startY[c]+(3*t2-4*t+1)*length*m0+(-6*t2+6*t)*endpoint+(3*t2-2*t)*length*m1)/length;
                    var tangent=lower.tangent;var normal=new Vector3(-tangent.x*slope,1,-tangent.y*slope).normalized;
                    p.normal=Vector3.Lerp(normal,Vector3.up,Mathf.SmoothStep(.8f,1,t)).normalized;
                    float across=p.across>=0?p.across:c/(float)(columns-1);
                    float channelDepth=lower.depth*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.3f,1,Mathf.Abs(across*2-1))));
                    float thickness=Mathf.Lerp(p.thickness>0?p.thickness:section.thickness,channelDepth+.018f,join);
                    p.position=front-p.normal*thickness;p.thickness=thickness;
                    p.surfaceKey=SurfacePrefix+lower.riverId;p.triangle=-1;p.barycentric=Vector3.zero;p.detached=false;
                }
            }
            route.sections.Clear();foreach(var sample in samples)route.sections.Add(sample.section);
            return route;
        }
        static float Horizontal(Vector3 a,Vector3 b)=>Vector2.Distance(new Vector2(a.x,a.z),new Vector2(b.x,b.z));
    }
}
