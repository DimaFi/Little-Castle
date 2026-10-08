using System;
using LittleCastle.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LittleCastle.Editor
{
    /// <summary>
    /// Explicit-only scene setup for the isolated concept river presentation.
    /// Never writes Main* assets, creates duplicate global water owners or
    /// silently saves/rewrites the user's current scene.
    /// </summary>
    public static class ConceptCrossingPresentationBuilder
    {
        [MenuItem("Little Castle/World/Concept Water/Attach Selected Water Material")]
        public static void AttachSelectedWaterMaterial()
        {
            Material material = Selection.activeObject as Material;
            if (material == null)
                throw new InvalidOperationException(
                    "Select a reviewed shared water Material in the Project first.");

            WorldStreamer[] streamers =
                UnityEngine.Object.FindObjectsByType<WorldStreamer>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (streamers.Length != 1)
                throw new InvalidOperationException(
                    "Expected exactly one WorldStreamer in active concept scene; found " +
                    streamers.Length + ". No automatic scene modification.");

            WorldStreamer streamer = streamers[0];
            if (!streamer.gameObject.scene.IsValid())
                throw new InvalidOperationException("WorldStreamer must be in a saved/active scene.");

            RiverWaterPresenter presenter =
                streamer.GetComponent<RiverWaterPresenter>();
            if (presenter == null)
                presenter = Undo.AddComponent<RiverWaterPresenter>(streamer.gameObject);

            Undo.RecordObject(presenter, "Configure Concept River Water");
            var serialized = new SerializedObject(presenter);
            serialized.FindProperty("streamer").objectReferenceValue = streamer;
            serialized.FindProperty("sharedWaterMaterial").objectReferenceValue = material;
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(presenter);
            EditorSceneManager.MarkSceneDirty(streamer.gameObject.scene);

            Debug.Log(
                "[Concept Water] Attached one shared-material river presenter " +
                "to the current scene WorldStreamer. Scene is DIRTY but NOT " +
                "automatically saved. Review in Unity before saving. " +
                "No authored Bridge_Water FBX was instantiated.");
        }
    }
}
