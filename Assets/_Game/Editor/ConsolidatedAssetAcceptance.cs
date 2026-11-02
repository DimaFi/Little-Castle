using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LittleCastle.Editor
{
    /// <summary>Read-only import/scene smoke check; never runs builders or saves scenes.</summary>
    public static class ConsolidatedAssetAcceptance
    {
        [Serializable] sealed class SceneResult { public string path; public int objects; public int missingScripts; }
        [Serializable] sealed class Report
        {
            public string utc; public string unity; public int sourceModels;
            public List<SceneResult> scenes=new List<SceneResult>();
            public List<string> errors=new List<string>();
            public string scope="Read-only imports and missing-script checks; visual, NavMesh agent and regeneration acceptance NOT RUN";
        }
        public static void Run()
        {
            var report=new Report{utc=DateTime.UtcNow.ToString("o"),unity=Application.unityVersion};
            var previous=SceneManager.GetActiveScene();
            try
            {
                string root=QuaterniusNatureIntegration.Source+"/Models";
                var files=Directory.GetFiles(root,"*.fbx");
                if(files.Length!=68)report.errors.Add("Expected 68 source FBX inputs; got "+files.Length);
                foreach(string file in files)
                {
                    string path=file.Replace('\\','/');
                    var model=AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if(!model||!model.GetComponentsInChildren<MeshFilter>(true).Any(m=>m.sharedMesh&&m.sharedMesh.vertexCount>0))
                        report.errors.Add("No imported source geometry: "+path);
                    else report.sourceModels++;
                }
                foreach(string path in new[]{GroundBiomeStudyBuilder.ScenePath,GroundBiomeSecondPassBuilder.Scene,
                    ValleyArtStudyBuilder.ScenePath,"Assets/_Game/Art/QuaterniusMeadow/Verification/Flowers_Review.unity",
                    RiverBankStudyBuilder.WaterScenePath})
                {
                    var scene=SceneManager.GetSceneByPath(path);bool openedHere=!scene.IsValid()||!scene.isLoaded;
                    if(openedHere)scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
                    try
                    {
                        var row=new SceneResult{path=path};
                        foreach(var rootObject in scene.GetRootGameObjects())foreach(var transform in rootObject.GetComponentsInChildren<Transform>(true))
                        {row.objects++;row.missingScripts+=GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject);}
                        if(row.missingScripts!=0)report.errors.Add("Missing scripts in "+path+": "+row.missingScripts);
                        report.scenes.Add(row);
                    }
                    finally{if(openedHere)EditorSceneManager.CloseScene(scene,true);}
                }
            }
            catch(Exception error){report.errors.Add(error.ToString());}
            finally
            {
                if(previous.IsValid()&&previous.isLoaded)SceneManager.SetActiveScene(previous);
                string output=Path.GetFullPath(Application.dataPath+"/../../ProjectInventory/Consolidation_20261028");
                Directory.CreateDirectory(output);File.WriteAllText(Path.Combine(output,"main-import-scene-acceptance.json"),JsonUtility.ToJson(report,true));
            }
            Debug.Log("[Consolidation] FBX="+report.sourceModels+" scenes="+report.scenes.Count+" errors="+report.errors.Count);
            if(Application.isBatchMode)EditorApplication.Exit(report.errors.Count==0?0:1);
        }
    }
}
