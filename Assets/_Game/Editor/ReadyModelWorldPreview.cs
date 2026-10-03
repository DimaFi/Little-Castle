using LittleCastle.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LittleCastle.Editor
{
    public static class ReadyModelWorldPreview
    {
        private const string TestScenePath =
            "Assets/_Game/Scenes/WorldGenerationTest.unity";

        [MenuItem("Little Castle/World/Sync Models And Generate Preview")]
        public static void Run()
        {
            ReadyModelCatalogSync.Sync();

            EditorSceneManager.OpenScene(
                TestScenePath,
                OpenSceneMode.Single);

            WorldGenerator generator =
                Object.FindAnyObjectByType<WorldGenerator>();

            if (generator == null)
            {
                Debug.LogError(
                    "WorldGenerationTest scene contains no WorldGenerator.");
                return;
            }

            generator.ClearPreview();
            generator.GeneratePreview();
            Selection.activeGameObject = generator.gameObject;

            Debug.Log(
                "Ready models synced and procedural world preview regenerated.");
        }
    }
}
