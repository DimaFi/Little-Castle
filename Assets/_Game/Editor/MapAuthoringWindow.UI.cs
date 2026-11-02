using System;
using System.Collections.Generic;
using System.IO;
using LittleCastle.Rendering;
using LittleCastle.Research;
using LittleCastle.World;
using UnityEditor;
using UnityEngine;

namespace LittleCastle.Editor
{
    public sealed partial class MapAuthoringWindow
    {
        static readonly string[] CategoryNames={"Рельеф","Покрытие","Растительность","Реки","Биомы и снег","Заборы"};
        [SerializeField] int toolCategory=1;
        [SerializeField] string activeToolId="surface.road";
        [SerializeField] bool showHeightLimits,showBrushPreset,showPreview;
        [SerializeField] string sessionCheckpoint,riverCheckpoint;
        [SerializeField] bool documentDirty;
        bool rebuildingPreview,restoreAttempted;

        void RunGui(Action action){Run(action);GUIUtility.ExitGUI();}
        void DrawMapHeader()
        {
            using(new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using(new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
                {
                    var selected=(SurfacePaintData)EditorGUILayout.ObjectField("База карты",source,typeof(SurfacePaintData),false);
                    if(selected!=source&&ConfirmReplaceDocument()){Connect(selected);GUIUtility.ExitGUI();}
                    if(!source&&GUILayout.Button("Подключить карту открытой сцены")){ConnectSceneMap();GUIUtility.ExitGUI();}
                    using(new EditorGUI.DisabledScope(session==null))
                    {
                        using(new EditorGUILayout.HorizontalScope())
                        {
                            if(GUILayout.Button("Открыть JSON…"))RunGui(OpenDocument);
                            if(GUILayout.Button("Сохранить JSON…"))RunGui(()=>SaveDocument());
                        }
                        if(GUILayout.Button("Проверить текущую карту в Game…"))
                            RunGui(()=>CurrentMapGameInspection.Open(this,source));
                        if(GUILayout.Button("Применить к авторской базе и маске"))RunGui(ApplyDocument);
                        using(new EditorGUILayout.HorizontalScope())
                        {
                            using(new EditorGUI.DisabledScope(session==null||!session.CanUndo))
                                if(GUILayout.Button("Отменить"))RunGui(()=>{tools.Undo();pointerHeld=false;riverTools.Refresh(session);});
                            using(new EditorGUI.DisabledScope(session==null||!session.CanRedo))
                                if(GUILayout.Button("Повторить"))RunGui(()=>{tools.Redo();pointerHeld=false;riverTools.Refresh(session);});
                            if(GUILayout.Button("Вся карта"))FrameMap();
                        }
                    }
                }
                if(source)EditorGUILayout.LabelField(source.mapId+" · "+source.bounds.z+" × "+source.bounds.w+" м"+(hasUnsavedChanges?" · есть несохранённые правки":""),EditorStyles.miniLabel);
            }
        }

        void DrawToolSelection()
        {
            int columns=Mathf.Clamp(Mathf.FloorToInt((position.width-16)/135),1,CategoryNames.Length);
            int category=GUILayout.SelectionGrid(toolCategory,CategoryNames,columns,EditorStyles.miniButton);
            if(category!=toolCategory)
            {
                string id=category==0?"terrain.raise":category==1?"surface.road":category==2?"vegetation.flowers":category==3?"river.points":category==5?"fence.draw":"environment.biome";
                SetTool(id);
                // The following control tree belongs to the new category. Rebuild it on Layout.
                GUIUtility.ExitGUI();
            }
            var ids=new List<string>();var names=new List<string>();int selected=0;
            foreach(var tool in tools.registry.Tools)
            {
                if(CategoryForTool(tool)!=toolCategory)continue;
                if(tool.Id==CurrentToolId)selected=ids.Count;
                ids.Add(tool.Id);names.Add(tool.Title);
            }
            if(ids.Count>0)
            {
                int next=EditorGUILayout.Popup("Активный инструмент",selected,names.ToArray());
                if(next!=selected){SetTool(ids[next]);GUIUtility.ExitGUI();}
            }
            bool enabled=EditorGUILayout.ToggleLeft("Рисовать в SceneView",brush,EditorStyles.boldLabel);
            if(enabled!=brush){tools.Complete();pointerHeld=false;brush=enabled;SceneView.RepaintAll();}
            if(toolCategory!=3)EditorGUILayout.HelpBox("ЛКМ — действие · Esc — отменить текущий штрих и выключить рисование. Alt / ПКМ / СКМ / колесо — камера.",MessageType.None);
        }

        static int CategoryForTool(IMapTool tool)
        {
            if(tool.Id.StartsWith("fence.",StringComparison.Ordinal))return 5;
            if(tool.Id.StartsWith("terrain.",StringComparison.Ordinal)||tool.Category=="Рельеф")return 0;
            if(tool.Id.StartsWith("river.",StringComparison.Ordinal)||tool.Id.StartsWith("waterfall.",StringComparison.Ordinal))return 3;
            if(tool.Id.StartsWith("environment.",StringComparison.Ordinal))return 4;
            if(tool.Id.StartsWith("tree.",StringComparison.Ordinal)||tool.Id.StartsWith("vegetation.",StringComparison.Ordinal)||tool.Category=="Растительность")return 2;
            return 1;
        }

        void DrawScatterSettings()
        {
            scatter=EditorGUILayout.Toggle("Цветы и мелочь",scatter);
            if(!scatter)return;
            density=EditorGUILayout.Slider("Плотность мелочи",density,0,1);
            profile=(MeadowScatterProfile)EditorGUILayout.Popup("Группа мелочи",(int)profile,MeadowScatterProfiles.Names);
        }

        void DrawHeightLimits()
        {
            EditorGUILayout.LabelField("Диапазон карты: "+session.MinimumHeight+" … "+session.MaximumHeight+" м",EditorStyles.miniLabel);
            showHeightLimits=EditorGUILayout.Foldout(showHeightLimits,"Пределы высоты карты",true);
            if(!showHeightLimits)return;
            minimumHeight=EditorGUILayout.FloatField("Нижний предел, м",minimumHeight);
            maximumHeight=EditorGUILayout.FloatField("Верхний предел, м",maximumHeight);
            EditorGUILayout.HelpBox("Изменение пределов начинает новую историю отмены. Текущие правки остаются в документе.",MessageType.None);
            if(GUILayout.Button("Задать пределы карты"))Run(()=>
            {
                tools.Complete();pointerHeld=false;var map=session.ExportMap();
                map.heightField.minimumHeight=minimumHeight;map.heightField.maximumHeight=maximumHeight;
                ReplaceSession(new MapEditSession(map));MarkDocumentDirty();
                status="Пределы изменены. История отмены начата заново; сохраните JSON или примените базу.";
            });
        }

        void DrawBrushPreset()
        {
            // Always in one place; profiles may also switch to another brush/category.
            showBrushPreset=EditorGUILayout.Foldout(showBrushPreset,"Профили кистей",true);
            if(!showBrushPreset)return;
            brushPreset=(MapBrushPreset)EditorGUILayout.ObjectField("Профиль",brushPreset,typeof(MapBrushPreset),false);
            using(new EditorGUILayout.HorizontalScope())
            {
                using(new EditorGUI.DisabledScope(!brushPreset))
                    if(GUILayout.Button("Применить профиль"))Run(()=>
                    {
                        var tool=tools.registry.Get(brushPreset.toolId);brushPreset.settings.Validate(tool.MaximumRadius(session));
                        SetTool(tool.Id);radius=brushPreset.settings.radius;strength=brushPreset.settings.strength;softness=brushPreset.settings.softness;
                        pathMaterialId=brushPreset.pathMaterialId;
                    });
                using(new EditorGUI.DisabledScope(tools.Selected==null||tools.Selected.Controls==MapToolControls.None))
                    if(GUILayout.Button("Сохранить профиль…"))Run(()=>
                    {
                        string path=EditorUtility.SaveFilePanelInProject("Профиль кисти","MapBrushPreset","asset","Сохранить настройки кисти");
                        if(path.Length==0)return;
                        var asset=CreateInstance<MapBrushPreset>();asset.toolId=CurrentToolId;asset.pathMaterialId=pathMaterialId;asset.settings=new MapBrushSettings{radius=radius,strength=strength,softness=softness};
                        AssetDatabase.CreateAsset(asset,path);brushPreset=asset;
                    });
            }
        }

        void DrawVegetationTools()
        {
            if(CurrentToolId=="vegetation.flowers")
            {
                EditorGUILayout.HelpBox("Готовые цветы: выберите группу «Цветы» и рисуйте ЛКМ по земле. Остальные группы используют ту же кисть луга и мелкой растительности.",MessageType.None);
                density=EditorGUILayout.Slider("Плотность мелочи",density,0,1);
                profile=(MeadowScatterProfile)EditorGUILayout.Popup("Готовая группа",(int)profile,MeadowScatterProfiles.Names);
                return;
            }
            if(!treeCatalog||treeCatalog.entries.Length==0)
            {EditorGUILayout.HelpBox("Каталог деревьев не найден. Подключите MapTreeCatalog, чтобы сажать деревья и рощи.",MessageType.Info);return;}
            selectedTreeSource=Mathf.Clamp(selectedTreeSource,0,treeCatalog.entries.Length-1);
            if(CurrentToolId=="vegetation.grove")
            {
                var titles=new string[treeCatalog.packs.Length];
                for(int i=0;i<titles.Length;i++)titles[i]=MapTreeCatalog.PackTitle(treeCatalog.packs[i]);
                if(titles.Length>0)selectedTreePack=EditorGUILayout.Popup("Набор рощи",Mathf.Clamp(selectedTreePack,0,titles.Length-1),titles);
                else EditorGUILayout.HelpBox("В каталоге нет наборов для рощи.",MessageType.Info);
                treeDensity=EditorGUILayout.Slider("Плотность деревьев",treeDensity,0,1);
                treeSpacing=EditorGUILayout.Slider("Интервал, м",treeSpacing,2,16);treeSeed=EditorGUILayout.IntField("Seed",treeSeed);
            }
            if(CurrentToolId=="tree.place")
            {
                selectedTreeGroup=EditorGUILayout.Popup("Группа деревьев",selectedTreeGroup,MapTreeCatalog.GroupNames);
                var indices=treeCatalog.GroupIndices(selectedTreeGroup);
                int columns=Mathf.Max(1,Mathf.FloorToInt((position.width-32)/124));
                for(int row=0;row<(indices.Count+columns-1)/columns;row++)
                {
                    using(new EditorGUILayout.HorizontalScope())for(int col=0;col<columns;col++)
                    {
                        int cell=row*columns+col;if(cell>=indices.Count)break;int index=indices[cell];var entry=treeCatalog.entries[index];
                        bool chosen=selectedTreeSource==index;
                        if(GUILayout.Toggle(chosen,new GUIContent(entry.title,entry.thumbnail),GUI.skin.button,GUILayout.Width(116),GUILayout.Height(104))&&!chosen)
                        {selectedTreeSource=index;treeScale=Mathf.Clamp(1,entry.definition.minimumScale,entry.definition.maximumScale);}
                    }
                }
                EditorGUILayout.LabelField("Выбрано: "+treeCatalog.entries[selectedTreeSource].title,EditorStyles.miniLabel);
            }
            bool hasSelected=selectedTree!=0&&session.HasTree(selectedTree);
            if(CurrentToolId=="tree.place"||hasSelected)
            {
                treeYaw=EditorGUILayout.Slider("Поворот дерева",treeYaw,0,359);
                var definition=treeCatalog.entries[selectedTreeSource].definition;
                treeScale=EditorGUILayout.Slider("Масштаб дерева",treeScale,definition.minimumScale,definition.maximumScale);
            }
            if(CurrentToolId=="tree.pick")EditorGUILayout.HelpBox("Щёлкните по кроне дерева в SceneView.",MessageType.None);
            if(CurrentToolId=="tree.move")EditorGUILayout.HelpBox(hasSelected?"Щёлкните по земле, чтобы переместить выбранное дерево.":"Сначала выберите дерево инструментом «Выбрать дерево».",MessageType.None);
            if(!hasSelected)return;
            EditorGUILayout.LabelField("Выбрано: "+session.GetTree(selectedTree).archetypeId,EditorStyles.boldLabel);
            using(new EditorGUILayout.HorizontalScope())
            {
                if(GUILayout.Button("Переместить"))SetTool("tree.move");
                if(GUILayout.Button("Поворот / масштаб"))Run(()=>{var r=session.GetTree(selectedTree);session.TransformTree(selectedTree,r.position,treeYaw,treeScale);});
                if(GUILayout.Button("Удалить"))Run(()=>{session.DeleteTree(selectedTree);selectedTree=0;});
            }
        }

        void FrameMap()
        {
            var view=SceneView.lastActiveSceneView;if(!view||session==null)return;var b=session.Bounds;
            view.Frame(new Bounds(new Vector3(b.x+b.z/2,(session.MinimumHeight+session.MaximumHeight)/2,b.y+b.w/2),new Vector3(b.z,session.MaximumHeight-session.MinimumHeight,b.w)),false);
        }

        bool ConfirmReplaceDocument()
        {
            if(!documentDirty&&(session==null||!session.HasPending&&!riverTools.HasUnappliedDraft(session)))return true;
            int result=EditorUtility.DisplayDialogComplex("Несохранённые правки карты","Сохранить текущий авторский документ перед заменой?","Сохранить JSON…","Отмена","Не сохранять");
            return result==2||result==0&&SaveDocument();
        }

        bool SaveDocument()
        {
            if(session==null)return false;
            string path=EditorUtility.SaveFilePanel("Авторская карта","","handmade-map.json","json");if(path.Length==0)return false;
            return SaveDocumentAtPath(path);
        }
        internal bool SaveDocumentAtPath(string path)
        {
            if(session==null)return false;
            tools.Complete();pointerHeld=false;
            var prepared=PrepareDocument();
            var map=session.ExportMap();
            // Recovery first: a failed map write still leaves a complete map+draft pair.
            MapAuthoringRecovery.Write(MapAuthoringRecovery.Companion(path),map,riverTools.ExportDraftJson());
            MapAuthoringRecovery.AtomicWrite(path,JsonUtility.ToJson(map,true));
            documentDirty=false;hasUnsavedChanges=false;
            status="JSON и восстановительная копия сохранены. "+DraftSaveMessage(prepared);return true;
        }

        MapRiverToolState.SavePreparation PrepareDocument()
        {
            CheckpointSession();
            string path="Library/WorldEditor-"+AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(source))+".recovery.json";
            MapAuthoringRecovery.Write(path,session.ExportMap(),riverTools.ExportDraftJson());
            return riverTools.PrepareForSave(session);
        }
        static string DraftSaveMessage(MapRiverToolState.SavePreparation prepared)=>
            prepared==MapRiverToolState.SavePreparation.Incomplete?"Незавершённые точки остались черновиком; они восстановятся из файла .recovery.json.":
            prepared==MapRiverToolState.SavePreparation.Invalid?"Черновик требует исправления и сохранён отдельно; в карте осталась последняя корректная геометрия.":
            prepared==MapRiverToolState.SavePreparation.Applied?"Готовая река автоматически применена.":"Карта сохранена.";

