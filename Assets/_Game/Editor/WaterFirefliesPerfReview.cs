using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using LittleCastle.Research;

namespace LittleCastle.Editor
{
    [InitializeOnLoad] public static partial class WaterFirefliesPerfReview
    {
        public const string Root="E:/Games_Develop/Research/WaterFirefliesPerf_20261102";
        public const string BuildTag="P01";
        const string Owner="01a191ef-b3bd-7be0-8e9d-bd5a9894b22c";
        [Serializable] sealed class Lease{public string ownerThreadId,status;}
        [Serializable] sealed class Request{public string id,action,revision;}
        [Serializable] sealed class Result{public string id,status,error;}
        static double next;
        static WaterFirefliesPerfReview(){EditorApplication.update+=Poll;EditorApplication.update+=TickGame;Application.logMessageReceived+=OnGameLog;}
        static bool Owns(){try{var l=JsonUtility.FromJson<Lease>(File.ReadAllText("Library/SharedPcControl.json"));return l.ownerThreadId==Owner&&l.status=="active";}catch{return false;}}
        static void Poll()
        {
            if(EditorApplication.timeSinceStartup<next)return;next=EditorApplication.timeSinceStartup+.5;
            if(!Owns()||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
            string path=Root+"/request.json";if(!File.Exists(path)||DateTime.UtcNow-File.GetLastWriteTimeUtc(path)>TimeSpan.FromMinutes(10))return;
            var r=JsonUtility.FromJson<Request>(File.ReadAllText(path));if(r==null||r.revision!=BuildTag||r.id==SessionState.GetString("LC.WaterFirefliesPerf.Request",""))return;
            var w=Resources.FindObjectsOfTypeAll<MapAuthoringWindow>().FirstOrDefault(v=>v.VerificationSession!=null);if(!w)return;
            SessionState.SetString("LC.WaterFirefliesPerf.Request",r.id);var result=new Result{id=r.id,status="PASS"};
            try
            {
                if(r.action=="baseline")
                {
                    if(w.VerificationSession.HasPending)throw new InvalidOperationException("User stroke active");
                    Directory.CreateDirectory(Root+"/live-before");
                    File.WriteAllText(Root+"/live-before/map.json",JsonUtility.ToJson(w.VerificationSession.ExportMap(),true));File.WriteAllText(Root+"/live-before/save.json",w.VerificationSession.ExportJson());
                    var state=(MapRiverToolState)typeof(MapAuthoringWindow).GetField("riverTools",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(w);
                    File.WriteAllText(Root+"/live-before/draft.json",state.ExportDraftJson());File.WriteAllText(Root+"/live-before/window.json",EditorJsonUtility.ToJson(w,true));
                }
                else if(r.action=="visual")StartVisual(w);
                else if(r.action=="core-audit")CoreAudit(w);
                else if(r.action=="presentation")PresentationAudit(w);
                else if(r.action=="b-ui")BeginBUI(w);
                else if(r.action=="b-ui-end")EndBUI(w);
                else if(r.action=="final-state")FinalState(w);
                else if(r.action=="benchmark")Benchmark(w);
                else if(r.action=="water-ab")WaterColourComparison(w);
                else if(r.action=="upper-join")UpperJoinReview(w);
                else if(r.action=="settings-prepare")
                {
                    Directory.CreateDirectory(Root+"/settings-before");
                    File.WriteAllText(Root+"/settings-before/map.json",JsonUtility.ToJson(w.VerificationSession.ExportMap(),true));
                    var state=(MapRiverToolState)typeof(MapAuthoringWindow).GetField("riverTools",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(w);
                    File.WriteAllText(Root+"/settings-before/draft.json",state.ExportDraftJson());
                    w.RestorePreview();
                    try
                    {
                        if(!UnityEditor.SceneManagement.EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene()))throw new InvalidOperationException("Cannot preserve current scene before tests");
                        SessionState.SetBool("LC.WaterFirefliesPerf.Tests",false);
                    }
                    catch{w.ResumeMapPreview();throw;}
                }
                else if(r.action=="settings-resume")w.ResumeMapPreview();
                else if(r.action=="settings-audit")
                {
                    var painter=UnityEngine.Object.FindAnyObjectByType<MeadowSurfacePainter>();
                    var first=LittleCastle.Editor.MapSystemsWindow.EnsureWeather(painter);
                    if(first!=LittleCastle.Editor.MapSystemsWindow.EnsureWeather(painter))throw new InvalidOperationException("Duplicate weather");
                    var serialized=new SerializedObject(first);
                    if(serialized.FindProperty("mapPainter").objectReferenceValue!=painter||serialized.FindProperty("gameplayCamera").objectReferenceValue!=painter.studyCamera||!serialized.FindProperty("atmosphere").objectReferenceValue)throw new InvalidOperationException("Weather links missing");
                    if(File.ReadAllText(Root+"/settings-before/map.json")!=JsonUtility.ToJson(w.VerificationSession.ExportMap(),true))throw new InvalidOperationException("Map changed");
                    var state=(MapRiverToolState)typeof(MapAuthoringWindow).GetField("riverTools",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(w);
                    if(File.ReadAllText(Root+"/settings-before/draft.json")!=state.ExportDraftJson())throw new InvalidOperationException("Draft changed");
                }
                else if(r.action=="game")StartGame(w);
                else if(r.action=="cold-visual"){StartVisual(w);visualStarted=EditorApplication.timeSinceStartup-44;}
                else if(r.action=="cleanup")
                {EditorApplication.update-=Poll;var type=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("LittleCastle.Tests.WaterFirefliesPerfTestRunner")).FirstOrDefault(t=>t!=null);type?.GetMethod("Disarm").Invoke(null,null);}
                else throw new InvalidOperationException("Unknown review action");
            }
            catch(Exception e){result.status="FAIL";result.error=e.ToString();}
            finally{File.WriteAllText(Root+"/result.json",JsonUtility.ToJson(result,true));if(!visualActive&&!GameActive)Release();}
        }
        static void Release(){if(Owns())File.WriteAllText("Library/SharedPcControl.json","{\"ownerThreadId\":\""+Owner+"\",\"status\":\"released\",\"purpose\":\"Water/firefly checks finished\",\"updatedAtUtc\":\""+DateTime.UtcNow.ToString("o")+"\"}");}
    }
}
