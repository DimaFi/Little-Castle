using LittleCastle.World;
using LittleCastle.World.Environment;
using UnityEditor;
using UnityEngine;

namespace LittleCastle.Editor
{
    public sealed partial class MapAuthoringWindow
    {
        static readonly string[] EnvironmentThemes={"Лето (0)","Зачарованный лес (1)","Эльфийский лес (2)","Зима (3)"};
        static readonly string[] EnvironmentModes={"Добавить","Стереть","Сгладить"};
        static readonly string[] SnowModes={"Добавить","Стереть"};
        [SerializeField] int environmentThemeId=3;
        [SerializeField] MapEnvironmentBrushMode environmentMode;
        [SerializeField] bool showEnvironmentWeather;

        void DrawEnvironmentTools()
        {
            using(new EditorGUI.DisabledScope(tools.Active))
            {
                bool snow=CurrentToolId=="environment.snow";
                if(!snow)environmentThemeId=EditorGUILayout.Popup("Биом",Mathf.Clamp(environmentThemeId,0,3),EnvironmentThemes);
                int next=EditorGUILayout.Popup("Действие",Mathf.Clamp((int)environmentMode,0,snow?1:2),snow?SnowModes:EnvironmentModes);
                environmentMode=(MapEnvironmentBrushMode)next;
            }
            EditorGUILayout.HelpBox("Один штрих — одна отмена. Биом меняет оформление области; снег рисуется отдельной маской.",MessageType.None);
            showEnvironmentWeather=EditorGUILayout.Foldout(showEnvironmentWeather,"Снег и погода карты",true);
            if(!showEnvironmentWeather)return;
            var current=session.EnvironmentSettings;
            var edited=new MapEnvironmentSettings{
                snowAmount=current.snowAmount,winterSnowPreview=current.winterSnowPreview,
                simulateWeather=current.simulateWeather,snowfallIntensity=current.snowfallIntensity,
                accumulationPerSecond=current.accumulationPerSecond,meltingPerSecond=current.meltingPerSecond};
            EditorGUI.BeginChangeCheck();
            edited.snowAmount=EditorGUILayout.Slider("Количество снега",edited.snowAmount,0,1);
            edited.winterSnowPreview=EditorGUILayout.Toggle("Снег зимнего биома",edited.winterSnowPreview);
            edited.simulateWeather=EditorGUILayout.Toggle("Симуляция погоды",edited.simulateWeather);
            edited.snowfallIntensity=EditorGUILayout.Slider("Снегопад",edited.snowfallIntensity,0,1);
            edited.accumulationPerSecond=EditorGUILayout.Slider("Накопление в секунду",edited.accumulationPerSecond,0,1);
            edited.meltingPerSecond=EditorGUILayout.Slider("Таяние в секунду",edited.meltingPerSecond,0,1);
            if(EditorGUI.EndChangeCheck())Run(()=>{tools.Complete();pointerHeld=false;session.SetEnvironmentSettings(edited);});
        }
    }
}
