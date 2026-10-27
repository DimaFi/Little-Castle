using System;
using System.IO;
using System.Linq;
using LittleCastle.Acceptance;
using LittleCastle.CameraSystem;
using LittleCastle.Rendering;
using LittleCastle.Research;
using LittleCastle.World;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object=UnityEngine.Object;

namespace LittleCastle.Editor
{
    public static class WorldEditorAcceptanceBuild
    {
        public const string Scene="Assets/_Game/Scenes/WorldEditor_20261027.unity";
        const string Source="Assets/_Game/Scenes/LightingMeadow_20261025.unity";
        const string Root="Assets/_Game/Settings/Rendering/LightingMeadow";
        static T Need<T>(string path)where T:Object
        {var value=AssetDatabase.LoadAssetAtPath<T>(path);if(!value)throw new FileNotFoundException(path);return value;}
        [MenuItem("Little Castle/World Editor/Create Integrated Scene 20261027")]
        public static void Setup()
        {
            // Never invoke old LightingMeadowBuild/SpatialIndirectStudyBuild.Setup.
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play first.");
            if(!string.Equals(Path.GetFileName(Path.GetDirectoryName(Application.dataPath)),"Little-Castle",StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Integrated scene setup is restricted to MAIN Little-Castle.");
            if(!MapTreeCatalog.Default||!MapRockCatalog.Default)
                throw new FileNotFoundException("Required Resources/MapAuthoring/MeadowTreeCatalog and MeadowRockCatalog must be integrated first.");
            if(File.Exists(Scene))throw new IOException("Owned scene already exists; refusing to overwrite authored changes: "+Scene);
            if(!AssetDatabase.CopyAsset(Source,Scene))throw new IOException("Cannot copy lighting baseline scene.");
            var scene=EditorSceneManager.OpenScene(Scene,OpenSceneMode.Single);
            var review=Object.FindAnyObjectByType<LightingMeadowReview>();
            if(!review)throw new InvalidOperationException("Lighting baseline references missing.");
            var camera=review.reviewCamera;var clock=review.clock;
            if(!camera||!clock||!review.painter||!review.atmosphere||!review.globals||!review.grass||!review.grass.data||!review.grass.material||!review.interaction||!review.demonstrator)
                throw new InvalidOperationException("Baseline fixture references incomplete.");
            var painter=review.painter;
            if(!painter.source||!painter.brushMaterial||!painter.terrainMaterial||!painter.grassMaterial||!painter.accentMaterial)
                throw new InvalidOperationException("Map painter source/material references incomplete.");
            var hdr=Need<UniversalRenderPipelineAsset>(Root+"/HDR.asset");
            var ao=Need<UniversalRenderPipelineAsset>(Root+"/AO.asset");
            // The verified Meadow baseline was authored and accepted in Linear.
            PlayerSettings.colorSpace=ColorSpace.Linear;
            review.atmosphere.Palette=Need<LightingPaletteProfile>(Root+"/MeadowPalette.asset");
            clock.RunAutomatically=true;clock.SimulationSpeedMultiplier=1;
            // Existing diagnostics stay intact. No deletion or material edits.
            foreach(var b in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            {
                if(b is LightingMeadowReview||b is SpatialIndirectStudyReview||b is SpatialIndirectGameViewStudy)b.enabled=false;
                if(b is SpatialIndirectVolume field){field.applyIndirect=false;field.enabled=false;}
            }
            // SpatialIndirectVolume allocates in Awake even when disabled. A diagnostic field
            // must be hosted in the separate spatial scene, not in this authoring scene.
            if(Object.FindObjectsByType<SpatialIndirectVolume>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length>0)
                throw new InvalidOperationException("Spatial field found in authoring baseline; keep it in separate diagnostic scene.");
            var rig=camera.GetComponent<StrategyCameraController>();if(!rig)rig=camera.gameObject.AddComponent<StrategyCameraController>();
            rig.enabled=true;camera.orthographic=false;camera.allowHDR=true;camera.GetUniversalAdditionalCameraData().renderPostProcessing=true;
            var ui=Object.FindAnyObjectByType<MeadowPreview>();if(!ui)ui=camera.gameObject.AddComponent<MeadowPreview>();
            ui.profile=ao;ui.studyCamera=camera;ui.sun=review.atmosphere.Sun;
            ui.globals=review.globals;ui.grass=review.grass;ui.interaction=review.interaction;ui.demonstrator=review.demonstrator;ui.painter=review.painter;ui.baselineGrass=review.grass.data;
            // Optional small production opt-in implemented by integration owner.
            var uiData=new SerializedObject(ui);var authoring=uiData.FindProperty("worldEditorMode");
            if(authoring==null)throw new InvalidOperationException("MeadowPreview needs serialized worldEditorMode opt-in: preserve UI, skip fixed SetView/camera controls and benchmark automation; use strategy camera. See harness README.");
            authoring.boolValue=true;uiData.ApplyModifiedPropertiesWithoutUndo();ui.enabled=true;
            review.painter.studyCamera=camera;review.painter.enabled=true;review.painter.allowedTools=MapEditAccess.All;review.painter.playerMode=false;review.painter.painting=false;
            var driver=camera.gameObject.AddComponent<WorldEditorAcceptance>();
            driver.camera=camera;driver.clock=clock;driver.atmosphere=review.atmosphere;driver.globals=review.globals;driver.painter=review.painter;driver.hdr=hdr;driver.ao=ao;
            AddDiagnostics(painter);
            // Disabled MonoBehaviours still run Awake. This diagnostic driver freezes
            // the clock/camera and must be absent from the authoring scene.
            Object.DestroyImmediate(review);
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("WORLD_EDITOR_SCENE_READY "+Scene+"; clock automatic; spatial GI absent; diagnostic geometry preserved.");
        }
        public static void FinalizeScene()
        {
            var scene=EditorSceneManager.OpenScene(Scene,OpenSceneMode.Single);
            foreach(var driver in Object.FindObjectsByType<LightingMeadowReview>(FindObjectsInactive.Include))Object.DestroyImmediate(driver);
            var clock=Object.FindAnyObjectByType<WorldTimeSystem>();clock.RunAutomatically=true;clock.SimulationSpeedMultiplier=1;
            UrpQualityProfileSetup.Run();
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        }
        [MenuItem("Little Castle/World Editor/Open Integrated Scene")]
        public static void OpenScene()
        {
            if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
            EditorSceneManager.OpenScene(Scene);EditorApplication.ExecuteMenuItem("Little Castle/Map/World Editor");
        }
        static void AddDiagnostics(MeadowSurfacePainter painter)
        {
            var group=new GameObject("Lighting diagnostics - cube and walls");
            var material=Need<Material>("Assets/_Game/Settings/Rendering/SpatialIndirectStudy/Neutral matte.mat");
            var origin=new Vector3(-4,0,5);
            foreach(var item in new[]{
                ("Diagnostic cube",new Vector3(0,1,0),new Vector3(2,2,2)),
                ("Diagnostic wall X",new Vector3(2.5f,1.5f,2.5f),new Vector3(5,3,.18f)),
                ("Diagnostic wall Z",new Vector3(5,1.5f,0),new Vector3(.18f,3,5))})
            {
                var obj=GameObject.CreatePrimitive(PrimitiveType.Cube);obj.name=item.Item1;obj.transform.SetParent(group.transform);
                var p=origin+item.Item2;p.y+=MeadowPreview.SampleHeight(p.x,p.z);obj.transform.position=p;obj.transform.localScale=item.Item3;
                obj.GetComponent<Renderer>().sharedMaterial=material;
            }
        }
        // Call separately after inspecting the authored scene. Does not regenerate scenes.
        [MenuItem("Little Castle/World Editor/Build verification Player")]
        public static void Build()
        {
            string[] args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"-weBuildOutput");
            if(Application.isBatchMode&&(at<0||at+1>=args.Length))throw new ArgumentException("-weBuildOutput absolute-player.exe required.");
            string path=Path.GetFullPath(at>=0&&at+1<args.Length?args[at+1]:Application.dataPath+"/../../Research/WorldIntegration_20261027/RestoreCheckPlayer/WorldEditor.exe");Directory.CreateDirectory(Path.GetDirectoryName(path));Need<SceneAsset>(Scene);
            bool oldTiming=PlayerSettings.enableFrameTimingStats;
            // BuildPipeline may unload assets. Retained Unity object references can
            // become null after the build, so restore persistent assets by path.
            string oldDefault=AssetDatabase.GetAssetPath(GraphicsSettings.defaultRenderPipeline);int oldQuality=QualitySettings.GetQualityLevel();
            var old=Enumerable.Range(0,QualitySettings.names.Length).Select(i=>AssetDatabase.GetAssetPath(QualitySettings.GetRenderPipelineAssetAt(i))).ToArray();
            string[] settings={"ProjectSettings/ProjectSettings.asset","ProjectSettings/GraphicsSettings.asset","ProjectSettings/QualitySettings.asset"};
            var before=settings.ToDictionary(p=>p,File.ReadAllBytes);
            try
            {
                var hdr=Need<UniversalRenderPipelineAsset>(Root+"/HDR.asset");var ao=Need<UniversalRenderPipelineAsset>(Root+"/AO.asset");
                PlayerSettings.enableFrameTimingStats=true;GraphicsSettings.defaultRenderPipeline=ao;
                // Both profiles participate in shader variant collection; no asset mutation.
                for(int i=0;i<old.Length;i++){QualitySettings.SetQualityLevel(i,false);QualitySettings.renderPipeline=i==0?hdr:ao;}
                QualitySettings.SetQualityLevel(oldQuality,false);
                var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{Scene},locationPathName=path,target=BuildTarget.StandaloneWindows64,options=BuildOptions.None});
                File.WriteAllText(Path.Combine(Path.GetDirectoryName(path),"world-editor-build.txt"),report.summary.result+"\nErrors="+report.summary.totalErrors+"; warnings="+report.summary.totalWarnings+"; colorSpace="+PlayerSettings.colorSpace);
                if(report.summary.result!=BuildResult.Succeeded)throw new Exception("World editor build failed.");
            }
            finally
            {
                PlayerSettings.enableFrameTimingStats=oldTiming;GraphicsSettings.defaultRenderPipeline=AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(oldDefault);
                for(int i=0;i<old.Length;i++){QualitySettings.SetQualityLevel(i,false);QualitySettings.renderPipeline=AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(old[i]);}
                QualitySettings.SetQualityLevel(oldQuality,false);
                AssetDatabase.SaveAssets();
                foreach(var p in settings)File.WriteAllBytes(p,before[p]);
                File.WriteAllText(Path.Combine(Path.GetDirectoryName(path),"build-restores-project-settings.txt"),
                    "Restored default="+oldDefault+"\n"+string.Join("\n",old)+"\n"+
                    string.Join("\n",settings.Select(p=>p+" bytes restored="+File.ReadAllBytes(p).SequenceEqual(before[p]))));
            }
        }
    }
}
