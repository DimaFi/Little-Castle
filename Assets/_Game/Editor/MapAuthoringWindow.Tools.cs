using System;
using LittleCastle.World;
using UnityEditor;
using UnityEngine;

namespace LittleCastle.Editor
{
    public sealed partial class MapAuthoringWindow
    {
        MapToolController tools;
        [SerializeField] float softness=.38f;
        [SerializeField] MapBrushPreset brushPreset;
        readonly Vector3[] outerOutline=new Vector3[65],innerOutline=new Vector3[65];
        bool pointerHeld;
        string[] toolNames;
        string extensionToolId;
        void InitializeTools()
        {
            tools?.Cancel();pointerHeld=false;
            var registry=MapToolRegistry.ExistingBrushes();
            if(treeCatalog&&treeCatalog.entries.Length>0)
            {
                registry.Register(new MapClickTool("tree.place","Посадить дерево","Растительность",p=>
                    selectedTree=session.PlaceTree(treeCatalog.entries[selectedTreeSource].definition.archetypeId,p.position,treeYaw,treeScale)));
                registry.Register(new MapClickTool("tree.pick","Выбрать дерево","Растительность",p=>
                {
                    if(treePreview==null||!treePreview.Pick(p.ray,out var id))return;
                    selectedTree=id;var r=session.GetTree(id);int index=Array.FindIndex(treeCatalog.entries,e=>e.definition.archetypeId==r.archetypeId);
                    if(index>=0)selectedTreeSource=index;treeYaw=r.yawDegrees;treeScale=r.uniformScale;
                },false));
                registry.Register(new MapClickTool("tree.move","Переместить дерево","Растительность",p=>
                {if(session.HasTree(selectedTree))session.TransformTree(selectedTree,p.position,treeYaw,treeScale);treeTool=3;}));
            }
            registry.Register(new MapClickTool("river.points","Контрольные точки","Вода и берега",p=>riverTools.Click(session,p.position),true,MapToolGesture.ControlPoints));
            tools=new MapToolController(session,registry);
            toolNames=new string[registry.Tools.Count];
            for(int i=0;i<toolNames.Length;i++)toolNames[i]=registry.Tools[i].Category+" / "+registry.Tools[i].Title;
        }
        string CurrentToolId=>extensionToolId??(showRivers&&riverTools.mode!=0?"river.points":treeTool==1?"tree.place":treeTool==2?"vegetation.grove":treeTool==3?"tree.pick":treeTool==4?"tree.move":sculpting?(lower?"terrain.lower":"terrain.raise"):erase?"surface.erase":road?"surface.road":"surface.meadow");
        internal void SetTool(string id)
        {
            tools.Complete();pointerHeld=false;riverTools.mode=0;
            extensionToolId=id=="surface.road"||id=="surface.meadow"||id=="surface.erase"||id=="terrain.raise"||id=="terrain.lower"||id=="vegetation.grove"||id=="tree.place"||id=="tree.pick"||id=="tree.move"||id=="river.points"?null:id;
            treeTool=id=="tree.place"?1:id=="vegetation.grove"?2:id=="tree.pick"?3:id=="tree.move"?4:0;
            sculpting=id.StartsWith("terrain.",StringComparison.Ordinal);lower=id=="terrain.lower";road=id=="surface.road";erase=id=="surface.erase";
            brush=treeTool==0&&id!="river.points";
            if(id=="river.points"){showRivers=true;riverTools.mode=2;}
            tools.Select(id);
        }
        void DrawCommonTools()
        {
            if(tools==null)InitializeTools();
            int index=0;var registered=tools.registry.Tools;
            for(int i=0;i<registered.Count;i++)if(registered[i].Id==CurrentToolId)index=i;
            int next=EditorGUILayout.Popup("Инструмент",index,toolNames);
            if(next!=index){SetTool(registered[next].Id);softness=CurrentToolId.StartsWith("terrain.")?1:CurrentToolId=="vegetation.grove"?0:.38f;}
            var tool=registered[next];
            if(tool.Controls!=MapToolControls.None)
            {
                if((tool.Controls&MapToolControls.Radius)!=0)radius=EditorGUILayout.Slider("Радиус, м",radius,treeTool==2?1:.5f,tool.MaximumRadius(session));
                if((tool.Controls&MapToolControls.Strength)!=0)strength=EditorGUILayout.Slider("Сила",strength,0,1);
                if((tool.Controls&MapToolControls.Softness)!=0)softness=EditorGUILayout.Slider("Мягкость края",softness,0,1);
                if(sculpting)terrainRate=EditorGUILayout.Slider("Метры в секунду",terrainRate,.1f,4);
                brushPreset=(MapBrushPreset)EditorGUILayout.ObjectField("Профиль кисти",brushPreset,typeof(MapBrushPreset),false);
                using(new EditorGUILayout.HorizontalScope())
                {
                    if(brushPreset&&GUILayout.Button("Применить профиль"))Run(()=>
                    {var t=tools.registry.Get(brushPreset.toolId);brushPreset.settings.Validate(t.MaximumRadius(session));SetTool(t.Id);radius=brushPreset.settings.radius;strength=brushPreset.settings.strength;softness=brushPreset.settings.softness;});
                    if(GUILayout.Button("Сохранить профиль…"))
                    {
                        string path=EditorUtility.SaveFilePanelInProject("Профиль кисти","MapBrushPreset","asset","Сохранить настройки кисти");
                        if(path.Length>0){var asset=CreateInstance<MapBrushPreset>();asset.toolId=CurrentToolId;asset.settings=new MapBrushSettings{radius=radius,strength=strength,softness=softness};AssetDatabase.CreateAsset(asset,path);brushPreset=asset;}
                    }
                }
            }
            if(GUILayout.Button("Показать всю карту в SceneView"))
            {
                var view=SceneView.lastActiveSceneView;if(view){var b=session.Bounds;view.Frame(new Bounds(new Vector3(b.x+b.z/2,(session.MinimumHeight+session.MaximumHeight)/2,b.y+b.w/2),new Vector3(b.z,session.MaximumHeight-session.MinimumHeight,b.w)),false);}
            }
            EditorGUILayout.HelpBox("ЛКМ — действие; Esc — отмена текущего штриха. Alt, ПКМ, СКМ и колесо — управление камерой. Внешний контур — радиус, внутренний — начало мягкого края.",MessageType.None);
        }
        void ConfigureTools()
        {
            if(tools==null)return;
            if(tools.Selected==null||tools.Selected.Id!=CurrentToolId)softness=CurrentToolId.StartsWith("terrain.")?1:CurrentToolId=="vegetation.grove"?0:.38f;
            tools.Select(CurrentToolId);
            if(tools.Active)return;
            var c=tools.context;c.settings.radius=Mathf.Clamp(radius,treeTool==2?1:.5f,tools.Selected.MaximumRadius(session));
            c.settings.strength=strength;c.settings.softness=softness;c.density=treeTool==2?treeDensity:density;
            c.scatter=scatter;c.scatterProfile=profile;c.terrainRate=terrainRate;c.treeSpacing=treeSpacing;c.seed=treeSeed;
            if(treeCatalog&&treeCatalog.packs.Length>0)c.treePackId=treeCatalog.packs[Mathf.Clamp(selectedTreePack,0,treeCatalog.packs.Length-1)].id;
        }
        internal MapEditSession VerificationSession=>session;
        internal void VerifySettings(float r,float s,float edge){radius=r;strength=s;softness=edge;}
        internal void CompleteVerificationStroke(){tools.Complete();pointerHeld=false;}
        void TickTools()
        {
            if(tools==null||session==null)return;
            if(!brush&&treeTool==0&&(!showRivers||riverTools.mode==0)){tools.Cancel();pointerHeld=false;return;}
            if(pointerHeld&&!(EditorWindow.mouseOverWindow is SceneView)){tools.Release();pointerHeld=false;}
            try{ConfigureTools();tools.Tick(EditorApplication.timeSinceStartup);}catch(Exception e){status=e.Message;pointerHeld=false;}
        }
        void SceneGUI(SceneView view)
        {
            if(session==null||EditorApplication.isPlaying||!brush&&treeTool==0&&(!showRivers||riverTools.mode==0))return;
            var e=Event.current;ConfigureTools();
            if(e.type==EventType.KeyDown&&e.keyCode==KeyCode.Escape)
            {tools.Cancel();pointerHeld=false;riverTools.mode=0;treeTool=0;e.Use();return;}
            if(e.type==EventType.KeyDown&&(e.control||e.command)&&(e.keyCode==KeyCode.Z||e.keyCode==KeyCode.Y))
            {pointerHeld=false;if(e.keyCode==KeyCode.Y||e.shift)tools.Redo();else tools.Undo();e.Use();return;}
            if(e.alt||e.button==1||e.button==2)
            {if(pointerHeld){tools.Release();pointerHeld=false;}return;}
            if(e.type==EventType.Layout)HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
            if(e.type==EventType.MouseUp&&e.button==0){tools.Release();pointerHeld=false;e.Use();return;}
            var ray=HandleUtility.GUIPointToWorldRay(e.mousePosition);
            bool hit=MapSurfaceQuery.Raycast(session,ray,out var ground);
            var p=new MapToolPointer(ray,hit,new Vector2(ground.point.x,ground.point.z));
            DrawRiverDraft();
            if(hit&&(tools.Selected.Controls&MapToolControls.Radius)!=0)
            {
                MapBrushFalloff.Outline(session,p.position,radius,outerOutline);Handles.color=new Color(.3f,1,.5f,.95f);Handles.DrawAAPolyLine(2,outerOutline);
                if(softness>0&&softness<1){MapBrushFalloff.Outline(session,p.position,radius*(1-softness),innerOutline);Handles.color=new Color(.3f,1,.5f,.4f);Handles.DrawAAPolyLine(1,innerOutline);}
            }
            try
            {
                if(e.type==EventType.MouseDown&&e.button==0){tools.Begin(p,EditorApplication.timeSinceStartup);pointerHeld=tools.Active;e.Use();}
                if(pointerHeld&&e.type==EventType.MouseDrag){if(hit)tools.Move(p);else{tools.Release();pointerHeld=false;}e.Use();}
            }
            catch(Exception error){status=error.Message;tools.Cancel();pointerHeld=false;}
            if(e.type==EventType.MouseMove||tools.Active)view.Repaint();
        }
    }
}
