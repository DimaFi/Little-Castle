using System;
using LittleCastle.Rendering;
using LittleCastle.Rendering.Fireflies;
using LittleCastle.Research;
using LittleCastle.World;
using UnityEditor;
using UnityEngine;

namespace LittleCastle.Editor
{
    /// <summary>Explicit, one-Play inspection of the current document, never an older save or fixture.</summary>
    [InitializeOnLoad]
    public sealed class CurrentMapGameInspection : EditorWindow
    {
        const string DefaultProfilePath="Assets/_Game/Settings/FireflyGameInspection.asset";
        const string DefaultMaterialPath="Assets/_Game/Settings/FireflyGameInspection.mat";
        const string Key="LC.CurrentMapGameInspection.";
        [SerializeField] MapAuthoringWindow author;
        [SerializeField] SurfacePaintData source;
        [SerializeField] FireflyPreviewProfile profile;
        static FireflyPreviewRenderer ownedRenderer;
        static FireflyPreviewProfile transientProfile;
        static MeadowSurfacePainter targetPainter;
        static string originalDiagnosticSaveId;

        static CurrentMapGameInspection()
        {
            MeadowSurfacePainter.InspectionDocumentProvider=ProvideDocument;
            EditorApplication.update+=Tick;
            EditorApplication.playModeStateChanged+=StateChanged;
            AssemblyReloadEvents.beforeAssemblyReload+=BeforeAssemblyReload;
        }

        [MenuItem("Little Castle/Map/Current Map Game Inspection")]
        static void Show()=>GetWindow<CurrentMapGameInspection>("Current map Game");
        internal static void Open(MapAuthoringWindow window,SurfacePaintData map)
        {
            var panel=GetWindow<CurrentMapGameInspection>("Current map Game");
            panel.author=window;panel.source=map;
            if(!panel.profile)panel.profile=AssetDatabase.LoadAssetAtPath<FireflyPreviewProfile>(DefaultProfilePath);
        }

        public static FireflyPreviewProfile PrepareDefaultProfile()
        {
            var saved=AssetDatabase.LoadAssetAtPath<FireflyPreviewProfile>(DefaultProfilePath);
            if(saved)return saved; // Reopening never replaces authored settings.
            var shader=Shader.Find("Little Castle/Effects/Firefly Preview URP");
            if(!shader||!shader.isSupported)throw new InvalidOperationException("Firefly Preview URP shader unavailable.");
            var material=AssetDatabase.LoadAssetAtPath<Material>(DefaultMaterialPath);
            if(!material)
            {
                material=new Material(shader){name="Firefly Game Inspection",enableInstancing=true};
                AssetDatabase.CreateAsset(material,DefaultMaterialPath);
            }
            saved=CreateInstance<FireflyPreviewProfile>();saved.name="Firefly Game Inspection";
            saved.automaticWorld.enabled=true;saved.automaticWorld.runtimeAutoStart=false;
            saved.materialForBuild=material;
            saved.Validate();
            AssetDatabase.CreateAsset(saved,DefaultProfilePath);
            AssetDatabase.SaveAssetIfDirty(material);AssetDatabase.SaveAssetIfDirty(saved);
            return saved;
        }

        public static string ValidateProfile(FireflyPreviewProfile value)
        {
            if(!value||!AssetDatabase.Contains(value))return "Выберите сохранённый профиль светлячков.";
            if(!value.enabled||value.automaticWorld==null||!value.automaticWorld.enabled)
                return "В профиле включите enabled и automaticWorld.enabled.";
            if(!value.materialForBuild||!AssetDatabase.Contains(value.materialForBuild)||
               !value.materialForBuild.shader||!value.materialForBuild.shader.isSupported||
               value.materialForBuild.shader.name!="Little Castle/Effects/Firefly Preview URP")
                return "Нужен сохранённый materialForBuild с поддерживаемым Firefly Preview URP shader.";
            if(value.runtimeBudget==null||value.runtimeBudget.quality==FireflyPreviewQuality.Off)
                return "В runtimeBudget выберите качество Low / Medium / High / Custom вместо Off.";
            return null;
        }

        public static string ValidatePlayConfiguration(bool optionsEnabled,EnterPlayModeOptions options,Vector4 documentBounds,Vector4 sourceBounds)
        {
            if(optionsEnabled&&(options&(EnterPlayModeOptions.DisableDomainReload|EnterPlayModeOptions.DisableSceneReload))!=0)
                return "Для Game-теста включите Domain Reload и Scene Reload в Enter Play Mode Settings.";
            if(documentBounds!=sourceBounds)
                return "Границы текущего документа не совпадают с базой сцены; Game mask использует границы базы. Тест отменён.";
            return null;
        }

