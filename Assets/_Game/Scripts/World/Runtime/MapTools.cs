using System;
using System.Collections.Generic;
using LittleCastle.Rendering;
using UnityEngine;

namespace LittleCastle.World
{
    [Flags] public enum MapToolControls { None=0, Radius=1, Strength=2, Softness=4, Brush=7 }
    public enum MapToolGesture { Stroke, Click, ControlPoints }

    [Serializable] public sealed class MapBrushSettings
    {
        public float radius=1.5f, strength=1, softness=.38f;
        public MapBrushSettings Copy()=>new MapBrushSettings{radius=radius,strength=strength,softness=softness};
        public void Validate(float maximumRadius)
        {
            if(!Finite(radius)||radius<.1f||radius>maximumRadius||!Finite(strength)||strength<0||strength>1||!Finite(softness)||softness<0||softness>1)
                throw new ArgumentOutOfRangeException(nameof(MapBrushSettings));
        }
        static bool Finite(float x)=>!float.IsNaN(x)&&!float.IsInfinity(x);
    }

    /// <summary>Same XZ falloff for data operations and their ground-conforming preview.</summary>
    public static class MapBrushFalloff
    {
        public static float Weight(float distance,float radius,float softness)
        {
            if(distance>=radius)return 0;
            if(softness<=0)return 1;
            return 1-Mathf.SmoothStep(0,1,Mathf.Clamp01((distance/radius-(1-softness))/softness));
        }
        // Zero on old serialized strokes means the original 62% core / 38% soft edge.
        public static float SurfaceWeight(SurfacePaintStroke s,float distance)=>Weight(distance,s.radius,s.brushSoftness==0?.38f:s.brushSoftness-.001f)*s.strength;
        public static void Outline(MapEditSession session,Vector2 center,float radius,Vector3[] points)
        {
            for(int i=0;i<points.Length;i++)
            {
                float a=i*Mathf.PI*2/(points.Length-1);float x=center.x+Mathf.Cos(a)*radius,z=center.y+Mathf.Sin(a)*radius;
                points[i]=new Vector3(x,session.SampleHeight(x,z)+.05f,z);
            }
        }
    }

    public readonly struct MapToolPointer
    {
        public readonly Ray ray;
        public readonly bool hasSurface;
        public readonly Vector2 position;
        public MapToolPointer(Ray ray,bool hasSurface,Vector2 position){this.ray=ray;this.hasSurface=hasSurface;this.position=position;}
    }

    /// <summary>Specialized data stays beside the common settings; no Editor or input dependencies.</summary>
    public sealed class MapToolContext
    {
        public readonly MapEditSession session;
        public MapBrushSettings settings=new MapBrushSettings();
        public float density=1,terrainRate=1.5f,treeSpacing=6;
        public bool scatter=true;
        public MeadowScatterProfile scatterProfile;
        public string treePackId;
        public int seed=1729;
        public MapToolContext(MapEditSession session){this.session=session??throw new ArgumentNullException(nameof(session));}
    }

    public interface IMapTool
    {
        string Id {get;}
        string Title {get;}
        string Category {get;}
        MapToolControls Controls {get;}
        MapToolGesture Gesture {get;}
        bool RequiresSurface {get;}
        bool RepeatAtRest {get;}
        float MaximumRadius(MapEditSession session);
        void Begin(MapToolContext context,MapToolPointer pointer);
        void Apply(MapToolContext context,MapToolPointer pointer,float seconds);
        void End(MapToolContext context,bool cancel);
    }

    public sealed class MapToolRegistry
    {
        readonly List<IMapTool> tools=new List<IMapTool>();
        readonly Dictionary<string,IMapTool> byId=new Dictionary<string,IMapTool>(StringComparer.Ordinal);
        public IReadOnlyList<IMapTool> Tools=>tools;
        public void Register(IMapTool tool)
        {
            if(tool==null||string.IsNullOrWhiteSpace(tool.Id)||byId.ContainsKey(tool.Id))throw new ArgumentException("Tool ID must be unique.");
            byId.Add(tool.Id,tool);tools.Add(tool);
        }
        public IMapTool Get(string id)=>byId.TryGetValue(id,out var tool)?tool:throw new ArgumentException("Unknown map tool: "+id);
        public static MapToolRegistry ExistingBrushes()
        {
            var r=new MapToolRegistry();r.Register(new MapSurfaceTool("surface.road","Дорожка",true,false));
            r.Register(new MapSurfaceTool("surface.meadow","Луг",false,false));r.Register(new MapSurfaceTool("surface.erase","Стереть траву",false,true));
            r.Register(new MapHeightTool(false));r.Register(new MapHeightTool(true));r.Register(new MapGroveTool());return r;
        }
    }

