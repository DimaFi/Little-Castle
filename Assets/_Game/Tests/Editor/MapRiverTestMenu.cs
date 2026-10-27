using System.IO;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
namespace LittleCastle.Tests
{
    public static class MapRiverTestMenu
    {
        static TestRunnerApi runner;
        [MenuItem("Little Castle/Map/Rivers/Run EditMode Regression %#F7")]
        public static void EditTests()=>Run(TestMode.EditMode);
        [MenuItem("Little Castle/Map/Rivers/Run PlayMode Regression %#F8")]
        public static void PlayTests()=>Run(TestMode.PlayMode);
        static void Run(TestMode mode)
        {
            runner=ScriptableObject.CreateInstance<TestRunnerApi>();runner.RegisterCallbacks(new Results(mode));
            runner.Execute(new ExecutionSettings(new Filter{testMode=mode,groupNames=new[]{"^LittleCastle.Tests.Map","^LittleCastle.Tests.FiniteSessionMapAndFogTests"}}));
        }
        sealed class Results : ICallbacks
        {
            readonly TestMode mode;public Results(TestMode mode){this.mode=mode;}
            public void RunStarted(ITestAdaptor test){}
            public void TestStarted(ITestAdaptor test){}
            public void TestFinished(ITestResultAdaptor result){}
            public void RunFinished(ITestResultAdaptor result)
            {
                string folder=Path.GetFullPath(Application.dataPath+"/../../Research/MapRivers_20261026");Directory.CreateDirectory(folder);TestRunnerApi.SaveResultToFile(result,Path.Combine(folder,mode==TestMode.EditMode?"editmode.xml":"playmode.xml"));
                Debug.Log("River regression finished: "+result.ResultState+"; report: "+folder);
            }
        }
    }
}
