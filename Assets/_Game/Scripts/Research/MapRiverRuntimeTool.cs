using System;
using System.Collections.Generic;
using LittleCastle.World;
using UnityEngine;

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
        public bool IsOpen=>open;
        public bool Embedded {get;set;}
        public bool HasUnappliedDraft=>painter&&painter.Session!=null&&state.HasUnappliedDraft(painter.Session);
        public int LastEscapeFrame {get;private set;}=-1;
        public void Connect(MeadowSurfacePainter owner){painter=owner;}
        public void Cancel(){state.Stop();LastEscapeFrame=Time.frameCount;}
        public void OpenTools(){open=true;state.Resume();TakeInput();}
        public void CloseTools(){open=false;Cancel();}
        public void BeginRiver()
        {
            open=true;
            if(HasUnappliedDraft)state.status="Сначала Применить или Отменить черновик.";
            else state.StartRiver();
            TakeInput();
        }
        public void ConfigureWaterOnly(bool enabled){state.ConfigureNewRiverProfile(enabled);}
        public void DiscardDraft(){state.DiscardDraft();}
        void TakeInput(){if(painter){painter.painting=false;painter.SetTreeTool(0);}}
        public void DrawTools()
        {
            if(!painter||painter.Session==null||(painter.Session.Access&MapEditAccess.Rivers)==0)return;
            if(Active&&(painter.painting||painter.treeTool!=0))state.Stop();
            int revision=state.ModeRevision;state.Draw(painter.Session);
            if(state.mode!=0&&revision!=state.ModeRevision)TakeInput();
        }
        public void Click(Vector2 point)=>state.Click(painter.Session,point);
        void OnGUI()
        {
            if(Embedded||!painter||painter.Session==null||(painter.Session.Access&MapEditAccess.Rivers)==0||Array.IndexOf(Environment.GetCommandLineArgs(),"-meadowCapture")>=0)return;
            GUILayout.BeginArea(new Rect(Screen.width-350,16,334,open?Mathf.Min(880,Screen.height-32):42),GUI.skin.box);
            // A later brush/tree choice relinquishes river input instead of being reset every OnGUI.
            if(Active&&(painter.painting||painter.treeTool!=0))state.Stop();
            bool nextOpen=GUILayout.Toggle(open,"РЕКИ И СКАЛЫ · инструменты автора");
            if(nextOpen!=open){if(nextOpen)OpenTools();else CloseTools();}
            if(open)
            {
                scroll=GUILayout.BeginScrollView(scroll);DrawTools();GUILayout.EndScrollView();
            }
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
        MapEditSession observedSession;
        long observedRevision=-1,selectedRiverId;
        bool creatingRiver,restorePending,showAdvanced;
        int resumeMode=2;
        string restoredMapId;
        bool newWaterOnly;
        public int ModeRevision {get;private set;}
        public bool IsCreatingRiver=>creatingRiver;
        public bool CanApplyRiver=>draft!=null&&draft.nodes.Count>=2;
        public bool HasUnappliedDraft(MapEditSession session)
        {
            if(section!=null&&section.points.Count>0)return true;
            if(draft==null)return false;
            if(creatingRiver)return draft.nodes.Count>0;
            var committed=session.ExportRivers().Find(r=>r.id==draft.id);
            return committed==null||JsonUtility.ToJson(committed)!=JsonUtility.ToJson(draft);
        }
        [Serializable] sealed class DraftSnapshot
        {
            public MapRiverRecord river;
            public MapRockSection rocks;
            public int mode,node,resumeMode;
            public bool creatingRiver;
            public bool showAdvanced;
            public long selectedRiverId;
            public string mapId;
        }
        public string ExportDraftJson()=>JsonUtility.ToJson(new DraftSnapshot{river=draft,rocks=section,mode=mode,node=node,resumeMode=resumeMode,showAdvanced=showAdvanced,creatingRiver=creatingRiver,selectedRiverId=selectedRiverId,mapId=observedSession?.MapId});
        public bool RestoreDraftJson(string json)
        {
            if(string.IsNullOrEmpty(json))return false;
            return Run(()=>
            {
                var saved=JsonUtility.FromJson<DraftSnapshot>(json);
                if(saved==null||saved.mode<0||saved.mode>6)throw new ArgumentException("Неверное состояние инструмента рек.");
                draft=saved.river;section=saved.rocks;mode=saved.mode;node=saved.node;
                creatingRiver=saved.creatingRiver;selectedRiverId=saved.selectedRiverId;
                resumeMode=saved.resumeMode>=1&&saved.resumeMode<=6?saved.resumeMode:creatingRiver?1:2;showAdvanced=saved.showAdvanced;
                observedSession=null;observedRevision=-1;restorePending=true;restoredMapId=saved.mapId;
            });
        }
        void Activate(int next){mode=next;resumeMode=next;ModeRevision++;}
        public void ConfigureNewRiverProfile(bool waterOnly){newWaterOnly=waterOnly;}
        public void DiscardDraft()
        {
            draft=null;section=null;selectedRiverId=0;creatingRiver=false;restorePending=false;
            mode=0;resumeMode=2;node=0;status="Черновик отменён.";
        }
        public void StartRiver()
        {
            draft=new MapRiverRecord{id=MapObjectIds.New(),layoutVersion=newWaterOnly?2:1};section=null;node=0;
            selectedRiverId=0;creatingRiver=true;Activate(1);
            status="Добавьте минимум две точки по течению (2–1024 м между ними), затем Применить.";
        }
        public void StartSelection(){Activate(2);status="Щёлкните по руслу или выберите реку в списке.";}
        public void Resume(){Activate(mode!=0?mode:resumeMode);}
        public void Stop(){if(mode!=0)resumeMode=mode;mode=0;}
        public void SelectRiver(MapEditSession session,long id)
        {
            draft=session.GetRiver(id);section=null;selectedRiverId=id;creatingRiver=false;node=0;
            observedSession=session;observedRevision=session.RiverRevision;Activate(2);
        }
        public bool ApplyRiver(MapEditSession session)
        {
            if(!CanApplyRiver){status="Для применения нужны минимум две контрольные точки.";return false;}
            return Run(()=>{session.UpsertRiver(draft);SelectRiver(session,draft.id);});
        }
        /// <summary>Reconcile committed selection after shared Undo/Redo/load, retaining a new unpublished draft.</summary>
        public void Refresh(MapEditSession session)
        {
            if(observedSession==session&&observedRevision==session.RiverRevision)return;
            bool replacedSession=observedSession!=null&&observedSession!=session;
            observedSession=session;observedRevision=session.RiverRevision;
            if(replacedSession){draft=null;section=null;selectedRiverId=0;creatingRiver=false;Stop();return;}
            if(restorePending)
            {
                restorePending=false;
                if(!string.IsNullOrEmpty(restoredMapId)&&restoredMapId!=session.MapId){draft=null;section=null;selectedRiverId=0;creatingRiver=false;Stop();return;}
                // The serialized editor draft can contain unapplied edits to an existing river.
                if(!creatingRiver&&draft!=null)
                {
                    selectedRiverId=draft.id;
                    if(session.ExportRivers().Exists(r=>r.id==selectedRiverId))return;
                }
            }
            if(creatingRiver)return;
            if(selectedRiverId==0&&draft!=null)selectedRiverId=draft.id;
            if(selectedRiverId!=0)draft=session.ExportRivers().Find(r=>r.id==selectedRiverId);
            if(draft!=null)node=Mathf.Clamp(node,0,draft.nodes.Count-1);
        }
        public List<Vector2> DraftPoints {get{var list=new List<Vector2>();if(mode==5&&section!=null)list.AddRange(section.points);else if(draft!=null)foreach(var n in draft.nodes)list.Add(n.position);return list;}}
        public void Draw(MapEditSession session,bool showHistory=true)
        {
            Refresh(session);
            GUILayout.Label("1. Новая река  →  2. Точки по земле  →  3. Применить");
            GUILayout.Label("ЛКМ добавляет точку; минимум две, 2–1024 м между ними. Esc завершает инструмент, черновик остаётся.");
            if(!string.IsNullOrEmpty(status))GUILayout.Label(status,GUI.skin.box);
            bool pending=HasUnappliedDraft(session);
            if(pending&&GUILayout.Button("Отменить черновик")){DiscardDraft();pending=false;}
            if(GUILayout.Button("Новая река: точки по земле")){if(pending)status="Сначала Применить или Отменить черновик.";else StartRiver();}
            if(GUILayout.Button("Выбрать реку по руслу")){if(pending)status="Сначала Применить или Отменить черновик.";else StartSelection();}
            foreach(var record in session.ExportRivers())if(GUILayout.Button("Река "+record.id+" · "+record.nodes.Count+" точек")){if(pending)status="Сначала Применить или Отменить черновик.";else SelectRiver(session,record.id);}
            bool advanced=GUILayout.Toggle(showAdvanced,"Дополнительно: берега, броды и скалы");
            if(showAdvanced&&!advanced&&mode==5)Stop();showAdvanced=advanced;
            if(draft!=null&&mode!=5)
            {
                GUILayout.Label((creatingRiver?"Новая река · черновик":"Выбранная река")+" · "+draft.nodes.Count+" точек");
                GUILayout.Label("Seed: "+draft.seed+" · скал: "+draft.rocks.Count);
                if(draft.nodes.Count>0)
                {
                    node=Mathf.Clamp(node,0,draft.nodes.Count-1);GUILayout.Label("Точка "+(node+1)+" / "+draft.nodes.Count);node=Mathf.RoundToInt(GUILayout.HorizontalSlider(node,0,draft.nodes.Count-1));var n=draft.nodes[node];
                    n.width=Slider("Ширина",n.width,4,24);n.depth=Slider("Глубина",n.depth,.2f,5);n.waterHeight=Slider("Отметка воды",n.waterHeight,session.MinimumHeight+n.depth,session.MaximumHeight);n.speed=Slider("Течение",n.speed,.05f,3);
                    if(showAdvanced)
                    {
                        n.leftTransition=Slider("Левый переход",n.leftTransition,2,15);n.rightTransition=Slider("Правый переход",n.rightTransition,2,15);
                        n.leftBank=(MapBankKind)GUILayout.SelectionGrid((int)n.leftBank,new[]{"Скала слева","Песок слева"},2);n.rightBank=(MapBankKind)GUILayout.SelectionGrid((int)n.rightBank,new[]{"Скала справа","Песок справа"},2);
                    }
                    if(GUILayout.Button("Передвинуть выбранную точку щелчком"))Activate(6);
                    if((creatingRiver||draft.nodes.Count>2)&&GUILayout.Button("Убрать выбранную точку")){draft.nodes.RemoveAt(node);node=0;}
                }
                if(creatingRiver&&mode!=1&&GUILayout.Button("Продолжить добавлять точки"))Activate(1);
                bool enabled=GUI.enabled;GUI.enabled=enabled&&CanApplyRiver;
                if(GUILayout.Button("Применить реку · одна отмена"))ApplyRiver(session);
                GUI.enabled=enabled&&!creatingRiver;
                if(showAdvanced)
                {
                GUILayout.BeginHorizontal();if(GUILayout.Button("← 2 м"))Run(()=>{session.TranslateRiver(draft.id,Vector2.left*2);draft=session.GetRiver(draft.id);});if(GUILayout.Button("2 м →"))Run(()=>{session.TranslateRiver(draft.id,Vector2.right*2);draft=session.GetRiver(draft.id);});GUILayout.EndHorizontal();
                left=GUILayout.Toggle(left,"Песчаный спуск слева");right=GUILayout.Toggle(right,"Песчаный спуск справа");brushRadius=Slider("Радиус участка",brushRadius,2,16);shallow=Slider("Глубина брода",shallow,.1f,.4f);
                if(GUILayout.Button("Кисть песчаного спуска · щелчок"))Activate(3);if(GUILayout.Button("Кисть брода · оба берега · щелчок"))Activate(4);
                foreach(var patch in new List<MapBankPatch>(draft.patches))if(GUILayout.Button("Удалить "+(patch.crossChannel?"брод":"спуск")+" @"+patch.station.ToString("F0")))Run(()=>{session.RemoveBankPatch(draft.id,patch.id);draft=session.GetRiver(draft.id);});
                }
                if(GUILayout.Button("Удалить выбранную реку"))Run(()=>{session.DeleteRiver(draft.id);draft=null;Stop();});
                GUI.enabled=enabled;
            }
            if(showAdvanced)
            {
            if(GUILayout.Button("Новый скальный участок вне реки")){if(pending)status="Сначала Применить или Отменить черновик.";else{section=new MapRockSection{id=MapObjectIds.New()};Activate(5);}}
            if(section!=null&&GUILayout.Button("Применить скальный участок"))Run(()=>{session.UpsertRockSection(section);section=null;mode=0;});
            foreach(var s in session.ExportRockSections())if(GUILayout.Button("Удалить скальный участок "+s.id))Run(()=>session.DeleteRockSection(s.id));
            }
            if(showHistory){GUILayout.BeginHorizontal();if(GUILayout.Button("Отменить"))Run(()=>{session.Undo();Refresh(session);});if(GUILayout.Button("Повторить"))Run(()=>{session.Redo();Refresh(session);});GUILayout.EndHorizontal();}
            if(GUILayout.Button("Закончить инструмент"))Stop();
        }
        static float Slider(string label,float value,float lo,float hi){GUILayout.Label(label+": "+value.ToString("F2"));return GUILayout.HorizontalSlider(value,lo,hi);}
        public void Click(MapEditSession session,Vector2 p)
        {
            Refresh(session);
            if(mode==0)return;
            bool success=Run(()=>
            {
                if(mode==1)
                {
                    if(draft==null)throw new InvalidOperationException("Сначала нажмите Новая река.");
                    if(draft.nodes.Count>0){float distance=Vector2.Distance(p,draft.nodes[draft.nodes.Count-1].position);if(distance<2||distance>1024)throw new InvalidOperationException("Между точками нужно 2–1024 м. Выберите другую точку.");}
                    float water=draft.nodes.Count==0?Mathf.Clamp(session.SampleBaseHeight(p.x,p.y)-1,session.MinimumHeight+2,session.MaximumHeight):draft.nodes[draft.nodes.Count-1].waterHeight;
                    draft.nodes.Add(new MapRiverNode{id=MapObjectIds.New(),position=p,waterHeight=water,leftBank=newWaterOnly?MapBankKind.Sand:MapBankKind.Cliff,rightBank=newWaterOnly?MapBankKind.Sand:MapBankKind.Cliff});node=draft.nodes.Count-1;
                }
                else if(mode==5)section.points.Add(p);
                else if(mode==6&&draft!=null){draft.nodes[node].position=p;mode=2;}
                else
                {
                    MapRiverPath best=null;MapRiverFrame frame=default;float nearest=30;
                    foreach(var path in session.QueryRiverPaths(new Rect(p.x-1,p.y-1,2,2)))if(path.Nearest(p,out var f,out _)&&Vector2.Distance(f.center,p)<nearest){best=path;frame=f;nearest=Vector2.Distance(f.center,p);}
                    if(best==null)throw new InvalidOperationException("Щёлкните ближе к руслу выбранной реки.");
                    if(mode==3||mode==4)session.PaintBank(best.record.id,frame.station,brushRadius,mode==4||left,mode==4||right,mode==4,shallow);
                    int currentMode=mode;SelectRiver(session,best.record.id);mode=currentMode;
                }
            });
            if(success&&mode==1)status="Черновик: "+draft.nodes.Count+" точек. "+(CanApplyRiver?"Можно нажать Применить или добавить ещё точки.":"Добавьте ещё минимум одну точку.");
        }
        bool Run(Action action){try{action();status="Готово";return true;}catch(Exception e){status=e.Message;return false;}}
    }
}
