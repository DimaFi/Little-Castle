using LittleCastle.CameraSystem;
using UnityEngine;

namespace LittleCastle.World
{
    /// <summary>
    /// Holds local camera input until a safe starting data ring is prepared.
    ///
    /// This does not instantiate the whole world. It only waits for generated
    /// chunk data around the current focus so the player cannot immediately
    /// outrun streaming after the match starts.
    /// </summary>
    [DefaultExecutionOrder(900)]
    public sealed class WorldStartupWarmupController : MonoBehaviour
    {
        [SerializeField]
        private WorldStreamer worldStreamer;

        [SerializeField]
        private StrategyCameraController strategyCamera;

        [Tooltip(
            "How many extra prepared data rings are required beyond the visible " +
            "load radius before camera input is released.")]
        [Min(0)]
        [SerializeField] private int extraPreparedRings = 1;

        [SerializeField]
        private bool lockCameraInputUntilReady = true;

        [SerializeField]
        private bool showLoadingOverlay = true;

        [Tooltip(
            "Development-only escape hatch. It releases camera input without " +
            "changing world generation state.")]
        [SerializeField]
        private KeyCode developmentSkipKey = KeyCode.F9;

        private bool ready;
        private bool priorityRequested;
        private WorldStartupBootstrapAdapter bootstrap;
        private GUIStyle labelStyle;
        private GUIStyle boxStyle;

        public bool IsReady => ready;

        /// <summary>
        /// Root-owned opt-in: attach BEFORE legacy warmup has completed.
        /// The root must integrate the same completed macro plan into
        /// WorldStreamer; Q02 intentionally does not modify WorldStreamer.
        /// </summary>
        public void AttachBootstrap(WorldStartupBootstrapAdapter value)
        {
            if (value == null)
                throw new System.ArgumentNullException(nameof(value));
            if (ready || bootstrap != null)
                throw new System.InvalidOperationException(
                    "Bootstrap may be attached only once before input is released.");

            bootstrap = value;
            ResolveReferences();
            if (strategyCamera != null)
                strategyCamera.SetInputEnabled(false);
        }

        public void CancelBootstrap()
        {
            if (bootstrap == null || ready)
                return;
            bootstrap.Cancel();
            if (strategyCamera != null)
                strategyCamera.SetInputEnabled(false);
        }

        public WorldStartupBootstrapAdapter.StartupPhase? BootstrapPhase =>
            bootstrap != null ? bootstrap.Phase :
            (WorldStartupBootstrapAdapter.StartupPhase?)null;

        public int TargetPreparedRadius
        {
            get
            {
                if (worldStreamer == null ||
                    worldStreamer.Definition == null ||
                    worldStreamer.Definition.StreamingSettings == null)
                {
                    return 0;
                }

                return
                    worldStreamer.Definition.StreamingSettings.LoadRadiusChunks +
                    Mathf.Max(
                        0,
                        extraPreparedRings);
            }
        }

        public float Readiness01
        {
            get
            {
                if (bootstrap != null)
                {
                    if (bootstrap.Phase ==
                        WorldStartupBootstrapAdapter.StartupPhase.StartingData)
                        return bootstrap.StartingDataReadiness01;
                    if (bootstrap.Phase ==
                        WorldStartupBootstrapAdapter.StartupPhase.VisibleReady)
                        return bootstrap.VisibleAreaReadiness01;
                    return bootstrap.IsReady ? 1f : 0f;
                }

                if (worldStreamer == null)
                    return 0f;

                float visible =
                    worldStreamer.VisibleReadiness01;

                float prepared =
                    worldStreamer.GetPreparedDataReadiness01(
                        TargetPreparedRadius);

                return
                    Mathf.Min(
                        visible,
                        prepared);
            }
        }

        private void Awake()
        {
            ResolveReferences();

            if (lockCameraInputUntilReady &&
                strategyCamera != null)
            {
                strategyCamera.SetInputEnabled(
                    false);
            }
        }

        private void Update()
        {
            if (ready)
                return;

            ResolveReferences();

            if (bootstrap != null)
            {
                bootstrap.Tick();
                if (bootstrap.CanAcceptGameInput)
                    CompleteWarmup();
                return;
            }

            // Never allow a debug bypass to weaken the root bootstrap gate.
            // The legacy skip remains available only in development builds.
            if ((Application.isEditor || Debug.isDebugBuild) &&
                Input.GetKeyDown(developmentSkipKey))
            {
                CompleteWarmup();
                return;
            }

            if (worldStreamer == null)
                return;

            if (!priorityRequested)
            {
                worldStreamer.SetPriorityPreparationRadius(
                    TargetPreparedRadius);

                priorityRequested = true;
            }

            if (worldStreamer.IsVisibleAreaReady &&
                worldStreamer.GetPreparedDataReadiness01(
                    TargetPreparedRadius) >=
                0.9999f)
            {
                CompleteWarmup();
            }
        }

