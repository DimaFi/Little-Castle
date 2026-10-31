using System;
using LittleCastle.Rendering;
using UnityEditor;
using UnityEngine;

namespace LittleCastle.Editor
{
    public sealed partial class MapAuthoringWindow
    {
        [SerializeField] bool showFireflyEditor = true;
        [SerializeField] int selectedFireflyArea;
        GameObject fireflyPreviewObject;
        FireflyWorldRenderer fireflyPreviewRenderer;

        void BindFireflyPreview()
        {
            RestoreFireflyPreview();
            if(session==null)return;
            fireflyPreviewObject=new GameObject("World Editor fireflies (transient preview)");
            fireflyPreviewObject.hideFlags=HideFlags.HideAndDontSave;
            fireflyPreviewRenderer=fireflyPreviewObject.AddComponent<FireflyWorldRenderer>();
            fireflyPreviewRenderer.Connect(session);
        }

        void RestoreFireflyPreview()
        {
            if(fireflyPreviewObject)DestroyImmediate(fireflyPreviewObject);
            fireflyPreviewObject=null;fireflyPreviewRenderer=null;
        }

        void DrawFireflyTools()
        {
            showFireflyEditor=EditorGUILayout.Foldout(showFireflyEditor,"Светлячки · области и производительность",true);
            if(!showFireflyEditor)return;
            var data=session.ExportFireflies();
            EditorGUI.BeginChangeCheck();
            data.enabled=EditorGUILayout.Toggle("Включены на карте",data.enabled);
            data.seed=EditorGUILayout.IntField("Глобальный seed",data.seed);
            data.densityPer100m2=EditorGUILayout.Slider("Огоньков / 100 м²",data.densityPer100m2,0,80);
            data.globalDesiredCap=EditorGUILayout.IntSlider("Желаемый cap карты",data.globalDesiredCap,0,50000);
            data.perAreaDesiredCap=EditorGUILayout.IntSlider("Cap области",data.perAreaDesiredCap,0,20000);
            data.cellDesiredCap=EditorGUILayout.IntSlider("Cap cell",data.cellDesiredCap,0,2000);
            data.minimumHeight=EditorGUILayout.Slider("Высота от земли min, м",data.minimumHeight,0,10);
            data.maximumHeight=EditorGUILayout.Slider("Высота от земли max, м",data.maximumHeight,data.minimumHeight,20);
            data.maximumSlopeDegrees=EditorGUILayout.Slider("Наклон земли max, °",data.maximumSlopeDegrees,0,90);
            data.allowSummer=EditorGUILayout.Toggle("Летний биом",data.allowSummer);
            data.allowEnchantedForest=EditorGUILayout.Toggle("Зачарованный лес",data.allowEnchantedForest);
            data.allowElvenForest=EditorGUILayout.Toggle("Эльфийский лес",data.allowElvenForest);
            data.allowWinter=EditorGUILayout.Toggle("Зимний биом",data.allowWinter);
            data.color=EditorGUILayout.ColorField("Тёплый цвет",data.color);
            data.worldSize=EditorGUILayout.Slider("Размер, м",data.worldSize,.01f,1f);
            data.glow=EditorGUILayout.Slider("Emission",data.glow,0,4);
            data.motionAmplitude=EditorGUILayout.Slider("Амплитуда, м",data.motionAmplitude,0,2);
            data.motionSpeed=EditorGUILayout.Slider("Скорость",data.motionSpeed,0,4);
            data.flickerStrength=EditorGUILayout.Slider("Мерцание",data.flickerStrength,0,.5f);
            data.twilightStart=EditorGUILayout.Slider("Появление в сумерках",data.twilightStart,0,1);
            data.minimumNightAmount=EditorGUILayout.Slider("Ночная яркость: порог",data.minimumNightAmount,0,.98f);
            data.allowNear=EditorGUILayout.Toggle("Близкие пролёты",data.allowNear);
            data.nearMode=(FireflyNearMode)EditorGUILayout.EnumPopup("Близкий слой",data.nearMode);
            data.desiredNearCount=EditorGUILayout.IntSlider("Близких огоньков",data.desiredNearCount,0,48);
            data.nearRadius=EditorGUILayout.Slider("Радиус камеры, м",data.nearRadius,1,25);
            data.defaultQuality=(FireflyQuality)EditorGUILayout.EnumPopup("Пресет карты",data.defaultQuality);
            if(EditorGUI.EndChangeCheck())Run(()=>session.SetFireflies(data));

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Редактируемые области",EditorStyles.boldLabel);
            using(new EditorGUILayout.HorizontalScope())
            {
                if(GUILayout.Button("+ Эллипс"))Run(()=>CreateFireflyArea(FireflyAreaShape.Ellipse));
                if(GUILayout.Button("+ Полигон"))Run(()=>CreateFireflyArea(FireflyAreaShape.Polygon));
            }
            if(data.areas.Count==0){EditorGUILayout.HelpBox("Создайте область; нажмите на неё в списке, чтобы настроить.",MessageType.Info);return;}
            selectedFireflyArea=Mathf.Clamp(selectedFireflyArea,0,data.areas.Count-1);
            var labels=new string[data.areas.Count];
            for(int i=0;i<labels.Length;i++)labels[i]=data.areas[i].name+" ("+i+")";
            selectedFireflyArea=EditorGUILayout.Popup("Выбрана",selectedFireflyArea,labels);
            var area=data.areas[selectedFireflyArea];
            EditorGUI.BeginChangeCheck();
            area.name=EditorGUILayout.TextField("Имя",area.name);
            EditorGUILayout.LabelField("ID: "+area.id,EditorStyles.miniLabel);
            area.enabled=EditorGUILayout.Toggle("Включена",area.enabled);
            area.seed=EditorGUILayout.IntField("Seed области",area.seed);
            area.priority=EditorGUILayout.IntSlider("Приоритет",area.priority,-100,100);
            area.shape=(FireflyAreaShape)EditorGUILayout.EnumPopup("Форма",area.shape);
            area.center=EditorGUILayout.Vector2Field("Центр X/Z, м",area.center);
            area.radius=EditorGUILayout.Vector2Field("Радиусы X/Z, м",area.radius);
            area.angleDegrees=EditorGUILayout.Slider("Поворот, °",area.angleDegrees,0,360);
            area.falloff=EditorGUILayout.Slider("Мягкость края",area.falloff,0,1);
            area.densityMode=(FireflyDensityMode)EditorGUILayout.EnumPopup("Плотность",area.densityMode);
            if(area.densityMode==FireflyDensityMode.Multiply)
                area.densityValue=EditorGUILayout.Slider("Множитель",area.densityValue,0,100);
            else if(area.densityMode==FireflyDensityMode.Set)
                area.densityValue=EditorGUILayout.Slider("Огоньков / 100 м²",area.densityValue,0,80);
            area.overrideColor=EditorGUILayout.Toggle("Свой цвет",area.overrideColor);
            if(area.overrideColor)area.color=EditorGUILayout.ColorField("Цвет области",area.color);
            if(area.shape==FireflyAreaShape.Polygon)
            {
                EditorGUILayout.LabelField("Вершины полигона (X/Z)",EditorStyles.miniLabel);
                for(int i=0;i<area.polygon.Count;i++)
                    area.polygon[i]=EditorGUILayout.Vector2Field("Точка "+(i+1),area.polygon[i]);
            }
            if(EditorGUI.EndChangeCheck())Run(()=>session.SetFireflies(data));
            using(new EditorGUILayout.HorizontalScope())
            {
                if(GUILayout.Button("К центру камеры"))Run(()=>
                {
                    var view=SceneView.lastActiveSceneView;
                    if(!view)return;
                    var d=session.ExportFireflies();var a=d.areas[selectedFireflyArea];
                    Vector2 p=new Vector2(view.pivot.x,view.pivot.z);
                    Vector2 delta=p-a.center;a.center=p;
                    if(a.shape==FireflyAreaShape.Polygon)
                        for(int i=0;i<a.polygon.Count;i++)a.polygon[i]+=delta;
                    session.SetFireflies(d);
                });
                if(GUILayout.Button("Удалить область"))Run(()=>
                {session.RemoveFireflyArea(area.id);selectedFireflyArea=0;});
            }
            if(fireflyPreviewRenderer)
            {
                var r=fireflyPreviewRenderer;
                EditorGUILayout.LabelField("Visible "+r.VisibleWorldCount+
                    " / desired "+r.DesiredWorldCount+" · Near "+r.VisibleNearCount+
                    " · Cells "+r.ResidentCellCount+" · Waiting "+r.WaitingCellCount+
                    " · Capped "+r.GlobalCapHitCount,EditorStyles.miniLabel);
                var q=r.quality;
                q.preset=(FireflyQuality)EditorGUILayout.EnumPopup("Качество preview",q.preset);
                q.graphicsVsFps=EditorGUILayout.Slider("FPS ← → графика",q.graphicsVsFps,0,1);
                if(q.preset==FireflyQuality.Custom)
                {
                    q.customWorldCap=EditorGUILayout.IntSlider("World cap",q.customWorldCap,0,50000);
                    q.customNearCap=EditorGUILayout.IntSlider("Near cap",q.customNearCap,0,48);
                    q.customDistance=EditorGUILayout.Slider("Дистанция, м",q.customDistance,5,300);
                }
            }
            EditorGUILayout.HelpBox("Для перемещения области снимите галочку «Рисовать в SceneView». Видны граница и мягкий край. Все правки — общие Undo/Redo карты.",MessageType.None);
        }

