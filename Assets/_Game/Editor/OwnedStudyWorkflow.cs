using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LittleCastle.Editor
{
    /// <summary>Explicit diagnostic ownership; never rebuild over an authored map or dirty scene.</summary>
    public static class OwnedStudyWorkflow
    {
        const string RiverWaterScene="Assets/_Game/Scenes/RiverWaterStudy_20261026.unity";
        public static void Require(string root, string scene)
        {
            bool owned = (root == GroundBiomeStudyBuilder.Root && scene == GroundBiomeStudyBuilder.ScenePath)
                || (root == GroundBiomeSecondPassBuilder.Root && scene == GroundBiomeSecondPassBuilder.Scene)
                || (root == ValleyArtAssets.Root && scene == ValleyArtStudyBuilder.ScenePath)
                || (root == GroundBiomeVegetationPreflight.Study && scene == InnerverseStarterStudyScene.ScenePath)
                || (root == "Assets/_Game/Art/Studies/RiverWater_20261026"
                    && scene == RiverWaterScene);
            if (!owned) throw new InvalidOperationException("This path is not an owned diagnostic fixture.");
            if (EditorApplication.isPlayingOrWillChangePlaymode || BuildPipeline.isBuildingPlayer)
                throw new InvalidOperationException("Exit Play Mode/build before preparing a study.");
            for (int i = 0; i < EditorSceneManager.sceneCount; i++)
            {
                var open = EditorSceneManager.GetSceneAt(i);
                if (open.isDirty) throw new InvalidOperationException("Unsaved scene preserved. Save it explicitly first.");
                if (open.path.Length == 0 && open.rootCount == 0) continue;
                if (open.path == GroundBiomeStudyBuilder.ScenePath
                    || open.path == GroundBiomeSecondPassBuilder.Scene
                    || open.path == ValleyArtStudyBuilder.ScenePath
                    || open.path == InnerverseStarterStudyScene.ScenePath
                    || open.path == RiverWaterScene
                    || open.path == "Assets/_Game/Art/QuaterniusMeadow/Verification/Flowers_Review.unity") continue;
                throw new InvalidOperationException("Open an owned study scene first. The current map is preserved: " + open.path);
            }
        }
    }
}
