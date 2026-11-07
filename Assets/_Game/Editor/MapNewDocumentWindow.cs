using System;
using System.IO;
using LittleCastle.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LittleCastle.Editor
{
    public sealed class MapNewDocumentWindow:EditorWindow
    {
        MapAuthoringWindow owner;
        [SerializeField] string mapName="NewMap";
        [SerializeField] DefaultAsset folder;
        [SerializeField] int width=128,length=128;
        [SerializeField] float baseHeight,minimumHeight=-4,maximumHeight=12;
        string status;
        public static void Open(MapAuthoringWindow owner)
        {
            var window=GetWindow<MapNewDocumentWindow>(true,"Новая карта",true);window.owner=owner;window.minSize=new Vector2(390,340);
            if(!window.folder)window.folder=AssetDatabase.LoadAssetAtPath<DefaultAsset>("Assets");window.Show();
        }
        void OnGUI()
        {
            EditorGUILayout.LabelField("Новая плоская карта",EditorStyles.boldLabel);
            mapName=EditorGUILayout.TextField("Название",mapName);folder=(DefaultAsset)EditorGUILayout.ObjectField("Папка ресурсов",folder,typeof(DefaultAsset),false);
            width=EditorGUILayout.IntField("Ширина, м",width);length=EditorGUILayout.IntField("Длина, м",length);
            baseHeight=EditorGUILayout.FloatField("Начальная высота, м",baseHeight);minimumHeight=EditorGUILayout.FloatField("Минимум рельефа, м",minimumHeight);maximumHeight=EditorGUILayout.FloatField("Максимум рельефа, м",maximumHeight);
            EditorGUILayout.HelpBox("Создаётся отдельная сцена и собственные ресурсы карты. Сначала сохраните текущую сцену и документ. Начальная карта пустая: без рек, деревьев и готового травяного ковра.",MessageType.Info);
            if(width>256||length>256)EditorGUILayout.HelpBox("Большая сетка 1 м требует больше памяти и времени открытия. Стоимость 512 м пока не измерена.",MessageType.Warning);
            using(new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode||!owner))
                if(GUILayout.Button("Создать отдельную сцену карты"))Create();
            if(!string.IsNullOrEmpty(status))EditorGUILayout.HelpBox(status,MessageType.None);
        }
        MapCreationOptions Options()=>new MapCreationOptions{widthMetres=width,lengthMetres=length,baseHeight=baseHeight,minimumHeight=minimumHeight,maximumHeight=maximumHeight};
        bool Validate(out string path,out string error)
        {
            path=folder?AssetDatabase.GetAssetPath(folder):"";
            if(!(path=="Assets"||path.StartsWith("Assets/",StringComparison.Ordinal))||!AssetDatabase.IsValidFolder(path)){error="Выберите существующую папку внутри Assets.";return false;}
            string name=(mapName??"").Trim();
            if(name.Length==0||name=="."||name==".."||name.IndexOfAny(Path.GetInvalidFileNameChars())>=0||name.IndexOf('/')>=0||name.IndexOf('\\')>=0){error="Введите допустимое название новой папки карты.";return false;}
            return MapCreationFactory.TryValidate(Options(),out error);
        }
        void Create()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode||!owner)return;
            if(!Validate(out string parent,out string error)){status=error;return;}
            MapNewSceneAssets assets=null;string createdFolder=null;bool sceneCreated=false,sceneSaved=false;
            var setup=EditorSceneManager.GetSceneManagerSetup();
            try
            {
                // Prepare and validate dependencies before asking to replace any existing scene.
                assets=MapNewSceneFactory.CreateAssets(Options());
                if(!owner.CanReplaceMap())return;
                owner.SuspendMapPreview();
                if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()){owner.ResumeMapPreview();return;}
                for(int i=0;i<UnityEngine.SceneManagement.SceneManager.sceneCount;i++)if(UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)
                {status="Сначала сохраните открытые сцены. Новая карта не создана.";owner.ResumeMapPreview();return;}
                setup=EditorSceneManager.GetSceneManagerSetup();
                createdFolder=AssetDatabase.GenerateUniqueAssetPath(parent+"/"+mapName.Trim());
                string guid=AssetDatabase.CreateFolder(parent,Path.GetFileName(createdFolder));if(string.IsNullOrEmpty(guid))throw new IOException("Не удалось создать папку карты.");
                createdFolder=AssetDatabase.GUIDToAssetPath(guid);
                Save(assets.source,"MapSource.asset");Save(assets.groundMesh,"Ground.asset");Save(assets.terrain,"Ground.mat");Save(assets.grass,"Grass.mat");Save(assets.accent,"Accents.mat");Save(assets.brush,"Brush.mat");Save(assets.sky,"Sky.mat");Save(assets.timeSettings,"Time.asset");
                void Save(UnityEngine.Object value,string file){AssetDatabase.CreateAsset(value,createdFolder+"/"+file);AssetDatabase.SaveAssetIfDirty(value);}
                var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);sceneCreated=true;
                MapNewSceneFactory.CreateObjects(assets);AssetDatabase.SaveAssetIfDirty(assets.sky);EditorSceneManager.MarkSceneDirty(scene);
                string scenePath=createdFolder+"/Map.unity";
                if(!EditorSceneManager.SaveScene(scene,scenePath))throw new IOException("Не удалось сохранить новую сцену карты.");
                sceneSaved=true;owner.ConnectCreatedMap(assets.source);Selection.activeObject=assets.source;
                status="Создана сцена: "+scenePath+". Перед повторным открытием сохраняйте правки кнопкой «Сохранить карту в базу»; JSON хранится отдельно и загружается после открытия сцены.";
            }
            catch(Exception exception)
            {
                if(!sceneSaved)
                {
                    if(sceneCreated)EditorSceneManager.RestoreSceneManagerSetup(setup);
                    if(!string.IsNullOrEmpty(createdFolder)&&AssetDatabase.IsValidFolder(createdFolder))AssetDatabase.DeleteAsset(createdFolder);
                    if(owner)owner.ResumeMapPreview();
                }
                status=(sceneSaved?"Сцена создана, но редактор не подключился: ":"Создание отменено: ")+exception.Message;Debug.LogException(exception);
            }
            finally{assets?.Dispose();}
        }
    }
}