        void OpenDocument()
        {
            string path=EditorUtility.OpenFilePanel("Авторская карта","","json");if(path.Length==0)return;
            var map=JsonUtility.FromJson<MapAuthoringDocument>(File.ReadAllText(path));
            var loaded=new MapEditSession(map);
            MapAuthoringRecovery recovery=null;
            if(File.Exists(MapAuthoringRecovery.Companion(path)))recovery=MapAuthoringRecovery.Read(MapAuthoringRecovery.Companion(path),map);
            if(loaded.MapId!=source.mapId||loaded.Bounds!=source.bounds)throw new InvalidDataException("Выбран другой ID/размер карты. Выберите соответствующую базу.");
            if(!ConfirmReplaceDocument())return;
            riverTools=new MapRiverToolState();ReplaceSession(loaded);documentDirty=false;hasUnsavedChanges=false;
            if(recovery!=null){riverTools.RestoreDraftJson(recovery.riverDraft);riverTools.Refresh(session);}
            status="JSON загружен. История отмены начата заново; авторская база не изменена.";
        }

        void ApplyDocument()
        {
            if((session.Access&MapEditAccess.Author)==0)throw new UnauthorizedAccessException("Применение авторской базы выключено для этой роли.");
            tools.Complete();pointerHeld=false;
            var prepared=PrepareDocument();
            if(prepared==MapRiverToolState.SavePreparation.Invalid){status=riverTools.status+" Восстановительная копия сохранена; авторская база не изменена.";return;}
            string draft=riverTools.ExportDraftJson();var map=session.ExportMap();
            // Validate the whole exported candidate before writing either source asset.
            _=new MapEditSession(map);
            var painter=FindAnyObjectByType<MeadowSurfacePainter>();
            var mask=painter&&painter.source==source?painter.authoredPreviewMask:null;
            if(mask&&(mask.width!=Resolution||mask.height!=Resolution||mask.format!=TextureFormat.RG16))throw new InvalidDataException("Expected 512x512 RG16 authored mask.");
            RestorePreview();Undo.RecordObject(source,"Apply authored map surface and heights");
            source.authored=map.surface;source.heightField=map.heightField;source.hasHeightBase=true;source.treeInstances=map.trees;source.hasTreeBase=true;
            source.environment=map.environment;source.hasEnvironmentBase=true;
            source.fences=map.fences;
            source.rivers=map.rivers;source.riverNetwork=map.riverNetwork;source.rockSections=map.rockSections;source.placementExclusions=map.placementExclusions;source.revision++;
            EditorUtility.SetDirty(source);AssetDatabase.SaveAssetIfDirty(source);
            if(mask)
            {
                Undo.RecordObject(mask,"Apply authored surface mask");mask.SetPixelData(MeadowSurfacePainter.BuildAuthoredPixels(source),0);mask.Apply(false,false);
                EditorUtility.SetDirty(mask);AssetDatabase.SaveAssetIfDirty(mask);
            }
            Connect(source);riverTools.RestoreDraftJson(draft);riverTools.Refresh(session);CheckpointSession();
            hasUnsavedChanges=riverTools.HasUnappliedDraft(session);status="Авторская база сохранена. "+DraftSaveMessage(prepared);
        }

