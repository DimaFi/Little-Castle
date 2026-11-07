using System;
using System.IO;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace LittleCastle.Editor
{
    // One explicit file request while the shared Editor lease belongs to this chat.
    [InitializeOnLoad]
    static class CozyLightingReview
    {
        const string Owner = "01a18b72-7012-7de0-8a5f-9a5a1f5f5265";
        const string Request = "Library/CozyLightingReviewRequest.json";
        const string Output = "E:/Games_Develop/Research/SkyAtmosphere_20261030/window-pass-20261031";
        const string Key = "LC.CozyLightingReview.20261031.windows";
        [Serializable] class Command {public string id, action;}
        [Serializable] class Lease {public string ownerThreadId, status, purpose, updatedAtUtc;}
        [Serializable] class Result {public string status; public int passed, failed, skipped;}
        static TestRunnerApi runner;
        static double next;
        static CozyLightingReview() {if (!SessionState.GetBool(Key+".disarmed", false)) EditorApplication.update += Poll;}
        static void Poll()
        {
            if (EditorApplication.timeSinceStartup < next) return;
            next = EditorApplication.timeSinceStartup + 1;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || runner ||
                !File.Exists(Request) || !File.Exists("Library/SharedPcControl.json")) return;
            var lease = JsonUtility.FromJson<Lease>(File.ReadAllText("Library/SharedPcControl.json"));
            if (lease.ownerThreadId != Owner || lease.status != "active") return;
            var command = JsonUtility.FromJson<Command>(File.ReadAllText(Request));
            if (command == null || string.IsNullOrEmpty(command.id) || command.id == SessionState.GetString(Key+".last", "")) return;
            SessionState.SetString(Key+".last", command.id);
            if (command.action == "forest")
            {
                SessionState.SetString("LC.Atmosphere.ArtCaptureReview.output", Output+"/forest");
                SessionState.SetBool("LC.Atmosphere.ArtCaptureReview.art", true);
                AtmosphereCaptureReview.Begin();
                return;
            }
            if (command.action == "disarm")
            {
                EditorApplication.update -= Poll; SessionState.SetBool(Key+".disarmed", true);
                Directory.CreateDirectory(Output); File.WriteAllText(Output+"/tests-disarmed.json", "{\"disarmed\":true}");
                return;
            }
            if (command.action != "tests") return;
            runner = ScriptableObject.CreateInstance<TestRunnerApi>();
            runner.RegisterCallbacks(new Tests());
            runner.Execute(new ExecutionSettings(new Filter {testMode = TestMode.EditMode, testNames = new[] {
                "LittleCastle.Tests.AtmosphereMoonTests", "LittleCastle.Tests.AtmosphereProfileTests",
                "LittleCastle.Tests.FoliageSnowCoverageTests", "LittleCastle.Tests.EnvironmentSnowUniformityTests",
                "LittleCastle.Tests.NightLightingRuntimeTests", "LittleCastle.Tests.NightLightPreviewBudgetTests",
                "LittleCastle.Tests.DistantWindowEmissionTests", "LittleCastle.Tests.LightingPaletteTests"}}));
        }
        sealed class Tests : ICallbacks
        {
            public void RunStarted(ITestAdaptor t) {}
            public void TestStarted(ITestAdaptor t) {}
            public void TestFinished(ITestResultAdaptor t) {}
            public void RunFinished(ITestResultAdaptor t)
            {
                try
                {
                    Directory.CreateDirectory(Output);
                    TestRunnerApi.SaveResultToFile(t, Output+"/tests-editmode.xml");
                    File.WriteAllText(Output+"/tests.json", JsonUtility.ToJson(new Result {
                        status=t.FailCount == 0 && t.PassCount > 0 && t.SkipCount == 0 ? "PASS" : "FAIL",
                        passed=t.PassCount, failed=t.FailCount, skipped=t.SkipCount}, true));
                }
                finally { if (runner) UnityEngine.Object.DestroyImmediate(runner); runner=null; }
            }
        }
    }
}
