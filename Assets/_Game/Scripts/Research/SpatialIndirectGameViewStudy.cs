using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using LittleCastle.CameraSystem;
using LittleCastle.Rendering;
using UnityEngine;

namespace LittleCastle.Research
{
    /// <summary>Owned Player test adapter for the actual strategy rig, no game/map edits.</summary>
    public sealed class SpatialIndirectGameViewStudy : MonoBehaviour
    {
        SpatialIndirectStudyReview review;StrategyCameraController rig;MeadowGrassRenderer grass;
        string output,errors="";bool collecting,complete,groundRelativeFog;readonly List<View> views=new List<View>();readonly List<Motion> motion=new List<Motion>();
        const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        static readonly FieldInfo Counts=typeof(MeadowGrassRenderer).GetField("counts",Private);
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            var args=Environment.GetCommandLineArgs();if(Array.IndexOf(args,"-giGameViewsStudy")<0)return;
            var review=FindAnyObjectByType<SpatialIndirectStudyReview>();if(!review)throw new InvalidOperationException("Owned spatial fixture required.");
            review.StopAllCoroutines();review.enabled=false;
            var driver=review.gameObject.AddComponent<SpatialIndirectGameViewStudy>();driver.review=review;
            driver.groundRelativeFog=Array.IndexOf(args,"-giGroundRelativeFog")>=0;
            int i=Array.IndexOf(args,"-giOutput");if(i<0||i+1>=args.Length)throw new ArgumentException("Explicit isolated output required.");driver.output=Path.GetFullPath(args[i+1]);Directory.CreateDirectory(driver.output);
        }
        void Set(string name,object value)
        {var f=typeof(StrategyCameraController).GetField(name,Private);if(f==null)throw new MissingFieldException(name);f.SetValue(rig,value);}
        float Distance=>(float)typeof(StrategyCameraController).GetField("smoothedDistance",Private).GetValue(rig);
        Vector3 Focus=>(Vector3)typeof(StrategyCameraController).GetField("smoothedFocus",Private).GetValue(rig);
        void Pose(float distance,float pitch,Vector3 focus)
        {
            // Public zoom/focus setters do not exist. This test injects the same
            // private targets as input, then the unchanged rig applies its normal Update.
            Set("targetDistance",distance);Set("smoothedDistance",distance);Set("distanceVelocity",0f);
            Set("targetFocus",focus);Set("smoothedFocus",focus);Set("focusVelocity",Vector3.zero);Set("currentGroundY",focus.y);Set("yaw",35f);Set("pitch",pitch);
        }
        void Log(string m,string s,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors+=m+"\n"+s+"\n";}
        void Update()
        {
            if(collecting)FrameTimingManager.CaptureFrameTimings();
            if(!complete&&errors.Length>0){File.WriteAllText(output+"/runtime-errors.txt",errors);File.WriteAllText(output+"/result.txt","FAIL\n"+errors);complete=true;Application.Quit(2);}
        }
        void LateUpdate()
        {
            if(!groundRelativeFog||!rig)return;
            // DayNightLightingController resets authored fog each Update. This owned
            // test offsets it after that, relative to a 35m ground-focus reference.
            // It never changes the source atmosphere/profile asset or game defaults.
            float offset=Mathf.Max(0,Distance-35);RenderSettings.fogStartDistance+=offset;RenderSettings.fogEndDistance+=offset;
        }
        IEnumerator Start()
        {
            Application.logMessageReceived+=Log;Application.runInBackground=true;QualitySettings.vSyncCount=0;Application.targetFrameRate=-1;
            grass=FindAnyObjectByType<MeadowGrassRenderer>();if(!grass||grass.forceFullDetail||Counts==null)throw new Exception("Native grass with authored distance LODs required.");
            rig=review.camera.GetComponent<StrategyCameraController>();if(!rig)rig=review.camera.gameObject.AddComponent<StrategyCameraController>();rig.SetInputEnabled(false);rig.enabled=true;review.camera.fieldOfView=50;
            yield return null;yield return null;
            float[] distances={11,35,75,115,190},pitches={42,50,57,62,68};var focus=new Vector3(-4,0,5);
            foreach(float hour in new[]{12f,18.5f,1f})
            {
                review.SetHour(hour);while(!review.field.Ready)yield return null;
                for(int i=0;i<distances.Length;i++)
                {
                    Pose(distances[i],pitches[i],focus);for(int f=0;f<20;f++)yield return null;
                    string name="game-"+hour.ToString("0.0",System.Globalization.CultureInfo.InvariantCulture)+"-d"+distances[i];
                    review.field.applyIndirect=false;yield return null;yield return review.Capture(name+"-off");
                    review.field.applyIndirect=true;yield return null;yield return review.Capture(name+"-on");
                    views.Add(new View{name=name,hour=hour,requestedDistance=distances[i],distance=Distance,pitch=review.camera.transform.eulerAngles.x,fov=review.camera.fieldOfView,position=review.camera.transform.position,focus=Focus,grass=Grass(),oaks=Oaks(),benchmarkOffset=(views.Count*2),fogStart=RenderSettings.fogStartDistance,fogEnd=RenderSettings.fogEndDistance,shadowDistance=UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline is UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset p?p.shadowDistance:0});
                    collecting=true;yield return review.Measure(false);yield return review.Measure(true);collecting=false;
                }
            }
            review.WriteBenchmarks();review.SetHour(12);while(!review.field.Ready)yield return null;
            var oak=FindObjectsByType<LODGroup>().OrderBy(g=>g.transform.position.x).FirstOrDefault();
            if(oak){var target=oak.transform.position;target.y=0;Pose(18,42,target);for(int f=0;f<20;f++)yield return null;yield return review.Capture("game-oak-close");}
            // Real rig smoothing, zoom and ground-focus motion; no player/character simulation.
            Application.targetFrameRate=60;Pose(18,42,focus);for(int f=0;f<20;f++)yield return null;
            for(int f=0;f<120;f++)
            {
                float t=f/119f;Set("targetDistance",Mathf.Lerp(18,115,t));Set("targetFocus",focus+new Vector3(10*t,0,8*t));Set("pitch",Mathf.Lerp(42,62,t));
                yield return null;
                if(f%6==0){motion.Add(new Motion{frame=f,distance=Distance,position=review.camera.transform.position,grass=Grass()});yield return review.Capture("game-motion-"+f.ToString("D3"));}
            }
            File.WriteAllText(output+"/game-views.json",JsonUtility.ToJson(new Report{views=views.ToArray(),motion=motion.ToArray(),rig="StrategyCameraController unchanged; test-only private target injection",treeVisibility="Renderer.isVisible indicators include shadows/crossfades; expectedLOD is a screen-height estimate",grassLodDistances=grass.lodDistances,grassMaximumDistance=grass.maximumDistance,groundRelativeFog=groundRelativeFog},true));
            File.WriteAllText(output+"/runtime-errors.txt",errors);File.WriteAllText(output+"/result.txt",errors.Length==0?"PASS owned strategy rig, distance/pitch route and native grass distance LODs.\n":"FAIL\n"+errors);complete=true;Application.Quit(errors.Length==0?0:2);
        }
        GrassStats Grass()=>new GrassStats{lodClumps=(int[])((int[])Counts.GetValue(grass)).Clone(),visiblePatches=grass.visiblePatches,clumps=grass.submittedClumps,triangles=grass.submittedTriangles,drawCalls=grass.drawCalls,forceFullDetail=grass.forceFullDetail};
        Oak[] Oaks()=>FindObjectsByType<LODGroup>().Select(g=>
        {
            var lods=g.GetLODs();float scale=Mathf.Max(Mathf.Abs(g.transform.lossyScale.x),Mathf.Max(Mathf.Abs(g.transform.lossyScale.y),Mathf.Abs(g.transform.lossyScale.z)));
            float d=Vector3.Distance(review.camera.transform.position,g.transform.TransformPoint(g.localReferencePoint));float size=g.size*scale*QualitySettings.lodBias/(2*Mathf.Max(.001f,d)*Mathf.Tan(review.camera.fieldOfView*.5f*Mathf.Deg2Rad));
            int expected=lods.Length;for(int i=0;i<lods.Length;i++)if(size>=lods[i].screenRelativeTransitionHeight){expected=i;break;}
            return new Oak{name=g.name,expectedLod=expected,rendererVisibleIndicators=lods.Select(l=>l.renderers.Count(r=>r&&r.isVisible)).ToArray(),screenHeight=size,transitionHeights=lods.Select(l=>l.screenRelativeTransitionHeight).ToArray()};
        }).ToArray();
        void OnDestroy(){Application.logMessageReceived-=Log;}
        [Serializable] public sealed class GrassStats{public int[] lodClumps;public int visiblePatches,clumps,triangles,drawCalls;public bool forceFullDetail;}
        [Serializable] public sealed class Oak{public string name;public int expectedLod;public int[] rendererVisibleIndicators;public float screenHeight;public float[] transitionHeights;}
        [Serializable] public sealed class View{public string name;public float hour,requestedDistance,distance,pitch,fov,fogStart,fogEnd,shadowDistance;public Vector3 position,focus;public GrassStats grass;public Oak[] oaks;public int benchmarkOffset;}
        [Serializable] public sealed class Motion{public int frame;public float distance;public Vector3 position;public GrassStats grass;}
        [Serializable] public sealed class Report{public View[] views;public Motion[] motion;public string rig,treeVisibility;public Vector3 grassLodDistances;public float grassMaximumDistance;public bool groundRelativeFog;}
    }
}
