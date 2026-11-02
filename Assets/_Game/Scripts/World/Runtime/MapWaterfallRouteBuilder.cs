using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace LittleCastle.World
{
    /// <summary>Bounded corridor projection onto real triangles. Never edits its support or river records.</summary>
    public static class MapWaterfallRouteBuilder
    {
        public static MapWaterfallRouteData Build(MapRiverEndpoint upper, MapRiverEndpoint lower, MapWaterfallSupport support)
        {
            float span = Vector2.Distance(upper.position, lower.position);
            // Start below the rendering budget; refine only where actual triangle clearance needs it.
            const float spacing=.4f;
            int columns = Mathf.CeilToInt(Mathf.Max(upper.width, lower.width) / spacing); if (columns % 2 != 0) columns++;
            int rows = Mathf.Max(8, Mathf.CeilToInt(span / spacing));
            var along = new List<float>(); var across = new List<float>();
            for (int i = 0; i <= rows; i++) along.Add(i / (float)rows);
            for(int i=0;i<columns/2;i++){float p=i/(float)columns;across.Add(p);across.Add(1-p);}
            across.Add(.5f);across.Sort();
            InvalidDataException lastClearance=null;
            for (int iteration = 0; iteration < 160; iteration++)
            {
                try { var route=BuildCore(upper, lower, support, along, across);return lower.layoutVersion==3?MapWaterfallLanding.Blend(route,lower):route; }
                catch (InvalidDataException e)
                {
                    if (!e.Data.Contains("clearance-row"))
                    {
                        if(lastClearance!=null)foreach(var key in lastClearance.Data.Keys)e.Data["last-"+key]=lastClearance.Data[key];
                        e.Data["rows"]=along.Count;e.Data["columns"]=across.Count;throw;
                    }
                    lastClearance=e;
                    int row = (int)e.Data["clearance-row"], col = (int)e.Data["clearance-column"];
                    string axis = (string)e.Data["clearance-axis"];
                    bool changed = false;
                    if (axis != "across") changed |= Refine(along, row, false);
                    if (axis != "along") changed |= Refine(across, col, true);
                    if (!changed) throw;
                }
            }
            throw new InvalidDataException("Не удалось уточнить контакт в бюджете. Сузьте поток или выберите другой маршрут.");
        }
        static bool Refine(List<float> values, int end, bool symmetric)
        {
            if (end < 1 || end >= values.Count || values[end] - values[end - 1] < .00001f) return false;
            float middle = (values[end] + values[end - 1]) * .5f;
            bool changed=false;
            if(!values.Exists(v=>Mathf.Abs(v-middle)<1e-7f)){values.Add(middle);changed=true;}
            if(symmetric&&!values.Exists(v=>Mathf.Abs(v-(1-middle))<1e-7f)){values.Add(1-middle);changed=true;}
            values.Sort();return changed;
        }
        static MapWaterfallRouteData BuildCore(MapRiverEndpoint upper, MapRiverEndpoint lower, MapWaterfallSupport support,
            List<float> alongParameters, List<float> acrossParameters)
        {
            if (support == null || support.TriangleCount == 0) throw new InvalidDataException("Нет контактной геометрии в области водопада.");
            Vector2 delta = lower.position - upper.position;
            float span = delta.magnitude;
            if (span < .1f) throw new InvalidDataException("Для контактного маршрута разнесите кромку и чашу хотя бы на 10 см.");
            int columns = acrossParameters.Count - 1, rows = alongParameters.Count - 1;
            if ((columns + 1) * (rows + 1) > 65536) throw new InvalidDataException("Слишком сложный контактный коридор. Уменьшите ширину или длину.");
            var result = new MapWaterfallRouteData { supportRevision = support.Revision };
            Vector2 direction = delta / span;
            Vector3 across = new Vector3(-direction.y, 0, direction.x);
            float thickness = .2f;
            var projected = new MapWaterfallSupport.Hit[rows + 1][];
            var supported = new bool[rows + 1];
            var levels = new float[rows + 1][];
            for (int row = 0; row <= rows; row++)
            {
                float t = alongParameters[row], width = Mathf.Lerp(upper.width, lower.width, t);
                Vector2 center = Vector2.Lerp(upper.position, lower.position, t);
                projected[row] = new MapWaterfallSupport.Hit[columns + 1]; levels[row] = new float[columns + 1]; supported[row] = true;
                for (int col = 0; col <= columns; col++)
                {
                    Vector3 p = new Vector3(center.x, 0, center.y) + across * (width * (acrossParameters[col] - .5f));
                    bool found = support.Height(new Vector2(p.x, p.z), upper.waterHeight + 256, out var hit);
                    supported[row] &= found; projected[row][col] = hit;
                    levels[row][col] = found ? Mathf.Min(upper.waterHeight + .018f, hit.point.y + .06f + thickness / Mathf.Max(.25f,hit.normal.y)) : float.NegativeInfinity;
                }
            }
            // Retained shallow water fills small surface depressions rather than reversing uphill.
            // This is a geometric, downstream envelope, not a time-stepped fluid simulation.
            for (int col = 0; col <= columns; col++)
            {
                float level = lower.waterHeight + .018f;
                for (int row = rows - 1; row > 0; row--)
                    if (supported[row]) { level = Mathf.Max(level, levels[row][col]); levels[row][col] = level; }
            }
            FillSections(result,upper,lower,support,alongParameters,acrossParameters,projected,levels);
            result.Validate(upper, lower);
            // Keep the exposed surface fixed when a coarse back edge cuts a narrow
            // ridge: reduce only hidden water depth before spending more triangles.
            for(int attempt=0;attempt<256;attempt++)
            {
                try{ValidateClearance(result,support);return result;}
                catch(InvalidDataException e)
                {
                    if(!e.Data.Contains("clearance-row")||(bool)e.Data["front-surface"])throw;
                    int r=(int)e.Data["clearance-row"],c=(int)e.Data["clearance-column"];string axis=(string)e.Data["clearance-axis"];
                    var a=axis=="across"?result.sections[r].points[c-1]:result.sections[r-1].points[c];
                    var b=axis=="diagonal"?result.sections[r].points[c-1]:result.sections[r].points[c];
                    bool Lift(MapWaterfallContactPoint point,float fallback)
                    {
                        if(Vector3.Dot(point.normal,Vector3.up)<.9999f)return false;
                        float depth=point.thickness>0?point.thickness:fallback;
                        float lift=Mathf.Min(.04f,depth-.004f);if(lift<.0001f)return false;
                        point.position+=Vector3.up*lift;point.thickness=Mathf.Max(.004f,depth-lift);return true;
                    }
                    bool changed=Lift(a,result.sections[r-1].thickness);changed|=Lift(b,result.sections[r].thickness);
                    if(!changed)throw;
                }
            }
            ValidateClearance(result,support);
            return result;
        }

        static void FillSections(MapWaterfallRouteData result,MapRiverEndpoint upper,MapRiverEndpoint lower,MapWaterfallSupport support,
            List<float> along,List<float> acrossParameters,MapWaterfallSupport.Hit[][] projected,float[][] levels)
        {
            int rows=along.Count-1,columns=acrossParameters.Count-1;
            Vector2 delta=lower.position-upper.position;float span=delta.magnitude;Vector2 direction=delta/span;
            Vector3 across=new Vector3(-direction.y,0,direction.x);
            var flying=new bool[columns+1];var start=new float[columns+1];var elapsed=new float[columns+1];
            var horizontal=new float[columns+1];var vertical=new float[columns+1];
            const float depth=.2f;
            for(int row=0;row<=rows;row++)
            {
                float t=along[row],width=Mathf.Lerp(upper.width,lower.width,t),dx=row==0?0:span*(t-along[row-1]);
                Vector2 center=Vector2.Lerp(upper.position,lower.position,t);
                var section=new MapWaterfallSection{thickness=depth,speed=Mathf.Lerp(2,5,t)};
                var contacts=new MapWaterfallContactPoint[columns+1];float maximumSpeed=section.speed;
                for(int col=0;col<=columns;col++)
                {
                    var hit=projected[row][col];float fraction=acrossParameters[col];
                    Vector3 xz=new Vector3(center.x,0,center.y)+across*(width*(fraction-.5f));
                    var point=new MapWaterfallContactPoint{across=fraction,normal=Vector3.up};
                    if(row==0||row==rows)
                    {
                        var endpoint=row==0?upper:lower;Vector3 edge=new Vector3(-endpoint.tangent.y,0,endpoint.tangent.x);
                        Vector3 front=new Vector3(endpoint.position.x,endpoint.waterHeight+.018f,endpoint.position.y)+edge*(endpoint.width*(fraction-.5f));
                        point.thickness=hit.key!=null?Mathf.Clamp(front.y-hit.point.y-.005f,.004f,depth):depth;
                        point.position=front-Vector3.up*point.thickness;point.surfaceKey="river:"+endpoint.riverId;
                        section.points.Add(point);continue;
                    }
                    var previous=result.sections[row-1];var prior=previous.points[col];float priorY=prior.Front(previous.thickness).y;
                    bool found=hit.key!=null;
                    if(found)
                    {
                        float gap=Mathf.Min(.06f,Mathf.Max(.002f,(upper.waterHeight+.018f-hit.point.y)*.4f));
                        point.position=hit.point+Vector3.up*gap;point.thickness=Mathf.Max(.004f,levels[row][col]-point.position.y);
                        point.surfaceKey=hit.key;point.triangle=hit.triangle;point.barycentric=hit.barycentric;
                    }
                    contacts[col]=point.Copy();
                    if(!flying[col])
                    {
                        float oldDx=row>1?span*(along[row-1]-along[row-2]):dx;
                        float slope=row>1?(priorY-result.sections[row-2].points[col].Front(depth).y)/Mathf.Max(.0001f,oldDx):0;
                        // Contact advection supplies launch speed. A neighbouring falling
                        // thread's gravity speed must not accelerate the whole lip sideways.
                        float h=Mathf.Clamp(Mathf.Lerp(2,2.5f,t)/Mathf.Sqrt(1+slope*slope),.5f,2.2f),v=Mathf.Min(0,slope*h);
                        if(!found)
                        {
                            float duration=Mathf.Sqrt(2*Mathf.Max(.02f,priorY-lower.waterHeight-.018f)/9.81f);
                            h=Mathf.Clamp(span*(1-along[row-1])/duration,.5f,12);v=0;
                        }
                        float dt=dx/h;Vector3 normal=Vector3.Cross(across,new Vector3(direction.x,(v-9.81f*dt)/h,direction.y)).normalized;
                        Vector3 forecast=new Vector3(xz.x,priorY+v*dt-4.905f*dt*dt,xz.z)-normal*depth;
                        bool open=!found||row>1&&forecast.y>hit.point.y+.055f&&priorY+v*dt-4.905f*dt*dt>levels[row][col]+.02f;
                        if(open){flying[col]=true;start[col]=priorY;elapsed[col]=0;horizontal[col]=h;vertical[col]=v;}
                    }
                    if(flying[col])
                    {
                        elapsed[col]+=dx/horizontal[col];float velocity=vertical[col]-9.81f*elapsed[col];
                        float y=start[col]+vertical[col]*elapsed[col]-4.905f*elapsed[col]*elapsed[col];
                        Vector3 normal=Vector3.Cross(across,new Vector3(direction.x,velocity/horizontal[col],direction.y)).normalized;
                        Vector3 front=new Vector3(xz.x,y,xz.z),back=front-normal*depth;
                        bool clear=(!found||y>levels[row][col]+.02f)&&Clear(prior.position,back,support)&&Clear(prior.Front(previous.thickness),front,support);
                        if(support.Height(new Vector2(back.x,back.z),upper.waterHeight+256,out var below)&&back.y<=below.point.y+.02f)clear=false;
                        if(clear)
                        {
                            point=new MapWaterfallContactPoint{across=fraction,position=back,normal=normal,thickness=depth,detached=true};
                            maximumSpeed=Mathf.Max(maximumSpeed,Mathf.Clamp(Mathf.Sqrt(horizontal[col]*horizontal[col]+velocity*velocity),.05f,12));
                        }
                        else flying[col]=false;
                    }
                    if(!point.detached&&!found)throw new InvalidDataException("Нет безопасной траектории над разрывом опоры.");
                    if(!point.detached&&point.Front(depth).y>priorY+.02f)throw new InvalidDataException("Перед потоком препятствие выше воды. Перенесите кромку ближе к уступу.");
                    section.points.Add(point);
                }
                // A protruding neighbouring rock can stop one thread without gluing the
                // entire width to the cliff. Preserve contact for the obstructed strip.
                for(int col=1;col<=columns;col++)
                {
                    var a=section.points[col-1];var b=section.points[col];
                    if(Clear(a.position,b.position,support)&&Clear(a.Front(depth),b.Front(depth),support))continue;
                    foreach(int c in new[]{col-1,col})if(section.points[c].detached&&contacts[c]!=null)
                    {section.points[c]=contacts[c];flying[c]=false;}
                }
                section.speed=maximumSpeed;
                section.kind=section.points.Exists(p=>p.detached)?MapWaterfallSectionKind.FreeFall:MapWaterfallSectionKind.Contact;
                if(row==rows)section.kind=MapWaterfallSectionKind.Impact;
                result.sections.Add(section);
            }
        }

        public static void ValidateClearance(MapWaterfallRouteData route, MapWaterfallSupport support)
        {
            // Test lattice edges/diagonals on both surfaces, not just point depths.
            for (int row = 1; row < route.sections.Count; row++)
                for (int col = 0; col < route.sections[row].points.Count; col++)
                {
                    var a = route.sections[row - 1]; var b = route.sections[row];
                    Check(a.points[col].position, b.points[col].position, support, row, col, "along");
                    Check(a.points[col].Front(a.thickness), b.points[col].Front(b.thickness), support, row, col, "along",true);
                    if (col == 0) continue;
                    Check(b.points[col - 1].position, b.points[col].position, support, row, col, "across");
                    Check(b.points[col-1].Front(b.thickness),b.points[col].Front(b.thickness),support,row,col,"across",true);
                    Check(a.points[col].position, b.points[col - 1].position, support, row, col, "diagonal");
                    Check(a.points[col].Front(a.thickness), b.points[col - 1].Front(b.thickness), support, row, col, "diagonal",true);
                }
        }
        static void Check(Vector3 a, Vector3 b, MapWaterfallSupport support, int row, int col, string axis,bool front=false)
        {
            Vector3 d = b - a;
            if (d.magnitude <= .005f) return;
            a += d.normalized * .002f; b -= d.normalized * .002f;
            if (support.Segment(a, b, out var hit))
            {
                var e = new InvalidDataException("Вода пересекает скалу. Сузьте поток или выберите другой маршрут.");
                e.Data["clearance-row"] = row; e.Data["clearance-column"] = col; e.Data["clearance-axis"] = axis;
                e.Data["front-surface"]=front;
                e.Data["support-point"] = hit.point;e.Data["edge-a"]=a;e.Data["edge-b"]=b;throw e;
            }
        }
        static bool Clear(Vector3 a,Vector3 b,MapWaterfallSupport support)
        {
            Vector3 d=b-a;if(d.magnitude<=.005f)return true;
            return !support.Segment(a+d.normalized*.002f,b-d.normalized*.002f,out _);
        }
    }
}