    /// <summary>Shared lifecycle, world-space spacing and bounded work for either input adapter.</summary>
    public sealed class MapToolController
    {
        public const float Interval=1f/15;
        public const int MaximumDabsPerTick=32;
        public readonly MapToolRegistry registry;
        public readonly MapToolContext context;
        public IMapTool Selected {get;private set;}
        public bool Active {get;private set;}
        public bool Finishing {get;private set;}
        MapBrushSettings authoredSettings;
        MapToolPointer last,target;
        double next;
        public MapToolController(MapEditSession session,MapToolRegistry registry)
        {context=new MapToolContext(session);this.registry=registry;}
        public void Select(string id)
        {
            var tool=registry.Get(id);if(Selected==tool)return;
            Complete();Selected=tool;
        }
        public void Begin(MapToolPointer pointer,double time)
        {
            if(Selected==null||Active||Selected.RequiresSurface&&!pointer.hasSurface)return;
            context.settings.Validate(Selected.MaximumRadius(context.session));
            authoredSettings=context.settings;context.settings=authoredSettings.Copy();last=target=pointer;
            Active=true;next=time+Interval;
            try{using(context.session.BatchNotifications()){Selected.Begin(context,pointer);Selected.Apply(context,pointer,Interval);}}
            catch{Cancel();throw;}
            if(Selected.Gesture!=MapToolGesture.Stroke)Complete();
        }
        public void Move(MapToolPointer pointer){if(Active&&!Finishing&&pointer.hasSurface)target=pointer;}
        public void Tick(double time)
        {
            if(!Active||time<next)return;next=time+Interval;
            try
            {
                using(context.session.BatchNotifications())
                {
                    float step=Mathf.Max(.15f,context.settings.radius*.25f);
                    int count=0;
                    while(Vector2.Distance(last.position,target.position)>.0001f&&count++<MaximumDabsPerTick)
                    {
                        var p=Vector2.MoveTowards(last.position,target.position,step);
                        last=new MapToolPointer(target.ray,true,p);Selected.Apply(context,last,Interval);
                    }
                    if(count==0&&Selected.RepeatAtRest&&!Finishing)Selected.Apply(context,last,Interval);
                    if(Finishing&&(last.position-target.position).sqrMagnitude<.000001f)Complete();
                }
            }
            catch{Cancel();throw;}
        }
        public void Release(){if(!Active)return;Finishing=true;if((last.position-target.position).sqrMagnitude<.000001f)Complete();}
        public void Cancel(){if(Active)Finish(true);}
        /// <summary>Explicit save/tool change drains the remaining path before committing. Normal pointer release is budgeted by Tick.</summary>
        public void Complete()
        {
            if(!Active)return;
            try
            {
                using(context.session.BatchNotifications())
                {
                    float step=Mathf.Max(.15f,context.settings.radius*.25f);int count=0;
                    while((last.position-target.position).sqrMagnitude>.000001f)
                    {
                        if(++count>65536)throw new InvalidOperationException("Stroke path is too long; split it into several strokes.");
                        last=new MapToolPointer(target.ray,true,Vector2.MoveTowards(last.position,target.position,step));Selected.Apply(context,last,Interval);
                    }
                    Finish(false);
                }
            }
            catch{Cancel();throw;}
        }
        void Finish(bool cancel)
        {
            try{Selected.End(context,cancel);}
            finally{Active=false;Finishing=false;context.settings=authoredSettings;authoredSettings=null;}
        }
    }

