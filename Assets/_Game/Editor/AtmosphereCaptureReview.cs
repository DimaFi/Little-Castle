using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using LittleCastle.CameraSystem;
using LittleCastle.Rendering;
using LittleCastle.Research;
using LittleCastle.World;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace LittleCastle.Editor
{
    /// <summary>Explicit local capture, guarded by this chat's short shared-PC lease.</summary>
    [InitializeOnLoad]
    public static class AtmosphereCaptureReview
    {
        const string Owner = "01a18b72-7012-7de0-8a5f-9a5a1f5f5265";
        const string Key = "LC.Atmosphere.ArtCaptureReview";
        static string Output => SessionState.GetString(Key + ".output", "E:/Games_Develop/Research/SkyAtmosphere_20261030/unity-review");
        const string Request = "Library/AtmosphereCaptureRequest.json";
        [Serializable] sealed class Lease { public string ownerThreadId, status, purpose, updatedAtUtc; }
        [Serializable] sealed class Command { public string id, action, output; }
        [Serializable] sealed class FileStamp { public string path, hash; }
        [Serializable] sealed class Stamps { public List<FileStamp> files = new List<FileStamp>(); }
        [Serializable] sealed class Result
        {
            public string status, scene, gpu, api, error;
            public bool actualPlayMode, sourceAssetsPreserved, previewPreservesClock, editModeRestored;
            public int captured, shaderErrors, testPassed, testFailed, testSkipped;
            public List<string> messages = new List<string>();
        }
        static Camera camera, skyCamera;
        static DayNightLightingController atmosphere;
        static StylizedLightingGlobals globals;
        static MeadowSurfacePainter painter;
        static SurfacePaintData transientSource;
        static UrpPreviewQualityOverride quality;
        static Result result;
        static TestRunnerApi runner;
        static int stage;
        static double next;
        static bool initialized;
        static double nextPoll;
        static AtmosphereArtFixture artFixture;
        static readonly float[] Hours = {13, 18.65f, 0, 0};
        static readonly string[] Names = {"01-sunny-day", "02-golden-hour", "03-blue-night", "04-snowy-moonlight"};

        static AtmosphereCaptureReview()
        {
            if (SessionState.GetBool(Key + ".disarmed", false)) return;
            InstallCallbacks();
        }

        static void InstallCallbacks()
        {
            EditorApplication.update -= Tick;
            EditorApplication.playModeStateChanged -= State;
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += State;
        }

        static bool OwnsLease()
        {
            if (!File.Exists("Library/SharedPcControl.json")) return false;
            var lease = JsonUtility.FromJson<Lease>(File.ReadAllText("Library/SharedPcControl.json"));
            return lease != null && lease.ownerThreadId == Owner && lease.status == "active";
        }

        [MenuItem("Little Castle/Rendering/Capture Atmosphere Review")]
        public static void Begin()
        {
            if (!OwnsLease()) throw new InvalidOperationException("An active atmosphere capture lease is required.");
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Begin review outside Play Mode.");
            PurgeTemporaryRoots();
            InstallCallbacks();
            var source = Object.FindAnyObjectByType<MeadowSurfacePainter>();
            if (!source || !source.source) throw new InvalidOperationException("Open the existing integrated World Editor scene.");
            if (!Object.FindAnyObjectByType<DayNightLightingController>()) throw new InvalidOperationException("Existing atmosphere missing.");
            Directory.CreateDirectory(Output);
            var previousSelection = Selection.objects;
            var preflight = new System.Text.StringBuilder();
            int preflightErrors = 0;
            void PreflightLog(string message, string stack, LogType type)
            {
                if (!message.Contains("Little Castle Foliage")) return;
                preflight.AppendLine(message);
                if (type == LogType.Error || type == LogType.Exception) preflightErrors++;
            }
            Application.logMessageReceived += PreflightLog;
            try
            {
                foreach (char variant in "ABCD")
                {
                    Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Art/Studies/ValleyArt_Pass03/Oak_" + variant + "_Study.prefab");
                    FoliageModelCompatibilityValidator.ValidateSelected();
                }
            }
            finally { Application.logMessageReceived -= PreflightLog; Selection.objects = previousSelection; }
            File.WriteAllText(Output + "/foliage-preflight.txt", preflight.ToString());
            if (preflightErrors > 0) throw new InvalidOperationException("Production foliage preflight failed. See foliage-preflight.txt.");
            MapAuthoringDocument document = null;
            foreach (var window in Resources.FindObjectsOfTypeAll<MapAuthoringWindow>())
                if (window.VerificationSession != null) { document = window.VerificationSession.ExportMap(); break; }
            if (document == null) document = MapEditSession.FromSurface(source.source);
            string json = JsonUtility.ToJson(document);
            File.WriteAllText(Output + "/map-snapshot-private.json", json);
            SessionState.SetString(Key + ".document", json);
            var stamps = new Stamps();
            var paths = new HashSet<string>(Directory.GetFiles("Assets/_Game/Settings/Rendering/Atmosphere", "*.asset"));
            paths.Add(AssetDatabase.GetAssetPath(source.source));
            paths.Add(SceneManager.GetActiveScene().path);
            var sky = new SerializedObject(Object.FindAnyObjectByType<DayNightLightingController>()).FindProperty("skyboxMaterial").objectReferenceValue;
            if (sky) paths.Add(AssetDatabase.GetAssetPath(sky));
            foreach (string path in paths) if (!string.IsNullOrEmpty(path) && File.Exists(path)) stamps.files.Add(new FileStamp {path = path, hash = Hash(path)});
            SessionState.SetString(Key + ".stamps", JsonUtility.ToJson(stamps));
            File.WriteAllText(Output + "/source-hashes-before.json", JsonUtility.ToJson(stamps, true));
            SessionState.SetString(Key + ".deadline", DateTime.UtcNow.AddMinutes(5).Ticks.ToString());
            SessionState.SetBool(Key, true);
            SessionState.SetBool(Key + ".finishedCapture", false);
            SessionState.SetString(Key + ".result", JsonUtility.ToJson(new Result {status = "RUNNING", previewPreservesClock = true}));
            EditorApplication.EnterPlaymode();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void BeforeSceneLoad()
        {
            if (!SessionState.GetBool(Key, false)) return;
            SceneManager.sceneLoaded -= Loaded;
            SceneManager.sceneLoaded += Loaded;
        }

        static void Loaded(Scene scene, LoadSceneMode mode)
        {
            if (!SessionState.GetBool(Key, false)) return;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var candidate in root.GetComponentsInChildren<MeadowSurfacePainter>(true))
                {
                    var document = JsonUtility.FromJson<MapAuthoringDocument>(SessionState.GetString(Key + ".document", ""));
                    transientSource = Object.Instantiate(candidate.source);
                    transientSource.hideFlags = HideFlags.HideAndDontSave;
                    transientSource.authored = document.surface; transientSource.heightField = document.heightField;
                    transientSource.hasHeightBase = true; transientSource.treeInstances = document.trees;
                    transientSource.hasTreeBase = true; transientSource.environment = document.environment;
                    transientSource.hasEnvironmentBase = true; transientSource.fences = document.fences;
                    transientSource.rivers = document.rivers; transientSource.riverNetwork = document.riverNetwork;
                    transientSource.rockSections = document.rockSections; transientSource.placementExclusions = document.placementExclusions;
                    candidate.source = transientSource; candidate.diagnosticSaveId = "atmosphere-capture-private"; candidate.painting = false;
                }
            SceneManager.sceneLoaded -= Loaded;
        }

        static void State(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(Key, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                stage = 0; initialized = false; next = EditorApplication.timeSinceStartup + 3;
                result = JsonUtility.FromJson<Result>(SessionState.GetString(Key + ".result", ""));
                Application.logMessageReceived += Log;
            }
            if (state == PlayModeStateChange.ExitingPlayMode) CleanupPresentation();
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                result = JsonUtility.FromJson<Result>(SessionState.GetString(Key + ".result", ""));
                result.editModeRestored = true;
                var stamps = JsonUtility.FromJson<Stamps>(SessionState.GetString(Key + ".stamps", ""));
                result.sourceAssetsPreserved = stamps.files.All(f => File.Exists(f.path) && Hash(f.path) == f.hash);
                SessionState.SetBool(Key, false);
                if (!SessionState.GetBool(Key + ".finishedCapture", false) && string.IsNullOrEmpty(result.error)) result.error = "Capture interrupted.";
                if (!string.IsNullOrEmpty(result.error)) { Finish(); return; }
                runner = ScriptableObject.CreateInstance<TestRunnerApi>();
                runner.RegisterCallbacks(new Tests());
                runner.Execute(new ExecutionSettings(new Filter {testMode = TestMode.EditMode,
                    testNames = new[] {"LittleCastle.Tests.AtmosphereProfileTests", "LittleCastle.Tests.LightingPaletteTests",
                        "LittleCastle.Tests.LightingMeadowPaletteTests", "LittleCastle.Tests.WorldTimeTests", "LittleCastle.Tests.FoliageSnowCoverageTests"}}));
            }
        }

        static void Tick()
        {
            if (!SessionState.GetBool(Key, false))
            {
                if (EditorApplication.timeSinceStartup < nextPoll) return;
                nextPoll = EditorApplication.timeSinceStartup + 1;
                if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
                var command = JsonUtility.FromJson<Command>(File.ReadAllText(Request));
                if (command == null || string.IsNullOrEmpty(command.id) || SessionState.GetString(Key + ".lastCommand", "") == command.id || !OwnsLease()) return;
                SessionState.SetString(Key + ".lastCommand", command.id);
                if (command.action == "disarm")
                {
                    SessionState.SetBool(Key + ".disarmed", true);
                    EditorApplication.update -= Tick; EditorApplication.playModeStateChanged -= State;
                    File.WriteAllText(Output + "/disarmed.json", "{\"disarmed\":true,\"playMode\":" + EditorApplication.isPlaying.ToString().ToLowerInvariant() + "}");
                    ReleaseLease(); return;
                }
                if (command.action == "capture" || command.action == "art")
                {
                    try
                    {
                        if (!string.IsNullOrEmpty(command.output))
                        {
                            string path = Path.GetFullPath(command.output).Replace('\\','/');
                            if (!path.StartsWith("E:/Games_Develop/Research/SkyAtmosphere_20261030/", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Capture output must stay in this task's evidence folder.");
                            SessionState.SetString(Key + ".output", path);
                        }
                        SessionState.SetBool(Key + ".art", command.action == "art");
                        Begin();
                    }
                    catch (Exception error)
                    {
                        Directory.CreateDirectory(Output);
                        result = new Result {error = error.ToString(), editModeRestored = !EditorApplication.isPlayingOrWillChangePlaymode};
                        Finish();
                    }
                }
                return;
            }
            try
            {
                if (!OwnsLease()) throw new InvalidOperationException("Atmosphere capture lease lost.");
                if (long.TryParse(SessionState.GetString(Key + ".deadline", "0"), out long deadline) && DateTime.UtcNow.Ticks > deadline) throw new TimeoutException("Atmosphere capture deadline exceeded.");
                if (!EditorApplication.isPlaying || EditorApplication.isPaused) return;
                // Resident snow uploads are bounded per tick. Keep them progressing during the settle interval.
                artFixture?.Tick();
                if (EditorApplication.timeSinceStartup < next) return;
                if (!initialized)
                {
                    painter = Object.FindAnyObjectByType<MeadowSurfacePainter>();
                    if (!painter || painter.Session == null) return;
                    camera = painter.studyCamera; atmosphere = Object.FindAnyObjectByType<DayNightLightingController>();
                    globals = Object.FindAnyObjectByType<StylizedLightingGlobals>();
                    if (!camera || !atmosphere || !globals) throw new InvalidOperationException("Missing production camera/lighting/globals.");
                    atmosphere.UnifiedProfile = AssetDatabase.LoadAssetAtPath<AtmosphereProfile>("Assets/_Game/Settings/Rendering/Atmosphere/LittleCastleAtmosphere.asset");
                    if (!atmosphere.UnifiedProfile || !atmosphere.UnifiedProfile.IsComplete) throw new InvalidOperationException("Incomplete atmosphere profile.");
                    if (camera.TryGetComponent<StrategyCameraController>(out var controls)) controls.enabled = false;
                    var meadow = Object.FindAnyObjectByType<MeadowPreview>(); if (meadow) meadow.enabled = false;
                    painter.painting = false;
                    quality = UrpPreviewQualityOverride.Create(TerrainStarterPreviewControls.PreviewQuality.High);
                    atmosphere.SetAtmosphereQuality(TerrainStarterPreviewControls.PreviewQuality.High);
                    Save(camera, Output + "/00-existing-map-before-reference.png");
                    if (SessionState.GetBool(Key + ".art", false))
                    {
                        // Local reference composition faces the evening sun for the leaf-transmission review.
                        // Exiting Play restores the authored controller and the strategy camera.
                        var presentation = new SerializedObject(atmosphere);
                        presentation.FindProperty("sunAzimuth").floatValue = 142;
                        presentation.ApplyModifiedPropertiesWithoutUndo();
                        artFixture = new AtmosphereArtFixture(camera, painter);
                    }
                    skyCamera = new GameObject("Temporary atmosphere sky diagnostic camera") {hideFlags = HideFlags.HideAndDontSave}.AddComponent<Camera>();
                    skyCamera.CopyFrom(camera); skyCamera.enabled = false; skyCamera.cullingMask = 0;
                    skyCamera.clearFlags = CameraClearFlags.Skybox; skyCamera.fieldOfView = 75; skyCamera.orthographic = false;
                    result.scene = SceneManager.GetActiveScene().path; result.gpu = SystemInfo.graphicsDeviceName;
                    result.api = SystemInfo.graphicsDeviceType.ToString(); result.actualPlayMode = Application.isPlaying;
                    initialized = true; SetStage(); next = EditorApplication.timeSinceStartup + 2; return;
                }
                int compositeBefore = TerrainStarterSoftSceneRendererFeature.CompositeDrawCount;
                Save(camera, Output + "/" + Names[stage] + "-game.png"); result.captured++;
                File.AppendAllText(Output + "/render-diagnostics.txt", Names[stage] +
                    " softComposites=" + (TerrainStarterSoftSceneRendererFeature.CompositeDrawCount - compositeBefore) +
                    " daylight=" + atmosphere.DaylightFactor + " sun=" + atmosphere.Sun.intensity +
                    " ambient=" + Shader.GetGlobalColor("_LC_AmbientSkyColor") +
                    " grade=" + Shader.GetGlobalVector("_LC_AtmosphereGrade") +
                    " groundFill=" + Shader.GetGlobalColor("_LC_AtmosphereGroundBounce") +
                    " pipeline=" + (QualitySettings.renderPipeline ? QualitySettings.renderPipeline.name : "default") + "\n");
                Vector3 light = stage < 2 ? -atmosphere.Sun.transform.forward : -atmosphere.Moon.transform.forward;
                skyCamera.transform.rotation = Quaternion.LookRotation(light, Vector3.up);
                Save(skyCamera, Output + "/" + Names[stage] + "-sky-diagnostic.png"); result.captured++;
                foreach (string shaderName in new[] {"Little Castle/Sky/Stylized Day Night", "Little Castle/Foliage/LC Foliage", "Little Castle/Foliage/LC Grass", "Little Castle/Surface/LC Stylized Lit", "Little Castle/Terrain/LC Terrain"})
                {
                    var shader = Shader.Find(shaderName); if (!shader) continue;
                    foreach (var message in ShaderUtil.GetShaderMessages(shader)) if (message.severity.ToString() == "Error") {result.shaderErrors++; result.messages.Add(shaderName + ": " + message.message);}
                }
                File.WriteAllText(Output + "/capture-progress.json", JsonUtility.ToJson(result, true));
                stage++;
                if (stage < Hours.Length) {SetStage(); next = EditorApplication.timeSinceStartup + 2;}
                else
                {
                    SessionState.SetBool(Key + ".finishedCapture", true);
                    SessionState.SetString(Key + ".result", JsonUtility.ToJson(result));
                    EditorApplication.ExitPlaymode();
                }
            }
            catch (Exception error)
            {
                if (result == null) result = new Result(); result.error = error.ToString();
                SessionState.SetString(Key + ".result", JsonUtility.ToJson(result));
                if (EditorApplication.isPlayingOrWillChangePlaymode) EditorApplication.ExitPlaymode();
                else { SessionState.SetBool(Key, false); Finish(); }
            }
        }

        static void SetStage()
        {
            string before = JsonUtility.ToJson(atmosphere.TimeSystem.CaptureState());
            atmosphere.PreviewAtmosphere(Hours[stage], stage == 3 ? 1 : 0, .08f);
            artFixture?.SetWinter(stage == 3);
            result.previewPreservesClock &= before == JsonUtility.ToJson(atmosphere.TimeSystem.CaptureState());
            globals.ApplyGlobals();
        }

        static void Log(string message, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) result?.messages.Add(type + ": " + message);
        }

        static void Save(Camera view, string path)
        {
            var target = RenderTexture.GetTemporary(1600, 900, 24, RenderTextureFormat.ARGB32);
            var active = RenderTexture.active; float aspect = view.aspect; Texture2D pixels = null;
            try
            {
                view.aspect = 1600f / 900;
                CameraCaptureCompatibility.Render(view, target); CameraCaptureCompatibility.Render(view, target);
                RenderTexture.active = target; pixels = new Texture2D(1600, 900, TextureFormat.RGB24, false);
                pixels.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); pixels.Apply();
                File.WriteAllBytes(path, pixels.EncodeToPNG());
            }
            finally { view.aspect = aspect; RenderTexture.active = active; if (pixels) Object.DestroyImmediate(pixels); RenderTexture.ReleaseTemporary(target); }
        }

        static void CleanupPresentation()
        {
            try {artFixture?.Dispose(); artFixture = null; if (atmosphere) {atmosphere.EndAtmospherePreview(); atmosphere.ClearAtmosphereQualityOverride();} quality?.Dispose();}
            finally
            {
                quality = null; Application.logMessageReceived -= Log;
                if (skyCamera) Object.DestroyImmediate(skyCamera.gameObject);
                skyCamera = null; SceneManager.sceneLoaded -= Loaded;
                if (transientSource) Object.DestroyImmediate(transientSource); transientSource = null;
                PurgeTemporaryRoots();
            }
        }

        static void PurgeTemporaryRoots()
        {
            // HideAndDontSave objects can survive a domain/scene reload that interrupts a capture.
            // Match only this harness's exact temporary root; never authored map objects or prefab assets.
            foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
                if (go && go.name == "Temporary atmosphere reference garden" &&
                    go.hideFlags == HideFlags.HideAndDontSave && !EditorUtility.IsPersistent(go))
                    Object.DestroyImmediate(go);
        }

        static void Finish()
        {
            try
            {
                result.status = string.IsNullOrEmpty(result.error) && result.shaderErrors == 0 && result.messages.Count == 0 && result.sourceAssetsPreserved && result.previewPreservesClock && result.testFailed == 0 ? "PASS" : "FAIL";
                File.WriteAllText(Output + "/result.json", JsonUtility.ToJson(result, true));
                Debug.Log("ATMOSPHERE_REVIEW_FINISHED " + result.status);
            }
            finally
            {
                SessionState.SetBool(Key, false);
                ReleaseLease();
            }
        }

        static void ReleaseLease()
        {
            if (!OwnsLease()) return;
            var lease = JsonUtility.FromJson<Lease>(File.ReadAllText("Library/SharedPcControl.json"));
            lease.status = "released"; lease.purpose = "Atmosphere captures/checks ended; Edit Mode restored, no input held.";
            lease.updatedAtUtc = DateTime.UtcNow.ToString("o"); File.WriteAllText("Library/SharedPcControl.json", JsonUtility.ToJson(lease, true));
        }

        static string Hash(string path) {using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant();}

        sealed class Tests : ICallbacks
        {
            public void RunStarted(ITestAdaptor tests) {}
            public void TestStarted(ITestAdaptor test) {}
            public void TestFinished(ITestResultAdaptor test) {}
            public void RunFinished(ITestResultAdaptor tests)
            {
                try {TestRunnerApi.SaveResultToFile(tests, Output + "/tests-editmode.xml"); result.testPassed = tests.PassCount; result.testFailed = tests.FailCount; result.testSkipped = tests.SkipCount;}
                finally {runner.UnregisterCallbacks(this); Object.DestroyImmediate(runner); runner = null; Finish();}
            }
        }
    }
}
