using System;
using System.IO;
using System.Linq;
using LittleCastle.Rendering;
using LittleCastle.Research;
using LittleCastle.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Object=UnityEngine.Object;

namespace LittleCastle.Editor
{
    public static partial class WaterRecoveryReview
    {
        static string PlayStage{get=>SessionState.GetString("LC.WaterRecovery.CurrentPlay","");set=>SessionState.SetString("LC.WaterRecovery.CurrentPlay",value);}
        static double nextPlayPoll;
        static void CollectPlayError(string message,string stack,LogType type)
        {if(PlayStage!=""&&(type==LogType.Error||type==LogType.Exception))File.AppendAllText(Root+"/play-current/errors.txt",message+"\n"+stack+"\n");}
        static void StartCurrentPlay()
        {
            if(PlayStage!="")throw new InvalidOperationException("Current-map Play review already active.");
            var w=Window();if(w.VerificationSession.HasPending)throw new InvalidOperationException("User stroke active.");
            Directory.CreateDirectory(Root+"/play-current");File.WriteAllText(Root+"/play-current/errors.txt","");
            File.WriteAllText(Root+"/play-current/original-map.json",JsonUtility.ToJson(w.VerificationSession.ExportMap()));
            File.WriteAllText(Root+"/play-current/original-draft.json",RiverState(w).ExportDraftJson());
            PlayStage="enter";Application.logMessageReceived+=CollectPlayError;File.WriteAllText(Root+"/play-current/status.txt","RUNNING");EditorApplication.isPlaying=true;
        }
        static void PollCurrentPlay()
        {
            if(PlayStage==""||!Owns()||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.timeSinceStartup<nextPlayPoll)return;
            nextPlayPoll=EditorApplication.timeSinceStartup+.2;
            try
            {
                if(PlayStage=="enter")
                {
                    if(!EditorApplication.isPlaying)return;
                    PlayStage="load";SessionState.SetFloat("LC.WaterRecovery.CurrentPlayDeadline",(float)EditorApplication.timeSinceStartup+1);return;
                }
                if(PlayStage=="load")
                {
                    if(EditorApplication.timeSinceStartup<SessionState.GetFloat("LC.WaterRecovery.CurrentPlayDeadline",0))return;
                    var painter=Object.FindAnyObjectByType<MeadowSurfacePainter>();if(!painter||painter.Session==null)return;
                    string candidate=File.ReadAllText(Root+"/trial/candidate-save.json");
                    var header=JsonUtility.FromJson<MapEditSave>(candidate);var baseline=painter.Session.ExportSave();
                    if(header.baseFingerprint!=baseline.baseFingerprint)
                    {
                        // A window checkpoint rebases its full document. The reviewed stage-A
                        // transport retains the real source base. Reuse it ONLY after proving
                        // the whole resulting document still equals the latest live candidate.
                        candidate=File.ReadAllText("E:/Games_Develop/Research/SoftRiverA_20261031/trial/candidate-save.json");
                        var check=new MapEditSession(painter.Session.ExportAuthored());check.LoadJson(candidate);
                        var expected=JsonUtility.FromJson<MapAuthoringDocument>(File.ReadAllText(Root+"/trial/candidate-map.json"));
                        if(JsonUtility.ToJson(check.ExportMap())!=JsonUtility.ToJson(expected))throw new InvalidOperationException("Source-compatible transport does not match the latest complete candidate; do not restore an older map.");
                        File.WriteAllText(Root+"/play-current/base-check.txt","Window checkpoint base "+header.baseFingerprint+"; runtime source base "+baseline.baseFingerprint+". Native validated transport produces the EXACT latest full document. No source writes.");
                    }
                    painter.Session.LoadJson(candidate);File.WriteAllText(Root+"/play-current/runtime-compatible-save.json",candidate);
                    PlayStage="render";SessionState.SetFloat("LC.WaterRecovery.CurrentPlayDeadline",(float)EditorApplication.timeSinceStartup+1);return;
                }
                if(PlayStage=="render")
                {
                    if(EditorApplication.timeSinceStartup<SessionState.GetFloat("LC.WaterRecovery.CurrentPlayDeadline",0))return;
                    var painter=Object.FindAnyObjectByType<MeadowSurfacePainter>();var s=painter.Session;
                    if(s.GetRiver(810001).layoutVersion!=3||s.GetRiver(810101).id!=810101||s.GetWaterfall(810003).route.IsEmpty)throw new InvalidOperationException("Runtime candidate records missing.");
                    var root=new GameObject("Current map Play verification camera"){hideFlags=HideFlags.HideAndDontSave};
                    try
                    {
                        var camera=root.AddComponent<Camera>();if(painter.studyCamera)camera.CopyFrom(painter.studyCamera);camera.enabled=false;camera.orthographic=false;camera.fieldOfView=48;camera.nearClipPlane=.05f;
                        var data=camera.GetUniversalAdditionalCameraData();data.requiresDepthTexture=true;data.requiresColorTexture=true;
                        var lower=s.RiverNetwork.Endpoint(810002,true);var join=new Vector3(lower.position.x,lower.waterHeight,lower.position.y);
                        var eye=join+new Vector3(4,1.7f,-4);camera.transform.SetPositionAndRotation(eye,Quaternion.LookRotation(join+new Vector3(-.8f,.15f,0)-eye));
                        Capture(camera,Root+"/play-current/landing-close.png");
                        var center=s.GetWaterfall(810003).route.Bounds.center;eye=center+new Vector3(21,14,-21);
                        camera.transform.SetPositionAndRotation(eye,Quaternion.LookRotation(center-eye));Capture(camera,Root+"/play-current/overview.png");
                        var systems=Resources.FindObjectsOfTypeAll<ParticleSystem>().Where(p=>p.name.StartsWith("Bounded spray at route row ")).ToArray();
                        if(systems.Sum(p=>p.particleCount)==0)throw new InvalidOperationException("Runtime spray did not advance.");
                        File.WriteAllText(Root+"/play-current/runtime.txt","Actual Play session loaded current v10 candidate: IDs/profile/route PASS. Runtime spray particles="+systems.Sum(p=>p.particleCount)+". Temporary capture camera only; source assets untouched.");
                    }
                    finally{Object.Destroy(root);}
                    PlayStage="exit";EditorApplication.isPlaying=false;return;
                }
                if(PlayStage=="exit")
                {
                    if(EditorApplication.isPlayingOrWillChangePlaymode)return;
                    var w=Resources.FindObjectsOfTypeAll<MapAuthoringWindow>().FirstOrDefault(v=>v.VerificationSession!=null);if(!w)return;
                    bool same=File.ReadAllText(Root+"/play-current/original-map.json")==JsonUtility.ToJson(w.VerificationSession.ExportMap())&&File.ReadAllText(Root+"/play-current/original-draft.json")==RiverState(w).ExportDraftJson();
                    if(!same)throw new InvalidOperationException("Current map/draft changed after actual Play review.");
                    string errors=File.ReadAllText(Root+"/play-current/errors.txt");
                    File.WriteAllText(Root+"/play-current/status.txt",errors.Length==0?"PASS; Edit Mode restored, exact map/draft match":"FAIL; inspect errors.txt");
                    PlayStage="";Application.logMessageReceived-=CollectPlayError;Release();
                }
            }
            catch(Exception e)
            {
                File.AppendAllText(Root+"/play-current/errors.txt",e+"\n");
                if(EditorApplication.isPlayingOrWillChangePlaymode){PlayStage="exit";EditorApplication.isPlaying=false;}
                else{File.WriteAllText(Root+"/play-current/status.txt","FAIL; "+e.Message);PlayStage="";Application.logMessageReceived-=CollectPlayError;Release();}
            }
        }
    }
}
