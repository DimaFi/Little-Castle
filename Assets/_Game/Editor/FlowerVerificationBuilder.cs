using System.IO;
using LittleCastle.Research;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LittleCastle.Editor
{
    public static class FlowerVerificationBuilder
    {
        public static void Build()
        {
            QuaterniusNatureIntegration.RefreshAutumnPalette();
            QuaterniusNatureIntegration.UseSharedMeadowLibrary();
            QuaterniusNatureIntegration.RefreshBrushGroups();
            QuaterniusNatureIntegration.ImportReviewThumbnails();
            Directory.CreateDirectory(QuaterniusNatureIntegration.Root+"/Verification");
            var scene=EditorSceneManager.OpenScene(MeadowStudyBuilder.ScenePath);
            var painter=Object.FindAnyObjectByType<MeadowSurfacePainter>();
            var preview=Object.FindAnyObjectByType<MeadowPreview>();
            // The camera/brush stay the real Meadow presentation; disable only demonstration camera motion/UI.
            if(preview)preview.enabled=false;
            var runner=new GameObject("Flower review capture").AddComponent<FlowerVerificationPreview>();runner.painter=painter;
            runner.reviewCamera=painter.studyCamera?painter.studyCamera:Camera.main;
            runner.output=QuaterniusNatureIntegration.Output+"/Capture";
            string copy=QuaterniusNatureIntegration.Root+"/Verification/Flowers_Review.unity";
            EditorSceneManager.SaveScene(scene,copy,true);
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{copy},locationPathName=QuaterniusNatureIntegration.Output+"/Player/LittleCastle_Flowers.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.None});
            File.WriteAllText(QuaterniusNatureIntegration.Output+"/build-result.txt",$"Result={report.summary.result}\nErrors={report.summary.totalErrors}\nWarnings={report.summary.totalWarnings}\nTime={report.summary.totalTime}");
            EditorApplication.Exit(report.summary.result==BuildResult.Succeeded?0:2);
        }
    }
}
