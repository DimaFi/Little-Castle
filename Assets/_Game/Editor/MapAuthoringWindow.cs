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
        SurfacePaintData source;
        MapEditSession session;
        bool brush,road=true,erase,scatter=true,sculpting,lower;
        float terrainRate=1.5f,minimumHeight,maximumHeight;

        MapTerrainPresentation terrainPreview;
        MapTreeCatalog treeCatalog;
        MapTreePresentation treePreview;
        int treeTool,selectedTreeSource,selectedTreePack,selectedTreeGroup,treeSeed=1729;
        long selectedTree;
        float treeScale=1,treeYaw,treeDensity=.7f,treeSpacing=6;
        Vector2 windowScroll;
        float radius=1.5f;
        float strength=1,density=1;
        MeadowScatterProfile profile;


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
            if(window.session==null)window.ConnectSceneMap();
        }
        void ConnectSceneMap()
        {
            var painter=FindAnyObjectByType<MeadowSurfacePainter>();
            if(painter&&painter.source)Connect(painter.source);
        }
        void OnEnable(){titleContent=new GUIContent("World Editor");SceneView.duringSceneGui+=SceneGUI;EditorApplication.playModeStateChanged+=PlayState;EditorApplication.update+=Tick;}
        void OnDisable(){tools?.Cancel();if(session!=null)session.Changed-=Changed;SceneView.duringSceneGui-=SceneGUI;EditorApplication.playModeStateChanged-=PlayState;EditorApplication.update-=Tick;RestorePreview();if(preview)DestroyImmediate(preview);}
        void Tick(){if(!EditorApplication.isPlaying){TickTools();terrainPreview?.FlushColliderBudget(2);var riverView=SceneView.lastActiveSceneView;if(riverView&&riverView.camera)riverPreview?.Tick(riverView.camera.transform.position);if(treePreview!=null){var view=SceneView.lastActiveSceneView;treePreview.Tick(view&&view.camera?view.camera.transform.position:Vector3.zero);}}}
        void PlayState(PlayModeStateChange state){tools?.Cancel();pointerHeld=false;if(state==PlayModeStateChange.ExitingEditMode)RestorePreview();if(state==PlayModeStateChange.EnteredEditMode&&preview)BindScenePreview();}
        internal void SuspendPreviewForSave()
        {RestorePreview();EditorApplication.delayCall+=()=>{if(this&&session!=null&&preview&&!EditorApplication.isPlaying)BindScenePreview();};}
        internal void RestorePreview()
        {
            foreach(var pair in rendererBindings)if(pair.Key)pair.Key.SetPropertyBlock(pair.Value);
            foreach(var pair in grassBindings)if(pair.Key)pair.Key.surfaceOverrides=pair.Value;
            foreach(var pair in accentBindings)if(pair.Key)pair.Key.surfaceOverrides=pair.Value;
            rendererBindings.Clear();grassBindings.Clear();accentBindings.Clear();riverPreview?.Dispose();riverPreview=null;treePreview?.Dispose();treePreview=null;terrainPreview?.Dispose();terrainPreview=null;
        }
        void BindScenePreview()
        {
            RestorePreview();if(EditorApplication.isPlaying)return;
            var painter=FindAnyObjectByType<MeadowSurfacePainter>();if(!painter||painter.source!=source)return;
            // Initialize the loaded scene's existing presentation values in Edit Mode.
            // No lighting settings, materials or shaders are authored by this window.
            var globals=FindAnyObjectByType<StylizedLightingGlobals>();if(globals)globals.ApplyGlobals();
            var ground=MeadowSurfacePainter.FindGround(painter.terrainMaterial);
            if(ground)terrainPreview=new MapTerrainPresentation(session,ground,FindAnyObjectByType<MeadowGrassRenderer>(),FindAnyObjectByType<InnerverseGrassStudy>());
            if(treeCatalog)treePreview=new MapTreePresentation(session,treeCatalog);var rockCatalog=MapRockCatalog.Default;if(rockCatalog)riverPreview=new MapRiverPresentation(session,rockCatalog);
            var b=source.bounds;var properties=new MaterialPropertyBlock();
            properties.SetTexture("_SurfacePaint",preview);properties.SetVector("_SurfacePaintBounds",new Vector4(b.x,b.y,1/b.z,1/b.w));properties.SetFloat("_UseSurfacePaint",1);
            foreach(var renderer in FindObjectsByType<Renderer>())if(renderer.sharedMaterial==painter.terrainMaterial)
            {var old=new MaterialPropertyBlock();renderer.GetPropertyBlock(old);rendererBindings.Add(renderer,old);var next=new MaterialPropertyBlock();renderer.GetPropertyBlock(next);next.SetTexture("_SurfacePaint",preview);next.SetVector("_SurfacePaintBounds",new Vector4(b.x,b.y,1/b.z,1/b.w));next.SetFloat("_UseSurfacePaint",1);renderer.SetPropertyBlock(next);}
            foreach(var grass in FindObjectsByType<MeadowGrassRenderer>())if(grass.material==painter.grassMaterial){grassBindings.Add(grass,grass.surfaceOverrides);grass.surfaceOverrides=properties;}
            foreach(var accent in FindObjectsByType<InnerverseGrassStudy>())if(accent.grassMaterial==painter.accentMaterial){accentBindings.Add(accent,accent.surfaceOverrides);accent.surfaceOverrides=properties;}
        }
        internal void Connect(SurfacePaintData data)
        {
            tools?.Cancel();pointerHeld=false;RestorePreview();
            if(session!=null)session.Changed-=Changed;
            source=data;var painter=FindAnyObjectByType<MeadowSurfacePainter>();
            var ground=painter&&painter.source==data?MeadowSurfacePainter.FindGround(painter.terrainMaterial):null;
            treeCatalog=MapTreeCatalog.Default;session=data?new MapEditSession(MeadowSurfacePainter.CreateDocument(data,ground,treeCatalog)):null;
            if(session!=null&&treeCatalog)session.RegisterTreeCatalog(treeCatalog.Definitions,treeCatalog.packs);
            if(session==null)return;
            InitializeTools();minimumHeight=session.MinimumHeight;maximumHeight=session.MaximumHeight;
            if(preview)DestroyImmediate(preview);
            preview=new Texture2D(Resolution,Resolution,TextureFormat.RG16,false,true){hideFlags=HideFlags.HideAndDontSave,filterMode=FilterMode.Bilinear};
            pixels=new byte[Resolution*Resolution*2];session.Changed+=Changed;
            Changed(new MapRegionChange(new Rect(data.bounds.x,data.bounds.y,data.bounds.z,data.bounds.w)));
            BindScenePreview();
        }
        void OnGUI()
        {
            using var scroll=new EditorGUILayout.ScrollViewScope(windowScroll);windowScroll=scroll.scrollPosition;
            EditorGUILayout.HelpBox("ЛКМ в SceneView — мазок. Alt — камера. Один мазок — одна отмена. Предпросмотр: R дорожка, G растительность. Сохранение файла не меняет базовую сцену.",MessageType.Info);
            var selected=(SurfacePaintData)EditorGUILayout.ObjectField("База карты",source,typeof(SurfacePaintData),false);
            if(selected!=source)Connect(selected);
            if(!source&&GUILayout.Button("Подключить карту открытой сцены"))ConnectSceneMap();
            if(session==null)return;
            if(EditorApplication.isPlaying){EditorGUILayout.HelpBox("Для правок в Play Mode используйте игровую панель Meadow. Авторский документ редактируется вне Play Mode.",MessageType.Info);return;}
            DrawRiverTools();
            brush=EditorGUILayout.Toggle("Инструмент в SceneView",brush);
            DrawCommonTools();
            minimumHeight=EditorGUILayout.FloatField("Нижний предел, м",minimumHeight);maximumHeight=EditorGUILayout.FloatField("Верхний предел, м",maximumHeight);
            if(GUILayout.Button("Задать пределы авторской карты"))Run(()=>
            {
                tools.Complete();pointerHeld=false;var map=session.ExportMap();map.heightField.minimumHeight=minimumHeight;map.heightField.maximumHeight=maximumHeight;
                var replacement=new MapEditSession(map);RestorePreview();session.Changed-=Changed;session=replacement;InitializeTools();if(treeCatalog)session.RegisterTreeCatalog(treeCatalog.Definitions,treeCatalog.packs);session.Changed+=Changed;BindScenePreview();
                status="Пределы изменены в авторском документе. История начата заново; сохраните JSON или примените базу.";
            });
            density=EditorGUILayout.Slider("Плотность мелочи",density,0,1);
            scatter=EditorGUILayout.Toggle("Цветы и мелочь",scatter);
            profile=(MeadowScatterProfile)EditorGUILayout.Popup("Группа мелочи",(int)profile,MeadowScatterProfiles.Names);
            EditorGUILayout.BeginHorizontal();
            if(GUILayout.Button("Отменить")){tools.Undo();pointerHeld=false;}if(GUILayout.Button("Повторить")){tools.Redo();pointerHeld=false;}
            EditorGUILayout.EndHorizontal();
            if(GUILayout.Button("Сохранить карту JSON…"))Run(()=>
            {
                tools.Complete();pointerHeld=false;var map=session.ExportMap();
                string path=EditorUtility.SaveFilePanel("Авторская карта","","handmade-map.json","json");if(path.Length>0)File.WriteAllText(path,JsonUtility.ToJson(map,true));
            });
            if(GUILayout.Button("Открыть карту JSON…"))Run(()=>
            {
                string path=EditorUtility.OpenFilePanel("Авторская карта","","json");if(path.Length==0)return;
                var loaded=new MapEditSession(JsonUtility.FromJson<MapAuthoringDocument>(File.ReadAllText(path)));
                if(loaded.MapId!=source.mapId||loaded.Bounds!=source.bounds)throw new InvalidDataException("Выбран другой ID/размер карты. Выберите соответствующую базу.");
                RestorePreview();session.Changed-=Changed;session=loaded;InitializeTools();if(treeCatalog)session.RegisterTreeCatalog(treeCatalog.Definitions,treeCatalog.packs);session.Changed+=Changed;minimumHeight=session.MinimumHeight;maximumHeight=session.MaximumHeight;BindScenePreview();
                Changed(new MapRegionChange(new Rect(source.bounds.x,source.bounds.y,source.bounds.z,source.bounds.w)));
            });
            if(GUILayout.Button("Применить к авторской базе и маске"))Run(()=>
            {
                tools.Complete();pointerHeld=false;var map=session.ExportMap();
                RestorePreview();Undo.RecordObject(source,"Apply authored map surface and heights");source.authored=map.surface;source.heightField=map.heightField;source.hasHeightBase=true;source.treeInstances=map.trees;source.hasTreeBase=true;source.rivers=map.rivers;source.rockSections=map.rockSections;source.placementExclusions=map.placementExclusions;source.revision++;
                EditorUtility.SetDirty(source);AssetDatabase.SaveAssetIfDirty(source);
                // Existing scene/lighting settings remain untouched. Only the explicit authored mask asset is updated.
                var painter=FindAnyObjectByType<MeadowSurfacePainter>();
                if(painter&&painter.source==source&&painter.authoredPreviewMask)
                {
                    var mask=painter.authoredPreviewMask;
                    if(mask.width!=Resolution||mask.height!=Resolution||mask.format!=TextureFormat.RG16)throw new InvalidDataException("Expected 512x512 RG16 authored mask.");
                    Undo.RecordObject(mask,"Apply authored surface mask");mask.SetPixelData(MeadowSurfacePainter.BuildAuthoredPixels(source),0);mask.Apply(false,false);
                    EditorUtility.SetDirty(mask);AssetDatabase.SaveAssetIfDirty(mask);
                }
                Connect(source);status="Авторская база сохранена. Игровые правки привязаны к новой ревизии.";
            });
            if(treeCatalog&&treeCatalog.entries.Length>0)
            {
                selectedTreeSource=Mathf.Clamp(selectedTreeSource,0,treeCatalog.entries.Length-1);
                GUILayout.Space(8);GUILayout.Label("Деревья · группы");
                selectedTreeGroup=EditorGUILayout.Popup("Группа деревьев",selectedTreeGroup,MapTreeCatalog.GroupNames);
                var groupIndices=treeCatalog.GroupIndices(selectedTreeGroup);
                for(int row=0;row<(groupIndices.Count+1)/2;row++)
                {
                    GUILayout.BeginHorizontal();for(int col=0;col<2;col++)
                    {
                        int cell=row*2+col;if(cell>=groupIndices.Count)break;int index=groupIndices[cell];var entry=treeCatalog.entries[index];
                        if(GUILayout.Button(new GUIContent(entry.title,entry.thumbnail),GUILayout.Width(124),GUILayout.Height(108))){selectedTreeSource=index;SetTool("tree.place");treeScale=Mathf.Clamp(1,entry.definition.minimumScale,entry.definition.maximumScale);}
                    }
                    GUILayout.EndHorizontal();
                }
                for(int p=0;p<treeCatalog.packs.Length;p++)if(GUILayout.Button("Кисть: "+MapTreeCatalog.PackTitle(treeCatalog.packs[p]))){selectedTreePack=p;SetTool("vegetation.grove");}
                if(treeTool==2){treeDensity=EditorGUILayout.Slider("Плотность",treeDensity,0,1);treeSpacing=EditorGUILayout.Slider("Интервал, м",treeSpacing,2,16);treeSeed=EditorGUILayout.IntField("Seed",treeSeed);}
                treeYaw=EditorGUILayout.Slider("Поворот дерева",treeYaw,0,359);
                var definition=treeCatalog.entries[selectedTreeSource].definition;treeScale=EditorGUILayout.Slider("Масштаб дерева",treeScale,definition.minimumScale,definition.maximumScale);
                if(GUILayout.Button("Выбрать дерево по кроне")){SetTool("tree.pick");}
                if(selectedTree!=0&&session.HasTree(selectedTree))
                {
                    GUILayout.Label("Выбрано: "+session.GetTree(selectedTree).archetypeId);
                    GUILayout.BeginHorizontal();if(GUILayout.Button("Переместить")){SetTool("tree.move");}
                    if(GUILayout.Button("Повернуть / масштаб"))Run(()=>{var r=session.GetTree(selectedTree);session.TransformTree(selectedTree,r.position,treeYaw,treeScale);});
                    if(GUILayout.Button("Удалить"))Run(()=>{session.DeleteTree(selectedTree);selectedTree=0;});GUILayout.EndHorizontal();
                }
                if(treeTool!=0&&GUILayout.Button("Закончить работу с деревьями")){tools.Complete();session.CommitStroke();treeTool=0;brush=false;}
            }
            GUILayout.Label(status??"");
            if(preview){float size=Mathf.Min(position.width-20,300);GUILayout.Label(preview,GUILayout.Width(size),GUILayout.Height(size));}
        }
        void Run(Action action){try{action();}catch(Exception e){status=e.Message;Debug.LogException(e);}Repaint();}
        void Changed(MapRegionChange change)
        {
            if((change.kind&MapChangeKind.Surface)==0){Repaint();SceneView.RepaintAll();return;}
            var b=source.bounds;var rect=SurfacePaintRasterizer.PixelRect(b,Resolution,Resolution,change.bounds);
            var area=new Rect(b.x+rect.x*b.z/Resolution,b.y+rect.y*b.w/Resolution,rect.width*b.z/Resolution,rect.height*b.w/Resolution);
            SurfacePaintRasterizer.Rebuild(pixels,b,Resolution,Resolution,session.QuerySurface(area),rect);
            SurfacePaintRasterizer.ApplyRiverExclusions(pixels,b,Resolution,Resolution,session,rect);
            preview.SetPixelData(pixels,0);preview.Apply(false,false);Repaint();SceneView.RepaintAll();
        }

    }
    public sealed class MapAuthoringSaveGuard : AssetModificationProcessor
    {
        static string[] OnWillSaveAssets(string[] paths)
        {foreach(var window in Resources.FindObjectsOfTypeAll<MapAuthoringWindow>())window.SuspendPreviewForSave();return paths;}
    }
}
