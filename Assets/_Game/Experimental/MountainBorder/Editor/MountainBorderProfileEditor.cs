using LittleCastle.Experimental.MountainBorder.Presentation;
using UnityEditor;
using UnityEngine;

namespace LittleCastle.Experimental.MountainBorder.Editor
{
    [CustomEditor(typeof(MountainBorderProfile))]
    public sealed class MountainBorderProfileEditor:UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUI.BeginChangeCheck();DrawDefaultInspector();
            if(EditorGUI.EndChangeCheck())
                foreach(var border in Object.FindObjectsByType<LandscapeBorderPresentation>(FindObjectsSortMode.None))
                    if(border.profile==target)border.Rebuild();
            EditorGUILayout.HelpBox("Water, snow and tree settings update this scene. Bake the three LOD meshes after changing rock detail or seed.",MessageType.Info);
            if(GUILayout.Button("Bake rock detail and refresh this map"))LandscapeIntegrationV2.Build();
        }
    }
    [CustomEditor(typeof(LandscapeBorderPresentation))]
    public sealed class LandscapeBorderPresentationEditor:UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();var border=(LandscapeBorderPresentation)target;
            if(GUILayout.Button("Refresh presentation"))border.Rebuild();
            EditorGUILayout.LabelField("Active land LOD",border.activeLandLod.ToString());
            EditorGUILayout.LabelField("Land triangles",border.landTriangles.ToString());
            EditorGUILayout.LabelField("Source trees",border.treeCount.ToString());
            if(!string.IsNullOrEmpty(border.lastError))EditorGUILayout.HelpBox(border.lastError,MessageType.Error);
        }
    }
}