        void CreateFireflyArea(FireflyAreaShape shape)
        {
            var view=SceneView.lastActiveSceneView;
            Vector2 p=view?new Vector2(view.pivot.x,view.pivot.z):Vector2.zero;
            var b=session.Bounds;
            p.x=Mathf.Clamp(p.x,b.x,b.x+b.z);p.y=Mathf.Clamp(p.y,b.y,b.y+b.w);
            var area=new FireflyAreaData{center=p,shape=shape,name="Светлячки "+(session.ExportFireflies().areas.Count+1)};
            if(shape==FireflyAreaShape.Polygon)
            {
                area.polygon.Add(p+new Vector2(-6,-6));
                area.polygon.Add(p+new Vector2(6,-6));
                area.polygon.Add(p+new Vector2(0,7));
            }
            session.UpsertFireflyArea(area);
            selectedFireflyArea=session.ExportFireflies().areas.Count-1;
            SceneView.RepaintAll();
        }

        void FireflySceneGUI(SceneView view)
        {
            if(!this||session==null||EditorApplication.isPlaying||!showFireflyEditor||toolCategory!=4)return;
            var data=session.ExportFireflies();
            if(data.areas.Count==0)return;
            selectedFireflyArea=Mathf.Clamp(selectedFireflyArea,0,data.areas.Count-1);
            var a=data.areas[selectedFireflyArea];
            Vector3 World(Vector2 p)=>new Vector3(p.x,session.SampleHeight(p.x,p.y)+.35f,p.y);
            Handles.color=new Color(1f,.84f,.25f,.9f);
            if(a.shape==FireflyAreaShape.Ellipse)
            {
                Vector3 prev=default;
                for(int i=0;i<=64;i++)
                {
                    float t=i*2*Mathf.PI/64;
                    Vector2 d=new Vector2(Mathf.Cos(t)*a.radius.x,Mathf.Sin(t)*a.radius.y);
                    float angle=a.angleDegrees*Mathf.Deg2Rad;
                    Vector2 p=a.center+new Vector2(d.x*Mathf.Cos(angle)-d.y*Mathf.Sin(angle),
                        d.x*Mathf.Sin(angle)+d.y*Mathf.Cos(angle));
                    Vector3 current=World(p);
                    if(i>0)Handles.DrawLine(prev,current);
                    prev=current;
                }
            }
            else if(a.polygon.Count>2)
            {
                for(int i=0;i<a.polygon.Count;i++)
                    Handles.DrawLine(World(a.polygon[i]),World(a.polygon[(i+1)%a.polygon.Count]));
            }
            if(brush)return;
            EditorGUI.BeginChangeCheck();
            Vector3 moved=Handles.PositionHandle(World(a.center),Quaternion.identity);
            if(EditorGUI.EndChangeCheck())
            {
                var delta=new Vector2(moved.x-a.center.x,moved.z-a.center.y);
                a.center+=delta;
                if(a.shape==FireflyAreaShape.Polygon)
                    for(int i=0;i<a.polygon.Count;i++)a.polygon[i]+=delta;
                Run(()=>session.SetFireflies(data));
            }
        }
    }
}
