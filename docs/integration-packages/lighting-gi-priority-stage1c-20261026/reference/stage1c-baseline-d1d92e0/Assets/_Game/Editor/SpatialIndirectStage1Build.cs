using System;
using System.IO;
using System.Linq;
using LittleCastle.Research;
using LittleCastle.World;
using LittleCastle.Rendering;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object=UnityEngine.Object;

namespace LittleCastle.Editor
{
    public static class SpatialIndirectStage1Build
    {
        public static string Output=>Array.IndexOf(Environment.GetCommandLineArgs(),"-giStage1B")>=0?"E:/Games_Develop/Research/SpatialIndirectStage1B_20261026":"E:/Games_Develop/Research/SpatialIndirectStage1_20261026";
        static void Open()
        {
            if(Path.GetFileName(Path.GetDirectoryName(Application.dataPath))!="Little-Castle_LightingIntegration_20261025")throw new InvalidOperationException("Isolated lighting copy only.");
            Directory.CreateDirectory(Output);
            EditorSceneManager.OpenScene(SpatialIndirectStudyBuild.Scene,OpenSceneMode.Single);
        }
        public static void ProfileReference()
        {
            int exit=2;
            try
            {
                Open();var field=Object.FindAnyObjectByType<SpatialIndirectVolume>();
                var clock=Object.FindAnyObjectByType<WorldTimeSystem>();clock.RunAutomatically=false;clock.TryInitialize();clock.RestoreState(new WorldTimeState(1,720));
                Object.FindAnyObjectByType<DayNightLightingController>().ApplyLighting();field.globals.ApplyGlobals();
                Vector3 Linear(Color c){if(QualitySettings.activeColorSpace!=ColorSpace.Linear)c=c.linear;return new Vector3(c.r,c.g,c.b);}
                var g=field.globals;
                var light=new SpatialIndirectTrace.Lighting(Linear(g.CurrentAmbientSkyColor),Linear(g.CurrentAmbientEquatorColor),Linear(g.CurrentAmbientGroundColor),g.SunDirection,Linear(g.CurrentSunColor),g.MoonDirection,Linear(g.CurrentMoonColor));
                var proxies=field.proxies.Where(p=>p.active).Select(p=>new SpatialIndirectTrace.Proxy(p.bounds,new Vector3(p.albedo.linear.r,p.albedo.linear.g,p.albedo.linear.b))).ToArray();
                var cache=new SpatialIndirectCache(field.origin,field.gridSize,field.spacing);
                var warm=new SpatialIndirectTrace(proxies,field.raysPerProbe);for(int i=0;i<300;i++)warm.Solve(cache.Position(i),light,field.rayDistance);
                var times=new double[2];SpatialIndirectProfile profile=null;
                for(int run=0;run<2;run++)
                {
                    var trace=new SpatialIndirectTrace(proxies,field.raysPerProbe);profile=new SpatialIndirectProfile{enabled=run==1};trace.Profile=profile;
                    var timer=System.Diagnostics.Stopwatch.StartNew();for(int i=0;i<cache.ProbeCount;i++)trace.Solve(cache.Position(i),light,field.rayDistance);times[run]=timer.Elapsed.TotalMilliseconds;
                }
                File.WriteAllText(Output+"/reference-profile.json",JsonUtility.ToJson(new ProfileReport{plainEditorMs=times[0],instrumentedEditorMs=times[1],primaryMs=profile.primaryMs,shadowMs=profile.shadowMs,skyMs=profile.skyMs,projectionMs=profile.projectionMs,counters=profile},true));exit=0;
                for(int run=0;run<2;run++)
                {
                    var trace=new SpatialIndirectTrace(proxies,field.raysPerProbe,false);profile=new SpatialIndirectProfile{enabled=run==1};trace.Profile=profile;
                    var transport=new SpatialIndirectTransport(cache,trace);transport.SetLighting(light,field.rayDistance);
                    var timer=System.Diagnostics.Stopwatch.StartNew();for(int i=0;i<cache.ProbeCount;i++)transport.Solve(i,cache.Position(i));times[run]=timer.Elapsed.TotalMilliseconds;
                    profile.geometryBuilds=transport.GeometryBuilds;profile.injections=transport.Injections;profile.directReweights=transport.DirectReweights;
                }
                File.WriteAllText(Output+"/cached-profile.json",JsonUtility.ToJson(new ProfileReport{plainEditorMs=times[0],instrumentedEditorMs=times[1],primaryMs=profile.primaryMs,shadowMs=profile.shadowMs,skyMs=profile.skyMs,projectionMs=profile.projectionMs,counters=profile},true));
            }
            catch(Exception e){File.WriteAllText(Output+"/profile-error.txt",e.ToString());Debug.LogException(e);}
            if(Application.isBatchMode)UrpBatchExit.WhenIdle(exit);
        }
        [Serializable] public sealed class ProfileReport{public double plainEditorMs,instrumentedEditorMs,primaryMs,shadowMs,skyMs,projectionMs;public SpatialIndirectProfile counters;}
        public static void BuildLinear()
        {
            Open();string[] settings={"ProjectSettings/ProjectSettings.asset","ProjectSettings/GraphicsSettings.asset","ProjectSettings/QualitySettings.asset"};var bytes=settings.ToDictionary(p=>p,File.ReadAllBytes);
            bool development=Array.IndexOf(Environment.GetCommandLineArgs(),"-giDevelopment")>=0;string destination=Output+(development?"/Development":"");Directory.CreateDirectory(destination);
            var space=PlayerSettings.colorSpace;bool timing=PlayerSettings.enableFrameTimingStats;int quality=QualitySettings.GetQualityLevel();
            var paths=Enumerable.Range(0,QualitySettings.names.Length).Select(i=>AssetDatabase.GetAssetPath(QualitySettings.GetRenderPipelineAssetAt(i))).ToArray();string defaultPath=AssetDatabase.GetAssetPath(GraphicsSettings.defaultRenderPipeline);int exit=2;
            try
            {
                PlayerSettings.colorSpace=ColorSpace.Linear;PlayerSettings.enableFrameTimingStats=true;
                var profiles=new[]{"HDR","AO"}.Select(n=>AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(LightingMeadowBuild.Root+"/"+n+".asset")).ToArray();GraphicsSettings.defaultRenderPipeline=profiles[1];
                for(int i=0;i<paths.Length;i++){QualitySettings.SetQualityLevel(i,false);QualitySettings.renderPipeline=profiles[i%2];}QualitySettings.SetQualityLevel(quality,false);
                var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{SpatialIndirectStudyBuild.Scene,LightingMeadowBuild.Scene},locationPathName=destination+"/Player/SpatialIndirectStudy.exe",target=BuildTarget.StandaloneWindows64,options=development?BuildOptions.Development:BuildOptions.None});
                File.WriteAllText(destination+"/build.txt",$"Result={report.summary.result}\nErrors={report.summary.totalErrors}\nWarnings={report.summary.totalWarnings}\nTime={report.summary.totalTime}\nDevelopment={development}; Linear; unchanged input scene; FrameTimingStats=true\n");
                File.WriteAllLines(destination+"/build-warnings.txt",report.steps.SelectMany(s=>s.messages).Where(m=>m.type==LogType.Warning||m.type==LogType.Error).Select(m=>m.type+": "+m.content));exit=report.summary.result==BuildResult.Succeeded&&report.summary.totalErrors==0?0:2;
            }
            catch(Exception e){File.WriteAllText(Output+"/build-error.txt",e.ToString());Debug.LogException(e);}
            finally
            {
                PlayerSettings.colorSpace=space;PlayerSettings.enableFrameTimingStats=timing;AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                for(int i=0;i<paths.Length;i++){QualitySettings.SetQualityLevel(i,false);QualitySettings.renderPipeline=AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(paths[i]);}QualitySettings.SetQualityLevel(quality,false);GraphicsSettings.defaultRenderPipeline=AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(defaultPath);AssetDatabase.SaveAssets();foreach(var p in settings)File.WriteAllBytes(p,bytes[p]);
            }
            if(Application.isBatchMode)UrpBatchExit.WhenIdle(exit);
        }
    }
}
