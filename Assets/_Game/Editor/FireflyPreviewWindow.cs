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
        [SerializeField] FireflyPreviewProfile profile;
        [SerializeField] bool previewEnabled = true;
        [SerializeField] bool editHandles = true;
        [SerializeField] int selectedZone;
        [SerializeField] FireflyPreviewQuality visualQuality=FireflyPreviewQuality.Medium;
        [SerializeField, Range(0f,1f)] float graphicsVsFps=.5f;
        [SerializeField] bool showFullProfile=true;
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

            previewEnabled=EditorGUILayout.Toggle("Показывать в SceneView",previewEnabled);
            editHandles=EditorGUILayout.Toggle("Перемещать области",editHandles);
            visualQuality=(FireflyPreviewQuality)EditorGUILayout.EnumPopup("Качество",visualQuality);
            graphicsVsFps=EditorGUILayout.Slider("FPS ← → графика",graphicsVsFps,0f,1f);

            var budget=new FireflyPreviewBudget{
                quality=visualQuality,graphicsVsPerformance=graphicsVsFps};
            budget.Resolve(out int maxWorld,out int maxNear,out float distance);
            EditorGUILayout.LabelField("Бюджет: "+maxWorld+" мировых, "+maxNear+
                " близких, радиус "+distance.ToString("F0")+" м",EditorStyles.miniLabel);

            if(rendererView)
            {
                EditorGUILayout.LabelField("Draw calls "+rendererView.DrawCalls+
                    " | отправлено "+rendererView.SubmittedWorldCount+
                    " | запрошено "+rendererView.DesiredWorldCount+
                    " | близких "+rendererView.SubmittedNearCount,EditorStyles.miniLabel);
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
            var densityMode=(FireflyPreviewDensityMode)EditorGUILayout.EnumPopup("Плотность",area.densityMode);
            float value=EditorGUILayout.Slider("Множитель / на 100 м²",area.densityValue,0f,100f);
            float feather=EditorGUILayout.Slider("Мягкость края",area.falloff,0f,1f);
            Vector2 center=EditorGUILayout.Vector2Field("Центр X/Z, м",area.center);
            Vector2 radii=EditorGUILayout.Vector2Field("Радиусы X/Z, м",area.radii);
            float rotation=EditorGUILayout.Slider("Поворот, °",area.rotationDegrees,0f,360f);
            if(EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(profile,"Edit firefly preview area");
                area.displayName=newName;
                area.enabled=newEnabled;
                area.priority=priority;
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