    public abstract class MapStrokeTool : IMapTool
    {
        public abstract string Id {get;}
        public abstract string Title {get;}
        public abstract string Category {get;}
        public virtual MapToolControls Controls=>MapToolControls.Brush;
        public MapToolGesture Gesture=>MapToolGesture.Stroke;
        public bool RequiresSurface=>true;
        public virtual bool RepeatAtRest=>false;
        public virtual float MaximumRadius(MapEditSession session)=>128;
        public abstract void Begin(MapToolContext context,MapToolPointer pointer);
        public abstract void Apply(MapToolContext context,MapToolPointer pointer,float seconds);
        public void End(MapToolContext c,bool cancel){if(cancel)c.session.CancelStroke();else c.session.CommitStroke();}
    }
    public sealed class MapSurfaceTool : MapStrokeTool
    {
        readonly string id,title;readonly bool road,erase;
        public MapSurfaceTool(string id,string title,bool road,bool erase){this.id=id;this.title=title;this.road=road;this.erase=erase;}
        public override string Id=>id;
        public override string Title=>title;
        public override string Category=>"Поверхности";
        public override void Begin(MapToolContext c,MapToolPointer p)=>c.session.BeginStroke(new SurfacePaintStroke{
            road=road,erase=erase,scatter=!road&&!erase&&c.scatter,scatterProfile=c.scatterProfile,
            radius=c.settings.radius,strength=c.settings.strength,brushSoftness=c.settings.softness+.001f,density=c.density});
        public override void Apply(MapToolContext c,MapToolPointer p,float seconds)=>c.session.AppendPoint(p.position);
    }
    public sealed class MapHeightTool : MapStrokeTool
    {
        readonly bool lower;
        public MapHeightTool(bool lower){this.lower=lower;}
        public override string Id=>lower?"terrain.lower":"terrain.raise";
        public override string Title=>lower?"Опустить землю":"Поднять землю";
        public override string Category=>"Рельеф";
        public override bool RepeatAtRest=>true;
        public override float MaximumRadius(MapEditSession s)=>Mathf.Min(128,Mathf.Max(32,s.HeightSpacing*64));
        public override void Begin(MapToolContext c,MapToolPointer p)=>c.session.BeginHeightStroke(c.settings.radius,
            (lower?-1:1)*Mathf.Max(.000001f,c.terrainRate*c.settings.strength)*MapToolController.Interval,c.settings.softness);
        public override void Apply(MapToolContext c,MapToolPointer p,float seconds){if(c.settings.strength>0)c.session.AppendHeightPoint(p.position);}
    }
    public sealed class MapGroveTool : MapStrokeTool
    {
        public override string Id=>"vegetation.grove";
        public override string Title=>"Роща";
        public override string Category=>"Растительность";
        public override void Begin(MapToolContext c,MapToolPointer p)=>c.session.BeginTreeBrush(c.treePackId,c.settings.radius,
            c.density*c.settings.strength,c.treeSpacing,c.seed,null,c.settings.softness);
        public override void Apply(MapToolContext c,MapToolPointer p,float seconds)=>c.session.AppendTreeBrush(p.position);
    }
    /// <summary>Point/spline tools participate in selection and input ownership, keeping their own drafts.</summary>
    public sealed class MapClickTool : IMapTool
    {
        public string Id {get;}
        public string Title {get;}
        public string Category {get;}
        public MapToolControls Controls=>MapToolControls.None;
        public MapToolGesture Gesture {get;}
        public bool RequiresSurface {get;}
        public bool RepeatAtRest=>false;
        readonly Action<MapToolPointer> action;
        public MapClickTool(string id,string title,string category,Action<MapToolPointer> action,bool requiresSurface=true,MapToolGesture gesture=MapToolGesture.Click)
        {Id=id;Title=title;Category=category;this.action=action;RequiresSurface=requiresSurface;Gesture=gesture;}
        public float MaximumRadius(MapEditSession s)=>128;
        public void Begin(MapToolContext c,MapToolPointer p){}
        public void Apply(MapToolContext c,MapToolPointer p,float seconds)=>action(p);
        public void End(MapToolContext c,bool cancel){}
    }
}
