using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace LittleCastle.Tests
{
    /// <summary>Explicit, lease-checked commands in the existing Editor. No new project or Editor instance.</summary>
    [InitializeOnLoad] public static class FenceTestMenu
    {
        const string Owner="01a186d7-dffa-7c52-b773-3b7d943fc9be";
        static readonly string Output=Path.GetFullPath(Application.dataPath+"/../../Research/FenceBrush_20261029");
        static TestRunnerApi runner;
        static double nextPoll;
        [Serializable] sealed class Request {public string id,action;}
        [Serializable] sealed class Lease {public string ownerThreadId,status;}
        static FenceTestMenu(){EditorApplication.update+=Poll;}
        static void CallEditor(string name,string method)
        {
            var type=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("LittleCastle.Editor."+name)).First(t=>t!=null);
            type.GetMethod(method).Invoke(null,null);
        }
        static void Poll()
        {
            if(EditorApplication.timeSinceStartup<nextPoll||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
            nextPoll=EditorApplication.timeSinceStartup+.5;
            string path=Output+"/editor-request.json";if(!File.Exists(path))return;
            try
            {
                var request=JsonUtility.FromJson<Request>(File.ReadAllText(path));
                if(request==null||string.IsNullOrEmpty(request.id)||request.id==SessionState.GetString("LC.Fence.Command",null))return;
                var lease=JsonUtility.FromJson<Lease>(File.ReadAllText("Library/SharedPcControl.json"));
                if(lease.ownerThreadId!=Owner||lease.status!="active")return;
                SessionState.SetString("LC.Fence.Command",request.id);
                if(request.action=="tests")Run();
                else if(request.action=="capture")
                {CallEditor("FenceAuthoringVerification","Capture");File.WriteAllText(Output+"/capture-result.json","{\"status\":\"PASS\"}");}
                else if(request.action=="playReview")CallEditor("FencePlayReview","Begin");
                else throw new ArgumentException("Unknown fence verification command.");
            }
            catch(Exception e){File.WriteAllText(Output+"/editor-error.txt",e.ToString());Debug.LogException(e);}
        }
        [MenuItem("Little Castle/World Editor/Run Fence Regression Tests")]
        public static void Run()
        {
            Directory.CreateDirectory(Output);CallEditor("FenceAuthoringSetup","Ensure");
            runner=ScriptableObject.CreateInstance<TestRunnerApi>();runner.RegisterCallbacks(new Results());
            runner.Execute(new ExecutionSettings(new Filter{testMode=TestMode.EditMode,
                groupNames=new[]{"^LittleCastle.Tests.MapFenceTests","^LittleCastle.Tests.MapEditTests","^LittleCastle.Tests.MapToolTests","^LittleCastle.Tests.MapEnvironmentSessionTests","^LittleCastle.Tests.MapRiverTests","^LittleCastle.Tests.MapAuthoringWindowLifecycleTests"}}));
        }
        sealed class Results : ICallbacks
        {
            public void RunStarted(ITestAdaptor tests){}
            public void TestStarted(ITestAdaptor test){}
            public void TestFinished(ITestResultAdaptor result){}
            public void RunFinished(ITestResultAdaptor result)
            {
                TestRunnerApi.SaveResultToFile(result,Output+"/editmode.xml");
                File.WriteAllText(Output+"/test-result.json","{\"status\":\""+result.ResultState+"\",\"passed\":"+result.PassCount+",\"failed\":"+result.FailCount+",\"skipped\":"+result.SkipCount+"}");
                Debug.Log("FENCE_REGRESSION_FINISHED "+result.ResultState);
                runner.UnregisterCallbacks(this);UnityEngine.Object.DestroyImmediate(runner);runner=null;
            }
        }
    }
}
