using System;
using System.IO;
using LittleCastle.CameraSystem;
using LittleCastle.Rendering;
using LittleCastle.Research;
using LittleCastle.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

namespace LittleCastle.Editor
{
    /// <summary>Explicit, temporary comparison in the loaded map and its real Play lighting.
    /// The authoring window checkpoint and all source assets remain the user's document.</summary>
    [InitializeOnLoad] public static class FencePlayReview
    {
        const string Key="LC.Fence.PlayReview";
        static string Output=>FenceAuthoringVerification.Output;
        static MapFencePresentation view;
        static MapEditSession session;
        static SurfacePaintData transientSource;
        static MeadowSurfacePainter painter;
        static Camera camera;
        static int stage;
        static double next;
        static bool injected;
        static Vector3 focus;
        static float framing;
        static FencePlayReview()
        {
            EditorApplication.playModeStateChanged+=State;
            EditorApplication.update+=Tick;
        }
        public static void Begin()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play before fence review.");
            var windows=Resources.FindObjectsOfTypeAll<MapAuthoringWindow>();MapAuthoringDocument document=null;
            foreach(var window in windows)if(window.VerificationSession!=null){document=window.VerificationSession.ExportMap();break;}
            if(document==null)
            {
                var source=Object.FindAnyObjectByType<MeadowSurfacePainter>();
                if(!source||!source.source)throw new InvalidOperationException("Open the integrated World Editor map first.");
                document=MapEditSession.FromSurface(source.source);
            }
            if(document.fences.Count==0)throw new InvalidOperationException("Draw a fence in World Editor before contextual review.");
            Directory.CreateDirectory(Output);File.WriteAllText(Output+"/play-review-document.json",JsonUtility.ToJson(document));
            SessionState.SetString(Key+".document",JsonUtility.ToJson(document));
            SessionState.SetString(Key+".deadline",DateTime.UtcNow.AddMinutes(3).Ticks.ToString());
            SessionState.SetBool(Key,true);SessionState.SetBool(Key+".captured",false);
            EditorApplication.EnterPlaymode();
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void BeforeSceneLoad()
        {
            if(!SessionState.GetBool(Key,false))return;
            SceneManager.sceneLoaded-=Loaded;SceneManager.sceneLoaded+=Loaded;
        }
        static void Loaded(Scene scene,LoadSceneMode mode)
        {
            if(!SessionState.GetBool(Key,false))return;
            foreach(var root in scene.GetRootGameObjects())foreach(var candidate in root.GetComponentsInChildren<MeadowSurfacePainter>(true))
            {
                var map=JsonUtility.FromJson<MapAuthoringDocument>(SessionState.GetString(Key+".document",""));
                transientSource=Object.Instantiate(candidate.source);transientSource.hideFlags=HideFlags.HideAndDontSave;
                transientSource.authored=map.surface;transientSource.heightField=map.heightField;transientSource.hasHeightBase=true;
                transientSource.treeInstances=map.trees;transientSource.hasTreeBase=true;transientSource.environment=map.environment;transientSource.hasEnvironmentBase=true;
                transientSource.fences=map.fences;transientSource.rivers=map.rivers;transientSource.riverNetwork=map.riverNetwork;transientSource.rockSections=map.rockSections;transientSource.placementExclusions=map.placementExclusions;
                candidate.source=transientSource;candidate.diagnosticSaveId="fence-play-review";candidate.painting=false;injected=true;
            }
            SceneManager.sceneLoaded-=Loaded;
        }
        static void State(PlayModeStateChange state)
        {
            if(!SessionState.GetBool(Key,false))return;
            if(state==PlayModeStateChange.EnteredPlayMode){stage=0;next=EditorApplication.timeSinceStartup+2;}
            if(state==PlayModeStateChange.ExitingPlayMode){view?.Dispose();view=null;}
            if(state==PlayModeStateChange.EnteredEditMode)
            {
                try
                {
                    if(!SessionState.GetBool(Key+".captured",false))File.WriteAllText(Output+"/play-review-error.txt","Review interrupted before all captures completed.");
                }
                finally{SessionState.SetBool(Key,false);SceneManager.sceneLoaded-=Loaded;session=null;painter=null;camera=null;injected=false;}
            }
        }
        static void SetHour(float hour)
        {
            var time=Object.FindAnyObjectByType<WorldTimeSystem>();if(!time)throw new InvalidOperationException("Loaded map has no WorldTimeSystem.");
            time.RunAutomatically=false;time.RestoreState(new WorldTimeState(time.CurrentDay,hour*60));
            var lighting=Object.FindAnyObjectByType<DayNightLightingController>();if(!lighting)throw new InvalidOperationException("Loaded map has no shared day/night lighting.");lighting.ApplyLighting();
        }
        static void CaptureViews(string name)
        {
            // Keep nearby crowns in the context while offering both sides of a fence under trees.
            var offsets=new[]{new Vector3(-1.8f,.75f,0),new Vector3(1.8f,.75f,0),new Vector3(-.3f,.75f,1.8f),new Vector3(.3f,.75f,-1.8f)};
            for(int i=0;i<offsets.Length;i++)
            {
                camera.transform.position=focus+offsets[i]*framing;camera.transform.LookAt(focus);
                FenceAuthoringVerification.Save(camera,Output+"/"+name+"-view"+(i+1)+".png");
            }
        }
        static void Tick()
        {
            if(!SessionState.GetBool(Key,false))return;
            try
            {
                if(long.TryParse(SessionState.GetString(Key+".deadline","0"),out long ticks)&&DateTime.UtcNow.Ticks>ticks)
                    throw new TimeoutException("Fence Play review timed out.");
                if(!EditorApplication.isPlaying||EditorApplication.isPaused||EditorApplication.timeSinceStartup<next)return;
                if(stage==0)
                {
                    painter=Object.FindAnyObjectByType<MeadowSurfacePainter>();if(!painter||painter.Session==null)return;
                    if(!injected)throw new InvalidOperationException("Authoring snapshot was not injected before runtime Start.");
                    camera=painter.studyCamera;if(!camera)throw new InvalidOperationException("No game camera.");
                    if(camera.TryGetComponent<StrategyCameraController>(out var controls))controls.enabled=false;
                    var preview=Object.FindAnyObjectByType<MeadowPreview>();if(preview)preview.enabled=false;
                    painter.painting=false;painter.FenceView?.Dispose();
                    var map=painter.Session.ExportMap();foreach(var record in map.fences)record.scale=1;session=new MapEditSession(map);
                    view=new MapFencePresentation(session,MapFenceCatalog.Default);
                    var bounds=new MapFencePath(map.fences[0]).bounds;
                    // Frame one real authored line so the same trees, terrain and camera give a useful size comparison.
                    focus=new Vector3(bounds.center.x,session.SampleHeight(bounds.center.x,bounds.center.y)+.45f,bounds.center.y);
                    float size=Mathf.Clamp(Mathf.Max(bounds.width,bounds.height)*.57f,4.8f,14);
                    framing=size;
                    camera.orthographic=true;camera.orthographicSize=size;
                    camera.transform.position=focus+new Vector3(size*.9f,size*1.2f,-size*1.6f);camera.transform.LookAt(focus);
                    view.Tick(focus);SetHour(13);stage=1;next=EditorApplication.timeSinceStartup+3;
                }
                else if(stage==1)
                {
                    CaptureViews("01-play-original-day");
                    foreach(var record in session.ExportFences()){record.scale=Mathf.Max(.5f,record.scale*.8f);session.UpsertFence(record);}view.Tick(focus);
                    stage=2;next=EditorApplication.timeSinceStartup+2;
                }
                else if(stage==2)
                {
                    CaptureViews("02-play-smaller-day");
                    foreach(var record in session.ExportFences()){record.variantId="fence.wood01";session.UpsertFence(record);}view.Tick(focus);
                    stage=3;next=EditorApplication.timeSinceStartup+1;
                }
                else if(stage==3)
                {
                    CaptureViews("03-play-smaller-wood01-day");
                    foreach(var record in session.ExportFences()){record.variantId="fence.wood02";session.UpsertFence(record);}view.Tick(focus);
                    stage=4;next=EditorApplication.timeSinceStartup+1;
                }
                else if(stage==4)
                {
                    CaptureViews("04-play-smaller-wood02-day");SetHour(18);
                    stage=5;next=EditorApplication.timeSinceStartup+2;
                }
                else if(stage==5)
                {
                    CaptureViews("05-play-smaller-evening");
                    File.WriteAllText(Output+"/play-review-result.json","{\"status\":\"PASS\",\"actualPlayMode\":"+Application.isPlaying.ToString().ToLowerInvariant()+",\"fenceLines\":"+session.FenceCount+",\"comparisonScale\":0.8,\"hours\":[13,18],\"scene\":\""+SceneManager.GetActiveScene().path+"\",\"graphicsApi\":\""+SystemInfo.graphicsDeviceType+"\",\"gpu\":\""+SystemInfo.graphicsDeviceName+"\"}");
                    SessionState.SetBool(Key+".captured",true);stage=6;EditorApplication.ExitPlaymode();
                }
            }
            catch(Exception error)
            {
                File.WriteAllText(Output+"/play-review-error.txt",error.ToString());Debug.LogException(error);SessionState.SetBool(Key+".captured",true);
                if(EditorApplication.isPlayingOrWillChangePlaymode)EditorApplication.ExitPlaymode();else SessionState.SetBool(Key,false);
            }
        }
    }
}
