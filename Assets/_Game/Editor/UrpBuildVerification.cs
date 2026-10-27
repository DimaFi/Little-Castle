using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace LittleCastle.Editor
{
    public static class UrpBuildVerification
    {
        public static void Run()
        {
            if (!Path.GetFileName(Path.GetDirectoryName(Application.dataPath)).StartsWith("Little-Castle_URP_"))
                throw new InvalidOperationException("Build verification is restricted to the isolated URP copy.");
            string output = Path.GetFullPath(Path.Combine(Application.dataPath, "../../Little-Castle_Assets/Research/URP_2026-10-24/Player"));
            Directory.CreateDirectory(output);
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { TerrainStarterSceneBuilder.StudyPath, InnerverseStarterStudyScene.ScenePath, "Assets/_Game/Scenes/ConceptProceduralWorld.unity" },
                locationPathName = Path.Combine(output, "LittleCastle_URP_Review.exe"),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            });
            var summary = report.summary;
            File.WriteAllText(Path.Combine(output, "build_result.txt"), "Result=" + summary.result + "\nErrors=" + summary.totalErrors +
                "\nWarnings=" + summary.totalWarnings + "\nBytes=" + summary.totalSize + "\nTime=" + summary.totalTime);
            if (Application.isBatchMode) EditorApplication.Exit(summary.result == BuildResult.Succeeded ? 0 : 2);
        }
    }
}
