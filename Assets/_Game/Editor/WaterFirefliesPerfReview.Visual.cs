using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using LittleCastle.Rendering;
using LittleCastle.Rendering.Fireflies;
using LittleCastle.Research;
using LittleCastle.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Object=UnityEngine.Object;

namespace LittleCastle.Editor
{
    public static partial class WaterFirefliesPerfReview
    {
        static bool visualActive;
        static GameObject visualRoot,visualCameraRoot;
        static Camera visualCamera;
        static FireflyPreviewProfile visualProfile;
        static FireflyPreviewRenderer visualRenderer;
        static MapAuthoringWindow visualWindow;
        static string visualMapBefore,visualDraftBefore;
        static double visualStarted,nextVisual;
        static int visualFrame;
        static readonly List<string> visualRows=new List<string>();
        static readonly List<string> visualErrors=new List<string>();
        static MapRiverToolState RiverState(MapAuthoringWindow w)=>(MapRiverToolState)typeof(MapAuthoringWindow).GetField("riverTools",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(w);
        static void StartVisual(MapAuthoringWindow w)
        {
            if(visualActive)throw new InvalidOperationException("Visual review already active");
            visualWindow=w;visualMapBefore=JsonUtility.ToJson(w.VerificationSession.ExportMap());visualDraftBefore=RiverState(w).ExportDraftJson();
            Directory.CreateDirectory(Root+"/visual");
            visualProfile=ScriptableObject.CreateInstance<FireflyPreviewProfile>();
            visualProfile.automaticWorld.enabled=true;visualProfile.densityPer100SquareMetres=12;
            visualProfile.nearEnabled=true;visualProfile.coreColor=new Color(1,.88f,.38f);
            visualRoot=new GameObject("Firefly integration review (temporary)"){hideFlags=HideFlags.HideAndDontSave};
            visualRenderer=visualRoot.AddComponent<FireflyPreviewRenderer>();visualRenderer.Connect(visualProfile);
            visualRenderer.ConnectReadOnlyWorld(w.VerificationSession);visualRenderer.CollectDiagnostics=true;
            visualRenderer.quality.adaptiveEnabled=false;
            visualCameraRoot=new GameObject("Firefly review camera"){hideFlags=HideFlags.HideAndDontSave};
            visualCamera=visualCameraRoot.AddComponent<Camera>();
            var painter=Object.FindAnyObjectByType<MeadowSurfacePainter>();
            if(painter&&painter.studyCamera)visualCamera.CopyFrom(painter.studyCamera);
            visualCamera.enabled=false;visualCamera.nearClipPlane=.05f;visualCamera.farClipPlane=350;
            visualCamera.fieldOfView=50;visualCamera.orthographic=false;
            var data=visualCamera.GetUniversalAdditionalCameraData();data.requiresDepthTexture=true;data.requiresColorTexture=true;
            typeof(FireflyPreviewRenderer).GetField("onlyCamera",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(visualRenderer,visualCamera);
            // Effect preview night is independent; no global time/light/exposure changes.
            visualRows.Clear();visualRows.Add("seconds,preset,submitted,near,resident,draws,lodRejected,cpuMs,allocatedBytes,buildSteps,buildMs,pending");
            visualErrors.Clear();Application.logMessageReceived+=OnVisualLog;
            visualFrame=0;visualStarted=EditorApplication.timeSinceStartup;nextVisual=visualStarted;
            visualActive=true;EditorApplication.update+=TickVisual;
        }
        static void OnVisualLog(string message,string stack,LogType type)
        {if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)visualErrors.Add(message+"\n"+stack);}
        static void TickVisual()
        {
            if(!visualActive)return;
            if(!Owns()||EditorApplication.isPlayingOrWillChangePlaymode||!visualWindow){EndVisual("Interrupted");return;}
            double now=EditorApplication.timeSinceStartup;if(now<nextVisual)return;nextVisual=now+.2;
            try
            {
                double time=now-visualStarted;if(time>=66){EndVisual("");return;}
                int stage=Math.Min(2,(int)(time/22));
                visualRenderer.quality.quality=(FireflyPreviewQuality)(stage+1);
                // Low riverbank, forest and strategic orthographic views over the current map.
                Vector3 target=stage==0?new Vector3(30,1,5):stage==1?new Vector3(2,2,4):new Vector3(0,3,15);
                Vector3 eye=stage==0?target+new Vector3(8,2,-11):stage==1?target+new Vector3(12,5,-16):target+new Vector3(40,40,-44);
                eye.x+=Mathf.Sin((float)time*.16f)*1.5f;
                visualCamera.orthographic=stage==2;visualCamera.orthographicSize=44;
                visualCamera.transform.SetPositionAndRotation(eye,Quaternion.LookRotation(target-eye));
                Capture(visualCamera,Root+"/visual/"+visualFrame.ToString("D4")+".png",960,600);
                visualRows.Add(FormattableString.Invariant($"{time:F3},{visualRenderer.quality.quality},{visualRenderer.SubmittedWorldCount},{visualRenderer.SubmittedNearCount},{visualRenderer.ResidentCellCount},{visualRenderer.DrawCalls},{visualRenderer.LodRejected},{visualRenderer.LastRenderCpuMilliseconds:F5},{visualRenderer.LastRenderAllocatedBytes},{visualRenderer.BuildStepsThisFrame},{visualRenderer.BuildMillisecondsThisFrame:F5},{visualRenderer.PendingCellCount}"));
                visualFrame++;
            }
            catch(Exception e){EndVisual(e.ToString());}
        }
        static void EndVisual(string error)
        {
            try
            {
                EditorApplication.update-=TickVisual;Application.logMessageReceived-=OnVisualLog;
                if(visualWindow)
                {
                    if(visualMapBefore!=JsonUtility.ToJson(visualWindow.VerificationSession.ExportMap()))error+="\nMAP CHANGED";
                    if(visualDraftBefore!=RiverState(visualWindow).ExportDraftJson())error+="\nDRAFT CHANGED";
                }
                if(visualErrors.Count>0)error+="\n"+string.Join("\n",visualErrors);
                File.WriteAllLines(Root+"/visual/metrics.csv",visualRows);
                File.WriteAllText(Root+"/visual/status.txt",string.IsNullOrEmpty(error)?"PASS":error);
            }
            finally
            {
                if(visualRoot)Object.DestroyImmediate(visualRoot);if(visualCameraRoot)Object.DestroyImmediate(visualCameraRoot);
                if(visualProfile)Object.DestroyImmediate(visualProfile);visualActive=false;Release();
            }
        }
        static void Capture(Camera camera,string path,int width,int height)
        {
            var rt=RenderTexture.GetTemporary(width,height,24,RenderTextureFormat.ARGB32);var old=RenderTexture.active;Texture2D pixels=null;
            try{CameraCaptureCompatibility.Render(camera,rt);RenderTexture.active=rt;pixels=new Texture2D(width,height,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,width,height),0,0);pixels.Apply();File.WriteAllBytes(path,pixels.EncodeToPNG());}
            finally{RenderTexture.active=old;RenderTexture.ReleaseTemporary(rt);if(pixels)Object.DestroyImmediate(pixels);}
        }
        static void CoreAudit(MapAuthoringWindow w)
        {
            var report=new List<string>();var map=w.VerificationSession.ExportMap();
            foreach(long id in new long[]{810001,810002})
            foreach(string parameter in new[]{"width","depth","speed","position"})
            {
                try
                {
                    var session=new MapEditSession(map);string before=session.ExportJson();var river=session.GetRiver(id);
                    var node=river.nodes[id==810001?river.nodes.Count-1:0];
                    if(parameter=="width")node.width+=.15f;
                    if(parameter=="depth")node.depth+=.1f;
                    if(parameter=="speed")node.speed+=.1f;
                    if(parameter=="position")node.position+=new Vector2(.15f,0);
                    session.ApplyRiverEdit(river);string after=session.ExportJson();
                    if(before==after)throw new InvalidOperationException("No change");
                    session.Undo();if(session.ExportJson()!=before)throw new InvalidOperationException("Undo mismatch");
                    session.Redo();if(session.ExportJson()!=after)throw new InvalidOperationException("Redo mismatch");
                    report.Add(id+" "+parameter+" PASS route="+session.GetWaterfall(810003).route.sections.Count);
                }
                catch(Exception e){report.Add(id+" "+parameter+" FAIL "+e);}
            }
            File.WriteAllLines(Root+"/linked-core-audit.txt",report);
        }
        static void PresentationAudit(MapAuthoringWindow w)
        {
            var lines=new List<string>();
            w.RestorePreview();
            foreach(var grass in Object.FindObjectsByType<MeadowGrassRenderer>())
            {
                lines.Add(grass.name+" data="+(grass.data?grass.data.name:"NULL")+" clumps="+(grass.data?grass.data.authoredClumps:0));
                // Recover only a lost transient reference from this scene's unchanged source.
                // No scene save or authored grass regeneration.
                if(!grass.data&&grass.name.Contains("Meadow"))
                {
                    var path=AssetDatabase.GUIDToAssetPath("4c3b27d6849881e489990774c0f53187");
                    grass.data=AssetDatabase.LoadAssetAtPath<MeadowGrassData>(path);
                    lines.Add("Recovered saved source: "+path);
                }
            }
            typeof(MapAuthoringWindow).GetMethod("BindScenePreview",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(w,null);
            foreach(var grass in Object.FindObjectsByType<MeadowGrassRenderer>())
                lines.Add("After rebind: "+grass.name+" data="+(grass.data?grass.data.name:"NULL")+" clumps="+(grass.data?grass.data.authoredClumps:0));
            long before=GC.GetAllocatedBytesForCurrentThread();var probe=new byte[65536];probe[0]=1;
            long allocated=GC.GetAllocatedBytesForCurrentThread()-before;GC.KeepAlive(probe);
            lines.Add("GC allocation counter positive control (65536 bytes): "+allocated);
            File.WriteAllLines(Root+"/presentation-audit.txt",lines);
        }
        static string bWindowBefore,bMapBefore,bDraftBefore;
        static Vector3 bPivot;static Quaternion bRotation;static float bSize;static bool bOrtho;
        static void BeginBUI(MapAuthoringWindow w)
        {
            if(w.VerificationSession.HasPending||RiverState(w).HasUnappliedDraft(w.VerificationSession))throw new InvalidOperationException("Pending author data");
            bMapBefore=JsonUtility.ToJson(w.VerificationSession.ExportMap());bDraftBefore=RiverState(w).ExportDraftJson();bWindowBefore=EditorJsonUtility.ToJson(w);
            File.WriteAllText(Root+"/b-ui-window-before.json",bWindowBefore);File.WriteAllText(Root+"/b-ui-map-before.json",bMapBefore);
            var view=SceneView.lastActiveSceneView;bPivot=view.pivot;bRotation=view.rotation;bSize=view.size;bOrtho=view.orthographic;
            var flags=BindingFlags.Instance|BindingFlags.NonPublic;
            typeof(MapAuthoringWindow).GetField("riverDirectEditingEnabled",flags).SetValue(w,true);
            typeof(MapAuthoringWindow).GetField("toolCategory",flags).SetValue(w,3);
            typeof(MapAuthoringWindow).GetField("windowScroll",flags).SetValue(w,Vector2.zero);
            RiverState(w).SelectRiver(w.VerificationSession,810101);RiverState(w).node=1;
            view.LookAt(new Vector3(32,.4f,0),Quaternion.Euler(60,0,0),18,false);view.Focus();w.Repaint();view.Repaint();
        }
        static void EndBUI(MapAuthoringWindow w)
        {
            var session=w.VerificationSession;var lines=new List<string>();
            string after=JsonUtility.ToJson(session.ExportMap());
            if(after!=bMapBefore)
            {
                session.Undo();if(JsonUtility.ToJson(session.ExportMap())!=bMapBefore)throw new InvalidOperationException("UI edit not one Undo");
                session.Redo();if(JsonUtility.ToJson(session.ExportMap())!=after)throw new InvalidOperationException("UI Redo mismatch");
                session.Undo();lines.Add("Actual GUI drag -> ONE Undo -> exact Redo -> Undo PASS");
            }
            else lines.Add("GUI toggled/selection only; no committed drag");
            EditorJsonUtility.FromJsonOverwrite(bWindowBefore,w);RiverState(w).RestoreDraftJson(bDraftBefore);
            typeof(MapAuthoringWindow).GetMethod("RestoreWindowSession",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(w,null);
            var view=SceneView.lastActiveSceneView;view.LookAt(bPivot,bRotation,bSize,bOrtho);
            if(JsonUtility.ToJson(session.ExportMap())!=bMapBefore)throw new InvalidOperationException("Map changed during UI restore");
            File.WriteAllLines(Root+"/b-ui-status.txt",lines);w.Repaint();SceneView.RepaintAll();
        }
        static void FinalState(MapAuthoringWindow w)
        {
            typeof(MapAuthoringWindow).GetMethod("BindScenePreview",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(w,null);
            string map=JsonUtility.ToJson(w.VerificationSession.ExportMap(),true),draft=RiverState(w).ExportDraftJson();
            string initialDraft=File.ReadAllText(Root+"/live-before/draft.json");
            // QA selects an existing river. Restore that selection only when the initial
            // author draft was empty and no unapplied edit exists; never reload the map.
            if(map==File.ReadAllText(Root+"/live-before/map.json")&&draft!=initialDraft)
            {
                var initial=new MapRiverToolState();initial.RestoreDraftJson(initialDraft);
                if(initial.draft!=null&&initial.draft.id==0&&initial.draft.nodes.Count==0&&
                   initial.section!=null&&initial.section.points.Count==0&&!RiverState(w).HasUnappliedDraft(w.VerificationSession))
                {
                    RiverState(w).RestoreDraftJson(initialDraft);RiverState(w).Refresh(w.VerificationSession);draft=RiverState(w).ExportDraftJson();
                    typeof(MapAuthoringWindow).GetMethod("CheckpointSession",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(w,null);
                }
            }
            File.WriteAllText(Root+"/map-after.json",map);File.WriteAllText(Root+"/draft-after.json",draft);
            if(map!=File.ReadAllText(Root+"/live-before/map.json"))throw new InvalidOperationException("Current map differs from initial snapshot");
            if(draft!=File.ReadAllText(Root+"/live-before/draft.json"))throw new InvalidOperationException("River draft differs from initial snapshot");
            File.WriteAllText(Root+"/state-invariants.txt","PASS: current map and draft unchanged; previews rebound");
        }
    }
}