        void ReplaceSession(MapEditSession replacement)
        {
            string draftState=riverTools.ExportDraftJson();
            tools?.Complete();pointerHeld=false;RestorePreview();if(session!=null)session.Changed-=Changed;
            session=replacement;if(treeCatalog)session.RegisterTreeCatalog(treeCatalog.Definitions,treeCatalog.packs);
            InitializeTools();session.Changed+=Changed;minimumHeight=session.MinimumHeight;maximumHeight=session.MaximumHeight;
            RebuildPreview();BindScenePreview();riverTools.RestoreDraftJson(draftState);riverTools.Refresh(session);
        }

        void MarkDocumentDirty(){documentDirty=true;hasUnsavedChanges=true;}
        public override void SaveChanges(){if(SaveDocument())base.SaveChanges();}
        public override void DiscardChanges(){documentDirty=false;base.DiscardChanges();}

        void CheckpointSession()
        {
            CancelRiverGesture();
            if(session==null)return;
            try
            {
                tools?.Complete();pointerHeld=false;sessionCheckpoint=JsonUtility.ToJson(session.ExportMap());
                riverCheckpoint=riverTools.ExportDraftJson();
            }
            catch(Exception error){status="Не удалось сохранить черновик окна: "+error.Message;Debug.LogException(error);}
        }

