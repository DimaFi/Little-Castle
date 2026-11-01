using System;
using LittleCastle.Rendering.Fireflies;
using LittleCastle.Research;
using UnityEditor;
using UnityEngine;

namespace LittleCastle.Editor
{
    /// <summary>
    /// An opt-in preview window. Creates only a HideAndDontSave GameObject and edits
    /// its dedicated preview asset; it never changes map sessions or scene objects.
    /// </summary>
    public sealed class FireflyPreviewWindow : EditorWindow
    {
        const string GlobalAssetPath="Assets/_Game/Resources/Fireflies/GlobalFireflies.asset";
        [SerializeField] FireflyPreviewProfile profile;
        [SerializeField] FireflyGlobalSettings globalSettings;
        [SerializeField] bool previewEnabled = true;
        [SerializeField] bool editHandles = true;
        [SerializeField] int selectedZone;
        [SerializeField] FireflyPreviewQuality visualQuality=FireflyPreviewQuality.Medium;
        [SerializeField, Range(0f,1f)] float graphicsVsFps=.5f;
        [SerializeField] bool showFullProfile=true;
        [SerializeField] int customWorldCap=4500;
        [SerializeField] int customNearCap=12;
        [SerializeField] float customViewRange=100f;
        [SerializeField] bool adaptiveQuality=true;
        [SerializeField] float targetFps=60f;
        [SerializeField] float minimumAdaptiveScale=.35f;
        [SerializeField] bool simulateLowFps;
        [SerializeField] float simulatedFps=28f;
        [SerializeField] Vector2 scroll;

        GameObject previewObject;
        FireflyPreviewRenderer rendererView;
        UnityEditor.Editor profileInspector;
        double nextRepaint;

        [MenuItem("Little Castle/Atmosphere/Firefly Preview (Isolated)")]
        public static void ShowWindow() => GetWindow<FireflyPreviewWindow>("Firefly Preview");

        void OnEnable()
        {
            minSize=new Vector2(340,440);
            globalSettings=AssetDatabase.LoadAssetAtPath<FireflyGlobalSettings>(GlobalAssetPath);

            EditorApplication.update+=Tick;
            SceneView.duringSceneGui+=DrawHandles;
            Undo.undoRedoPerformed+=HandleUndo;
        }

        void OnDisable()
        {
            EditorApplication.update-=Tick;
            SceneView.duringSceneGui-=DrawHandles;
            Undo.undoRedoPerformed-=HandleUndo;
            DestroyPreview();
            if(profileInspector)DestroyImmediate(profileInspector);
        }

        void HandleUndo()
        {
            if(rendererView)rendererView.Invalidate();
            Repaint();SceneView.RepaintAll();
        }

