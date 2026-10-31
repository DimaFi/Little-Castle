using System.Collections.Generic;
using System.Linq;
using LittleCastle.World;
using UnityEngine;

namespace LittleCastle.Rendering
{
    public readonly struct MapWaterfallSpraySource
    {
        public readonly int row;
        public readonly Vector3 position;
        public readonly float width,strength;
        public MapWaterfallSpraySource(int row,MapWaterfallSection section,float strength)
        {this.row=row;position=section.FrontCenter;width=Vector3.Distance(section.points[0].position,section.points[section.points.Count-1].position);this.strength=strength;}
    }
    public static class MapWaterfallSpraySources
    {
        public static int Budget(RiverEffectsQuality quality)=>quality==RiverEffectsQuality.Off?0:quality==RiverEffectsQuality.Low?40:quality==RiverEffectsQuality.Medium?96:176;
        public static List<MapWaterfallSpraySource> Resolve(MapWaterfallRouteData route)
        {
            var result=new List<MapWaterfallSpraySource>();if(route==null||route.IsEmpty)return result;
            var sections=route.sections;
            void Add(int row,float strength)
            {
                var source=new MapWaterfallSpraySource(row,sections[row],strength);
                float separation=Mathf.Clamp(source.width*.35f,1,2);
                for(int i=0;i<result.Count;i++)if(Vector3.SqrMagnitude(result[i].position-source.position)<separation*separation)
                {if(source.strength>result[i].strength)result[i]=source;return;}
                result.Add(source);
            }
            Add(0,.3f);Add(sections.Count-1,1);
            for(int i=1;i<sections.Count-1;i++)
            {
                var previous=sections[i-1];var current=sections[i];bool returns=false;
                for(int c=0;c<current.points.Count;c++)returns|=previous.points[c].detached&&!current.points[c].detached;
                var incoming=(current.FrontCenter-previous.FrontCenter).normalized;
                var outgoing=(sections[i+1].FrontCenter-current.FrontCenter).normalized;
                int before=i-1,after=i+1;
                while(before>0&&Vector3.Distance(current.FrontCenter,sections[before].FrontCenter)<1.2f)before--;
                while(after<sections.Count-1&&Vector3.Distance(current.FrontCenter,sections[after].FrontCenter)<1.2f)after++;
                var approach=(current.FrontCenter-sections[before].FrontCenter).normalized;
                var departure=(sections[after].FrontCenter-current.FrontCenter).normalized;
                if(returns||current.kind==MapWaterfallSectionKind.Impact||incoming.y<-.4f&&outgoing.y-incoming.y>.3f||approach.y<-.4f&&departure.y-approach.y>.18f)
                    Add(i,Mathf.Clamp01(.55f+outgoing.y-incoming.y));
            }
            result.Sort((a,b)=>{int strength=b.strength.CompareTo(a.strength);return strength!=0?strength:a.row.CompareTo(b.row);});
            if(result.Count>8)
            {
                var ends=result.Where(p=>p.row==0||p.row==sections.Count-1).ToList();
                result=result.Where(p=>p.row!=0&&p.row!=sections.Count-1).Take(8-ends.Count).Concat(ends).ToList();
            }
            result.Sort((a,b)=>a.row.CompareTo(b.row));return result;
        }
    }
}
