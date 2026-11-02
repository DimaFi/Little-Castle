using LittleCastle.World.Weather;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LittleCastle.EditorTools
{
    public static class RainStormPreviewMenu
    {
        [MenuItem("Little Castle/Weather/Create Cozy Rain Preview in Current Scene")]
        public static void AddWeatherPreview()
        {
            var existing = Object.FindAnyObjectByType<RainStormController>();
            if (existing)
            {
                Selection.activeGameObject = existing.gameObject;
                EditorGUIUtility.PingObject(existing);
                Debug.Log("RainStorm preview already exists. Select it and use the Game debug panel during Play.");
                return;
            }
            var root = new GameObject("Little Castle - Cozy Rain Preview (opt-in)");
            Undo.RegisterCreatedObjectUndo(root, "Add Cozy Rain Preview");
            var weather = Undo.AddComponent<RainStormController>(root);
            Selection.activeGameObject = root;
            EditorGUIUtility.PingObject(weather);
            EditorSceneManager.MarkSceneDirty(root.scene);
            Debug.Log("RainStorm: select Camera / Atmosphere / Map Painter references in the component if needed. Enter Play, choose rain in Game. No authored world objects have been replaced.");
        }
    }

    [CustomEditor(typeof(RainStormController))]
    public sealed class RainStormControllerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.HelpBox(
                "Weather preview is opt-in. If Camera.main is absent, assign the gameplay camera. " +
                "Check ground/water layers to keep puddles off rivers and roofs. Shaders are under Resources/RainStorm. " +
                "Game preview buttons work in Play Mode.", MessageType.Info);
            if (!Application.isPlaying) return;
            var weather = (RainStormController)target;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Game weather controls", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Off")) weather.SetMode(RainMode.Off);
            if (GUILayout.Button("Drizzle")) weather.SetMode(RainMode.Drizzle);
            if (GUILayout.Button("Rain")) weather.SetMode(RainMode.Rain);
            if (GUILayout.Button("Downpour")) weather.SetMode(RainMode.Downpour);
            if (GUILayout.Button("Storm")) weather.SetMode(RainMode.Storm);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Low")) weather.SetQuality(RainQuality.Low);
            if (GUILayout.Button("Medium")) weather.SetQuality(RainQuality.Medium);
            if (GUILayout.Button("High")) weather.SetQuality(RainQuality.High);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.LabelField("Current rain", weather.CurrentIntensity.ToString("P0"));
            EditorGUILayout.LabelField("Current wetness", weather.CurrentWetness.ToString("P0"));
            if (GUILayout.Button("Toggle in-game panel")) weather.ToggleDebugUI();
            if (GUI.changed) EditorUtility.SetDirty(weather);
        }
    }
}