        internal static MapAuthoringDocument ProvideDocument(MeadowSurfacePainter painter)
        {
            if(!SessionState.GetBool(Key+"armed",false)||!painter.source)return null;
            string expected=SessionState.GetString(Key+"source","");
            if(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(painter.source))!=expected)return null;
            if(SessionState.GetBool(Key+"consumed",false))return null;
            var document=JsonUtility.FromJson<MapAuthoringDocument>(SessionState.GetString(Key+"document",""));
            if(document==null)throw new InvalidOperationException("Current map inspection document is missing.");
            if(document.bounds!=painter.source.bounds)
                throw new InvalidOperationException("Current map inspection bounds differ from scene source bounds.");
            // Consume before binding presenters; source and editor session are never mutated.
            SessionState.SetBool(Key+"consumed",true);
            targetPainter=painter;
            originalDiagnosticSaveId=painter.diagnosticSaveId;
            painter.diagnosticSaveId="current-map-game-inspection";
            SessionState.SetString(Key+"status","Текущий документ передан; ожидание runtime Session/presenters: "+document.mapId);
            return document;
        }

        void Begin()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;
            string error=ValidateProfile(profile);
            if(error!=null)throw new InvalidOperationException(error);
            var registered=Resources.Load<FireflyPreviewProfile>("LittleCastle/FireflyAutoWorld");
            if(registered&&registered.enabled&&registered.automaticWorld!=null&&registered.automaticWorld.enabled&&registered.automaticWorld.runtimeAutoStart)
                throw new InvalidOperationException("Сначала выключите глобальный автозапуск в Firefly Preview: отдельный тест управляет своим renderer.");
            if(!author||author.VerificationSession==null||!source)
                throw new InvalidOperationException("Откройте тест из заголовка нужного World Editor.");
            if(author.VerificationSession.HasPending)
                throw new InvalidOperationException("Сначала завершите или отмените активный штрих.");
            var painters=FindObjectsByType<MeadowSurfacePainter>(FindObjectsSortMode.None);
            if(painters.Length!=1||painters[0].source!=source)
                throw new InvalidOperationException("Открытая сцена должна содержать ровно один MeadowSurfacePainter с этой базой карты.");
            var document=author.VerificationSession.ExportMap();
            error=ValidatePlayConfiguration(EditorSettings.enterPlayModeOptionsEnabled,EditorSettings.enterPlayModeOptions,document.bounds,source.bounds);
            if(error!=null)throw new InvalidOperationException(error);
            SessionState.SetString(Key+"document",JsonUtility.ToJson(document));
            SessionState.SetString(Key+"source",AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(source)));
            SessionState.SetString(Key+"profile",AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(profile)));
            SessionState.SetBool(Key+"consumed",false);
            SessionState.SetBool(Key+"armed",true);
            SessionState.SetString(Key+"status","Ожидание загрузки текущей сцены…");
            EditorApplication.isPlaying=true;
        }

        static void Tick()
        {
            if(!Application.isPlaying||!SessionState.GetBool(Key+"armed",false))return;
            if(!targetPainter||targetPainter.Session==null||ownedRenderer)return;
            if(targetPainter.RiverView==null)
            {
                SessionState.SetString(Key+"status","Runtime Session загружена, но water presentation отсутствует: проверьте rock catalog/настройки сцены.");
                return;
            }
            var saved=AssetDatabase.LoadAssetAtPath<FireflyPreviewProfile>(AssetDatabase.GUIDToAssetPath(SessionState.GetString(Key+"profile","")));
            string error=ValidateProfile(saved);
            if(error!=null){SessionState.SetString(Key+"status",error);return;}
            // Never create duplicate presentation if another explicit component is already active.
            var existing=FindAnyObjectByType<FireflyPreviewRenderer>();
            if(existing){SessionState.SetString(Key+"status","Уже работает другой Firefly renderer; выключите глобальный автозапуск перед отдельным тестом.");return;}
            transientProfile=Instantiate(saved);transientProfile.hideFlags=HideFlags.HideAndDontSave;
            transientProfile.name=saved.name+" (one-Play inspection)";
            var root=new GameObject("Current map: fireflies inspection (Play only)");
            root.hideFlags=HideFlags.DontSave;
            ownedRenderer=root.AddComponent<FireflyPreviewRenderer>();
            ownedRenderer.ConfigureRuntime(transientProfile);
            ownedRenderer.ConnectReadOnlyWorld(targetPainter.Session);
            SessionState.SetString(Key+"status","Game использует текущую карту и сохранённый профиль; runtimeAutoStart не изменён.");
        }

        static void BeforeAssemblyReload()
        {
            // The normal Edit→Play domain reload must keep the pending one-shot request.
            // A reload after consumption must release Unity objects while references still exist.
            if(!SessionState.GetBool(Key+"consumed",false)&&!ownedRenderer&&!targetPainter)return;
            bool playing=Application.isPlaying;
            // The injected Session may remain alive through reload until scene teardown.
            // Keep its isolated save path for that entire interval, even after ownership is released.
            ReleaseInspection(!playing);
            SessionState.SetString(Key+"status","Тест прерван перед reload скриптов; выход из Play запрошен. Запустите тест заново.");
            if(playing)EditorApplication.isPlaying=false;
        }

        static void Cleanup()=>ReleaseInspection(true);

        static void ReleaseInspection(bool restoreSaveId)
        {
            SessionState.SetBool(Key+"armed",false);SessionState.SetBool(Key+"consumed",false);
            SessionState.EraseString(Key+"document");
            if(ownedRenderer)DestroyImmediate(ownedRenderer.gameObject);
            if(transientProfile)DestroyImmediate(transientProfile);
            if(restoreSaveId&&targetPainter)targetPainter.diagnosticSaveId=originalDiagnosticSaveId;
            ownedRenderer=null;transientProfile=null;targetPainter=null;originalDiagnosticSaveId=null;
        }

        static void StateChanged(PlayModeStateChange state)
        {
            if(state!=PlayModeStateChange.ExitingPlayMode&&state!=PlayModeStateChange.EnteredEditMode)return;
            bool active=SessionState.GetBool(Key+"armed",false)||ownedRenderer||targetPainter;
            Cleanup();
            if(active)SessionState.SetString(Key+"status","Тест завершён; Edit Mode восстанавливается. Game-правки не применялись к авторской базе.");
        }

        void OnInspectorUpdate()=>Repaint();
        void OnGUI()
        {
            EditorGUILayout.HelpBox("Одноразовый Game-тест текущего документа World Editor, включая неопубликованные правки. Исходная карта, сцена и глобальный автозапуск не сохраняются и не меняются. Незавершённый черновик реки не входит в документ.",MessageType.Info);
            using(new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            {
                EditorGUILayout.ObjectField("World Editor",author,typeof(MapAuthoringWindow),false);
                EditorGUILayout.ObjectField("База карты",source,typeof(SurfacePaintData),false);
                profile=(FireflyPreviewProfile)EditorGUILayout.ObjectField("Сохранённый профиль",profile,typeof(FireflyPreviewProfile),false);
                if(GUILayout.Button("Подготовить / выбрать профиль Game-теста"))
                    try{profile=PrepareDefaultProfile();}catch(Exception e){Debug.LogException(e);}
                string error=ValidateProfile(profile);
                if(error!=null)EditorGUILayout.HelpBox(error+" Нажмите «Подготовить / выбрать профиль Game-теста».",MessageType.Warning);
                using(new EditorGUI.DisabledScope(error!=null||!author||!source))
                    if(GUILayout.Button("Запустить текущую карту + светлячки в Game"))
                        try{Begin();}catch(Exception e){Debug.LogException(e);}
            }
            EditorGUILayout.LabelField(SessionState.GetString(Key+"status","Тест не запущен."),EditorStyles.wordWrappedLabel);
            if(!Application.isPlaying)return;
            var painter=targetPainter;
            EditorGUILayout.LabelField("Game map",painter&&painter.Session!=null?painter.Session.MapId:"не подключена");
            EditorGUILayout.LabelField("Water",painter&&painter.RiverView!=null?"runtime presentation подключён":"нет runtime presentation");
            var lighting=FindAnyObjectByType<StylizedLightingGlobals>();
            var cycle=FindAnyObjectByType<DayNightLightingController>();
            EditorGUILayout.LabelField("Часы мира",cycle&&cycle.TimeSystem?cycle.TimeSystem.CurrentHour.ToString("F2"):"не найдены");
            float night=lighting?lighting.NightAmount:0;
            EditorGUILayout.LabelField("Night amount",lighting?night.ToString("F2"):"нет StylizedLightingGlobals");
            if(ownedRenderer)
            {
                EditorGUILayout.LabelField(ownedRenderer.AutomaticWorldStatus,EditorStyles.wordWrappedLabel);
                EditorGUILayout.LabelField("Cells ready / pending",(ownedRenderer.ResidentCellCount-ownedRenderer.PendingCellCount)+" / "+ownedRenderer.PendingCellCount);
                EditorGUILayout.LabelField("Submitted / near / draws",ownedRenderer.SubmittedWorldCount+" / "+ownedRenderer.SubmittedNearCount+" / "+ownedRenderer.DrawCalls);
                EditorGUILayout.LabelField("Runtime quality",ownedRenderer.quality.quality.ToString());
                if(night<=transientProfile.minimumNightAmount)EditorGUILayout.HelpBox("Сейчас день или мало ночного света: светлячки затушены общей системой освещения. Дождитесь ночи либо используйте существующий Atmosphere Preview → Blue night; Live clock возвращает обычное освещение.",MessageType.Info);
            }
            if(GUILayout.Button("Открыть Atmosphere Preview"))EditorApplication.ExecuteMenuItem("Little Castle/Rendering/Atmosphere Preview");
            if(GUILayout.Button("Остановить тест / выйти из Play"))EditorApplication.isPlaying=false;
        }
    }
}
