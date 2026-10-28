using System;
using System.IO;
using System.Collections.Generic;
using LittleCastle.Rendering;
using LittleCastle.Research;
using LittleCastle.World;
using UnityEditor;
using UnityEngine;

namespace LittleCastle.Editor
{
    /// <summary>SceneView adapter; authoritative editing uses the same session as the player.</summary>
    public sealed partial class MapAuthoringWindow : EditorWindow
    {
        [SerializeField] SurfacePaintData source;
        MapEditSession session;
        [SerializeField] bool brush,road=true,erase,scatter=true,sculpting,lower;
        [SerializeField] float terrainRate=1.5f,minimumHeight,maximumHeight;

        MapTerrainPresentation terrainPreview;
        MapTreeCatalog treeCatalog;
        MapTreePresentation treePreview;
        [SerializeField] int treeTool,selectedTreeSource,selectedTreePack,selectedTreeGroup,treeSeed=1729;
        [SerializeField] long selectedTree;
        [SerializeField] float treeScale=1,treeYaw,treeDensity=.7f,treeSpacing=6;
        [SerializeField] Vector2 windowScroll;
        [SerializeField] float radius=1.5f;
        [SerializeField] float strength=1,density=1;
        [SerializeField] MeadowScatterProfile profile;


        string status;
        Texture2D preview;
        byte[] pixels;
        const int Resolution=512;
        readonly Dictionary<Renderer,MaterialPropertyBlock> rendererBindings=new Dictionary<Renderer,MaterialPropertyBlock>();
        readonly Dictionary<MeadowGrassRenderer,MaterialPropertyBlock> grassBindings=new Dictionary<MeadowGrassRenderer,MaterialPropertyBlock>();
        readonly Dictionary<InnerverseGrassStudy,MaterialPropertyBlock> accentBindings=new Dictionary<InnerverseGrassStudy,MaterialPropertyBlock>();
        [MenuItem("Little Castle/Map/World Editor")]
        static void Open()
        {
            var window=GetWindow<MapAuthoringWindow>("World Editor");
            window.titleContent=new GUIContent("World Editor");
            if(window.session==null)window.RestoreWindowSession();
        }
        void ConnectSceneMap()
        {
            var painter=FindAnyObjectByType<MeadowSurfacePainter>();
            if(painter&&painter.source)Connect(painter.source);
        }
        void OnEnable()
        {
            restoreAttempted=false;
            titleContent=new GUIContent("World Editor");minSize=new Vector2(340,420);
            SceneView.duringSceneGui+=SceneGUI;EditorApplication.playModeStateChanged+=PlayState;EditorApplication.update+=Tick;
            AssemblyReloadEvents.beforeAssemblyReload+=CheckpointSession;EditorApplication.delayCall+=RestoreWindowSession;
        }
        void OnDisable()
        {
            CheckpointSession();if(session!=null)session.Changed-=Changed;
            SceneView.duringSceneGui-=SceneGUI;EditorApplication.playModeStateChanged-=PlayState;EditorApplication.update-=Tick;
            AssemblyReloadEvents.beforeAssemblyReload-=CheckpointSession;EditorApplication.delayCall-=RestoreWindowSession;
            RestorePreview();if(preview)DestroyImmediate(preview);
        }
        void Tick(){if(!EditorApplication.isPlaying){ConfigureWaterPreviewCameras();TickTools();terrainPreview?.FlushColliderBudget(2);var riverView=SceneView.lastActiveSceneView;if(riverView&&riverView.camera)riverPreview?.Tick(riverView.camera.transform.position);if(treePreview!=null){var view=SceneView.lastActiveSceneView;treePreview.Tick(view&&view.camera?view.camera.transform.position:Vector3.zero);}}}
        void PlayState(PlayModeStateChange state)
        {
            if(state==PlayModeStateChange.ExitingEditMode){CheckpointSession();RestorePreview();}
            if(state==PlayModeStateChange.EnteredEditMode)RestoreWindowSession();
        }
        internal void SuspendPreviewForSave()
        {RestorePreview();EditorApplication.delayCall+=()=>{if(this&&session!=null&&preview&&!EditorApplication.isPlaying)BindScenePreview();};}
        internal void RestorePreview()
        {
            RestoreScatterPreview();
            RestoreWaterPreviewCameras();
            foreach(var pair in rendererBindings)if(pair.Key)pair.Key.SetPropertyBlock(pair.Value);
            foreach(var pair in grassBindings)if(pair.Key)pair.Key.surfaceOverrides=pair.Value;
            foreach(var pair in accentBindings)if(pair.Key)pair.Key.surfaceOverrides=pair.Value;
            rendererBindings.Clear();grassBindings.Clear();accentBindings.Clear();riverPreview?.Dispose();riverPreview=null;treePreview?.Dispose();treePreview=null;terrainPreview?.Dispose();terrainPreview=null;
        }
        void BindScenePreview()
        {
            ConfigureRiverProfile();RestorePreview();if(EditorApplication.isPlaying)return;
            var painter=FindAnyObjectByType<MeadowSurfacePainter>();if(!painter||painter.source!=source)return;
            // Initialize the loaded scene's existing presentation values in Edit Mode.
            // No lighting settings, materials or shaders are authored by this window.
            var globals=FindAnyObjectByType<StylizedLightingGlobals>();if(globals)globals.ApplyGlobals();
            var ground=MeadowSurfacePainter.FindGround(painter.terrainMaterial);
            if(ground)terrainPreview=new MapTerrainPresentation(session,ground,FindAnyObjectByType<MeadowGrassRenderer>(),FindAnyObjectByType<InnerverseGrassStudy>());
            if(treeCatalog)treePreview=new MapTreePresentation(session,treeCatalog);
            var rockCatalog=painter.riverCatalogOverride?painter.riverCatalogOverride:MapRockCatalog.Default;
            if(rockCatalog)riverPreview=new MapRiverPresentation(session,rockCatalog,painter.waterOnlyRivers);
            previewNeedsWaterBuffers=painter.waterOnlyRivers;ConfigureWaterPreviewCameras();
            var b=source.bounds;var properties=new MaterialPropertyBlock();
            properties.SetTexture("_SurfacePaint",preview);properties.SetVector("_SurfacePaintBounds",new Vector4(b.x,b.y,1/b.z,1/b.w));properties.SetFloat("_UseSurfacePaint",1);
            foreach(var renderer in FindObjectsByType<Renderer>())if(renderer.sharedMaterial==painter.terrainMaterial)
            {var old=new MaterialPropertyBlock();renderer.GetPropertyBlock(old);rendererBindings.Add(renderer,old);var next=new MaterialPropertyBlock();renderer.GetPropertyBlock(next);next.SetTexture("_SurfacePaint",preview);next.SetVector("_SurfacePaintBounds",new Vector4(b.x,b.y,1/b.z,1/b.w));next.SetFloat("_UseSurfacePaint",1);renderer.SetPropertyBlock(next);}
            foreach(var grass in FindObjectsByType<MeadowGrassRenderer>())if(grass.material==painter.grassMaterial){grassBindings.Add(grass,grass.surfaceOverrides);grass.surfaceOverrides=properties;}
            foreach(var accent in FindObjectsByType<InnerverseGrassStudy>())if(accent.grassMaterial==painter.accentMaterial){accentBindings.Add(accent,accent.surfaceOverrides);accent.surfaceOverrides=properties;}
            BindScatterPreview(painter);
        }
        internal void Connect(SurfacePaintData data)
        {
            restoreAttempted=true;
            tools?.Complete();pointerHeld=false;RestorePreview();
            if(session!=null)session.Changed-=Changed;
            source=data;var painter=FindAnyObjectByType<MeadowSurfacePainter>();
            var ground=painter&&painter.source==data?MeadowSurfacePainter.FindGround(painter.terrainMaterial):null;
            treeCatalog=MapTreeCatalog.Default;session=data?new MapEditSession(MeadowSurfacePainter.CreateDocument(data,ground,treeCatalog)):null;
            if(session!=null&&treeCatalog)session.RegisterTreeCatalog(treeCatalog.Definitions,treeCatalog.packs);
            sessionCheckpoint=null;riverCheckpoint=null;riverTools=new MapRiverToolState();documentDirty=false;hasUnsavedChanges=false;
            ConfigureRiverProfile();
            if(session==null)return;
            InitializeTools();minimumHeight=session.MinimumHeight;maximumHeight=session.MaximumHeight;
            if(preview)DestroyImmediate(preview);
            preview=new Texture2D(Resolution,Resolution,TextureFormat.RG16,false,true){hideFlags=HideFlags.HideAndDontSave,filterMode=FilterMode.Bilinear};
            pixels=new byte[Resolution*Resolution*2];session.Changed+=Changed;
            RebuildPreview();
            BindScenePreview();
        }
        void OnGUI()
        {
            DrawMapHeader();
            if(EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorGUILayout.HelpBox("Авторский документ сохранён в окне. Для правок выйдите из Play Mode.",MessageType.Info);return;
            }
            if(session==null)
            {
                if(source&&!restoreAttempted)RestoreWindowSession();
                EditorGUILayout.HelpBox(status??"Выберите базу карты или подключите карту открытой сцены.",MessageType.Info);
                if(source&&GUILayout.Button("Повторить восстановление документа"))RestoreWindowSession();return;
            }
            hasUnsavedChanges=documentDirty||riverTools.HasUnappliedDraft(session);
            DrawToolSelection();
            using var scroll=new EditorGUILayout.ScrollViewScope(windowScroll);windowScroll=scroll.scrollPosition;
            DrawCommonTools();
            if(toolCategory==0)DrawHeightLimits();
            else if(toolCategory==1&&CurrentToolId=="surface.meadow")DrawScatterSettings();
            else if(toolCategory==2)DrawVegetationTools();
            else if(toolCategory==3)DrawRiverTools();
            DrawBrushPreset();
            if(!string.IsNullOrEmpty(status))EditorGUILayout.HelpBox(status,MessageType.None);
            showPreview=EditorGUILayout.Foldout(showPreview,"Маска покрытия · R дорожка / G растительность",true);
            if(showPreview&&preview){float size=Mathf.Min(position.width-24,300);GUILayout.Label(preview,GUILayout.Width(size),GUILayout.Height(size));}
        }
        void Run(Action action){try{action();}catch(Exception e){status=e.Message;Debug.LogException(e);}Repaint();}
        void Changed(MapRegionChange change)
        {
            if(!rebuildingPreview)MarkDocumentDirty();
            if((change.kind&MapChangeKind.Surface)==0){if((change.kind&MapChangeKind.Height)!=0)RebuildScatterPreview(change.bounds);Repaint();SceneView.RepaintAll();return;}
            var b=source.bounds;var rect=SurfacePaintRasterizer.PixelRect(b,Resolution,Resolution,change.bounds);
            var area=new Rect(b.x+rect.x*b.z/Resolution,b.y+rect.y*b.w/Resolution,rect.width*b.z/Resolution,rect.height*b.w/Resolution);
            SurfacePaintRasterizer.Rebuild(pixels,b,Resolution,Resolution,session.QuerySurface(area),rect);
            SurfacePaintRasterizer.ApplyRiverExclusions(pixels,b,Resolution,Resolution,session,rect);
            preview.SetPixelData(pixels,0);preview.Apply(false,false);RebuildScatterPreview(area);Repaint();SceneView.RepaintAll();
        }

    }
    public sealed class MapAuthoringSaveGuard : AssetModificationProcessor
    {
        static string[] OnWillSaveAssets(string[] paths)
        {foreach(var window in Resources.FindObjectsOfTypeAll<MapAuthoringWindow>())window.SuspendPreviewForSave();return paths;}
    }
}
