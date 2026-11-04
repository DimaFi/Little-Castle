using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace LittleCastle.Tests
{
    [InitializeOnLoad] public static class WaterFirefliesPerfTestRunner
    {
        const string Root="E:/Games_Develop/Research/WaterFirefliesPerf_20261102";
        const string Owner="01a191ef-b3bd-7be0-8e9d-bd5a9894b22c";
        [Serializable] sealed class Lease {public string ownerThreadId,status;}
        const string BuildTag="M02";
        [Serializable] sealed class Request {public string id,mode,group,revision;}
        static double next;static TestRunnerApi runner;
        static WaterFirefliesPerfTestRunner(){EditorApplication.update+=Poll;EditorApplication.delayCall+=Resume;}
        static bool Owns()
        {try{var l=JsonUtility.FromJson<Lease>(File.ReadAllText("Library/SharedPcControl.json"));return l.ownerThreadId==Owner&&l.status=="active";}catch{return false;}}
        static void Resume()
        {
            if(!Owns()||!SessionState.GetBool("LC.WaterFirefliesPerf.Tests",false))return;
            runner=ScriptableObject.CreateInstance<TestRunnerApi>();runner.RegisterCallbacks(new Results(SessionState.GetString("LC.WaterFirefliesPerf.TestId","")));
        }
        public static void Disarm(){EditorApplication.update-=Poll;EditorApplication.delayCall-=Resume;}
        static void Poll()
        {
            if(EditorApplication.timeSinceStartup<next)return;next=EditorApplication.timeSinceStartup+.5;
            if(!Owns()||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||SessionState.GetBool("LC.WaterFirefliesPerf.Tests",false))return;
            string path=Root+"/test-request.json";
            if(!File.Exists(path)||DateTime.UtcNow-File.GetLastWriteTimeUtc(path)>TimeSpan.FromMinutes(10))return;
            var r=JsonUtility.FromJson<Request>(File.ReadAllText(path));if(r==null||string.IsNullOrEmpty(r.id)||r.id==SessionState.GetString("LC.WaterFirefliesPerf.TestId",""))return;
            if(r.revision!=BuildTag)return;
            if(r.mode!="edit"&&r.mode!="play")return;
            SessionState.SetString("LC.WaterFirefliesPerf.TestId",r.id);SessionState.SetBool("LC.WaterFirefliesPerf.Tests",true);
            runner=ScriptableObject.CreateInstance<TestRunnerApi>();runner.RegisterCallbacks(new Results(r.id));
            var filter=new Filter{testMode=r.mode=="edit"?TestMode.EditMode:TestMode.PlayMode};
            if(r.mode=="edit"&&r.group=="gpu")filter.testNames=new[]{"LittleCastle.Tests.FireflyIntegrationTests"};
            if(r.mode=="edit"&&r.group=="weather")filter.testNames=new[]{"LittleCastle.Tests.RainStormMathTests","LittleCastle.Tests.RainShelterVolumeTests","LittleCastle.Tests.AmbientSnowfallTests"};
            if(r.mode=="edit"&&r.group=="map-settings")filter.testNames=new[]{"LittleCastle.Tests.MapCreationFactoryTests","LittleCastle.Tests.MapNewDocumentTests","LittleCastle.Tests.RainStormMathTests","LittleCastle.Tests.RainShelterVolumeTests","LittleCastle.Tests.AmbientSnowfallTests"};
            if(r.mode=="edit"&&r.group=="map-creation")filter.testNames=new[]{"LittleCastle.Tests.MapCreationFactoryTests","LittleCastle.Tests.MapNewDocumentTests"};
            if(r.mode=="edit"&&r.group=="focused")filter.testNames=new[]{"LittleCastle.Tests.RegionalPreviewTests","LittleCastle.Tests.FireflyIntegrationTests","LittleCastle.Tests.MapRiverBTests","LittleCastle.Tests.FireflyPreviewTests","LittleCastle.Tests.MapRiverRecoveryTests","LittleCastle.Tests.MapSoftRiverTests","LittleCastle.Tests.ConnectedWaterM1Tests","LittleCastle.Tests.MapRiverNetworkSessionTests","LittleCastle.Tests.RiverBankAuthoringFreedomTests","LittleCastle.Tests.MapRiverTests","LittleCastle.Tests.MapRiverWaterInteropTests","LittleCastle.Tests.MapTerrainPresentationTests"};
            File.WriteAllText(Root+"/tests-"+r.id+"-status.txt","RUNNING");runner.Execute(new ExecutionSettings(filter));
        }
        sealed class Results:ICallbacks
        {
            readonly string id;public Results(string id){this.id=id;}
            public void RunStarted(ITestAdaptor test){}public void TestStarted(ITestAdaptor test){}public void TestFinished(ITestResultAdaptor result){}
            public void RunFinished(ITestResultAdaptor result)
            {
                if(id!=SessionState.GetString("LC.WaterFirefliesPerf.TestId",""))return;
                try{TestRunnerApi.SaveResultToFile(result,Root+"/tests-"+id+".xml");File.WriteAllText(Root+"/tests-"+id+"-status.txt",result.ResultState);}
                finally
                {
                    SessionState.SetBool("LC.WaterFirefliesPerf.Tests",false);if(runner)runner.UnregisterCallbacks(this);
                    if(File.Exists(Root+"/test-request.json"))File.Delete(Root+"/test-request.json");
                    if(Owns())File.WriteAllText("Library/SharedPcControl.json","{\"ownerThreadId\":\""+Owner+"\",\"status\":\"released\",\"purpose\":\"Soft river tests finished\",\"updatedAtUtc\":\""+DateTime.UtcNow.ToString("o")+"\"}");
                }
            }
        }
    }
}
