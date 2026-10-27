using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LittleCastle.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace LittleCastle.World
{
    /// <summary>Isolated lighting review scene. Captures never count as performance samples.</summary>
    [DefaultExecutionOrder(30)]
    public sealed class LightingBenchmarkController : MonoBehaviour
    {
        public Camera reviewCamera;
        public WorldTimeSystem clock;
        public DayNightLightingController atmosphere;
        public StylizedLightingGlobals lightingGlobals;
        public TerrainStarterSoftScene softScene;
        public Volume finishingVolume;
        public UniversalRenderPipelineAsset baselinePipeline, hdrPipeline, aoPipeline;
        public LightingPaletteProfile linearPalette;
        public bool showControls = true;

        public enum LightingMode { Baseline, HDR, AO }
        public LightingMode mode = LightingMode.AO;
        public float hour = 12;
        public int view;
        public bool depthOfField;

        static readonly string[] ViewNames = { "village", "gate", "foundation", "courtyard", "roof", "bridge", "foliage", "foliage-far" };
        static readonly Vector3[] Positions = {
            new Vector3(-27, 19, -29), new Vector3(-19, 12, -22), new Vector3(-14, 7, -18),
            new Vector3(37, 13, -24), new Vector3(28, 7, -18), new Vector3(37, 14, 3),
            new Vector3(34, 7, -14), new Vector3(46, 24, -30) };
        static readonly Vector3[] Targets = {
            new Vector3(-8, 1, -3), new Vector3(-9.8f, 1, -10), new Vector3(-9.8f, 0, -10),
            new Vector3(24, 2, -8), new Vector3(23, 2.5f, -8), new Vector3(24, 1, 15),
            new Vector3(30, 2.6f, -6), new Vector3(30, 2, -6) };
        static readonly float[] Sizes = { 15, 9, 4.5f, 10, 6, 11, 5.8f, 14 };
        static readonly float[] Hours = { 8, 12, 18.5f, 1 };

        readonly List<float> frameIntervals = new List<float>(600);
        readonly List<float> gpuTimes = new List<float>(600);
        readonly List<float> cpuTimes = new List<float>(600);
        readonly FrameTiming[] timings = new FrameTiming[1];
        readonly System.Diagnostics.Stopwatch wallClock = new System.Diagnostics.Stopwatch();
        RenderPipelineAsset previousPipeline;
        int previousQuality, previousVsync, previousTarget, warmupFrame, previousSampleFrame = -1;
        bool previousBackground, collecting, capturing, automated, completed;
        long previousStamp;
        string output;

        void OnEnable()
        {
            previousQuality = QualitySettings.GetQualityLevel();
            previousPipeline = QualitySettings.renderPipeline;
            previousVsync = QualitySettings.vSyncCount;
            previousTarget = Application.targetFrameRate;
            previousBackground = Application.runInBackground;
            string[] args = Environment.GetCommandLineArgs();
            automated = args.Contains("-lightingBenchmark") || args.Contains("-lightingCapture");
            output = Path.GetFullPath(Argument(args, "-lightingOutput", Path.Combine(Application.dataPath, "../Captures")));
            Directory.CreateDirectory(output);
            if (Enum.TryParse(Argument(args, "-lightingMode", mode.ToString()), true, out LightingMode requested)) mode = requested;
            if (float.TryParse(Argument(args, "-lightingHour", "12"), System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out float requestedHour)) hour = requestedHour;
            if (int.TryParse(Argument(args, "-lightingView", "0"), out int requestedView)) view = Mathf.Clamp(requestedView, 0, ViewNames.Length - 1);
            showControls = !automated;
            Application.runInBackground = true;
            QualitySettings.vSyncCount = automated ? 0 : 1;
            Application.targetFrameRate = automated ? -1 : 60;
            RenderPipelineManager.endCameraRendering += OnCameraRendered;
            ApplyMode();
            SetView(view);
            SetHour(hour);
            if (args.Contains("-lightingBenchmark")) BeginBenchmark();
            if (args.Contains("-lightingCapture")) StartCoroutine(CaptureMatrix());
        }

        static string Argument(string[] args, string name, string fallback)
        {
            int index = Array.IndexOf(args, name);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : fallback;
        }

        public void ApplyMode()
        {
            var selected = mode == LightingMode.Baseline ? baselinePipeline : mode == LightingMode.HDR ? hdrPipeline : aoPipeline;
            if (!selected) throw new InvalidOperationException("Lighting study pipeline is missing: " + mode);
            QualitySettings.renderPipeline = selected;
            reviewCamera.allowHDR = mode != LightingMode.Baseline;
            var data = reviewCamera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = mode != LightingMode.Baseline;
            if (finishingVolume) finishingVolume.enabled = mode != LightingMode.Baseline;
            if (atmosphere)
            {
                atmosphere.Palette = QualitySettings.activeColorSpace == ColorSpace.Linear && mode != LightingMode.Baseline ? linearPalette : null;
                atmosphere.ApplyLighting();
                lightingGlobals?.ApplyGlobals();
            }
            if (softScene)
            {
                // AO is independent of the optional photographic blur. Never darken twice.
                softScene.contactOcclusion = 0;
                softScene.blurStrength = depthOfField ? .5f : 0;
                softScene.enabled = depthOfField;
            }
        }

        public void SetHour(float value)
        {
            hour = Mathf.Repeat(value, 24);
            if (clock)
            {
                clock.RunAutomatically = false;
                if (clock.TryInitialize()) clock.RestoreState(new WorldTimeState(1, hour * 60));
            }
            atmosphere?.ApplyLighting();
            lightingGlobals?.ApplyGlobals();
        }

        public void SetView(int index)
        {
            view = Mathf.Clamp(index, 0, ViewNames.Length - 1);
            reviewCamera.transform.position = Positions[view];
            reviewCamera.transform.LookAt(Targets[view]);
            reviewCamera.orthographic = true;
            reviewCamera.orthographicSize = Sizes[view];
        }

        void LateUpdate()
        {
            // Hold the same wind pose for every visual A/B. Continuous wind remains in interactive mode.
            if (capturing)
            {
                var time = Shader.GetGlobalVector("_LC_GameTime");
                time.z = 2;
                Shader.SetGlobalVector("_LC_GameTime", time);
            }
        }

        void Update()
        {
            if (collecting)
            {
                FrameTimingManager.CaptureFrameTimings();
                if (wallClock.Elapsed.TotalSeconds > 90) FinishBenchmark();
            }
            if (automated) return;
            if (Input.GetKeyDown(KeyCode.F1)) { mode = (LightingMode)(((int)mode + 1) % 3); ApplyMode(); }
            if (Input.GetKeyDown(KeyCode.F2)) SetView((view + 1) % ViewNames.Length);
            if (Input.GetKeyDown(KeyCode.F3))
            {
                int current = Array.FindIndex(Hours, h => Mathf.Abs(h - hour) < .01f);
                SetHour(Hours[(current + 1) % Hours.Length]);
            }
            if (Input.GetKeyDown(KeyCode.F4)) { depthOfField = !depthOfField; ApplyMode(); }
            if (Input.GetKeyDown(KeyCode.F5) && !collecting) BeginBenchmark();
            if (Input.GetKeyDown(KeyCode.F6) && !capturing && !collecting) StartCoroutine(CaptureMatrix());
        }

        IEnumerator CaptureMatrix()
        {
            capturing = true;
            bool previousControls = showControls;
            showControls = false;
            LightingMode previousMode = mode;
            float previousHour = hour;
            int previousView = view;
            bool previousDof = depthOfField;
            depthOfField = false;
            try
            {
                foreach (LightingMode variant in Enum.GetValues(typeof(LightingMode)))
                {
                    mode = variant;
                    ApplyMode();
                    foreach (float time in Hours)
                    {
                        SetHour(time);
                        for (int index = 0; index < ViewNames.Length; index++)
                        {
                            SetView(index);
                            for (int warmup = 0; warmup < 4; warmup++) yield return null;
                            CaptureCurrent();
                            yield return null;
                        }
                    }
                }
                File.WriteAllText(Path.Combine(output, "captures-complete.txt"),
                    "96 views; four times; three modes; fixed wind pose; DoF off. Captures are not FPS evidence.");
                if (automated) { completed = true; Application.Quit(0); }
            }
            finally
            {
                mode = previousMode; hour = previousHour; depthOfField = previousDof;
                ApplyMode(); SetHour(hour); SetView(previousView);
                showControls = previousControls; capturing = false;
            }
        }

        public void CaptureCurrent()
        {
            const int width = 1920, height = 1080;
            var descriptor = new RenderTextureDescriptor(width, height, RenderTextureFormat.ARGB32, 24)
            {
                msaaSamples = 4,
                sRGB = QualitySettings.activeColorSpace == ColorSpace.Linear
            };
            descriptor.msaaSamples = SystemInfo.GetRenderTextureSupportedMSAASampleCount(descriptor);
            var target = RenderTexture.GetTemporary(descriptor);
            var active = RenderTexture.active;
            var previousTargetTexture = reviewCamera.targetTexture;
            var pixels = new Texture2D(width, height, TextureFormat.RGB24, false, false);
            try
            {
                reviewCamera.targetTexture = target;
                CameraCaptureCompatibility.Render(reviewCamera, target);
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                pixels.Apply();
                string name = mode.ToString().ToLowerInvariant() + "-" + ViewNames[view] + "-" +
                    hour.ToString("00.0", System.Globalization.CultureInfo.InvariantCulture);
                File.WriteAllBytes(Path.Combine(output, name + ".png"), pixels.EncodeToPNG());
                File.WriteAllText(Path.Combine(output, name + ".json"), JsonUtility.ToJson(CreateReport("VISUAL_CAPTURE_NOT_BENCHMARK"), true));
            }
            finally
            {
                reviewCamera.targetTexture = previousTargetTexture;
                RenderTexture.active = active;
                RenderTexture.ReleaseTemporary(target);
                Destroy(pixels);
                previousStamp = 0;
            }
        }

        void BeginBenchmark()
        {
            frameIntervals.Clear(); gpuTimes.Clear(); cpuTimes.Clear();
            warmupFrame = Time.frameCount;
            previousStamp = 0;
            previousSampleFrame = -1;
            wallClock.Restart();
            collecting = true;
        }

        void OnCameraRendered(ScriptableRenderContext context, Camera camera)
        {
            if (!collecting || capturing || camera != reviewCamera || camera.targetTexture != null ||
                Time.frameCount - warmupFrame < 120 || previousSampleFrame == Time.frameCount) return;
            previousSampleFrame = Time.frameCount;
            long stamp = System.Diagnostics.Stopwatch.GetTimestamp();
            if (previousStamp != 0)
            {
                frameIntervals.Add((float)((stamp - previousStamp) * 1000.0 / System.Diagnostics.Stopwatch.Frequency));
                if (FrameTimingManager.GetLatestTimings(1, timings) > 0)
                {
                    if (timings[0].gpuFrameTime > 0) gpuTimes.Add((float)timings[0].gpuFrameTime);
                    if (timings[0].cpuFrameTime > 0) cpuTimes.Add((float)timings[0].cpuFrameTime);
                }
            }
            previousStamp = stamp;
            if (frameIntervals.Count >= 600) FinishBenchmark();
        }

        void FinishBenchmark()
        {
            if (!collecting) return;
            collecting = false;
            var report = CreateReport(frameIntervals.Count >= 600 ? "PASS" : "INSUFFICIENT_RENDER_SAMPLES");
            report.valid = frameIntervals.Count >= 600;
            report.samples = frameIntervals.Count;
            report.requiredSamples = 600;
            report.frame = Distribution.From(frameIntervals);
            report.cpu = Distribution.From(cpuTimes);
            report.gpu = Distribution.From(gpuTimes);
            report.elapsedSeconds = (float)wallClock.Elapsed.TotalSeconds;
            report.allocatedBytes = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong();
            File.WriteAllText(Path.Combine(output, "benchmark-" + mode.ToString().ToLowerInvariant() + ".json"), JsonUtility.ToJson(report, true));
            Debug.Log("LC_LIGHTING_BENCHMARK " + report.status + " samples=" + report.samples + " output=" + output);
            if (automated) { completed = true; Application.Quit(report.valid ? 0 : 2); }
        }

        Report CreateReport(string status) => new Report
        {
            status = status, utc = DateTime.UtcNow.ToString("O"), unity = Application.unityVersion,
            device = SystemInfo.graphicsDeviceName, processor = SystemInfo.processorType,
            graphicsAPI = SystemInfo.graphicsDeviceType.ToString(), colorSpace = QualitySettings.activeColorSpace.ToString(),
            width = Screen.width, height = Screen.height, mode = mode.ToString(), view = ViewNames[view], hour = hour,
            hdr = reviewCamera.allowHDR, dof = depthOfField, ao = mode == LightingMode.AO,
            palette = atmosphere && atmosphere.Palette ? atmosphere.Palette.name : "Scene legacy palette",
            pipeline = UrpPreviewQuality.ReadActive(),
            cameraPosition = reviewCamera.transform.position, cameraRotation = reviewCamera.transform.eulerAngles
        };

        void OnGUI()
        {
            if (!showControls || capturing) return;
            GUI.Box(new Rect(12, 12, 750, 68), "Lighting study | " + QualitySettings.activeColorSpace + " | " + mode + " | " + hour.ToString("00.0") + "h | " + ViewNames[view]);
            GUI.Label(new Rect(24, 38, 730, 35), "F1 lighting | F2 camera | F3 time | F4 depth of field | F5 benchmark | F6 capture all");
        }

        void OnDisable()
        {
            RenderPipelineManager.endCameraRendering -= OnCameraRendered;
            int activeLevel = QualitySettings.GetQualityLevel();
            if (activeLevel != previousQuality) QualitySettings.SetQualityLevel(previousQuality, false);
            var active = QualitySettings.renderPipeline;
            if (active == baselinePipeline || active == hdrPipeline || active == aoPipeline) QualitySettings.renderPipeline = previousPipeline;
            if (activeLevel != previousQuality) QualitySettings.SetQualityLevel(activeLevel, false);
            QualitySettings.vSyncCount = previousVsync;
            Application.targetFrameRate = previousTarget;
            Application.runInBackground = previousBackground;
        }

        void OnApplicationQuit()
        {
            if (automated && !completed) File.WriteAllText(Path.Combine(output, "interrupted.txt"), "Run ended before the requested evidence was complete.");
        }

        [Serializable] public sealed class Distribution
        {
            public int samples;
            public float meanMs = -1, medianMs = -1, p95Ms = -1, p99Ms = -1;
            public static Distribution From(List<float> source)
            {
                var result = new Distribution { samples = source.Count };
                if (source.Count == 0) return result;
                float[] sorted = source.OrderBy(x => x).ToArray();
                result.meanMs = sorted.Average();
                result.medianMs = sorted[(sorted.Length - 1) / 2];
                result.p95Ms = sorted[Mathf.CeilToInt(sorted.Length * .95f) - 1];
                result.p99Ms = sorted[Mathf.CeilToInt(sorted.Length * .99f) - 1];
                return result;
            }
        }

        [Serializable] sealed class Report
        {
            public string status, utc, unity, device, processor, graphicsAPI, colorSpace, mode, view, palette;
            public int width, height, samples, requiredSamples;
            public bool valid, hdr, ao, dof;
            public float hour, elapsedSeconds;
            public long allocatedBytes;
            public Vector3 cameraPosition, cameraRotation;
            public UrpPreviewQuality.Snapshot pipeline;
            public Distribution frame, cpu, gpu;
        }
    }
}