        private void CompleteWarmup()
        {
            if (ready)
                return;

            // A root bootstrap session must pass all three gates: macro,
            // starting data and actually visible/usable chunk presentation.
            if (bootstrap != null && !bootstrap.CanAcceptGameInput)
                return;

            ready = true;

            if (bootstrap == null && worldStreamer != null)
            {
                worldStreamer.ClearPriorityPreparationRadius();
            }

            if (strategyCamera != null)
            {
                strategyCamera.SetInputEnabled(
                    true);
            }
        }

        private void ResolveReferences()
        {
            if (worldStreamer == null)
            {
                worldStreamer =
                    FindAnyObjectByType<
                        WorldStreamer>();
            }

            if (strategyCamera == null)
            {
                strategyCamera =
                    FindAnyObjectByType<
                        StrategyCameraController>();
            }
        }

        private void OnDisable()
        {
            if (bootstrap != null && !ready)
                bootstrap.Cancel();
            else if (bootstrap == null && priorityRequested &&
                     worldStreamer != null)
                worldStreamer.ClearPriorityPreparationRadius();
        }

        private void OnGUI()
        {
            if (!showLoadingOverlay || ready ||
                (worldStreamer == null && bootstrap == null))
                return;

            EnsureStyles();

            const float width = 440f;
            const float height = 118f;

            Rect rect =
                new Rect(
                    (Screen.width - width) * 0.5f,
                    (Screen.height - height) * 0.5f,
                    width,
                    height);

            GUI.Box(
                rect,
                GUIContent.none,
                boxStyle);

            GUILayout.BeginArea(
                new Rect(
                    rect.x + 18f,
                    rect.y + 14f,
                    rect.width - 36f,
                    rect.height - 28f));

            GUILayout.Label(
                "PREPARING WORLD",
                labelStyle);

            if (bootstrap != null)
            {
                switch (bootstrap.Phase)
                {
                    case WorldStartupBootstrapAdapter.StartupPhase.MacroPlanning:
                        GUILayout.Label(
                            "Macro: " + bootstrap.MacroPhase +
                            "  •  grid candidates: " +
                            bootstrap.MacroPointCellsExamined,
                            labelStyle);
                        break;
                    case WorldStartupBootstrapAdapter.StartupPhase.StartingData:
                        GUILayout.Label(
                            "Starting-area data: " +
                            (bootstrap.StartingDataReadiness01 * 100f)
                                .ToString("0") + "%", labelStyle);
                        break;
                    case WorldStartupBootstrapAdapter.StartupPhase.VisibleReady:
                        GUILayout.Label(
                            "Visible starting area: " +
                            (bootstrap.VisibleAreaReadiness01 * 100f)
                                .ToString("0") + "%", labelStyle);
                        break;
                    case WorldStartupBootstrapAdapter.StartupPhase.Cancelled:
                        GUILayout.Label("Startup cancelled", labelStyle);
                        break;
                    case WorldStartupBootstrapAdapter.StartupPhase.Failed:
                        GUILayout.Label("Startup failed — inspect log", labelStyle);
                        break;
                }
                GUILayout.Label(
                    "Waiting for verified match readiness", labelStyle);
            }
            else
            {
                GUILayout.Label(
                    "Safe starting area: " +
                    (Readiness01 * 100f).ToString("0") + "%",
                    labelStyle);
                GUILayout.Label(
                    "Prepared radius: " + TargetPreparedRadius +
                    " chunks   •   F9 is a development-only skip",
                    labelStyle);
            }

            GUILayout.EndArea();
        }

        private void EnsureStyles()
        {
            if (labelStyle == null)
            {
                labelStyle =
                    new GUIStyle(
                        GUI.skin.label);

                labelStyle.alignment =
                    TextAnchor.MiddleCenter;

                labelStyle.fontSize = 16;
                labelStyle.normal.textColor =
                    Color.white;
            }

            if (boxStyle == null)
            {
                boxStyle =
                    new GUIStyle(
                        GUI.skin.box);
            }
        }

        private void OnValidate()
        {
            extraPreparedRings =
                Mathf.Max(
                    0,
                    extraPreparedRings);
        }
    }
}
