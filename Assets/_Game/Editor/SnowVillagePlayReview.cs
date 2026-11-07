using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using LittleCastle.World;
using LittleCastle.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace LittleCastle.Editor
{
    /// <summary>Explicit local Play check using Unity's start-scene override, restoring the user's scene setup.</summary>
    [InitializeOnLoad]
    static class SnowVillagePlayReview
    {
        const string Owner="01a18b72-7012-7de0-8a5f-9a5a1f5f5265";
        const string Key="LC.SnowVillagePlayReview.20261031.windows";
        const string Request="Library/SnowVillagePlayRequest.json";
        const string Output="E:/Games_Develop/Research/SkyAtmosphere_20261030/window-pass-20261031";
        [Serializable] sealed class Command {public string id,action;}
        [Serializable] sealed class Lease {public string ownerThreadId,status,purpose,updatedAtUtc;}
        [Serializable] sealed class Result
        {
            public string status,error;
            public bool actualPlayMode,localClockWorks,sourceAssetsPreserved,sourceMapPreserved,originalSceneRestored,startSceneRestored;
            public int clocks,frames,errors,dayActiveLights,nightActiveLights;
        }
        static double nextPoll;
        static UrpPreviewQualityOverride quality;
        static bool Running=>SessionState.GetBool(Key+".running",false);
        static Result Read()=>JsonUtility.FromJson<Result>(SessionState.GetString(Key+".result","{}"));
        static void Store(Result value)=>SessionState.SetString(Key+".result",JsonUtility.ToJson(value));
        static SnowVillagePlayReview()
        {
            // Save only the authored sky source. Its Edit preview clone is HideAndDontSave.
            EditorSceneManager.sceneSaving+=SceneSaving;
            EditorSceneManager.sceneSaved+=SceneSaved;
            if(SessionState.GetBool(Key+".disarmed",false))return;
            EditorApplication.update+=Tick; EditorApplication.playModeStateChanged+=State;
            if(Running)Application.logMessageReceived+=Log;
        }
        static void SceneSaving(Scene scene,string path)
        {
            if(path!=SnowVillageSceneBuilder.ScenePath||SceneManager.GetActiveScene()!=scene)return;
            var p=Find(scene); if(!p||!p.atmosphere)return;
            RenderSettings.skybox=new SerializedObject(p.atmosphere).FindProperty("skyboxMaterial").objectReferenceValue as Material;
        }
        static void SceneSaved(Scene scene)
        {
            if(scene.path!=SnowVillageSceneBuilder.ScenePath)return;
            EditorApplication.delayCall+=()=>{if(scene.IsValid()&&scene.isLoaded&&SceneManager.GetActiveScene()==scene)Find(scene)?.RefreshPresentation();};
        }
        static SnowVillagePresentation Find(Scene scene)=>scene.IsValid()&&scene.isLoaded?
            scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<SnowVillagePresentation>(true)).FirstOrDefault():null;
        static bool Owns()
        {
            if(!File.Exists("Library/SharedPcControl.json"))return false;
            var lease=JsonUtility.FromJson<Lease>(File.ReadAllText("Library/SharedPcControl.json"));
            return lease!=null&&lease.ownerThreadId==Owner&&lease.status=="active";
        }
        static void Release()
        {
            if(!Owns())return;
            var lease=JsonUtility.FromJson<Lease>(File.ReadAllText("Library/SharedPcControl.json"));
            lease.status="released";lease.purpose="Snow village Play check ended; original Editor scene and start scene restored.";
            lease.updatedAtUtc=DateTime.UtcNow.ToString("o");File.WriteAllText("Library/SharedPcControl.json",JsonUtility.ToJson(lease,true));
        }
        static void Tick()
        {
            if(Running)
            {
                try
                {
                    if(!Owns())throw new InvalidOperationException("Snow review lease lost.");
                    if(DateTime.UtcNow.Ticks>long.Parse(SessionState.GetString(Key+".deadline","0")))throw new TimeoutException("Snow Play check timed out.");
                    if(!EditorApplication.isPlaying||SessionState.GetBool(Key+".captured",false))return;
                    var result=Read();result.frames++;Store(result);
                    if(result.frames<30||DateTime.UtcNow.Ticks<long.Parse(SessionState.GetString(Key+".settle","0")))return;
                    var scene=SceneManager.GetActiveScene();var p=Find(scene);
                    if(!p)throw new InvalidOperationException("Saved snow scene did not initialize.");
                    result.actualPlayMode=Application.isPlaying;
                    result.clocks=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<WorldTimeSystem>(true)).Count();
                    int phase=SessionState.GetInt(Key+".phase",0);
                    if(phase==0)
                    {
                        p.PresentAtHour(13); result.localClockWorks=Mathf.Abs(p.clock.CurrentHour-13)<.001f;
                        Store(result); SessionState.SetInt(Key+".phase",1);
                        SessionState.SetString(Key+".settle",DateTime.UtcNow.AddSeconds(1).Ticks.ToString());return;
                    }
                    var budget=p.GetComponent<LittleCastle.Rendering.NightLightBudgetManager>();
                    if(phase==1)
                    {
                        result.dayActiveLights=budget?budget.ActiveRealtimeLightCount:-1;
                        Capture(p.sceneCamera,"05-snow-day-play.png");
                        p.PresentAtHour(0); result.localClockWorks&=Mathf.Abs(p.clock.CurrentHour)<.001f;
                        Store(result); SessionState.SetInt(Key+".phase",2);
                        SessionState.SetString(Key+".settle",DateTime.UtcNow.AddSeconds(1).Ticks.ToString());return;
                    }
                    result.nightActiveLights=budget?budget.ActiveRealtimeLightCount:-1;
                    if(result.dayActiveLights!=0||result.nightActiveLights<1||result.nightActiveLights>6)
                        throw new InvalidOperationException("Runtime day/night light budget failed.");
                    Capture(p.sceneCamera);
                    var position=p.sceneCamera.transform.position;
                    var focus=new Vector3(280,4,302);
                    try {p.sceneCamera.transform.position=focus+(position-focus)*1.65f;Capture(p.sceneCamera,"06-snow-night-far-play.png");}
                    finally {p.sceneCamera.transform.position=position;}
                    var logged=Read();result.errors=logged.errors;
                    if(!string.IsNullOrEmpty(logged.error))result.error=logged.error;
                    Store(result);SessionState.SetBool(Key+".captured",true);EditorApplication.ExitPlaymode();
                }
                catch(Exception error)
                {
                    var result=Read();result.error=error.ToString();Store(result);
                    if(EditorApplication.isPlayingOrWillChangePlaymode)EditorApplication.ExitPlaymode();else Finish();
                }
                return;
            }
            if(EditorApplication.timeSinceStartup<nextPoll)return;nextPoll=EditorApplication.timeSinceStartup+1;
            if(EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating||!Owns()||!File.Exists(Request))return;
            var command=JsonUtility.FromJson<Command>(File.ReadAllText(Request));
            if(command==null||string.IsNullOrEmpty(command.id)||command.id==SessionState.GetString(Key+".last",""))return;
            SessionState.SetString(Key+".last",command.id);
            if(command.action=="disarm")
            {
                EditorApplication.update-=Tick;EditorApplication.playModeStateChanged-=State;SessionState.SetBool(Key+".disarmed",true);
                Directory.CreateDirectory(Output);File.WriteAllText(Output+"/play-disarmed.json","{\"disarmed\":true,\"playMode\":false}");Release();return;
            }
            if(command.action!="play")return;
            var original=SceneManager.GetActiveScene();
            SessionState.SetString(Key+".original",original.path);SessionState.SetString(Key+".originalHash",HashFile(original.path));
            SessionState.SetString(Key+".mapHash",MapHash());
            SessionState.SetString(Key+".previousStart",AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
            SessionState.SetString(Key+".deadline",DateTime.UtcNow.AddMinutes(2).Ticks.ToString());
            Store(new Result());SessionState.SetInt(Key+".phase",0);SessionState.SetBool(Key+".captured",false);SessionState.SetBool(Key+".running",true);
            Application.logMessageReceived+=Log;
            EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>(SnowVillageSceneBuilder.ScenePath);
            EditorApplication.EnterPlaymode();
        }
        static void State(PlayModeStateChange state)
        {
            if(!Running)return;
            if(state==PlayModeStateChange.EnteredPlayMode)
            {
                SessionState.SetString(Key+".settle",DateTime.UtcNow.AddSeconds(2).Ticks.ToString());
                quality=UrpPreviewQualityOverride.Create(TerrainStarterPreviewControls.PreviewQuality.High);
                Find(SceneManager.GetActiveScene())?.atmosphere.SetAtmosphereQuality(TerrainStarterPreviewControls.PreviewQuality.High);
            }
            if(state==PlayModeStateChange.ExitingPlayMode) {quality?.Dispose();quality=null;}
            if(state==PlayModeStateChange.EnteredEditMode)Finish();
        }
        static void Log(string message,string stack,LogType type)
        {
            if(!Running||type!=LogType.Error&&type!=LogType.Exception&&type!=LogType.Assert)return;
            var result=Read();result.errors++;result.error+=(result.error==null?"":"\n")+message;Store(result);
        }
        static void Finish()
        {
            try
            {
                Application.logMessageReceived-=Log;
                string previous=SessionState.GetString(Key+".previousStart","");
                EditorSceneManager.playModeStartScene=string.IsNullOrEmpty(previous)?null:AssetDatabase.LoadAssetAtPath<SceneAsset>(previous);
                var result=Read();string original=SessionState.GetString(Key+".original","");
                result.originalSceneRestored=SceneManager.GetActiveScene().path==original&&!EditorApplication.isPlayingOrWillChangePlaymode;
                result.sourceAssetsPreserved=HashFile(original)==SessionState.GetString(Key+".originalHash","");
                result.sourceMapPreserved=MapHash()==SessionState.GetString(Key+".mapHash","");
                result.startSceneRestored=AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene)==previous;
                result.status=string.IsNullOrEmpty(result.error)&&result.errors==0&&result.actualPlayMode&&result.localClockWorks&&
                    result.clocks==1&&result.originalSceneRestored&&result.sourceAssetsPreserved&&result.sourceMapPreserved&&result.startSceneRestored?"PASS":"FAIL";
                Directory.CreateDirectory(Output);File.WriteAllText(Output+"/play-result.json",JsonUtility.ToJson(result,true));
            }
            finally {SessionState.SetBool(Key+".running",false);Release();}
        }
        static void Capture(Camera camera,string filename="04-snow-night-play.png")
        {
            var rt=RenderTexture.GetTemporary(1600,900,24,RenderTextureFormat.ARGB32);var active=RenderTexture.active;var aspect=camera.aspect;Texture2D pixels=null;
            try
            {
                camera.aspect=1600f/900;CameraCaptureCompatibility.Render(camera,rt);CameraCaptureCompatibility.Render(camera,rt);
                RenderTexture.active=rt;pixels=new Texture2D(1600,900,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,1600,900),0,0);pixels.Apply();
                File.WriteAllBytes(Output+"/"+filename,pixels.EncodeToPNG());
            }
            finally {camera.aspect=aspect;RenderTexture.active=active;if(pixels)Object.DestroyImmediate(pixels);RenderTexture.ReleaseTemporary(rt);}
        }
        static string MapHash()=>string.Join("|",Resources.FindObjectsOfTypeAll<MapAuthoringWindow>().Where(w=>w.VerificationSession!=null).Select(w=>Hash(System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(w.VerificationSession.ExportMap())))));
        static string HashFile(string path)=>File.Exists(path)?Hash(File.ReadAllBytes(path)):"absent";
        static string Hash(byte[] data){using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(data)).Replace("-","");}
    }
}