        void Tick()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode || !previewEnabled || !profile)
            {
                DestroyPreview();
                return;
            }
            EnsurePreview();
            if(rendererView)
            {
                rendererView.quality.quality=visualQuality;
                rendererView.quality.graphicsVsPerformance=graphicsVsFps;
                rendererView.quality.customWorldCap=customWorldCap;
                rendererView.quality.customNearCap=customNearCap;
                rendererView.quality.customRangeMetres=customViewRange;
                rendererView.quality.adaptiveEnabled=adaptiveQuality;
                rendererView.quality.targetFps=targetFps;
                rendererView.quality.minimumAdaptiveScale=minimumAdaptiveScale;
                rendererView.ConfigureFpsSimulation(simulateLowFps,simulatedFps);
            }
            if(EditorApplication.timeSinceStartup>=nextRepaint)
            {
                nextRepaint=EditorApplication.timeSinceStartup+.05;
                SceneView.lastActiveSceneView?.Repaint();
            }
        }

        void EnsurePreview()
        {
            if(rendererView)
            {
                rendererView.Connect(profile);
                return;
            }
            previewObject=new GameObject("Firefly visual preview (NOT saved)");
            previewObject.hideFlags=HideFlags.HideAndDontSave;
            rendererView=previewObject.AddComponent<FireflyPreviewRenderer>();
            rendererView.Connect(profile);
        }

        void DestroyPreview()
        {
            if(previewObject)DestroyImmediate(previewObject);
            previewObject=null;
            rendererView=null;
        }

        void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "Безопасный предпросмотр #85. Не меняет MapEditSession, реку B, игровое освещение, сохранение карты и сцены. " +
                "Профиль сохраняется отдельным .asset; пока НЕ переносится в саму карту.",
                MessageType.Info);

            EditorGUI.BeginChangeCheck();
            profile=(FireflyPreviewProfile)EditorGUILayout.ObjectField(
                "Профиль предпросмотра",profile,typeof(FireflyPreviewProfile),false);
            if(EditorGUI.EndChangeCheck()){DestroyPreview();Repaint();}
            using(new EditorGUILayout.HorizontalScope())
            {
                if(GUILayout.Button("Создать профиль"))CreateProfile();
                using(new EditorGUI.DisabledScope(!profile))
                    if(GUILayout.Button("Выделить asset"))Selection.activeObject=profile;
            }
            if(!profile)return;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Автоматическая система для всех карт",EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            bool autoEnabled=EditorGUILayout.Toggle(
                "Автоматически по миру",profile.automaticWorldPlacement);
            if(EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(profile,"Toggle automatic fireflies across map");
                profile.automaticWorldPlacement=autoEnabled;
                MarkChanged();
            }
            EditorGUILayout.HelpBox(
                "В режиме авто используются существующие деревья, вода, дорожки, " +
                "луговая маска, биом и снег. Ручные зоны остаются переопределениями. " +
                "Это настройки профиля; для автоматического запуска в Play Mode " +
                "нужна отдельная глобальная настройка ниже.",MessageType.None);
            if(rendererView)
                EditorGUILayout.LabelField(rendererView.HasActiveMap ?
                    "Привязана карта: "+rendererView.ActiveMapId :
                    "Карта/MapEditSession ещё не обнаружена: автозаполнение ожидает карту",
                    EditorStyles.miniLabel);
            DrawGlobalSettings();

            previewEnabled=EditorGUILayout.Toggle("Показывать в SceneView",previewEnabled);
            editHandles=EditorGUILayout.Toggle("Перемещать области",editHandles);
            visualQuality=(FireflyPreviewQuality)EditorGUILayout.EnumPopup("Качество",visualQuality);
            graphicsVsFps=EditorGUILayout.Slider("FPS ← → графика",graphicsVsFps,0f,1f);
            adaptiveQuality=EditorGUILayout.Toggle("Автобюджет FPS",adaptiveQuality);
            if(adaptiveQuality)
            {
                targetFps=EditorGUILayout.Slider("Целевой FPS (Play)",targetFps,20f,144f);
                minimumAdaptiveScale=EditorGUILayout.Slider("Мин. бюджет",minimumAdaptiveScale,.2f,1f);
                simulateLowFps=EditorGUILayout.Toggle("Симуляция низкого FPS",simulateLowFps);
                if(simulateLowFps)
                    simulatedFps=EditorGUILayout.Slider("Тестовый FPS (НЕ замер)",simulatedFps,5f,120f);
                EditorGUILayout.HelpBox(
                    "В Edit Mode автобюджет меняется только при включённой симуляции. " +
                    "Реальный FPS считывается лишь из Game камеры в Play Mode; " +
                    "само окно предпросмотра автоматически закрывает временный объект при Play.",
                    MessageType.None);
            }
            if(visualQuality==FireflyPreviewQuality.Custom)
            {
                customWorldCap=EditorGUILayout.IntSlider("Лимит мировых",customWorldCap,0,50000);
                customNearCap=EditorGUILayout.IntSlider("Лимит близких",customNearCap,0,48);
                customViewRange=EditorGUILayout.Slider("Дистанция, м",customViewRange,1f,300f);
            }

            var budget=new FireflyPreviewBudget{
                quality=visualQuality,graphicsVsPerformance=graphicsVsFps,
                customWorldCap=customWorldCap,customNearCap=customNearCap,
                customRangeMetres=customViewRange,
                adaptiveEnabled=adaptiveQuality,targetFps=targetFps,
                minimumAdaptiveScale=minimumAdaptiveScale};
            budget.Resolve(out int maxWorld,out int maxNear,out float distance);
            EditorGUILayout.LabelField("Бюджет: "+maxWorld+" мировых, "+maxNear+
                " близких, радиус "+distance.ToString("F0")+" м",EditorStyles.miniLabel);

            if(rendererView)
            {
                EditorGUILayout.LabelField("Draw calls "+rendererView.DrawCalls+
                    " | отправлено "+rendererView.SubmittedWorldCount+
                    " | запрошено "+rendererView.DesiredWorldCount+
                    " | близких "+rendererView.SubmittedNearCount,EditorStyles.miniLabel);
                EditorGUILayout.LabelField("Текущий адаптивный множитель "+
                    rendererView.AdaptiveScale.ToString("F2")+
                    " (автобюджет влияет только на светлячков)",EditorStyles.miniLabel);
                EditorGUILayout.LabelField("Cells "+rendererView.ResidentCellCount+
                    " | ожидают "+rendererView.MissingCells+
                    " | отсечено бюджетом "+rendererView.BudgetRejected,EditorStyles.miniLabel);
            }
            using(new EditorGUILayout.HorizontalScope())
            {
                if(GUILayout.Button("Перестроить предпросмотр"))
                    rendererView?.Invalidate();
                if(GUILayout.Button("Остановить")){previewEnabled=false;DestroyPreview();}
            }

            using var scope=new EditorGUILayout.ScrollViewScope(scroll);
            scroll=scope.scrollPosition;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Локальные области",EditorStyles.boldLabel);
            using(new EditorGUILayout.HorizontalScope())
            {
                if(GUILayout.Button("Тест: очень плотная поляна"))
                    AddSampleZones(true);
                if(GUILayout.Button("Пример: 3 скопления"))
                    AddSampleZones(false);
            }
            using(new EditorGUILayout.HorizontalScope())
            {
                if(GUILayout.Button("+ Эллипс"))AddArea(FireflyPreviewZoneShape.Ellipse);
                if(GUILayout.Button("+ Полигон"))AddArea(FireflyPreviewZoneShape.Polygon);
                using(new EditorGUI.DisabledScope(profile.areas.Count==0))
                    if(GUILayout.Button("Удалить"))RemoveArea();
            }
            if(profile.areas.Count>0)
            {
                selectedZone=Mathf.Clamp(selectedZone,0,profile.areas.Count-1);
                string[] labels=new string[profile.areas.Count];
                for(int i=0;i<labels.Length;i++)
                    labels[i]=(profile.areas[i]?.displayName ?? "Без названия")+" ["+i+"]";
                selectedZone=EditorGUILayout.Popup("Область",selectedZone,labels);
                var area=profile.areas[selectedZone];
                if(area!=null)DrawZoneEditor(area);
            }
            else EditorGUILayout.HelpBox("Можно использовать общую плотность без локальных областей.",MessageType.None);

            EditorGUILayout.Space();
            showFullProfile=EditorGUILayout.Foldout(showFullProfile,"Все параметры профиля (для настройки)",true);
            if(showFullProfile)
            {
                UnityEditor.Editor.CreateCachedEditor(profile,null,ref profileInspector);
                EditorGUI.BeginChangeCheck();
                profileInspector.OnInspectorGUI();
                if(EditorGUI.EndChangeCheck())MarkChanged();
            }
            EditorGUILayout.HelpBox(
                "Не открывайте одновременно несколько окон предпросмотра. " +
                "Если участок очень плотный, ограничение честно показано в счётчиках. " +
                "Никаких Point Lights этот preview не создаёт.",
                MessageType.None);
        }

        void DrawZoneEditor(FireflyPreviewZone area)
        {
            EditorGUI.BeginChangeCheck();
            string newName=EditorGUILayout.TextField("Название",area.displayName);
            bool newEnabled=EditorGUILayout.Toggle("Включена",area.enabled);
            int priority=EditorGUILayout.IntField("Приоритет",area.priority);
            int seed=EditorGUILayout.IntField("Seed области",area.seed);
            var densityMode=(FireflyPreviewDensityMode)EditorGUILayout.EnumPopup("Плотность",area.densityMode);
            float value=EditorGUILayout.Slider("Множитель / на 100 м²",area.densityValue,0f,100f);
            float feather=EditorGUILayout.Slider("Мягкость края",area.falloff,0f,1f);
            Vector2 center=EditorGUILayout.Vector2Field("Центр X/Z, м",area.center);
            Vector2 radii=EditorGUILayout.Vector2Field("Радиусы X/Z, м",area.radii);
            float rotation=EditorGUILayout.Slider("Поворот, °",area.rotationDegrees,0f,360f);
            bool hasLocalColor=EditorGUILayout.Toggle("Свой цвет",area.overrideColor);
            Color localColor=hasLocalColor?EditorGUILayout.ColorField("Цвет области",area.color):area.color;
            if(EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(profile,"Edit firefly preview area");
                area.displayName=newName;
                area.enabled=newEnabled;
                area.priority=priority;
                area.seed=seed;
                area.overrideColor=hasLocalColor;
                area.color=localColor;
                area.densityMode=densityMode;
                area.densityValue=value;
                area.falloff=feather;
                Vector2 delta=center-area.center;
                area.center=center;area.radii=radii;area.rotationDegrees=rotation;
                if(area.shape==FireflyPreviewZoneShape.Polygon && delta!=Vector2.zero)
                    for(int i=0;i<area.polygon.Count;i++)area.polygon[i]+=delta;
                profile.Validate();MarkChanged();
            }
        }

        void AddArea(FireflyPreviewZoneShape shape)
        {
            Undo.RecordObject(profile,"Add firefly preview area");
            var view=SceneView.lastActiveSceneView;
            Vector2 p=view?new Vector2(view.pivot.x,view.pivot.z):
                new Vector2(profile.mapBounds.x+profile.mapBounds.z*.5f,
                            profile.mapBounds.y+profile.mapBounds.w*.5f);
            p.x=Mathf.Clamp(p.x,profile.mapBounds.x,profile.mapBounds.x+profile.mapBounds.z);
            p.y=Mathf.Clamp(p.y,profile.mapBounds.y,profile.mapBounds.y+profile.mapBounds.w);
            var a=new FireflyPreviewZone{
                id=Guid.NewGuid().ToString("N"),shape=shape,center=p,
                displayName="Светлячки "+(profile.areas.Count+1)
            };
            if(shape==FireflyPreviewZoneShape.Polygon)
            {
                a.polygon.Add(p+new Vector2(-6f,-6f));
                a.polygon.Add(p+new Vector2(6f,-6f));
                a.polygon.Add(p+new Vector2(0f,7f));
            }
            profile.areas.Add(a);selectedZone=profile.areas.Count-1;MarkChanged();
        }

        void DrawGlobalSettings()
        {
            if(!globalSettings)
                globalSettings=AssetDatabase.LoadAssetAtPath<FireflyGlobalSettings>(GlobalAssetPath);
            if(!globalSettings)
            {
                if(GUILayout.Button("Создать единые настройки для Play Mode"))
                    CreateGlobalSettings();
                EditorGUILayout.HelpBox("Пока глобальный конфиг не создан, " +
                    "обычные сцены и сборки не меняют поведение.",MessageType.None);
                return;
            }
            EditorGUI.BeginChangeCheck();
            bool enabled=EditorGUILayout.Toggle("Включить во всей игре",
                globalSettings.enabledEverywhere);
            if(EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(globalSettings,"Toggle global fireflies");
                globalSettings.enabledEverywhere=enabled;
                if(enabled && profile && !profile.automaticWorldPlacement)
                {
                    Undo.RecordObject(profile,"Enable automatic firefly ecology");
                    profile.automaticWorldPlacement=true;
                    EditorUtility.SetDirty(profile);
                    rendererView?.Invalidate();
                }
                EditorUtility.SetDirty(globalSettings);
                AssetDatabase.SaveAssets();
            }
            var nextProfile=(FireflyPreviewProfile)EditorGUILayout.ObjectField(
                "Игровой профиль",globalSettings.profile,
                typeof(FireflyPreviewProfile),false);
            if(nextProfile!=globalSettings.profile)
            {
                Undo.RecordObject(globalSettings,"Assign global firefly profile");
                globalSettings.profile=nextProfile;
                EditorUtility.SetDirty(globalSettings);
                AssetDatabase.SaveAssets();
            }
            if(!globalSettings.previewShader)
            {
                EditorGUILayout.HelpBox("Для сборки назначьте шейдер Firefly Preview URP; " +
                    "его можно присвоить автоматически через кнопку.",MessageType.Warning);
                if(GUILayout.Button("Назначить шейдер"))
                {
                    Undo.RecordObject(globalSettings,"Assign firefly shader");
                    globalSettings.previewShader=Shader.Find("Little Castle/Effects/Firefly Preview URP");
                    EditorUtility.SetDirty(globalSettings);
                    AssetDatabase.SaveAssets();
                }
            }
            if(GUILayout.Button("Открыть глобальные настройки качества"))
                Selection.activeObject=globalSettings;
            if(globalSettings.enabledEverywhere &&
                (!globalSettings.profile || !globalSettings.profile.automaticWorldPlacement))
                EditorGUILayout.HelpBox("Выберите профиль с включённым авторазмещением. " +
                    "Без него глобальный автозапуск безопасно пропускается.",
                    MessageType.Warning);
        }

        void CreateGlobalSettings()
        {
            if(!AssetDatabase.IsValidFolder("Assets/_Game/Resources"))
                AssetDatabase.CreateFolder("Assets/_Game","Resources");
            if(!AssetDatabase.IsValidFolder("Assets/_Game/Resources/Fireflies"))
                AssetDatabase.CreateFolder("Assets/_Game/Resources","Fireflies");
            globalSettings=AssetDatabase.LoadAssetAtPath<FireflyGlobalSettings>(GlobalAssetPath);
            if(globalSettings)return;
            var value=CreateInstance<FireflyGlobalSettings>();
            value.profile=profile;
            value.previewShader=Shader.Find("Little Castle/Effects/Firefly Preview URP");
            value.enabledEverywhere=false; // always opt-in
            AssetDatabase.CreateAsset(value,GlobalAssetPath);
            AssetDatabase.SaveAssets();
            globalSettings=value;
            Selection.activeObject=value;
        }

        void AddSampleZones(bool stress)
        {
            if(!profile)return;
            var view=SceneView.lastActiveSceneView;
            Vector2 center=view ? new Vector2(view.pivot.x,view.pivot.z)
                : new Vector2(profile.mapBounds.x+profile.mapBounds.z*.5f,
                              profile.mapBounds.y+profile.mapBounds.w*.5f);
            center.x=Mathf.Clamp(center.x,profile.mapBounds.x,
                profile.mapBounds.x+profile.mapBounds.z);
            center.y=Mathf.Clamp(center.y,profile.mapBounds.y,
                profile.mapBounds.y+profile.mapBounds.w);

            Undo.RecordObject(profile,stress ?
                "Create stress-test firefly meadow" : "Create example firefly clusters");
            int total=stress ? 1 : 3;
            for(int i=0;i<total;i++)
            {
                Vector2 offset=stress ? Vector2.zero
                    : new Vector2((i-1)*12f, i==1?6f:-3f);
                var zone=new FireflyPreviewZone {
                    id=Guid.NewGuid().ToString("N"),
                    displayName=stress ? "СТРЕСС-ТЕСТ (80 / 100 м²)"
                        : "Пример: тёплая группа "+(i+1),
                    shape=FireflyPreviewZoneShape.Ellipse,
                    center=center+offset,
                    radii=stress ? new Vector2(24f,18f)
                        : new Vector2(9f+i*2f,6f+i),
                    falloff=stress ? .35f : .45f,
                    priority=stress ? 100 : 5,
                    seed=profile.seed+110+i*997,
                    densityMode=stress ? FireflyPreviewDensityMode.Set :
                        FireflyPreviewDensityMode.Multiply,
                    densityValue=stress ? 80f : 8f,
                    overrideColor=true,
                    color=stress ? new Color(1f,.89f,.41f) :
                        new Color(1f,.84f+i*.025f,.28f+i*.08f)
                };
                profile.areas.Add(zone);
            }
            selectedZone=profile.areas.Count-total;
            profile.Validate();
            MarkChanged();
        }

        void RemoveArea()
        {
            if(profile.areas.Count<=0)return;
            Undo.RecordObject(profile,"Delete firefly preview area");
            profile.areas.RemoveAt(Mathf.Clamp(selectedZone,0,profile.areas.Count-1));
            selectedZone=Mathf.Clamp(selectedZone,0,Mathf.Max(0,profile.areas.Count-1));
            MarkChanged();
        }

        void CreateProfile()
        {
            string path=EditorUtility.SaveFilePanelInProject(
                "Firefly preview profile","FireflyPreview","asset",
                "Сохранить отдельные настройки светлячков");
            if(string.IsNullOrEmpty(path))return;
            var created=CreateInstance<FireflyPreviewProfile>();
            var painter=FindAnyObjectByType<MeadowSurfacePainter>();
            if(painter && painter.source)created.mapBounds=painter.source.bounds;
            created.Validate();
            AssetDatabase.CreateAsset(created,path);
            AssetDatabase.SaveAssets();
            profile=created;Selection.activeObject=created;
            DestroyPreview();Repaint();
        }

        void MarkChanged()
        {
            EditorUtility.SetDirty(profile);
            rendererView?.Invalidate();
            SceneView.RepaintAll();
        }

        static Vector3 DrawPoint(Vector2 p,float height) =>
            new Vector3(p.x,height,p.y);

        void DrawHandles(SceneView view)
        {
            if(!profile || !editHandles || !previewEnabled ||
               EditorApplication.isPlayingOrWillChangePlaymode ||
               profile.areas==null || profile.areas.Count==0)return;

            for(int a=0;a<profile.areas.Count;a++)
            {
                var area=profile.areas[a];
                if(area==null || !area.enabled)continue;
                float h=view.pivot.y;
                Handles.color=a==selectedZone?new Color(1f,.85f,.3f,.95f):
                    new Color(.65f,.85f,.5f,.38f);
                if(area.shape==FireflyPreviewZoneShape.Ellipse)
                {
                    Vector3 previous=Vector3.zero;
                    for(int i=0;i<=64;i++)
                    {
                        float angle=i*Mathf.PI*2/64f;
                        Vector2 d=new Vector2(Mathf.Cos(angle)*area.radii.x,
                            Mathf.Sin(angle)*area.radii.y);
                        float r=area.rotationDegrees*Mathf.Deg2Rad;
                        Vector2 p=area.center+new Vector2(d.x*Mathf.Cos(r)-d.y*Mathf.Sin(r),
                            d.x*Mathf.Sin(r)+d.y*Mathf.Cos(r));
                        Vector3 now=DrawPoint(p,h);
                        if(i>0)Handles.DrawAAPolyLine(2f,previous,now);
                        previous=now;
                    }
                }
                else if(area.polygon!=null && area.polygon.Count>=3)
                    for(int i=0;i<area.polygon.Count;i++)
                        Handles.DrawAAPolyLine(2f,
                            DrawPoint(area.polygon[i],h),
                            DrawPoint(area.polygon[(i+1)%area.polygon.Count],h));
            }
            selectedZone=Mathf.Clamp(selectedZone,0,profile.areas.Count-1);
            var selected=profile.areas[selectedZone];
            if(selected==null)return;
            Vector3 center=DrawPoint(selected.center,view.pivot.y);
            EditorGUI.BeginChangeCheck();
            Vector3 changed=Handles.PositionHandle(center,Quaternion.identity);
            if(EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(profile,"Move firefly preview area");
                Vector2 delta=new Vector2(changed.x-center.x,changed.z-center.z);
                selected.center+=delta;
                if(selected.shape==FireflyPreviewZoneShape.Polygon)
                    for(int i=0;i<selected.polygon.Count;i++)selected.polygon[i]+=delta;
                MarkChanged();Repaint();
            }
        }
    }
}
