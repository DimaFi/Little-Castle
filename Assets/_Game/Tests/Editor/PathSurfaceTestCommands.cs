using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace LittleCastle.Tests
{
    // No background work without this chat's explicit short PC/Editor lease and request.
    [InitializeOnLoad]
    public static class PathSurfaceTestCommands
    {
        const string Owner="01a18703-566a-7952-a0bc-8d8300f07587";
        static readonly string Evidence=Path.GetFullPath(Application.dataPath+"/../../Research/Paths_20261029");
        static double nextPoll;
        static TestRunnerApi runner;
        [Serializable] sealed class Request{public string id,action;}
        [Serializable] sealed class Lease{public string ownerThreadId,status,purpose,updatedAtUtc;}
        [Serializable] sealed class Reply{public string id,status,error;}
        static PathSurfaceTestCommands(){EditorApplication.update+=Poll;}
        static void Poll()
        {
            if(EditorApplication.timeSinceStartup<nextPoll||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
            nextPoll=EditorApplication.timeSinceStartup+1;
            if(!File.Exists("Library/SharedPcControl.json")||!File.Exists("Library/PathSurfaceRequest.json"))return;
            var lease=JsonUtility.FromJson<Lease>(File.ReadAllText("Library/SharedPcControl.json"));
            if(lease.status!="active"||lease.ownerThreadId!=Owner)return;
            var request=JsonUtility.FromJson<Request>(File.ReadAllText("Library/PathSurfaceRequest.json"));
            if(request==null||string.IsNullOrEmpty(request.id)||request.id==SessionState.GetString("LC.Paths.Request",null))return;
            SessionState.SetString("LC.Paths.Request",request.id);Directory.CreateDirectory(Evidence);
            Write(request.id,"RUNNING");
            try
            {
                if(request.action=="verify")
                {
                    Invoke("LittleCastle.Editor.PathSurfaceLibraryBuilder","Build");
                    Invoke("LittleCastle.Editor.PathSurfaceVerification","Preflight");
                    Invoke("LittleCastle.Editor.PathSurfaceVerification","Capture");
                    RunTests(request.id);
                }
                else if(request.action=="library"||request.action=="capture"||request.action=="preflight")
                {
                    string type=request.action=="library"?"LittleCastle.Editor.PathSurfaceLibraryBuilder":"LittleCastle.Editor.PathSurfaceVerification";
                    string method=request.action=="library"?"Build":request.action=="capture"?"Capture":"Preflight";
                    Invoke(type,method);
                    Write(request.id,"PASS");
                }
                else if(request.action=="tests")
                {
                    RunTests(request.id);
                }
                else throw new ArgumentException("Unknown path verification action.");
            }
            catch(Exception e){Write(request.id,"FAIL",e.ToString());Debug.LogException(e);if(Application.isBatchMode)EditorApplication.Exit(2);}
        }
        static void Invoke(string type,string method)=>AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType(type)).First(t=>t!=null).GetMethod(method).Invoke(null,null);
        static void RunTests(string id)
        {
            runner=ScriptableObject.CreateInstance<TestRunnerApi>();runner.RegisterCallbacks(new Results(id));
            runner.Execute(new ExecutionSettings(new Filter{testMode=TestMode.EditMode,testNames=new[]{
                "LittleCastle.Tests.MapPathMaterialTests","LittleCastle.Tests.MapToolTests","LittleCastle.Tests.MapEditTests",
                "LittleCastle.Tests.MapTerrainTests","LittleCastle.Tests.MapEnvironmentSessionTests",
                "LittleCastle.Tests.MapEnvironmentPresentationTests","LittleCastle.Tests.MapRiverWaterInteropTests"}}));
        }
        static void Write(string id,string state,string error=null)
        {
            try{File.WriteAllText(Evidence+"/editor-result.json",JsonUtility.ToJson(new Reply{id=id,status=state,error=error},true));}
            finally
            {
                // Release each explicit action even when the calling chat is interrupted.
                // The caller separately sends the user-authorized cross-chat return notice.
                if(state!="RUNNING"&&File.Exists("Library/SharedPcControl.json"))
                {
                    using(var file=new FileStream("Library/SharedPcControl.json",FileMode.Open,FileAccess.ReadWrite,FileShare.None))
                    {
                        string raw;using(var reader=new StreamReader(file,System.Text.Encoding.UTF8,true,1024,true))raw=reader.ReadToEnd();
                        var lease=JsonUtility.FromJson<Lease>(raw);
                        if(lease.ownerThreadId==Owner)
                        {
                            lease.status="released";lease.purpose="Path action "+id+" ended: "+state+"; no input/Play held.";lease.updatedAtUtc=DateTime.UtcNow.ToString("o");
                            var bytes=System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(lease,true));file.SetLength(0);file.Write(bytes,0,bytes.Length);
                        }
                    }
                }
            }
        }
        sealed class Results:ICallbacks
        {
            readonly string id;public Results(string id){this.id=id;}
            public void RunStarted(ITestAdaptor test){}
            public void TestStarted(ITestAdaptor test){}
            public void TestFinished(ITestResultAdaptor result){}
            public void RunFinished(ITestResultAdaptor result)
            {TestRunnerApi.SaveResultToFile(result,Evidence+"/tests-editmode.xml");Write(id,result.ResultState);runner.UnregisterCallbacks(this);UnityEngine.Object.DestroyImmediate(runner);runner=null;if(Application.isBatchMode)EditorApplication.Exit(result.FailCount==0?0:2);}
        }
    }
}
