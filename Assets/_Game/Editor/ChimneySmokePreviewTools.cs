using System.IO;
using LittleCastle.Rendering.ChimneySmoke;
using UnityEditor;
using UnityEngine;

namespace LittleCastle.Editor
{
    /// <summary>Explicit authoring commands; no automatic prefab/scene/catalog modifications.</summary>
    public static class ChimneySmokePreviewTools
    {
        private const string ProfilePath = "Assets/_Game/Art/ChimneySmokePreview/ChimneySmokeDefault.asset";

        [MenuItem("Little Castle/Rendering/Chimney Smoke/Create Default Profile")]
        private static void CreateProfile()
        {
            var existing = AssetDatabase.LoadAssetAtPath<ChimneySmokeProfile>(ProfilePath);
            if (existing)
            {
                Selection.activeObject = existing;
                EditorGUIUtility.PingObject(existing);
                return;
            }
            Directory.CreateDirectory("Assets/_Game/Art/ChimneySmokePreview");
            var profile = ScriptableObject.CreateInstance<ChimneySmokeProfile>();
            AssetDatabase.CreateAsset(profile, ProfilePath);
            AssetDatabase.SaveAssets();
            Selection.activeObject = profile;
            EditorGUIUtility.PingObject(profile);
        }

        [MenuItem("Little Castle/Rendering/Chimney Smoke/Add Preview At Selection")]
        private static void AddAtSelection()
        {
            var parent = Selection.activeTransform;
            var profile = AssetDatabase.LoadAssetAtPath<ChimneySmokeProfile>(ProfilePath);
            if (!profile)
            {
                Debug.LogWarning("Create the default chimney smoke profile first.");
                return;
            }
            var outlet = new GameObject("Chimney Outlet (move to pipe mouth)");
            Undo.RegisterCreatedObjectUndo(outlet, "Add smoke outlet");
            if (parent)
            {
                Undo.SetTransformParent(outlet.transform, parent, "Parent smoke outlet");
                outlet.transform.localPosition = Vector3.up * 2f;
            }
            else outlet.transform.position = Vector3.up * 2f;
            var emitter = Undo.AddComponent<ChimneySmokeEmitter>(outlet);
            emitter.Configure(profile, outlet.transform, outlet.GetInstanceID());
            EditorUtility.SetDirty(emitter);
            Selection.activeGameObject = outlet;
            Debug.Log("Move the outlet to the real chimney mouth. Smoke appears in Play Mode only.");
        }
    }
}
