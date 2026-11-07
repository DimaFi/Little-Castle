using System;
using LittleCastle.Experimental.MountainBorder.Presentation;
using LittleCastle.Rendering;
using LittleCastle.Research;
using LittleCastle.World;
using LittleCastle.World.Weather;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace LittleCastle.Editor
{
    /// <summary>Editor adapter over existing scene components and saved profiles.</summary>
    public sealed class MapSystemsWindow:EditorWindow
    {
        Vector2 scroll;
        UnityEditor.Editor rainEditor,profileEditor,lightingEditor,fireflyEditor,atmosphereEditor;
        bool rain=true,lighting,fireflies,atmosphere;
        string message;
        [MenuItem("Little Castle/Map/Map System Settings")]
        public static void Open()
        {var window=GetWindow<MapSystemsWindow>("Настройки карты");window.minSize=new Vector2(500,560);}
        void OnInspectorUpdate()=>Repaint();
        void OnDisable(){DestroyImmediate(rainEditor);DestroyImmediate(profileEditor);DestroyImmediate(lightingEditor);DestroyImmediate(fireflyEditor);DestroyImmediate(atmosphereEditor);}
        public const string AtmosphereRendererPath = "Assets/_Game/Settings/Rendering/URP/LittleCastle_URP_AtmosphereRenderer.asset";

        static T FindOnMap<T>(MeadowSurfacePainter painter) where T : Component
        {
            foreach (var root in painter.gameObject.scene.GetRootGameObjects())
            {
                var component = root.GetComponentInChildren<T>(true);
                if (component) return component;
            }
            return null;
        }

        public static MapAtmospherePresentation EnsureAtmosphere(MeadowSurfacePainter painter)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || !painter || !painter.source ||
                !painter.studyCamera || painter.studyCamera.gameObject.scene != painter.gameObject.scene)
                throw new InvalidOperationException("Откройте сцену карты с настроенной камерой вне Play.");
            var existing = FindOnMap<MapAtmospherePresentation>(painter);
            if (existing) return existing;

            var lighting = FindOnMap<DayNightLightingController>(painter);
            var renderer = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(AtmosphereRendererPath);
            if (!lighting || !renderer)
                throw new InvalidOperationException("Для подключения нужны освещение карты и рендерер атмосферы.");

            EnsureWeather(painter);
            var settings = Undo.AddComponent<MapAtmospherePresentation>(painter.gameObject);
            var serialized = new SerializedObject(settings);
            serialized.FindProperty("painter").objectReferenceValue = painter;
            serialized.FindProperty("atmosphere").objectReferenceValue = lighting;
            serialized.FindProperty("mountainBorder").objectReferenceValue = FindOnMap<LandscapeBorderPresentation>(painter);
            serialized.FindProperty("experimentalRenderer").objectReferenceValue = renderer;
            serialized.ApplyModifiedProperties();
            EditorSceneManager.MarkSceneDirty(painter.gameObject.scene);
            return settings;
        }

        public static RainStormController EnsureWeather(MeadowSurfacePainter painter)
        {
            if(!painter||!painter.source)throw new InvalidOperationException("Откройте сцену авторской карты.");
            var existing=FindObjectsByType<RainStormController>(FindObjectsInactive.Include);
            if(existing.Length>1)throw new InvalidOperationException("В сценах несколько систем дождя. Оставьте одну.");
            if(existing.Length==1&&existing[0].gameObject.scene!=painter.gameObject.scene)
                throw new InvalidOperationException("Дождь принадлежит другой открытой сцене. Откройте одну карту.");
            RainStormController controller;
            if(existing.Length==1)controller=existing[0];
            else
            {
                var root=new GameObject("Map weather");
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root,painter.gameObject.scene);
                Undo.RegisterCreatedObjectUndo(root,"Connect map weather");
                controller=Undo.AddComponent<RainStormController>(root);
                controller.SetMode(RainMode.Off);
            }
            var serialized=new SerializedObject(controller);
            serialized.FindProperty("mapPainter").objectReferenceValue=painter;
            serialized.FindProperty("gameplayCamera").objectReferenceValue=painter.studyCamera;
            foreach(var cycle in painter.gameObject.scene.GetRootGameObjects())
            {var atmosphere=cycle.GetComponentInChildren<DayNightLightingController>(true);if(atmosphere){serialized.FindProperty("atmosphere").objectReferenceValue=atmosphere;break;}}
            serialized.ApplyModifiedProperties();
            if(!Application.isPlaying)EditorSceneManager.MarkSceneDirty(painter.gameObject.scene);
            return controller;
        }
        static void Inspector(UnityEngine.Object value,ref UnityEditor.Editor editor)
        {if(!value)return;UnityEditor.Editor.CreateCachedEditor(value,null,ref editor);editor.OnInspectorGUI();}
        void OnGUI()
        {
            scroll=EditorGUILayout.BeginScrollView(scroll);
            var painter=FindAnyObjectByType<MeadowSurfacePainter>();
            if(!painter||!painter.source){EditorGUILayout.HelpBox("Откройте карту через World Editor.",MessageType.Info);EditorGUILayout.EndScrollView();return;}
            EditorGUILayout.LabelField("Карта: "+painter.source.mapId,EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Профили сохраняют настройки. Подключение системы сохраняется в сцене через Ctrl+S. Изменения компонентов в Play временные.",MessageType.Info);
            rain=EditorGUILayout.Foldout(rain,"Дождь и гроза",true);
            if(rain)
            {
                var weather=FindAnyObjectByType<RainStormController>();
                if(weather&&weather.gameObject.scene!=painter.gameObject.scene)
                {EditorGUILayout.HelpBox("Дождь находится в другой открытой сцене. Для настройки откройте одну карту.",MessageType.Warning);weather=null;}
                if(!weather)
                {if(GUILayout.Button("Подключить дождь и грозу к этой карте"))try{weather=EnsureWeather(painter);}catch(Exception e){message=e.Message;}}
                if(weather)
                {
                    var serialized=new SerializedObject(weather);var profile=serialized.FindProperty("profile").objectReferenceValue;
                    using(new EditorGUI.DisabledScope(Application.isPlaying))
                    if(!profile&&GUILayout.Button("Создать сохраняемый профиль погоды"))
                    {
                        const string folder="Assets/_Game/Settings";
                        if(!AssetDatabase.IsValidFolder(folder))throw new InvalidOperationException("Не найдена папка настроек проекта.");
                        var saved=CreateInstance<RainStormProfile>();AssetDatabase.CreateAsset(saved,AssetDatabase.GenerateUniqueAssetPath(folder+"/MapWeather.asset"));serialized.FindProperty("profile").objectReferenceValue=saved;serialized.ApplyModifiedProperties();profile=saved;
                    }
                    Inspector(weather,ref rainEditor);
                    if(profile){EditorGUILayout.LabelField("Параметры профиля",EditorStyles.boldLabel);Inspector(profile,ref profileEditor);}
                }
            }
            lighting=EditorGUILayout.Foldout(lighting,"Освещение и время суток",true);
            if(lighting){if(GUILayout.Button("Открыть управление атмосферой"))EditorApplication.ExecuteMenuItem("Little Castle/Rendering/Atmosphere Preview");Inspector(FindAnyObjectByType<DayNightLightingController>(),ref lightingEditor);}
            atmosphere=EditorGUILayout.Foldout(atmosphere,"Облака и дальняя долина",true);
            if(atmosphere)
            {
                var presentation = FindOnMap<MapAtmospherePresentation>(painter);
                if (!presentation)
                {
                    using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
                        if (GUILayout.Button("Подключить атмосферу к этой карте"))
                            try { presentation = EnsureAtmosphere(painter); }
                            catch (Exception e) { message = e.Message; }
                }
                if (presentation)
                {
                    EditorGUILayout.HelpBox("Сохраните сцену. Эффекты запускаются в обычном Play. Физическое небо и объёмный туман включаются отдельно.", MessageType.Info);
                    Inspector(presentation, ref atmosphereEditor);
                }
            }
            fireflies=EditorGUILayout.Foldout(fireflies,"Светлячки",true);
            if(fireflies)
            {
                if(GUILayout.Button("Подготовить / открыть настройки светлячков"))try{Selection.activeObject=CurrentMapGameInspection.PrepareDefaultProfile();}catch(Exception e){message=e.Message;}
                Inspector(AssetDatabase.LoadAssetAtPath<LittleCastle.Rendering.Fireflies.FireflyPreviewProfile>("Assets/_Game/Settings/FireflyGameInspection.asset"),ref fireflyEditor);
                EditorGUILayout.HelpBox("Для запуска используйте «Проверить текущую карту в Game…» в World Editor. Днём светлячки затушены.",MessageType.Info);
            }
            if(!Application.isPlaying&&GUILayout.Button("Сохранить настройки профилей"))
            {
                var weather=FindAnyObjectByType<RainStormController>();
                if(weather&&weather.gameObject.scene==painter.gameObject.scene)
                {var value=new SerializedObject(weather).FindProperty("profile").objectReferenceValue;if(value)AssetDatabase.SaveAssetIfDirty(value);}
                var valueFirefly=AssetDatabase.LoadAssetAtPath<LittleCastle.Rendering.Fireflies.FireflyPreviewProfile>("Assets/_Game/Settings/FireflyGameInspection.asset");if(valueFirefly)AssetDatabase.SaveAssetIfDirty(valueFirefly);
            }
            if(!string.IsNullOrEmpty(message))EditorGUILayout.HelpBox(message,MessageType.Warning);
            EditorGUILayout.EndScrollView();
        }
    }
}
