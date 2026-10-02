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
        private GUIStyle labelStyle;
        private GUIStyle boxStyle;

        public bool IsReady => ready;

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

            if (Input.GetKeyDown(
                    developmentSkipKey))
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

            ready = true;

            if (worldStreamer != null)
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

        private void OnGUI()
        {
            if (!showLoadingOverlay ||
                ready ||
                worldStreamer == null)
            {
                return;
            }

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

            GUILayout.Label(
                "Safe starting area: " +
                (Readiness01 * 100f).ToString("0") +
                "%",
                labelStyle);

            GUILayout.Label(
                "Prepared radius: " +
                TargetPreparedRadius +
                " chunks   •   F9 skips only in development",
                labelStyle);

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