        void RestoreWindowSession()
        {
            if(!this||EditorApplication.isPlayingOrWillChangePlaymode)return;
            restoreAttempted=true;
            saveChangesMessage="В авторском документе есть несохранённые правки. Сохраните JSON перед закрытием World Editor.";
            try
            {
                if(session!=null)
                {
                    session.Changed-=Changed;session.Changed+=Changed;RebuildPreview();BindScenePreview();return;
                }
                if(!string.IsNullOrEmpty(sessionCheckpoint)&&source)
                {
                    var restored=new MapEditSession(JsonUtility.FromJson<MapAuthoringDocument>(sessionCheckpoint));
                    if(restored.MapId!=source.mapId||restored.Bounds!=source.bounds)throw new InvalidDataException("Черновик окна не совпадает с выбранной базой. Сначала сохраните или восстановите его.");
                    treeCatalog=MapTreeCatalog.Default;riverTools.RestoreDraftJson(riverCheckpoint);ReplaceSession(restored);
                    hasUnsavedChanges=documentDirty;
                    status="Черновик восстановлен после перезагрузки. Правки и точки реки сохранены; история отмены начата заново.";
                }
                else if(source)Connect(source);
                else ConnectSceneMap();
            }
            catch(Exception error){status="Не удалось восстановить документ: "+error.Message;Debug.LogException(error);}
            Repaint();
        }

        void RebuildPreview()
        {
            if(!preview)preview=new Texture2D(Resolution,Resolution,TextureFormat.RG16,false,true){hideFlags=HideFlags.HideAndDontSave,filterMode=FilterMode.Bilinear};
            if(pixels==null||pixels.Length!=Resolution*Resolution*2)pixels=new byte[Resolution*Resolution*2];
            rebuildingPreview=true;
            try{var b=session.Bounds;Changed(new MapRegionChange(new Rect(b.x,b.y,b.z,b.w)));}
            finally{rebuildingPreview=false;}
        }
    }
}
