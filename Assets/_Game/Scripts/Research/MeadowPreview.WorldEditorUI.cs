using LittleCastle.CameraSystem;
using LittleCastle.Rendering;
using LittleCastle.World;
using UnityEngine;

namespace LittleCastle.Research
{
    public sealed partial class MeadowPreview
    {
        static readonly string[] AuthoringSections={"Рельеф","Покрытие","Растительность","Реки"};
        static readonly string[] PlantSections={"Цветы и мелочь","Деревья","Рощи"};
        int authoringSection=1,plantSection;
        bool authoringViewOptions,authoringDiagnostics;
        MapRiverRuntimeTool authoringRivers;
        GUIStyle authoringWrap;
        GUIStyle authoringWrappedButton;
        string authoringNotice;

        void DrawWorldEditorUI()
        {
            if(!showUI)return;
            float scale=Mathf.Clamp(Screen.width/1600f,.85f,1.3f);
            var previousMatrix=GUI.matrix;GUI.matrix=Matrix4x4.Scale(Vector3.one*scale);
            authoringWrap??=new GUIStyle(GUI.skin.label){wordWrap=true};
            authoringWrappedButton??=new GUIStyle(GUI.skin.button){wordWrap=true};
            if(painter&&!authoringRivers)authoringRivers=painter.GetComponent<MapRiverRuntimeTool>();
            if(authoringRivers)authoringRivers.Embedded=true;
            if(!string.IsNullOrEmpty(authoringNotice)&&authoringRivers&&!authoringRivers.HasUnappliedDraft)authoringNotice=null;
            GUILayout.BeginArea(new Rect(16,16,300,Mathf.Min(900,Screen.height/scale-32)),GUI.skin.box);
            GUILayout.Label("WORLD EDITOR");
            if(!painter||painter.Session==null){GUILayout.Label("Подготовка карты…");GUILayout.EndArea();GUI.matrix=previousMatrix;return;}
            using(new GUILayout.HorizontalScope())
            {
                if(GUILayout.Button("Отменить"))painter.Undo();
                if(GUILayout.Button("Повторить"))painter.Redo();
            }
            using(new GUILayout.HorizontalScope())
            {
                if(GUILayout.Button("Сохранить")&&AuthoringDocumentReady()){painter.EndStroke();painter.SaveEdits();}
                if(GUILayout.Button("Загрузить")&&AuthoringDocumentReady()){painter.EndStroke();painter.LoadEdits();}
            }
            if(!string.IsNullOrEmpty(authoringNotice))GUILayout.Label(authoringNotice,authoringWrap,GUILayout.Width(280));
            int section=GUILayout.SelectionGrid(authoringSection,AuthoringSections,2,GUILayout.Width(280));
            if(section!=authoringSection)ChangeAuthoringSection(section);
            panelScroll=GUILayout.BeginScrollView(panelScroll,false,true,GUILayout.Width(288));
            if(authoringSection==3)
            {
                GUILayout.Label("1. Новая река → 2. точки на земле → 3. Применить",authoringWrap,GUILayout.Width(268));
                if(authoringRivers)
                {
                    var label=GUI.skin.label;var button=GUI.skin.button;
                    try{GUI.skin.label=authoringWrap;GUI.skin.button=authoringWrappedButton;authoringRivers.DrawTools();}
                    finally{GUI.skin.label=label;GUI.skin.button=button;}
                }
                else GUILayout.Label("Инструмент рек ещё готовится.",authoringWrap);
            }
            else
            {
                if(authoringSection==0)
                {
                    using(new GUILayout.HorizontalScope())
                    {if(GUILayout.Button("Поднять"))ArmAuthoringBrush("terrain.raise");if(GUILayout.Button("Опустить"))ArmAuthoringBrush("terrain.lower");}
                    GUILayout.Label("Пределы: "+painter.Session.MinimumHeight.ToString("F1")+"…"+painter.Session.MaximumHeight.ToString("F1")+" м");
                    painter.terrainMetresPerSecond=AuthoringSlider("Скорость, м/с",painter.terrainMetresPerSecond,.1f,4);
                }
                else if(authoringSection==1)
                {
                    if(GUILayout.Button("Дорожка"))ArmAuthoringBrush("surface.road");
                    using(new GUILayout.HorizontalScope())
                    {if(GUILayout.Button("Луг"))ArmAuthoringBrush("surface.meadow");if(GUILayout.Button("Убрать траву"))ArmAuthoringBrush("surface.erase");}
                }
                else DrawAuthoringPlants();
                bool availableGrove=painter.treeCatalog&&painter.treeCatalog.packs.Length>0&&!painter.playerMode;
                if(authoringSection!=2||plantSection==0||plantSection==2&&availableGrove)
                {
                    bool enabled=painter.painting||painter.treeTool==2;
                    bool next=GUILayout.Toggle(enabled,"Инструмент активен · ЛКМ по земле");
                    if(next!=enabled)
                    {
                        if(next){if(authoringSection==2&&plantSection==2)painter.SetTreeTool(2);else ArmAuthoringBrush(authoringSection==0?(painter.lowerTerrain?"terrain.lower":"terrain.raise"):authoringSection==1&&painter.road?"surface.road":authoringSection==1&&painter.erase?"surface.erase":"surface.meadow");}
                        else{painter.EndStroke();painter.painting=false;painter.SetTreeTool(0);}
                    }
                    GUILayout.Label("Активно: "+(painter.treeTool==2?"Роща":painter.sculpting?(painter.lowerTerrain?"Опустить землю":"Поднять землю"):painter.road?"Дорожка":painter.erase?"Удаление травы":authoringSection==2&&plantSection==0?ProfileNames[(int)painter.currentProfile]:"Луг и растительность"),authoringWrap);
                    float minimum=painter.treeTool==2?1:.5f;
                    float maximum=painter.sculpting?Mathf.Min(128,Mathf.Max(32,painter.Session.HeightSpacing*64)):128;
                    painter.radius=AuthoringSlider("Радиус, м",painter.radius,minimum,maximum);
                    painter.strength=AuthoringSlider("Сила",painter.strength,0,1);
                    painter.brushSoftness=AuthoringSlider("Мягкость края",painter.brushSoftness,0,1);
                }
                if(authoringSection==1&&!painter.road&&!painter.erase)
                    painter.populatePlants=GUILayout.Toggle(painter.populatePlants,"Добавлять цветы и мелочь");
            }
            GUILayout.Space(8);
            if(!string.IsNullOrEmpty(painter.status))GUILayout.Label(painter.status,authoringWrap,GUILayout.Width(268));
            if(GUILayout.Button(authoringViewOptions?"▼ Освещение и камера":"▶ Освещение и камера"))authoringViewOptions=!authoringViewOptions;
            if(authoringViewOptions)DrawAuthoringView();
            if(GUILayout.Button(authoringDiagnostics?"▼ Проверка отображения":"▶ Проверка отображения"))authoringDiagnostics=!authoringDiagnostics;
            if(authoringDiagnostics)
            {
                bool player=GUILayout.Toggle(painter.playerMode,"Проверить доступ игрока");
                if(player!=painter.playerMode)
                {
                    painter.EndStroke();painter.playerMode=player;painter.Session.Access=player?painter.allowedTools&MapEditAccess.Player:painter.allowedTools;
                    if(player){if(authoringRivers)authoringRivers.CloseTools();if(painter.treeTool==2)painter.SetTreeTool(1);}
                }
                bool nextWind=GUILayout.Toggle(wind,"Ветер");if(nextWind!=wind){wind=nextWind;ApplyWind();}
                grass.enabled=GUILayout.Toggle(grass.enabled,"Объёмная трава");
                interaction.interactionEnabled=GUILayout.Toggle(interaction.interactionEnabled,"Пригибание рядом с объектом");
                moveProxy=GUILayout.Toggle(moveProxy,"Движение тестового объекта");
                bool soft=GUILayout.Toggle(grass.material.GetFloat("_MeadowSoftness")>.01f,"Мягкая трава вдали");grass.material.SetFloat("_MeadowSoftness",soft?originalSoftness:0);
                float distance=AuthoringSlider("Дальность травы, м",grass.maximumDistance,40,128);
                if(!Mathf.Approximately(distance,grass.maximumDistance)){grass.maximumDistance=distance;grass.material.SetFloat("_FadeStart",distance*.84375f);grass.material.SetFloat("_FadeEnd",distance);}
                interaction.activeDistance=AuthoringSlider("Дальность пригибания, м",interaction.activeDistance,12,60);grass.material.SetFloat("_InteractionDistance",interaction.activeDistance);
                GUILayout.Label("Пучки "+grass.submittedClumps+" · вызовы "+grass.drawCalls,authoringWrap);
                if(GUILayout.Button("Сбросить изменения этой сессии")&&AuthoringDocumentReady()){painter.EndStroke();painter.ResetEdits();}
            }
            GUILayout.Label("WASD / СКМ — движение · ПКМ — поворот\nКолесо — масштаб · H — скрыть панель",authoringWrap,GUILayout.Width(268));
            GUILayout.EndScrollView();GUILayout.EndArea();GUI.matrix=previousMatrix;
        }
        void ChangeAuthoringSection(int section)
        {
            painter.EndStroke();painter.painting=false;painter.SetTreeTool(0);painter.sculpting=section==0;
            authoringSection=section;panelScroll=Vector2.zero;
            if(authoringRivers){if(section==3)authoringRivers.OpenTools();else authoringRivers.CloseTools();}
            if(section==1){painter.road=true;painter.erase=false;}
            if(section==2){painter.road=false;painter.erase=false;painter.populatePlants=true;}
        }
        bool AuthoringDocumentReady()
        {
            if(authoringRivers&&authoringRivers.HasUnappliedDraft)
            {
                ChangeAuthoringSection(3);authoringNotice="У реки есть неприменённые точки. Нажмите Применить либо Отменить черновик перед сохранением или загрузкой.";return false;
            }
            authoringNotice=null;return true;
        }
        void ArmAuthoringBrush(string id)
        {
            painter.EndStroke();if(authoringRivers)authoringRivers.CloseTools();painter.SetTreeTool(0);
            painter.sculpting=id.StartsWith("terrain.");painter.lowerTerrain=id=="terrain.lower";
            painter.road=id=="surface.road";painter.erase=id=="surface.erase";painter.painting=true;
            free=true;studyCamera.GetComponent<StrategyCameraController>().enabled=true;
        }
        float AuthoringSlider(string label,float value,float min,float max)
        {GUILayout.Label(label+": "+value.ToString("F2"));return GUILayout.HorizontalSlider(value,min,max,GUILayout.Width(268));}
        void DrawAuthoringPlants()
        {
            int section=GUILayout.SelectionGrid(plantSection,PlantSections,1,GUILayout.Width(268));
            if(section!=plantSection){painter.EndStroke();painter.SetTreeTool(0);painter.painting=false;plantSection=section;}
            if(plantSection==0)
            {
                if(GUILayout.Button("Цветы · рисовать готовую группу"))
                {painter.currentProfile=MeadowScatterProfile.Flowers;ArmAuthoringBrush("surface.meadow");}
                if(GUILayout.Button("Рисовать выбранную растительность"))ArmAuthoringBrush("surface.meadow");
                painter.populatePlants=true;painter.road=false;painter.erase=false;painter.sculpting=false;
                painter.currentProfile=(MeadowScatterProfile)GUILayout.SelectionGrid((int)painter.currentProfile,ProfileNames,2,GUILayout.Width(268));
                painter.density=AuthoringSlider("Плотность мелочи",painter.density,0,1);return;
            }
            var catalog=painter.treeCatalog;if(!catalog||catalog.entries.Length==0){GUILayout.Label("Каталог деревьев не найден.");return;}
            if(plantSection==2)
            {
                if(painter.playerMode){GUILayout.Label("Рощи доступны в режиме автора.");return;}
                for(int i=0;i<catalog.packs.Length;i++)if(GUILayout.Button(MapTreeCatalog.PackTitle(catalog.packs[i]),GUILayout.Width(268))){painter.selectedTreePack=i;painter.SetTreeTool(2);}
                painter.treeBrushDensity=AuthoringSlider("Плотность рощи",painter.treeBrushDensity,0,1);
                painter.treeBrushSpacing=AuthoringSlider("Интервал, м",painter.treeBrushSpacing,3,12);return;
            }
            painter.selectedTreeGroup=GUILayout.SelectionGrid(painter.selectedTreeGroup,MapTreeCatalog.GroupNames,2,GUILayout.Width(268));
            var members=catalog.GroupIndices(painter.selectedTreeGroup);
            for(int row=0;row<(members.Count+1)/2;row++)
            {
                using(new GUILayout.HorizontalScope())for(int col=0;col<2;col++)
                {
                    int cell=row*2+col;if(cell>=members.Count)break;int i=members[cell];var entry=catalog.entries[i];
                    if(!painter.Session.CanPlaceTree(entry.definition.archetypeId))continue;
                    if(GUILayout.Button(new GUIContent(entry.title,entry.thumbnail),GUILayout.Width(128),GUILayout.Height(96)))
                    {painter.selectedTreeSource=i;painter.treeScale=Mathf.Clamp(1,entry.definition.minimumScale,entry.definition.maximumScale);painter.SetTreeTool(1);}
                }
            }
            painter.selectedTreeSource=Mathf.Clamp(painter.selectedTreeSource,0,catalog.entries.Length-1);var selected=catalog.entries[painter.selectedTreeSource];
            GUILayout.Label("Выбрано: "+selected.title,authoringWrap);
            if(GUILayout.Button("Выбрать дерево на карте"))painter.SetTreeTool(3);
            painter.treeYaw=AuthoringSlider("Поворот, °",painter.treeYaw,0,359);
            painter.treeScale=AuthoringSlider("Масштаб",painter.treeScale,selected.definition.minimumScale,selected.definition.maximumScale);
            if(painter.selectedTree!=0&&painter.Session.HasTree(painter.selectedTree))
            {
                if(GUILayout.Button("Переместить выбранное дерево"))painter.SetTreeTool(4);
                if(GUILayout.Button("Применить поворот и масштаб"))painter.ApplySelectedTreeTransform();
                if(GUILayout.Button("Удалить выбранное дерево"))painter.DeleteSelectedTree();
            }
            if(painter.treeTool!=0&&GUILayout.Button("Закончить работу с деревьями"))painter.SetTreeTool(0);
        }
        void DrawAuthoringView()
        {
            if(authoringClock)
            {
                GUILayout.Label("Время: "+authoringClock.CurrentHourInt.ToString("D2")+":"+authoringClock.CurrentMinute.ToString("D2"));
                authoringClock.RunAutomatically=GUILayout.Toggle(authoringClock.RunAutomatically,"Автоматическая смена суток");
                using(new GUILayout.HorizontalScope()){if(GUILayout.Button("День"))SetAuthoringHour(12);if(GUILayout.Button("Закат"))SetAuthoringHour(18.5f);if(GUILayout.Button("Ночь"))SetAuthoringHour(1);}
            }
            using(new GUILayout.HorizontalScope()){if(GUILayout.Button("Общий вид"))SetView(0);if(GUILayout.Button("Вблизи"))SetView(1);}
            using(new GUILayout.HorizontalScope()){if(GUILayout.Button("Стратегия"))SetView(2);if(GUILayout.Button("Тени"))SetView(3);}
        }
    }
}
