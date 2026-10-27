using System;
using LittleCastle.CameraSystem;
using LittleCastle.World;
using UnityEngine;
using UnityEngine.EventSystems;

namespace LittleCastle.Research
{
    public sealed partial class MeadowSurfacePainter
    {
        public float brushSoftness=.38f;
        MapToolController tools;
        readonly Vector3[] brushOutline=new Vector3[49];
        public MapToolController Tools=>tools;
        void InitializeTools()
        {
            var registry=MapToolRegistry.ExistingBrushes();
            registry.Register(new MapClickTool("tree.place","Посадить дерево","Растительность",p=>
                selectedTree=Session.PlaceTree(treeCatalog.entries[selectedTreeSource].definition.archetypeId,p.position,treeYaw,treeScale)));
            registry.Register(new MapClickTool("tree.pick","Выбрать дерево","Растительность",p=>
            {
                if(TreeView==null||!TreeView.Pick(p.ray,out long id))return;selectedTree=id;var r=Session.GetTree(id);
                int index=Array.FindIndex(treeCatalog.entries,e=>e.definition.archetypeId==r.archetypeId);if(index>=0)selectedTreeSource=index;
                treeYaw=r.yawDegrees;treeScale=r.uniformScale;
            },false));
            registry.Register(new MapClickTool("tree.move","Переместить дерево","Растительность",p=>
            {if(Session.HasTree(selectedTree))Session.TransformTree(selectedTree,p.position,treeYaw,treeScale);treeTool=3;}));
            registry.Register(new MapClickTool("river.points","Контрольные точки","Вода и берега",p=>GetComponent<MapRiverRuntimeTool>().Click(p.position),true,MapToolGesture.ControlPoints));
            tools=new MapToolController(Session,registry);
        }
        void Update()
        {
            if(cursor)cursor.enabled=false;if(Session==null||!studyCamera)return;
            Session.Access=playerMode?allowedTools&MapEditAccess.Player:allowedTools;
            var river=GetComponent<MapRiverRuntimeTool>();
            if(river&&river.Active&&(Session.Access&MapEditAccess.Rivers)==0)river.Cancel();
            bool riverActive=river&&river.Active;
            if(!painting&&treeTool==0&&!riverActive){tools.Cancel();return;}
            if(Input.GetKeyDown(KeyCode.Escape)){tools.Cancel();if(riverActive)river.Cancel();else CancelTreeTool();return;}
            if(Input.GetKey(KeyCode.LeftControl)||Input.GetKey(KeyCode.RightControl))
            {if(Input.GetKeyDown(KeyCode.Z)){Undo();return;}if(Input.GetKeyDown(KeyCode.Y)){Redo();return;}}
            string id=riverActive?"river.points":treeTool==1?"tree.place":treeTool==2?"vegetation.grove":treeTool==3?"tree.pick":treeTool==4?"tree.move":sculpting?(lowerTerrain?"terrain.lower":"terrain.raise"):erase?"surface.erase":road?"surface.road":"surface.meadow";
            try
            {
                if(tools.Selected==null||tools.Selected.Id!=id)brushSoftness=id.StartsWith("terrain.")?1:id=="vegetation.grove"?0:.38f;
                tools.Select(id);
                bool blocked=Input.mousePosition.x<320*Mathf.Clamp(Screen.width/1600f,.85f,1.3f)||
                    riverActive&&Input.mousePosition.x>Screen.width-360||EventSystem.current&&EventSystem.current.IsPointerOverGameObject()||
                    Input.GetMouseButton(1)||Input.GetMouseButton(2)||Input.GetKey(KeyCode.LeftAlt)||Input.GetKey(KeyCode.RightAlt);
                if(blocked){tools.Release();tools.Tick(Time.unscaledTimeAsDouble);return;}
                var ray=studyCamera.ScreenPointToRay(Input.mousePosition);bool hit=RaycastGround(ray,out var ground);
                var pointer=new MapToolPointer(ray,hit,new Vector2(ground.point.x,ground.point.z));
                if(!tools.Active)
                {
                    var c=tools.context;c.settings.radius=Mathf.Clamp(radius,treeTool==2?1:.5f,tools.Selected.MaximumRadius(Session));
                    c.settings.strength=strength;c.settings.softness=brushSoftness;c.density=treeTool==2?treeBrushDensity:density;
                    c.terrainRate=terrainMetresPerSecond;c.scatter=populatePlants;c.scatterProfile=currentProfile;c.treeSpacing=treeBrushSpacing;c.seed=treeBrushSeed;
                    if(treeCatalog&&treeCatalog.packs.Length>0)c.treePackId=treeCatalog.packs[Mathf.Clamp(selectedTreePack,0,treeCatalog.packs.Length-1)].id;
                }
                if(hit&&(tools.Selected.Controls&MapToolControls.Radius)!=0&&cursor)
                {cursor.enabled=true;MapBrushFalloff.Outline(Session,pointer.position,radius,brushOutline);cursor.positionCount=brushOutline.Length;cursor.SetPositions(brushOutline);}
                if(Input.GetMouseButtonDown(0))tools.Begin(pointer,Time.unscaledTimeAsDouble);
                else if(Input.GetMouseButton(0)){if(hit)tools.Move(pointer);else tools.Release();}
                if(Input.GetMouseButtonUp(0))tools.Release();
                tools.Tick(Time.unscaledTimeAsDouble);
            }
            catch(Exception error){tools.Cancel();status=error.Message;}
        }
    }
}
