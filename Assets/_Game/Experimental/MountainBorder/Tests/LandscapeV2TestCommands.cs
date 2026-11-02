using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace LittleCastle.Experimental.MountainBorder.Tests
{
    [InitializeOnLoad]
    public static class LandscapeV2TestCommands
    {
        const string Root=Editor.LandscapeIntegrationV2.Evidence;
        static TestRunnerApi runner;
        static double nextPoll;
        [Serializable] sealed class Request{public string id,mode,group;}
        static LandscapeV2TestCommands(){EditorApplication.update+=Poll;EditorApplication.delayCall+=RegisterPending;}
        static void RegisterPending()
        {
            if(!SessionState.GetBool("LC.LandscapeV2.TestsRunning",false)||!Editor.LandscapeIntegrationV2.OwnsLease())return;
            runner=ScriptableObject.CreateInstance<TestRunnerApi>();runner.RegisterCallbacks(new Results(SessionState.GetString("LC.LandscapeV2.TestMode","edit"),SessionState.GetString("LC.LandscapeV2.TestRequest",null)));
        }
        static void Poll()
        {
            if(EditorApplication.timeSinceStartup<nextPoll||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||!Editor.LandscapeIntegrationV2.OwnsLease())return;
            nextPoll=EditorApplication.timeSinceStartup+1;
            string path=Root+"/test-request.json";if(!File.Exists(path))return;
            if(DateTime.UtcNow-File.GetLastWriteTimeUtc(path)>TimeSpan.FromMinutes(10))return;
            var request=JsonUtility.FromJson<Request>(File.ReadAllText(path));
            if(request==null||string.IsNullOrEmpty(request.id)||request.id==SessionState.GetString("LC.LandscapeV2.TestRequest",null))return;
            SessionState.SetString("LC.LandscapeV2.TestRequest",request.id);
            if(request.mode!="edit"&&request.mode!="play")return;
            SessionState.SetBool("LC.LandscapeV2.TestsRunning",true);SessionState.SetString("LC.LandscapeV2.TestMode",request.mode);
            runner=ScriptableObject.CreateInstance<TestRunnerApi>();runner.RegisterCallbacks(new Results(request.mode,request.id));
            File.WriteAllText(Root+"/tests-"+request.mode+"-status.txt","RUNNING");
            var filter=new Filter{testMode=request.mode=="edit"?TestMode.EditMode:TestMode.PlayMode};
            // Explicit class list excludes only that separate oak GPU fixture,
            // without changing or executing it on behalf of the other task.
            if(request.mode=="edit")
            {
                var tests=AppDomain.CurrentDomain.GetAssemblies().Where(a=>a.GetName().Name=="LittleCastle.Tests.EditMode"||a.GetName().Name=="LittleCastle.MountainBorder.Prototype.Tests")
                    .SelectMany(a=>a.GetTypes()).Where(t=>t.IsPublic&&t.IsClass&&t.Namespace!=null&&t.Namespace.StartsWith("LittleCastle")&&t.Name!="FoliageSnowCoverageTests"&&
                    t.GetMethods().Any(m=>m.GetCustomAttributes(false).Any(v=>v.GetType().Name=="TestAttribute"||v.GetType().Name=="TestCaseAttribute"||v.GetType().Name=="TestCaseSourceAttribute"||v.GetType().Name=="UnityTestAttribute")))
                    .Select(t=>t.FullName).ToArray();
                if(request.group=="landscape")tests=tests.Where(t=>t.StartsWith("LittleCastle.Experimental.MountainBorder.Tests.")||t=="LittleCastle.Tests.RiverSurfaceEffectsTests"||t=="LittleCastle.Tests.RiverBankAuthoringFreedomTests"||t=="LittleCastle.Tests.MapRiverWaterInteropTests").ToArray();
                if(request.group=="river-network")tests=tests.Where(t=>t=="LittleCastle.Tests.MapRiverNetworkTests"||t=="LittleCastle.Tests.MapRiverNetworkSessionTests").ToArray();
                if(request.group=="connected-water")tests=tests.Where(t=>t=="LittleCastle.Tests.ConnectedWaterM1Tests"||t=="LittleCastle.Tests.MapRiverNetworkTests"||t=="LittleCastle.Tests.MapRiverNetworkSessionTests").ToArray();
                filter.testNames=tests;filter.categoryNames=null;
            }
            runner.Execute(new ExecutionSettings(filter));
        }
        sealed class Results:ICallbacks
        {
            readonly string mode,requestId;public Results(string mode,string requestId){this.mode=mode;this.requestId=requestId;}
            public void RunStarted(ITestAdaptor test){}
            public void TestStarted(ITestAdaptor test){}
            public void TestFinished(ITestResultAdaptor result){}
            public void RunFinished(ITestResultAdaptor result)
            {
                if(requestId!=SessionState.GetString("LC.LandscapeV2.TestRequest",null)||mode!=SessionState.GetString("LC.LandscapeV2.TestMode",null))return;
                try{TestRunnerApi.SaveResultToFile(result,Root+"/tests-"+mode+".xml");File.WriteAllText(Root+"/tests-"+mode+"-status.txt",result.ResultState);}
                finally
                {
                    SessionState.SetBool("LC.LandscapeV2.TestsRunning",false);
                    if(runner)runner.UnregisterCallbacks(this);
                    if(Editor.LandscapeIntegrationV2.OwnsLease())File.WriteAllText(Application.dataPath+"/../Library/SharedPcControl.json","{\"ownerThreadId\":\"01a1884e-2c88-78b1-872c-874f75849e21\",\"status\":\"released\",\"purpose\":\"Landscape "+mode+" tests completed\",\"updatedAtUtc\":\""+DateTime.UtcNow.ToString("o")+"\"}");
                }
                Debug.Log("LANDSCAPE_V2_TESTS "+mode+" "+result.ResultState);
            }
        }
    }
}
