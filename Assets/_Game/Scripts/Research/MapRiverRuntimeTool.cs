using System;
using System.Collections.Generic;
using LittleCastle.World;
using UnityEngine;
using UnityEngine.EventSystems;

namespace LittleCastle.Research
{
    /// <summary>Player adapter for developer-only river commands. Draft lines are cheap; Apply commits once.</summary>
    public sealed class MapRiverRuntimeTool : MonoBehaviour
    {
        MeadowSurfacePainter painter;
        readonly MapRiverToolState state=new MapRiverToolState();
        bool open;
        Vector2 scroll;
        public bool Active=>open&&state.mode!=0;
        public int LastEscapeFrame {get;private set;}=-1;
        public void Connect(MeadowSurfacePainter owner){painter=owner;}
        public void Cancel(){state.mode=0;LastEscapeFrame=Time.frameCount;}
        public void Click(Vector2 point)=>state.Click(painter.Session,point);
        void OnGUI()
        {
            if(!painter||painter.Session==null||(painter.Session.Access&MapEditAccess.Rivers)==0||Array.IndexOf(Environment.GetCommandLineArgs(),"-meadowCapture")>=0)return;
            GUILayout.BeginArea(new Rect(Screen.width-350,16,334,Mathf.Min(880,Screen.height-32)),GUI.skin.box);
            open=GUILayout.Toggle(open,"РЕКИ И СКАЛЫ · инструменты автора");
            if(open){scroll=GUILayout.BeginScrollView(scroll);state.Draw(painter.Session);GUILayout.EndScrollView();if(state.mode!=0){painter.painting=false;painter.SetTreeTool(0);}}
            GUILayout.EndArea();
        }
        void OnRenderObject()
        {
            // The editor draws draft lines with Handles. Runtime uses one shared line renderer.
        }
        LineRenderer line;
        Material lineMaterial;
        void LateUpdate()
        {
            if(!painter||painter.Session==null)return;
            var points=state.DraftPoints;
            if(!Active||points.Count<1){if(line)line.enabled=false;return;}
            if(!line)
            {
                var root=new GameObject("River draft line"){hideFlags=HideFlags.HideAndDontSave};root.transform.SetParent(transform,false);line=root.AddComponent<LineRenderer>();
                lineMaterial=new Material(painter.brushMaterial);line.sharedMaterial=lineMaterial;line.widthMultiplier=.1f;line.useWorldSpace=true;line.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            line.enabled=true;line.positionCount=points.Count;for(int i=0;i<points.Count;i++)line.SetPosition(i,new Vector3(points[i].x,painter.Session.SampleHeight(points[i].x,points[i].y)+.15f,points[i].y));
        }
        void OnDestroy(){if(lineMaterial)Destroy(lineMaterial);}
    }
    /// <summary>Shared control state and GUI for both adapters. All authoritative mutations use MapEditSession.</summary>
    public sealed class MapRiverToolState
    {
        public int mode; // 1 draw river, 2 select, 3 sand, 4 ford, 5 standalone cliff, 6 move node
        public MapRiverRecord draft;
        public MapRockSection section;
        public int node;
        float brushRadius=6,shallow=.25f;
        bool left=true,right;
        public string status;
        public List<Vector2> DraftPoints {get{var list=new List<Vector2>();if(mode==5&&section!=null)list.AddRange(section.points);else if(draft!=null)foreach(var n in draft.nodes)list.Add(n.position);return list;}}
        public void Draw(MapEditSession session)
        {
            GUILayout.Label("Контрольные точки → Применить. Esc завершает инструмент.");
            if(GUILayout.Button("Новая река: точки по земле")){draft=new MapRiverRecord{id=MapObjectIds.New()};mode=1;status="Добавляйте точки по течению; затем Применить.";}
            if(GUILayout.Button("Выбрать реку по руслу"))mode=2;
            foreach(var record in session.ExportRivers())if(GUILayout.Button("Река "+record.id+" · "+record.nodes.Count+" точек")){draft=record;mode=2;}
            if(draft!=null)
            {
                GUILayout.Label("Seed: "+draft.seed+" · скал: "+draft.rocks.Count);
                if(draft.nodes.Count>0)
                {
                    node=Mathf.Clamp(node,0,draft.nodes.Count-1);GUILayout.Label("Точка "+(node+1)+" / "+draft.nodes.Count);node=Mathf.RoundToInt(GUILayout.HorizontalSlider(node,0,draft.nodes.Count-1));var n=draft.nodes[node];
                    n.width=Slider("Ширина",n.width,4,24);n.depth=Slider("Глубина",n.depth,.2f,5);n.waterHeight=Slider("Отметка воды",n.waterHeight,session.MinimumHeight+n.depth,Mathf.Min(session.MaximumHeight,5));n.speed=Slider("Течение",n.speed,.05f,3);
                    n.leftTransition=Slider("Левый переход",n.leftTransition,2,15);n.rightTransition=Slider("Правый переход",n.rightTransition,2,15);
                    n.leftBank=(MapBankKind)GUILayout.SelectionGrid((int)n.leftBank,new[]{"Скала слева","Песок слева"},2);n.rightBank=(MapBankKind)GUILayout.SelectionGrid((int)n.rightBank,new[]{"Скала справа","Песок справа"},2);
                    if(GUILayout.Button("Передвинуть выбранную точку щелчком"))mode=6;
                    if(draft.nodes.Count>2&&GUILayout.Button("Убрать выбранную точку")){draft.nodes.RemoveAt(node);node=0;}
                }
                if(GUILayout.Button("Применить реку · одна отмена"))Run(()=>{session.UpsertRiver(draft);draft=session.GetRiver(draft.id);mode=2;});
                GUILayout.BeginHorizontal();if(GUILayout.Button("← 2 м"))Run(()=>{session.TranslateRiver(draft.id,Vector2.left*2);draft=session.GetRiver(draft.id);});if(GUILayout.Button("2 м →"))Run(()=>{session.TranslateRiver(draft.id,Vector2.right*2);draft=session.GetRiver(draft.id);});GUILayout.EndHorizontal();
                left=GUILayout.Toggle(left,"Песчаный спуск слева");right=GUILayout.Toggle(right,"Песчаный спуск справа");brushRadius=Slider("Радиус участка",brushRadius,2,16);shallow=Slider("Глубина брода",shallow,.1f,.4f);
                if(GUILayout.Button("Кисть песчаного спуска · щелчок"))mode=3;if(GUILayout.Button("Кисть брода · оба берега · щелчок"))mode=4;
                foreach(var patch in new List<MapBankPatch>(draft.patches))if(GUILayout.Button("Удалить "+(patch.crossChannel?"брод":"спуск")+" @"+patch.station.ToString("F0")))Run(()=>{session.RemoveBankPatch(draft.id,patch.id);draft=session.GetRiver(draft.id);});
                if(GUILayout.Button("Удалить выбранную реку"))Run(()=>{session.DeleteRiver(draft.id);draft=null;mode=0;});
            }
            if(GUILayout.Button("Новый скальный участок вне реки")){section=new MapRockSection{id=MapObjectIds.New()};mode=5;}
            if(section!=null&&GUILayout.Button("Применить скальный участок"))Run(()=>{session.UpsertRockSection(section);section=null;mode=0;});
            foreach(var s in session.ExportRockSections())if(GUILayout.Button("Удалить скальный участок "+s.id))Run(()=>session.DeleteRockSection(s.id));
            GUILayout.BeginHorizontal();if(GUILayout.Button("Отменить"))Run(()=>{session.Undo();Refresh(session);});if(GUILayout.Button("Повторить"))Run(()=>{session.Redo();Refresh(session);});GUILayout.EndHorizontal();
            if(GUILayout.Button("Закончить инструмент"))mode=0;
            if(!string.IsNullOrEmpty(status))GUILayout.Label(status);
        }
        void Refresh(MapEditSession s){if(draft!=null){var all=s.ExportRivers();draft=all.Find(r=>r.id==draft.id);}}
        static float Slider(string label,float value,float lo,float hi){GUILayout.Label(label+": "+value.ToString("F2"));return GUILayout.HorizontalSlider(value,lo,hi);}
        public void Click(MapEditSession session,Vector2 p)
        {
            Run(()=>
            {
                if(mode==1){float water=draft.nodes.Count==0?session.SampleBaseHeight(p.x,p.y)-1:draft.nodes[draft.nodes.Count-1].waterHeight;draft.nodes.Add(new MapRiverNode{id=MapObjectIds.New(),position=p,waterHeight=water});node=draft.nodes.Count-1;}
                else if(mode==5)section.points.Add(p);
                else if(mode==6&&draft!=null){draft.nodes[node].position=p;mode=2;}
                else
                {
                    MapRiverPath best=null;MapRiverFrame frame=default;float nearest=30;
                    foreach(var path in session.QueryRiverPaths(new Rect(p.x-1,p.y-1,2,2)))if(path.Nearest(p,out var f,out _)&&Vector2.Distance(f.center,p)<nearest){best=path;frame=f;nearest=Vector2.Distance(f.center,p);}
                    if(best==null)throw new InvalidOperationException("Щёлкните ближе к руслу выбранной реки.");
                    if(mode==3||mode==4)session.PaintBank(best.record.id,frame.station,brushRadius,mode==4||left,mode==4||right,mode==4,shallow);
                    draft=session.GetRiver(best.record.id);
                }
            });
        }
        void Run(Action action){try{action();status="Готово";}catch(Exception e){status=e.Message;}}
    }
}
