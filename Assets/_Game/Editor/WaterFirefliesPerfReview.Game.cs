using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using LittleCastle.CameraSystem;
using LittleCastle.Rendering;
using LittleCastle.Rendering.Fireflies;
using LittleCastle.Research;
using LittleCastle.World;
using LittleCastle.World.Environment;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

namespace LittleCastle.Editor
{
    public static partial class WaterFirefliesPerfReview
    {
        const string GameRoot="E:/Games_Develop/Research/WaterFirefliesFinish_20261102/game";
        const string GameKey="LC.WaterFirefliesFinish.Game.";
        static string GameStage{get=>SessionState.GetString(GameKey+"stage","");set=>SessionState.SetString(GameKey+"stage",value);}
        static bool GameActive=>GameStage!="";
        static double nextGameTick;
        static Camera gameCamera;
        static MeadowSurfacePainter gamePainter;
        static FireflyPreviewRenderer gameFireflies;
        static DayNightLightingController gameCycle;
        static FireflyPreviewQuality gameQuality;
        static string gameDepth="unobserved",gameOpaque="unobserved";
        static int gameCaptureNumber;
        static string gameWaiting="not probed";
        static Vector3 gameRiverTarget,gameForestTarget;

        static string GameHash(string path)
        {
            if(string.IsNullOrEmpty(path)||!File.Exists(path))return "missing";
            using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-","").ToLowerInvariant();
        }
        static void OnGameLog(string message,string stack,LogType type)
        {
            if(!GameActive||(type!=LogType.Error&&type!=LogType.Exception&&type!=LogType.Assert))return;
            try{Directory.CreateDirectory(GameRoot);File.AppendAllText(GameRoot+"/errors.txt",DateTime.UtcNow.ToString("o")+" "+message+"\n"+stack+"\n");}catch{}
        }
        static void StartGame(MapAuthoringWindow window)
        {
            if(GameActive||EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Game QA already active or Play is entering.");
            if(window.VerificationSession.HasPending)throw new InvalidOperationException("User stroke active.");
            Directory.CreateDirectory(GameRoot);
            File.WriteAllText(GameRoot+"/status.txt","RUNNING; current-map native Game QA");
            File.WriteAllText(GameRoot+"/errors.txt","");
            File.WriteAllText(GameRoot+"/map-before.json",JsonUtility.ToJson(window.VerificationSession.ExportMap()));
            File.WriteAllText(GameRoot+"/draft-before.json",RiverState(window).ExportDraftJson());
            var source=(SurfacePaintData)typeof(MapAuthoringWindow).GetField("source",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(window);
            string sourcePath=AssetDatabase.GetAssetPath(source),scenePath=SceneManager.GetActiveScene().path;
            SessionState.SetString(GameKey+"sourcePath",sourcePath);SessionState.SetString(GameKey+"sourceHash",GameHash(sourcePath));
            SessionState.SetString(GameKey+"scenePath",scenePath);SessionState.SetString(GameKey+"sceneHash",GameHash(scenePath));
            SessionState.SetFloat(GameKey+"deadline",(float)EditorApplication.timeSinceStartup+60);
            foreach(string key in new[]{"nightVisible","offClean","onReady","pixelProof","proofCaptured"})SessionState.SetBool(GameKey+key,false);
            SessionState.SetInt(GameKey+"maxNear",0);SessionState.SetInt(GameKey+"maxWorld",0);
            GameStage="enter";
            gameWaiting="entering Play / domain reload";
            TraceGame();
            try
            {
                var saved=CurrentMapGameInspection.PrepareDefaultProfile();
                CurrentMapGameInspection.Open(window,source);
                var panel=Resources.FindObjectsOfTypeAll<CurrentMapGameInspection>().First();
                typeof(CurrentMapGameInspection).GetField("profile",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(panel,saved);
                typeof(CurrentMapGameInspection).GetMethod("Begin",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(panel,null);
            }
            catch(Exception e)
            {
                GameStage="";File.WriteAllText(GameRoot+"/status.txt","FAIL before Play: "+e);
                throw;
            }
        }
        static void SetupGame()
        {
            gamePainter=Object.FindAnyObjectByType<MeadowSurfacePainter>();
            if(!gamePainter){gameWaiting="waiting for scene MeadowSurfacePainter";return;}
            if(gamePainter.Session==null){gameWaiting="waiting for runtime Session initialization";return;}
            // The inspection root has DontSave: normal FindAnyObjectByType intentionally excludes it.
            // Resolve the exact owned renderer, not another manual component or a SceneView preview.
            gameFireflies=(FireflyPreviewRenderer)typeof(CurrentMapGameInspection).GetField("ownedRenderer",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
            if(!gameFireflies){gameWaiting="waiting for CurrentMapGameInspection.ownedRenderer; inspection="+SessionState.GetString("LC.CurrentMapGameInspection.status","");return;}
            gameCycle=Object.FindAnyObjectByType<DayNightLightingController>();
            gameWaiting="validating exact current document and actual camera";
            if(!gameCycle||!gameCycle.UnifiedProfile||!gameCycle.TimeSystem)throw new InvalidOperationException("Existing atmosphere profile/authoritative clock missing.");
            gameCamera=gamePainter.studyCamera;
            if(!gameCamera||!gameCamera.GetComponent<StrategyCameraController>())throw new InvalidOperationException("Actual painter study/strategy camera missing.");
            if(gamePainter.RiverView==null)throw new InvalidOperationException("Runtime water presenter missing.");
            string actual=JsonUtility.ToJson(gamePainter.Session.ExportMap());
            File.WriteAllText(GameRoot+"/runtime-map.json",actual);
            if(actual!=File.ReadAllText(GameRoot+"/map-before.json"))throw new InvalidOperationException("Runtime document is not the exact complete current World Editor document.");
            var document=gamePainter.Session.ExportMap();
            Vector2 center=new Vector2(document.bounds.x+document.bounds.z*.5f,document.bounds.y+document.bounds.w*.5f);
            Vector2 river=center;float water=gamePainter.Session.SampleHeight(center.x,center.y);
            var candidates=document.rivers.Where(r=>r.nodes!=null&&r.nodes.Count>0).ToList();
            var firstRiver=candidates.FirstOrDefault(r=>r.id==810101)??candidates.OrderBy(r=>r.nodes.Min(n=>n.waterHeight)).FirstOrDefault();
            if(firstRiver!=null)
            {
                var middle=firstRiver.nodes[firstRiver.nodes.Count/2].position;
                var node=firstRiver.nodes.OrderBy(n=>GameWinterScore(n.position)).ThenBy(n=>(n.position-middle).sqrMagnitude).First();
                river=node.position;water=node.waterHeight;
            }
            gameRiverTarget=new Vector3(river.x,Mathf.Max(water,gamePainter.Session.SampleHeight(river.x,river.y))+.4f,river.y);
            Vector2 forest=document.trees.Count>0?document.trees.OrderBy(t=>GameWinterScore(t.position)).ThenBy(t=>(t.position-river).sqrMagnitude).First().position:center;
            gameForestTarget=new Vector3(forest.x,gamePainter.Session.SampleHeight(forest.x,forest.y)+1.2f,forest.y);
            gameCamera.GetComponent<StrategyCameraController>().enabled=false;
            gameQuality=gameFireflies.quality.quality;gameFireflies.CollectDiagnostics=true;
            RenderPipelineManager.endCameraRendering-=GameCameraRendered;RenderPipelineManager.endCameraRendering+=GameCameraRendered;
            gameCaptureNumber=0;gameDepth=gameOpaque="unobserved";
            File.WriteAllText(GameRoot+"/metrics.csv","seconds,phase,quality,submitted,near,resident,pending,draws,buildSteps,buildMs,renderCpuMs,night,clockHour,postCameraGlobalDepthTexture,postCameraGlobalOpaqueTexture,cameraDepthRequested,cameraOpaqueRequested,unityQuality,cameraX,cameraY,cameraZ\n");
            File.WriteAllText(GameRoot+"/setup.txt","Exact full current document equals runtime ExportMap: PASS\nCamera="+gameCamera.name+"; StrategyCameraController paused only in Play.\nWater presenter=ready; profile="+gameFireflies.Profile.name+"; material="+gameFireflies.Profile.materialForBuild.name+"\nNo camera buffers forced by QA.\n");
            File.AppendAllText(GameRoot+"/setup.txt","End-camera global texture dimensions are observations after camera execution, not proof of attachment availability during opaque rendering.\n"+
                "River target="+gameRiverTarget+"; winter/snow score="+GameWinterScore(river)+"; selected river="+(firstRiver!=null?firstRiver.id.ToString():"none")+"\nForest target="+gameForestTarget+"; winter/snow score="+GameWinterScore(forest)+"\n");
            SessionState.SetFloat(GameKey+"started",(float)EditorApplication.timeSinceStartup);
            GameStage="record";gameWaiting="recording actual Game camera";TraceGame();
        }
        static float GameWinterScore(Vector2 point)=>gamePainter.Session.SampleEnvironmentSnow(point.x,point.y)+
            gamePainter.Session.EnvironmentThemes.Sample(EnvironmentThemeIds.Winter,point.x,point.y);

        [Serializable] sealed class GameHeartbeat
        {
            public string utc,stage,waiting,inspection;
            public bool playing,changingPlayMode,compiling,updating,ownsLease,painter,session,renderer,camera;
            public float editorSeconds,deadline;
        }
        static void TraceGame()
        {
            var heartbeat=new GameHeartbeat{utc=DateTime.UtcNow.ToString("o"),stage=GameStage,waiting=gameWaiting,
                inspection=SessionState.GetString("LC.CurrentMapGameInspection.status",""),
                playing=Application.isPlaying,changingPlayMode=EditorApplication.isPlayingOrWillChangePlaymode,
                compiling=EditorApplication.isCompiling,updating=EditorApplication.isUpdating,ownsLease=Owns(),
                painter=gamePainter,session=gamePainter&&gamePainter.Session!=null,renderer=gameFireflies,camera=gameCamera,
                editorSeconds=(float)EditorApplication.timeSinceStartup,deadline=SessionState.GetFloat(GameKey+"deadline",0)};
            File.WriteAllText(GameRoot+"/heartbeat.json",JsonUtility.ToJson(heartbeat,true));
            if(GameActive)File.WriteAllText(GameRoot+"/status.txt","RUNNING "+GameStage+"; "+gameWaiting+"; "+heartbeat.utc);
        }
        static string TextureSize(Texture texture)=>texture?texture.width+"x"+texture.height:"missing";
        static void GameCameraRendered(ScriptableRenderContext context,Camera camera)
        {
            if(camera!=gameCamera)return;
            gameDepth=TextureSize(Shader.GetGlobalTexture("_CameraDepthTexture"));
            gameOpaque=TextureSize(Shader.GetGlobalTexture("_CameraOpaqueTexture"));
        }
        static Color32[] CaptureGame(Camera camera,string path)
        {
            var rt=RenderTexture.GetTemporary(960,600,24,RenderTextureFormat.ARGB32);
            var originalTarget=camera.targetTexture;var originalActive=RenderTexture.active;Texture2D pixels=null;
            try
            {
                camera.targetTexture=rt;CameraCaptureCompatibility.Render(camera,rt);RenderTexture.active=rt;
                pixels=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);
                pixels.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);pixels.Apply();File.WriteAllBytes(path,pixels.EncodeToPNG());
                return pixels.GetPixels32();
            }
            finally
            {
                camera.targetTexture=originalTarget;RenderTexture.active=originalActive;
                RenderTexture.ReleaseTemporary(rt);if(pixels)Object.DestroyImmediate(pixels);
            }
        }
        static void RecordGame(double seconds)
        {
            string phase=seconds<12?"day-river":seconds<27?"night-river":seconds<30?"night-forest":seconds<33?"off-forest":seconds<39?"on-forest":"night-strategic";
            gameCycle.PreviewAtmosphere(seconds<12?13:0,0,.08f);
            Vector3 target=seconds<27?gameRiverTarget:gameForestTarget;
            Vector3 eye=target+(seconds<27?new Vector3(7,3,-10):seconds<39?new Vector3(6,3,-8):new Vector3(35,40,-40));
            if(seconds<39)eye.y=gamePainter.Session.SampleHeight(eye.x,eye.z)+2.7f;
            gameCamera.transform.SetPositionAndRotation(eye,Quaternion.LookRotation(target-eye));
            gameFireflies.quality.quality=phase=="off-forest"?FireflyPreviewQuality.Off:gameQuality;
            CaptureGame(gameCamera,GameRoot+"/"+gameCaptureNumber.ToString("D3")+"-"+phase+".png");gameCaptureNumber++;
            var lighting=Object.FindAnyObjectByType<StylizedLightingGlobals>();float night=lighting?lighting.NightAmount:0;
            if(seconds>=14&&gameFireflies.SubmittedWorldCount>0)SessionState.SetBool(GameKey+"nightVisible",true);
            if(seconds>=14)
            {
                SessionState.SetInt(GameKey+"maxNear",Math.Max(SessionState.GetInt(GameKey+"maxNear",0),gameFireflies.SubmittedNearCount));
                SessionState.SetInt(GameKey+"maxWorld",Math.Max(SessionState.GetInt(GameKey+"maxWorld",0),gameFireflies.SubmittedWorldCount));
            }
            if(phase=="off-forest")
            {
                bool noMaterial=typeof(FireflyPreviewRenderer).GetField("generatedMaterial",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(gameFireflies)==null;
                bool noQuad=typeof(FireflyPreviewRenderer).GetField("quad",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(gameFireflies)==null;
                if(gameFireflies.ResidentCellCount==0&&gameFireflies.SubmittedWorldCount==0&&noMaterial&&noQuad)SessionState.SetBool(GameKey+"offClean",true);
            }
            if(seconds>=34&&seconds<39&&gameFireflies.AutomaticWorldReady&&gameFireflies.SubmittedWorldCount>0)SessionState.SetBool(GameKey+"onReady",true);
            var data=gameCamera.GetUniversalAdditionalCameraData();var p=gameCamera.transform.position;
            File.AppendAllText(GameRoot+"/metrics.csv",FormattableString.Invariant($"{seconds:F3},{phase},{gameFireflies.quality.quality},{gameFireflies.SubmittedWorldCount},{gameFireflies.SubmittedNearCount},{gameFireflies.ResidentCellCount},{gameFireflies.PendingCellCount},{gameFireflies.DrawCalls},{gameFireflies.BuildStepsThisFrame},{gameFireflies.BuildMillisecondsThisFrame:F5},{gameFireflies.LastRenderCpuMilliseconds:F5},{night:F3},{gameCycle.TimeSystem.CurrentHour:F3},{gameDepth},{gameOpaque},{data.requiresDepthTexture},{data.requiresColorTexture},{QualitySettings.names[QualitySettings.GetQualityLevel()]},{p.x:F3},{p.y:F3},{p.z:F3}\n"));
        }
        static void GlowPixelProof()
        {
            var quality=gameFireflies.quality.quality;
            try
            {
                // Consecutive requests in the same Update: identical camera, atmosphere and frame time.
                // The on image precedes destructive Off cleanup; no shared profile/habitat settings change.
                var on=CaptureGame(gameCamera,GameRoot+"/glow-proof-on.png");
                gameFireflies.quality.quality=FireflyPreviewQuality.Off;
                var off=CaptureGame(gameCamera,GameRoot+"/glow-proof-off.png");
                var difference=new Color32[on.Length];int warmPixels=0;long warmEnergy=0;
                for(int i=0;i<on.Length;i++)
                {
                    int dr=on[i].r-off[i].r,dg=on[i].g-off[i].g,db=on[i].b-off[i].b;
                    bool warm=dr>6&&dg>3&&on[i].r>on[i].b+8&&on[i].g>on[i].b+4&&dr>db*1.2f&&dg>db;
                    if(warm){warmPixels++;warmEnergy+=dr+dg+Math.Max(0,db);}
                    difference[i]=new Color32((byte)Mathf.Clamp(dr*6,0,255),(byte)Mathf.Clamp(dg*6,0,255),(byte)Mathf.Clamp(db*6,0,255),255);
                }
                var pixels=new Texture2D(960,600,TextureFormat.RGB24,false);
                try{pixels.SetPixels32(difference);pixels.Apply();File.WriteAllBytes(GameRoot+"/glow-proof-positive-diff-x6.png",pixels.EncodeToPNG());}
                finally{Object.DestroyImmediate(pixels);}
                SessionState.SetBool(GameKey+"pixelProof",warmPixels>4);
                File.WriteAllText(GameRoot+"/glow-proof.txt","Same actual camera / same Editor Update / night presentation: on then Off.\nWarm positive delta pixels="+warmPixels+"; energy="+warmEnergy+"; threshold >4 pixels.\nDifference is amplified x6 for inspection; water/foliage can still animate between render requests, so the warm filter and saved pair must be visually reviewed.\n");
            }
            finally{gameFireflies.quality.quality=quality;}
        }

        static void ExitGame(string error)
        {
            if(!string.IsNullOrEmpty(error))File.AppendAllText(GameRoot+"/errors.txt",error+"\n");
            try{if(gameCycle)gameCycle.EndAtmospherePreview();}
            finally
            {
                RenderPipelineManager.endCameraRendering-=GameCameraRendered;
                GameStage="exit";SessionState.SetFloat(GameKey+"deadline",(float)EditorApplication.timeSinceStartup+60);
                if(EditorApplication.isPlayingOrWillChangePlaymode)EditorApplication.isPlaying=false;
            }
        }
        static void FinishGame()
        {
            try
            {
                var window=Resources.FindObjectsOfTypeAll<MapAuthoringWindow>().FirstOrDefault(w=>w.VerificationSession!=null);
                if(!window)throw new InvalidOperationException("Original World Editor document unavailable after Play.");
                typeof(MapAuthoringWindow).GetMethod("EnsureScenePreview",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(window,null);
                var editorTerrain=(MapTerrainPresentation)typeof(MapAuthoringWindow).GetField("terrainPreview",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(window);
                bool editorView=editorTerrain!=null&&editorTerrain.IsValid;
                bool map=File.ReadAllText(GameRoot+"/map-before.json")==JsonUtility.ToJson(window.VerificationSession.ExportMap());
                bool draft=File.ReadAllText(GameRoot+"/draft-before.json")==RiverState(window).ExportDraftJson();
                bool source=SessionState.GetString(GameKey+"sourceHash","")==GameHash(SessionState.GetString(GameKey+"sourcePath",""));
                bool scene=SessionState.GetString(GameKey+"sceneHash","")==GameHash(SessionState.GetString(GameKey+"scenePath",""));
                bool night=SessionState.GetBool(GameKey+"nightVisible",false),off=SessionState.GetBool(GameKey+"offClean",false),on=SessionState.GetBool(GameKey+"onReady",false);
                string errors=File.ReadAllText(GameRoot+"/errors.txt");
                bool pixels=SessionState.GetBool(GameKey+"pixelProof",false);
                string details=$"Exact current map preserved={map}; draft preserved={draft}; source hash unchanged={source}; scene hash unchanged={scene}; editor view restored={editorView}; night submitted={night}; Off resources clean={off}; on reconnected={on}; warm glow pixel delta={pixels}; maxNear={SessionState.GetInt(GameKey+"maxNear",0)}; maxWorld={SessionState.GetInt(GameKey+"maxWorld",0)}; scoped errors={errors.Length}";
                File.WriteAllText(GameRoot+"/status.txt",(map&&draft&&source&&scene&&editorView&&night&&off&&on&&pixels&&errors.Length==0?"PASS":"FAIL")+"\n"+details+"\n"+errors);
            }
            catch(Exception e){File.WriteAllText(GameRoot+"/status.txt","FAIL final verification: "+e);}
            finally{GameStage="";gameCamera=null;gamePainter=null;gameFireflies=null;gameCycle=null;Release();}
        }
        static void TickGame()
        {
            if(!GameActive)return;
            double now=EditorApplication.timeSinceStartup;if(now<nextGameTick)return;nextGameTick=now+.75;
            try
            {
                TraceGame();
                if(EditorApplication.isCompiling||(!Application.isPlaying&&EditorApplication.isUpdating))return;
                if(GameStage=="exit")
                {
                    if(!EditorApplication.isPlayingOrWillChangePlaymode){FinishGame();return;}
                    if(now>SessionState.GetFloat(GameKey+"deadline",0))throw new TimeoutException("Play exit timeout.");
                    return;
                }
                if(!Owns()){ExitGame("Shared PC ownership lost; Game QA interrupted.");return;}
                if(GameStage=="enter")
                {
                    if(Application.isPlaying)SetupGame();
                    if(GameStage=="enter"&&now>SessionState.GetFloat(GameKey+"deadline",0))ExitGame("Current-map Game/bootstrap readiness timeout: "+gameWaiting);
                    else if(GameStage=="enter")TraceGame();
                    return;
                }
                if(!Application.isPlaying){ExitGame("Play ended before 45-second QA finished.");return;}
                if(!gameCamera||!gameFireflies||!gamePainter||!gameCycle){ExitGame("Game QA references lost; restart required.");return;}
                double seconds=now-SessionState.GetFloat(GameKey+"started",0);
                if(seconds>=45){ExitGame("");return;}
                RecordGame(seconds);
                if(seconds>=37&&seconds<39&&!SessionState.GetBool(GameKey+"proofCaptured",false))
                {GlowPixelProof();SessionState.SetBool(GameKey+"proofCaptured",true);}
            }
            catch(Exception e)
            {
                if(GameStage=="exit"&&!EditorApplication.isPlayingOrWillChangePlaymode){try{File.AppendAllText(GameRoot+"/errors.txt",e+"\n");}finally{FinishGame();}}
                else ExitGame(e.ToString());
            }
        }
    }
}
