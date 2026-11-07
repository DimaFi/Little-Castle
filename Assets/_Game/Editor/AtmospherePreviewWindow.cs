using LittleCastle.World;
using UnityEditor;
using UnityEngine;

namespace LittleCastle.Editor
{
    /// <summary>Presentation-only preview in the current scene. Never loads a scene or changes world time.</summary>
    public sealed class AtmospherePreviewWindow : EditorWindow
    {
        DayNightLightingController controller;
        float hour = 18.25f, snow, overcast;
        bool preview;
        TerrainStarterPreviewControls.PreviewQuality previousQuality;
        bool previousQualityOverride;

        [MenuItem("Little Castle/Rendering/Atmosphere Preview")]
        static void Open() => GetWindow<AtmospherePreviewWindow>("Atmosphere");

        void OnEnable() => EditorApplication.playModeStateChanged += OnPlayModeChanged;

        void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode) Stop();
        }

        void OnGUI()
        {
            EditorGUILayout.HelpBox("Local visual preview in Play Mode. The authoritative clock, map, weather and source materials are preserved. Close this window or choose Live clock to return.", MessageType.Info);
            using (new EditorGUI.DisabledScope(preview))
                controller = (DayNightLightingController)EditorGUILayout.ObjectField("Existing atmosphere", controller, typeof(DayNightLightingController), true);
            if (!controller && Application.isPlaying) controller = FindAnyObjectByType<DayNightLightingController>();
            if (!Application.isPlaying || !controller || !controller.UnifiedProfile)
            {
                EditorGUILayout.HelpBox("Use the integrated World Editor scene in Play Mode with its Atmosphere Cycle profile assigned.", MessageType.Info);
                return;
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Sunny")) Set(13, 0, .08f);
                if (GUILayout.Button("Golden")) Set(18.25f, 0, .12f);
                if (GUILayout.Button("Blue night")) Set(0, 0, .08f);
                if (GUILayout.Button("Snow moon")) Set(0, 1, .10f);
            }
            EditorGUI.BeginChangeCheck();
            hour = EditorGUILayout.Slider("Local hour", hour, 0, 24);
            snow = EditorGUILayout.Slider("Local snow appearance", snow, 0, 1);
            overcast = EditorGUILayout.Slider("Cloudiness", overcast, 0, 1);
            var quality = (TerrainStarterPreviewControls.PreviewQuality)EditorGUILayout.EnumPopup("Atmosphere quality", controller.AtmosphereQuality);
            if (EditorGUI.EndChangeCheck())
            {
                Begin();
                controller.SetAtmosphereQuality(quality);
                controller.PreviewAtmosphere(hour, snow, overcast);
            }
            if (GUILayout.Button("Live clock")) Stop();
            EditorGUILayout.LabelField("Authoritative clock", controller.TimeSystem ? controller.TimeSystem.CurrentHour.ToString("0.00") : "Unavailable");
        }

        void Set(float time, float snowAmount, float clouds)
        {
            Begin(); hour = time; snow = snowAmount; overcast = clouds;
            controller.PreviewAtmosphere(hour, snow, overcast);
        }

        void Begin()
        {
            if (preview) return;
            previousQuality = controller.AtmosphereQuality;
            previousQualityOverride = controller.HasAtmosphereQualityOverride;
            preview = true;
        }

        void Stop()
        {
            if (preview && controller)
            {
                controller.SetAtmosphereQuality(previousQuality);
                if (!previousQualityOverride) controller.ClearAtmosphereQualityOverride();
                controller.EndAtmospherePreview();
            }
            preview = false;
            SceneView.RepaintAll();
        }

        void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            Stop();
        }
    }
}
