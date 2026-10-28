using System;
using LittleCastle.Rendering;
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
        void InitializeTools()
        {
            tools?.Complete();pointerHeld=false;
            var registry=MapToolRegistry.ExistingBrushes();
            registry.Register(new MapSurfaceTool("vegetation.flowers","Цветы и мелочь",false,false));
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
                {if(session.HasTree(selectedTree))session.TransformTree(selectedTree,p.position,treeYaw,treeScale);treeTool=3;activeToolId="tree.pick";}));
            }
            registry.Register(new MapClickTool("river.points","Контрольные точки","Вода и берега",p=>riverTools.Click(session,p.position),true,MapToolGesture.ControlPoints));
            tools=new MapToolController(session,registry);
            bool found=false;foreach(var tool in registry.Tools)if(tool.Id==activeToolId){found=true;break;}
            if(!found)activeToolId="surface.road";
            treeTool=activeToolId=="tree.place"?1:activeToolId=="vegetation.grove"?2:activeToolId=="tree.pick"?3:activeToolId=="tree.move"?4:0;
            sculpting=activeToolId.StartsWith("terrain.",StringComparison.Ordinal);
            tools.Select(activeToolId);toolCategory=CategoryForTool(tools.Selected);
        }
        string CurrentToolId=>activeToolId;
        internal void SetTool(string id)
        {
            tools.Complete();pointerHeld=false;
            var selected=tools.registry.Get(id);
            if(id!="river.points")riverTools.Stop();
            else if(riverTools.mode==0)riverTools.Resume();
            if(CurrentToolId!=id)softness=id.StartsWith("terrain.")?1:id=="vegetation.grove"?0:.38f;
            activeToolId=id;toolCategory=CategoryForTool(selected);
            if(id=="vegetation.flowers"){scatter=true;profile=MeadowScatterProfile.Flowers;}
            treeTool=id=="tree.place"?1:id=="vegetation.grove"?2:id=="tree.pick"?3:id=="tree.move"?4:0;
            sculpting=id.StartsWith("terrain.",StringComparison.Ordinal);lower=id=="terrain.lower";road=id=="surface.road";erase=id=="surface.erase";
            brush=true;tools.Select(id);SceneView.RepaintAll();
        }
        void DrawCommonTools()
        {
            if(tools==null)InitializeTools();
            var tool=tools.registry.Get(CurrentToolId);
            if((tool.Controls&MapToolControls.Radius)!=0)radius=EditorGUILayout.Slider("Радиус, м",radius,treeTool==2?1:.5f,tool.MaximumRadius(session));
            if((tool.Controls&MapToolControls.Strength)!=0)strength=EditorGUILayout.Slider("Сила",strength,0,1);
            if((tool.Controls&MapToolControls.Softness)!=0)softness=EditorGUILayout.Slider("Мягкость края",softness,0,1);
            if(sculpting)terrainRate=EditorGUILayout.Slider("Метры в секунду",terrainRate,.1f,4);
            if(toolCategory==4)DrawEnvironmentTools();
        }
        void ConfigureTools()
        {
            if(tools==null)return;
            if(tools.Selected==null||tools.Selected.Id!=CurrentToolId)softness=CurrentToolId.StartsWith("terrain.")?1:CurrentToolId=="vegetation.grove"?0:.38f;
            tools.Select(CurrentToolId);
            if(tools.Active)return;
            var c=tools.context;c.settings.radius=Mathf.Clamp(radius,treeTool==2?1:.5f,tools.Selected.MaximumRadius(session));
            c.environmentThemeId=environmentThemeId;c.environmentMode=environmentMode;
            c.settings.strength=strength;c.settings.softness=softness;c.density=treeTool==2?treeDensity:density;
            c.scatter=CurrentToolId=="vegetation.flowers"||scatter;c.scatterProfile=profile;c.terrainRate=terrainRate;c.treeSpacing=treeSpacing;c.seed=treeSeed;
            if(treeCatalog&&treeCatalog.packs.Length>0)c.treePackId=treeCatalog.packs[Mathf.Clamp(selectedTreePack,0,treeCatalog.packs.Length-1)].id;
        }
        internal MapEditSession VerificationSession=>session;
        internal void VerifySettings(float r,float s,float edge){radius=r;strength=s;softness=edge;}
        internal void CompleteVerificationStroke(){tools.Complete();pointerHeld=false;}
        void TickTools()
        {
            if(tools==null||session==null)return;
            if(!brush){tools.Complete();pointerHeld=false;return;}
            if(pointerHeld&&!(EditorWindow.mouseOverWindow is SceneView)){tools.Release();pointerHeld=false;}
            try{ConfigureTools();tools.Tick(EditorApplication.timeSinceStartup);}catch(Exception e){status=e.Message;pointerHeld=false;}
        }
        void SceneGUI(SceneView view)
        {
            if(session==null||EditorApplication.isPlaying||!brush)return;
            var e=Event.current;ConfigureTools();
            if(e.type==EventType.KeyDown&&e.keyCode==KeyCode.Escape)
            {tools.Cancel();pointerHeld=false;riverTools.Stop();brush=false;e.Use();return;}
            if(e.type==EventType.KeyDown&&(e.control||e.command)&&(e.keyCode==KeyCode.Z||e.keyCode==KeyCode.Y))
            {pointerHeld=false;if(e.keyCode==KeyCode.Y||e.shift)tools.Redo();else tools.Undo();riverTools.Refresh(session);e.Use();return;}
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
                if(e.type==EventType.MouseDown&&e.button==0)
                {
                    tools.Begin(p,EditorApplication.timeSinceStartup);pointerHeld=tools.Active;
                    if(CurrentToolId=="river.points")hasUnsavedChanges=documentDirty||riverTools.HasUnappliedDraft(session);
                    Repaint();view.Repaint();e.Use();
                }
                if(pointerHeld&&e.type==EventType.MouseDrag){if(hit)tools.Move(p);else{tools.Release();pointerHeld=false;}e.Use();}
            }
            catch(Exception error){status=error.Message;tools.Cancel();pointerHeld=false;}
            if(e.type==EventType.MouseMove||tools.Active)view.Repaint();
        }
    }
}
