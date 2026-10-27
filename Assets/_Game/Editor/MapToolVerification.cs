using System;
using System.IO;
using System.Text;
using LittleCastle.Rendering;
using LittleCastle.Research;
using LittleCastle.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LittleCastle.Editor
{
    /// <summary>Uses the existing local Meadow assets. Never rebuilds their source art or saves the scene.</summary>
    public static class MapToolVerification
    {
        static readonly string Output=Path.GetFullPath(Application.dataPath+"/../../Research/BrushFoundation_20261027");
        [MenuItem("Little Castle/Map/Verification/Open unified tools acceptance scene")]
        public static void Open()
        {
            Directory.CreateDirectory(Output);EditorSceneManager.OpenScene(MeadowStudyBuilder.ScenePath);
            var painter=UnityEngine.Object.FindAnyObjectByType<MeadowSurfacePainter>();
            if(!painter)throw new InvalidOperationException("Local Meadow fixture is unavailable; sync the art library first.");
            var globals=UnityEngine.Object.FindAnyObjectByType<StylizedLightingGlobals>();if(globals)globals.ApplyGlobals();
            Preflight();
            var window=EditorWindow.GetWindow<MapAuthoringWindow>("Карта · инструменты");window.position=new Rect(20,80,410,850);window.Connect(painter.source);window.SetTool("surface.road");
            var view=EditorWindow.GetWindow<SceneView>();view.Show();
            var b=painter.source.bounds;view.LookAt(new Vector3(b.x+b.z*.5f,0,b.y+b.w*.5f),Quaternion.Euler(65,0,0),90,false,true);
            window.VerificationSession.Changed+=change=>
            {
                File.AppendAllText(Path.Combine(Output,"sceneview-actions.txt"),DateTime.UtcNow.ToString("O")+" "+change.kind+" preview="+change.preview+" bounds="+change.bounds+"\n");
            };
            File.WriteAllText(Path.Combine(Output,"sceneview-ready.txt"),"Existing Meadow scene + MapAuthoringWindow, shared tools active. Scene not saved.\n");
        }
        public static void BuildPlayer()=>Build();
        public static void VerifySceneEvents()
        {
            Open();var window=EditorWindow.GetWindow<MapAuthoringWindow>();var view=SceneView.lastActiveSceneView;
            int frames=0,scenario=0,phase=0;Vector2 a=default,b=default;bool coordinatesReady=false;
            var report=new StringBuilder();Action<SceneView> project=null;
            EditorApplication.CallbackFunction run=null;run=()=>
            {
                if(++frames<10)return;frames=0;
                try
                {
                    var s=window.VerificationSession;
                    float size=scenario==0?12:scenario==1?90:scenario==2?1200:90;
                    if(phase==0)
                    {
                        view.LookAt(new Vector3(0,0,12),Quaternion.Euler(70,0,0),size,false,true);
                        window.SetTool(scenario<3?"surface.road":"terrain.raise");window.VerifySettings(scenario<3?3:8,scenario<3?.8f:1,scenario<3?.6f:1);
                        coordinatesReady=false;phase=1;view.Repaint();return;
                    }
                    if(phase==1)
                    {
                        project=v=>
                        {
                            if(v!=view||Event.current.type!=EventType.Repaint)return;
                            a=HandleUtility.WorldToGUIPoint(new Vector3(-5,s.SampleHeight(-5,10),10));
                            b=HandleUtility.WorldToGUIPoint(new Vector3(5,s.SampleHeight(5,10),10));
                            coordinatesReady=true;SceneView.duringSceneGui-=project;
                        };
                        SceneView.duringSceneGui+=project;phase=2;view.Repaint();return;
                    }
                    if(!coordinatesReady)return;
                    SendStroke(view,a,b);window.CompleteVerificationStroke();
                    if(scenario<3)
                    {
                        var strokes=s.ExportEdits().strokes;var last=strokes[strokes.Count-1];
                        if(last.points.Count<2||Mathf.Abs(last.radius-3)>.001f)throw new Exception("SceneView drag failed at size "+size);
                        report.AppendLine("PASS SceneView MouseDown/Drag/Up at size="+size+"; points="+last.points.Count+"; radius="+last.radius+" world metres");
                        s.Undo();s.Redo();scenario++;phase=0;return;
                    }
                    if(s.EditedHeightSamples==0)throw new Exception("Height adapter did not run.");
                    report.AppendLine("PASS same SceneView adapter applies height brush; changed nodes="+s.EditedHeightSamples);
                    string before=s.ExportJson();s.Undo();s.Redo();if(s.ExportJson()!=before)throw new Exception("Undo/Redo differs.");
                    report.AppendLine("PASS SceneView height Undo/Redo exact save replay");
                    var map=s.ExportMap();var reopened=new MapEditSession(JsonUtility.FromJson<MapAuthoringDocument>(JsonUtility.ToJson(map)));
                    if(reopened.EditedHeightSamples!=0||Mathf.Abs(reopened.SampleHeight(0,12)-s.SampleHeight(0,12))>.0001f)throw new Exception("Author document reopen differs.");
                    report.AppendLine("PASS author JSON reopen preserves baked height and strokes");
                    EditorApplication.update-=run;File.WriteAllText(Path.Combine(Output,"sceneview-automated.txt"),report.ToString());EditorApplication.Exit(0);
                }
                catch(Exception e){EditorApplication.update-=run;if(project!=null)SceneView.duringSceneGui-=project;File.WriteAllText(Path.Combine(Output,"sceneview-automated.txt"),report+"FAIL "+e);Debug.LogException(e);EditorApplication.Exit(2);}
            };
            EditorApplication.update+=run;
        }
        static void SendStroke(SceneView view,Vector2 pa,Vector2 pb)
        {
            // Real GUI-context projection accounts for editor toolbars and display scaling.
            view.SendEvent(new Event{type=EventType.MouseDown,button=0,mousePosition=pa});
            view.SendEvent(new Event{type=EventType.MouseDrag,button=0,mousePosition=pb});
            view.SendEvent(new Event{type=EventType.MouseUp,button=0,mousePosition=pb});
        }
        static void Build()
        {
            Preflight();var report=UnityEditor.BuildPipeline.BuildPlayer(new UnityEditor.BuildPlayerOptions{
                scenes=new[]{MeadowStudyBuilder.ScenePath},locationPathName=Path.Combine(Output,"Player/Meadow_UnifiedTools.exe"),
                target=BuildTarget.StandaloneWindows64,options=BuildOptions.None});
            File.WriteAllText(Path.Combine(Output,"build-result.txt"),$"{report.summary.result}; errors={report.summary.totalErrors}; warnings={report.summary.totalWarnings}\n");
            EditorApplication.Exit(report.summary.result==UnityEditor.Build.Reporting.BuildResult.Succeeded?0:2);
        }
        static void Preflight()
        {
            Directory.CreateDirectory(Output);var log=new StringBuilder();
            Application.LogCallback collect=(message,stack,type)=>{if(message.Contains("[Little Castle Foliage]"))log.AppendLine(type+": "+message);};
            Application.logMessageReceived+=collect;
            try
            {
                var native=AssetDatabase.LoadAssetAtPath<MeadowGrassData>(MeadowStudyBuilder.Root+"/Meadow_InstanceData.asset");
                var material=AssetDatabase.LoadAssetAtPath<Material>(MeadowStudyBuilder.Root+"/Meadow_Blades.mat");
                if(native&&material)
                {
                    var fixture=new GameObject("Existing native grass preflight");
                    try{fixture.AddComponent<MeshFilter>().sharedMesh=native.lodMeshes[0];fixture.AddComponent<MeshRenderer>().sharedMaterial=material;Selection.activeGameObject=fixture;FoliageModelCompatibilityValidator.ValidateSelected();}
                    finally{UnityEngine.Object.DestroyImmediate(fixture);}
                }
                var catalog=MapTreeCatalog.Default;
                if(catalog)foreach(var entry in catalog.entries)
                {
                    if(!entry.prefab)continue;var tree=UnityEngine.Object.Instantiate(entry.prefab);
                    try{Selection.activeGameObject=tree;FoliageModelCompatibilityValidator.ValidateSelected();}
                    finally{UnityEngine.Object.DestroyImmediate(tree);}
                }
            }
            finally{Application.logMessageReceived-=collect;File.WriteAllText(Path.Combine(Output,"foliage-preflight.txt"),log.ToString());}
        }
    }
}
