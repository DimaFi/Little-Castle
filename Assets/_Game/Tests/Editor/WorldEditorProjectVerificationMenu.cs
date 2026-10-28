using System.IO;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace LittleCastle.Tests
{
    public static class WorldEditorProjectVerificationMenu
    {
        static TestRunnerApi runner;
        [MenuItem("Little Castle/World Editor/Checks/Run all EditMode tests")]
        public static void EditMode()=>Run(TestMode.EditMode);
        [MenuItem("Little Castle/World Editor/Checks/Run all PlayMode tests")]
        public static void PlayMode()=>Run(TestMode.PlayMode);
        static void Run(TestMode mode)
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode){Debug.LogError("Exit Play Mode before running project checks.");return;}
            runner=ScriptableObject.CreateInstance<TestRunnerApi>();runner.RegisterCallbacks(new Results(mode));
            runner.Execute(new ExecutionSettings(new Filter{testMode=mode,assemblyNames=new[]{mode==TestMode.EditMode?"LittleCastle.Tests.EditMode":"LittleCastle.Tests.PlayMode"}}));
        }
        sealed class Results:ICallbacks
        {
            readonly TestMode mode;public Results(TestMode mode){this.mode=mode;}
            public void RunStarted(ITestAdaptor test){}
            public void TestStarted(ITestAdaptor test){}
            public void TestFinished(ITestResultAdaptor result){}
            public void RunFinished(ITestResultAdaptor result)
            {
                string folder=Path.GetFullPath(Application.dataPath+"/../../Research/WorldEditorUsability_20261027/Checks");Directory.CreateDirectory(folder);
                string name=mode==TestMode.EditMode?"editmode":"playmode";
                TestRunnerApi.SaveResultToFile(result,Path.Combine(folder,name+".xml"));
                File.WriteAllText(Path.Combine(folder,name+"-finished.txt"),result.ResultState+"\n");
                Debug.Log("WORLD_EDITOR_PROJECT_CHECKS "+name+" "+result.ResultState+"; "+folder);
            }
        }
    }
}
