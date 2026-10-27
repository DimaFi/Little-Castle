using System;
using System.IO;
using LittleCastle.Rendering;
using LittleCastle.World;
using UnityEngine.Rendering;
using UnityEngine;
using UnityEngine.EventSystems;

namespace LittleCastle.Research
{
    /// <summary>Study front-end for the same stroke format used by authored paths and runtime edits.</summary>
    public sealed class MeadowSurfacePainter : MonoBehaviour
    {
        public SurfacePaintData source;
        public Camera studyCamera;
        public Material terrainMaterial,grassMaterial,accentMaterial;
        public Material brushMaterial;
        public Texture2D authoredPreviewMask;
        public bool painting,road=true;
        public bool erase,populatePlants=true;
        public MeadowScatterProfile currentProfile=MeadowScatterProfile.Mixed;
        public MeadowScatterRenderer scatterRenderer;
        [Range(.3f,4)] public float radius=1.5f;
        [Range(0,1)] public float strength=1,density=1;
        public float stampsPerSecond=15;
        public string status="";
        public const int Resolution=512;
        public Texture2D mask {get;private set;}
        public MapEditAccess allowedTools=MapEditAccess.All;
        public MapEditSession Session {get;private set;}
        public MapRiverPresentation RiverView {get;private set;}
        public SurfacePaintDocument edits=>Session?.ExportEdits()??new SurfacePaintDocument();
        public bool sculpting,lowerTerrain;
        [Range(.1f,4)] public float terrainMetresPerSecond=1.5f;
        public MapTerrainPresentation TerrainView {get;private set;}
        public MapTreeCatalog treeCatalog;
        public MapTreePresentation TreeView {get;private set;}
        public bool playerMode;
        public int treeTool;
        public int selectedTreeSource,selectedTreePack,selectedTreeGroup;
        public float treeScale=1,treeYaw,treeBrushSpacing=6,treeBrushDensity=.7f;
        public int treeBrushSeed=1729;
        public long selectedTree;
        bool treeClickHeld;
        int treeCancelFrame=-1;
        public bool ShouldQuitOnEscape=>treeTool==0&&treeCancelFrame!=Time.frameCount&&!(GetComponent<MapRiverRuntimeTool>() is MapRiverRuntimeTool rt&&(rt.Active||rt.LastEscapeFrame==Time.frameCount));
        public int lastUpdatedPixels {get;private set;}
        public bool regionalGpuUpload {get;private set;}
        Texture2D uploadTile;
        byte[] pixels;
        
        Vector2 last;
        bool held;
        float nextStamp;
        
        LineRenderer cursor;
        Material cursorMaterial;
        struct Binding {public Material material;public Texture texture;public Vector4 bounds;public bool keyword;public float enabled;}
        Binding[] previousBindings;
        string SavePath=>Path.Combine(Application.persistentDataPath,"meadow-map-edits-v5.json");
        string V4SavePath=>Path.Combine(Application.persistentDataPath,"meadow-map-edits-v4.json");
        string V3SavePath=>Path.Combine(Application.persistentDataPath,"meadow-map-edits-v3.json");
        string V2SavePath=>Path.Combine(Application.persistentDataPath,"meadow-map-edits-v2.json");
        string LegacySavePath=>Path.Combine(Application.persistentDataPath,"meadow-surface-edits-v1.json");
        void Start()
        {
            var ground=FindGround(terrainMaterial);
            treeCatalog=treeCatalog?treeCatalog:MapTreeCatalog.Default;
            var document=CreateDocument(source,ground,treeCatalog);
            Session=new MapEditSession(document,allowedTools);
            if(ground)TerrainView=new MapTerrainPresentation(Session,ground,FindAnyObjectByType<MeadowGrassRenderer>(),FindAnyObjectByType<InnerverseGrassStudy>(),scatterRenderer);
            var rockCatalog=MapRockCatalog.Default;if(rockCatalog){RiverView=new MapRiverPresentation(Session,rockCatalog);gameObject.AddComponent<MapRiverRuntimeTool>().Connect(this);}
            if(treeCatalog){Session.RegisterTreeCatalog(treeCatalog.Definitions,treeCatalog.packs);TreeView=new MapTreePresentation(Session,treeCatalog);}
            mask=new Texture2D(Resolution,Resolution,TextureFormat.RG16,false,true){name="Derived road and meadow mask · not save state",filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
            pixels=new byte[Resolution*Resolution*2];
            cursor=new GameObject("Single brush footprint preview").AddComponent<LineRenderer>();cursor.transform.SetParent(transform,false);cursor.loop=true;cursor.positionCount=48;cursor.widthMultiplier=.035f;cursor.useWorldSpace=true;cursor.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;cursor.receiveShadows=false;cursorMaterial=new Material(brushMaterial);cursor.sharedMaterial=cursorMaterial;cursor.enabled=false;
            var materials=new[]{terrainMaterial,grassMaterial,accentMaterial};previousBindings=new Binding[materials.Length];
            for(int i=0;i<materials.Length;i++)
            {
                var material=materials[i];if(!material)continue;
                previousBindings[i]=new Binding{material=material,texture=material.GetTexture("_SurfacePaint"),bounds=material.GetVector("_SurfacePaintBounds"),keyword=material.IsKeywordEnabled("_LC_SURFACE_PAINT"),enabled=material.GetFloat("_UseSurfacePaint")};
                BindMask(material,mask,source.bounds);
            }
            Rebuild();
            Session.Changed+=OnMapChanged;
        }
        void OnDestroy()
        {
            if(Session!=null)Session.Changed-=OnMapChanged;
            RiverView?.Dispose();RiverView=null;TreeView?.Dispose();TreeView=null;TerrainView?.Dispose();TerrainView=null;
            if(uploadTile)Destroy(uploadTile);
            if(previousBindings!=null)foreach(var state in previousBindings)
            {
                var material=state.material;if(!material||material.GetTexture("_SurfacePaint")!=mask)continue;
                material.SetTexture("_SurfacePaint",state.texture);material.SetVector("_SurfacePaintBounds",state.bounds);material.SetFloat("_UseSurfacePaint",state.enabled);
                if(state.keyword)material.EnableKeyword("_LC_SURFACE_PAINT");else material.DisableKeyword("_LC_SURFACE_PAINT");
            }
            if(mask)Destroy(mask);if(cursorMaterial)Destroy(cursorMaterial);
        }
        public static void BindMask(Material material,Texture texture,Vector4 bounds)
        {
            material.SetTexture("_SurfacePaint",texture);material.SetVector("_SurfacePaintBounds",new Vector4(bounds.x,bounds.y,1/bounds.z,1/bounds.w));material.EnableKeyword("_LC_SURFACE_PAINT");material.SetFloat("_UseSurfacePaint",1);
        }
        void Update()
        {
            if(cursor)cursor.enabled=false;
            Session.Access=playerMode?allowedTools&MapEditAccess.Player:allowedTools;
            if(GetComponent<MapRiverRuntimeTool>() is MapRiverRuntimeTool riverTool&&riverTool.Active)return;
            if(treeTool!=0){UpdateTreeTool();return;}
            if(!painting){EndStroke();return;}
            if(Input.GetKey(KeyCode.LeftControl)||Input.GetKey(KeyCode.RightControl))
            {if(Input.GetKeyDown(KeyCode.Z))Undo();if(Input.GetKeyDown(KeyCode.Y))Redo();}
            if(Input.mousePosition.x>320*Mathf.Clamp(Screen.width/1600f,.85f,1.3f)&&RaycastGround(studyCamera.ScreenPointToRay(Input.mousePosition),out var hover))
            {cursor.enabled=true;for(int i=0;i<48;i++){float a=i*Mathf.PI*2/48;float x=hover.point.x+Mathf.Cos(a)*radius,z=hover.point.z+Mathf.Sin(a)*radius;cursor.SetPosition(i,new Vector3(x,Session.SampleHeight(x,z)+.05f,z));}}
            if(Input.GetMouseButtonUp(0))EndStroke();
            if(!Input.GetMouseButton(0))return;
            if(Input.mousePosition.x<320*Mathf.Clamp(Screen.width/1600f,.85f,1.3f)){EndStroke();return;}
            if(EventSystem.current && EventSystem.current.IsPointerOverGameObject()){EndStroke();return;}
            if(Time.unscaledTime<nextStamp)return;nextStamp=Time.unscaledTime+1/Mathf.Max(1,stampsPerSecond);
            if(!RaycastGround(studyCamera.ScreenPointToRay(Input.mousePosition),out var hit))return;
            Vector2 p=new Vector2(hit.point.x,hit.point.z);if(p.x<source.bounds.x||p.x>source.bounds.x+source.bounds.z||p.y<source.bounds.y||p.y>source.bounds.y+source.bounds.w)return;
            try{if(!held){BeginStroke(road,radius);last=p;held=true;}else if(!sculpting&&Vector2.SqrMagnitude(p-last)<.025f)return;
                if(sculpting)Session.AppendHeightPoint(p);else Session.AppendPoint(p);last=p;}catch(Exception error){Session.CancelStroke();held=false;status=error.Message;}
        }
        void LateUpdate(){if(RiverView!=null&&studyCamera)RiverView.Tick(studyCamera.transform.position);TerrainView?.FlushColliderBudget(2);if(TreeView!=null&&studyCamera)TreeView.Tick(studyCamera.transform.position);}
        public static MeshFilter FindGround(Material material)
        {
            if(!material)return null;
            foreach(var mesh in FindObjectsByType<MeshFilter>())
            {var renderer=mesh.GetComponent<MeshRenderer>();if(renderer&&renderer.sharedMaterial==material&&(mesh.gameObject.hideFlags&HideFlags.DontSave)==0)return mesh;}return null;
        }
        public static MapAuthoringDocument CreateDocument(SurfacePaintData data,MeshFilter ground,MapTreeCatalog catalog=null)
        {
            var document=MapEditSession.FromSurface(data);
            if(document.heightField==null&&ground)document.heightField=MapHeightMeshImport.Read(ground.sharedMesh,ground.transform,out _,out _);
            catalog=catalog?catalog:MapTreeCatalog.Default;if(!data.hasTreeBase&&catalog)catalog.ImportSceneBase(document,ground);
            return document;
        }
        public bool RaycastGround(Ray ray,out RaycastHit hit)=>TerrainView!=null?TerrainView.Raycast(ray,out hit,300):Physics.Raycast(ray,out hit,300);
        void UpdateTreeTool()
        {
            if(!treeCatalog||treeCatalog.entries.Length==0){CancelTreeTool();return;}selectedTreeSource=Mathf.Clamp(selectedTreeSource,0,treeCatalog.entries.Length-1);
            if(Input.GetKeyDown(KeyCode.Escape)||Input.GetMouseButtonDown(1)){CancelTreeTool();return;}
            if(Input.GetMouseButtonUp(0)){treeClickHeld=false;EndStroke();}
            if(!studyCamera||Input.mousePosition.x<320*Mathf.Clamp(Screen.width/1600f,.85f,1.3f))return;
            if(EventSystem.current&&EventSystem.current.IsPointerOverGameObject())return;
            Ray ray=studyCamera.ScreenPointToRay(Input.mousePosition);
            if(treeTool==3&&Input.GetMouseButtonDown(0)){if(TreeView!=null&&TreeView.Pick(ray,out long id)){selectedTree=id;var r=Session.GetTree(id);int source=Array.FindIndex(treeCatalog.entries,e=>e.definition.archetypeId==r.archetypeId);if(source>=0)selectedTreeSource=source;treeYaw=r.yawDegrees;treeScale=r.uniformScale;}return;}
            if(!RaycastGround(ray,out var hit))return;Vector2 p=new Vector2(hit.point.x,hit.point.z);
            if(cursor){cursor.enabled=true;float ring=treeTool==2?radius:2;for(int i=0;i<48;i++){float a=i*Mathf.PI*2/48;float x=p.x+Mathf.Cos(a)*ring,z=p.y+Mathf.Sin(a)*ring;cursor.SetPosition(i,new Vector3(x,Session.SampleHeight(x,z)+.05f,z));}}
            try
            {
                if(treeTool==1&&Input.GetMouseButtonDown(0))selectedTree=Session.PlaceTree(treeCatalog.entries[selectedTreeSource].definition.archetypeId,p,treeYaw,treeScale);
                if(treeTool==4&&Input.GetMouseButtonDown(0)&&Session.HasTree(selectedTree)){Session.TransformTree(selectedTree,p,treeYaw,treeScale);treeTool=3;}
                if(treeTool==2&&Input.GetMouseButton(0)&&Time.unscaledTime>=nextStamp)
                {
                    nextStamp=Time.unscaledTime+1/Mathf.Max(1,stampsPerSecond);
                    if(!treeClickHeld){Session.BeginTreeBrush(treeCatalog.packs[selectedTreePack].id,radius,treeBrushDensity,treeBrushSpacing,treeBrushSeed);treeClickHeld=true;}
                    Session.AppendTreeBrush(p);
                }
            }
            catch(Exception e){status=e.Message;Session.CancelStroke();treeClickHeld=false;}
        }
        public void SetTreeTool(int mode){EndStroke();painting=false;treeTool=mode;treeClickHeld=false;}
        public void CancelTreeTool(){Session.CancelStroke();treeTool=0;treeClickHeld=false;treeCancelFrame=Time.frameCount;}
        public void ApplySelectedTreeTransform(){try{if(Session.HasTree(selectedTree)){var r=Session.GetTree(selectedTree);Session.TransformTree(selectedTree,r.position,treeYaw,treeScale);}}catch(Exception e){status=e.Message;}}
        public void DeleteSelectedTree(){try{if(Session.HasTree(selectedTree))Session.DeleteTree(selectedTree);selectedTree=0;}catch(Exception e){status=e.Message;}}
        void OnApplicationFocus(bool focus){if(!focus)EndStroke();}
        public void EndStroke(){held=false;treeClickHeld=false;Session?.CommitStroke();}
        public void BeginStroke(bool isRoad,float brushRadius)
        {
            if(sculpting){Session.BeginHeightStroke(brushRadius,(lowerTerrain?-1:1)*terrainMetresPerSecond/Mathf.Max(1,stampsPerSecond));return;}
            Session.BeginStroke(new SurfacePaintStroke{id=Guid.NewGuid().ToString("N"),road=isRoad,erase=erase,
                scatter=!isRoad&&!erase&&populatePlants,scatterProfile=currentProfile,radius=brushRadius,strength=strength,density=density});
        }
        public void AddStroke(SurfacePaintStroke stroke)=>Session.AddStroke(stroke);
        public void Undo(){held=false;try{Session.Undo();}catch(Exception e){status=e.Message;}}
        public void Redo(){held=false;try{Session.Redo();}catch(Exception e){status=e.Message;}}
        public void ResetEdits(){held=false;try{Session.ClearEdits();}catch(Exception e){status=e.Message;}}
        public void SaveEdits()
        {
            try{EndStroke();string temporary=SavePath+".tmp";File.WriteAllText(temporary,ExportJson());
                if(File.Exists(SavePath))File.Replace(temporary,SavePath,SavePath+".bak");else File.Move(temporary,SavePath);
                status="Сохранено локально";
            }catch(Exception error){status="Не удалось сохранить: "+error.Message;}
        }
        public void LoadEdits()
        {
            try{string path=File.Exists(SavePath)?SavePath:File.Exists(V4SavePath)?V4SavePath:File.Exists(V3SavePath)?V3SavePath:File.Exists(V2SavePath)?V2SavePath:LegacySavePath;if(!File.Exists(path)){status="Сохранения пока нет";return;}
                LoadJson(File.ReadAllText(path));status="Изменения загружены";
            }catch(Exception error){status="Не удалось загрузить: "+error.Message;}
        }
        public string ExportJson(){EndStroke();return Session.ExportJson();}
        public void LoadJson(string json){Session.LoadJson(json);held=false;}
        public static byte[] BuildAuthoredPixels(SurfacePaintData data)
        {
            var result=new byte[Resolution*Resolution*2];
            SurfacePaintRasterizer.Rebuild(result,data.bounds,Resolution,Resolution,data.authored.strokes,new RectInt(0,0,Resolution,Resolution));return result;
        }
        void Rebuild()
        {
            var b=source.bounds;var area=new Rect(b.x,b.y,b.z,b.w);
            SurfacePaintRasterizer.Rebuild(pixels,b,Resolution,Resolution,Session.QuerySurface(area),new RectInt(0,0,Resolution,Resolution));
            SurfacePaintRasterizer.ApplyRiverExclusions(pixels,b,Resolution,Resolution,Session,new RectInt(0,0,Resolution,Resolution));
            mask.SetPixelData(pixels,0);mask.Apply(false,false);scatterRenderer?.Rebuild(this);
        }
        void OnMapChanged(MapRegionChange change)
        {
            if((change.kind&MapChangeKind.Surface)==0)return;
            var b=source.bounds;var rect=SurfacePaintRasterizer.PixelRect(b,Resolution,Resolution,change.bounds);
            if(rect.width==0||rect.height==0)return;
            // Include the rounded pixel footprint when querying spatially adjacent strokes.
            var area=new Rect(b.x+rect.x*b.z/Resolution,b.y+rect.y*b.w/Resolution,rect.width*b.z/Resolution,rect.height*b.w/Resolution);
            SurfacePaintRasterizer.Rebuild(pixels,b,Resolution,Resolution,Session.QuerySurface(area),rect);
            SurfacePaintRasterizer.ApplyRiverExclusions(pixels,b,Resolution,Resolution,Session,rect);
            lastUpdatedPixels=rect.width*rect.height;Upload(rect);scatterRenderer?.Rebuild(this,area);
        }
        void Upload(RectInt rect)
        {
            regionalGpuUpload=(SystemInfo.copyTextureSupport&CopyTextureSupport.Basic)!=0;
            if(!regionalGpuUpload){mask.SetPixelData(pixels,0);mask.Apply(false,false);return;}
            if(!uploadTile||uploadTile.width!=rect.width||uploadTile.height!=rect.height)
            {
                if(uploadTile)Destroy(uploadTile);
                uploadTile=new Texture2D(rect.width,rect.height,TextureFormat.RG16,false,true){name="Map dirty rectangle upload"};
            }
            var region=new byte[rect.width*rect.height*2];
            for(int y=0;y<rect.height;y++)Array.Copy(pixels,((rect.y+y)*Resolution+rect.x)*2,region,y*rect.width*2,rect.width*2);
            uploadTile.SetPixelData(region,0);uploadTile.Apply(false,false);
            Graphics.CopyTexture(uploadTile,0,0,0,0,rect.width,rect.height,mask,0,0,rect.x,rect.y);
        }
        public byte Sample(float x,float z)=>SampleChannel(x,z,0);
        byte SampleChannel(float x,float z,int channel){var b=source.bounds;int ix=Mathf.Clamp((int)((x-b.x)*Resolution/b.z),0,Resolution-1),iy=Mathf.Clamp((int)((z-b.y)*Resolution/b.w),0,Resolution-1);return channel==1&&!Session.VegetationAllowed(x,z)?(byte)0:pixels[(iy*Resolution+ix)*2+channel];}
        public float SampleRoad01(float x,float z)=>SampleChannel(x,z,0)/255f;
        public float SampleGrass01(float x,float z)=>SampleChannel(x,z,1)/255f;
        public ulong MaskDigest(){ulong hash=14695981039346656037UL;foreach(byte b in pixels){hash^=b;hash=unchecked(hash*1099511628211UL);}return hash;}
    }
}
