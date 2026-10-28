using LittleCastle.World;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LittleCastle.Tests
{
    public sealed class ConceptSceneIntegrationTests
    {
        private const string Root = "Assets/_Game/Settings/World/ConceptWorld_v001/";

        [Test]
        public void SavedStreamingSceneHasPersistentDefinitionAndNoBakedChunks()
        {
            var scene = EditorSceneManager.OpenPreviewScene("Assets/_Game/Scenes/ConceptProceduralWorld.unity");
            try
            {
                WorldStreamer streamer = null;
                int chunks = 0;
                foreach (var root in scene.GetRootGameObjects())
                {
                    if (root.GetComponent<WorldStreamer>() != null) streamer = root.GetComponent<WorldStreamer>();
                    chunks += root.GetComponentsInChildren<StreamedChunkView>(true).Length;
                    var clock = root.GetComponent<WorldTimeSystem>();
                    if (clock != null)
                        Assert.That(new SerializedObject(clock).FindProperty("worldDefinition").objectReferenceValue,
                            Is.EqualTo(AssetDatabase.LoadAssetAtPath<WorldDefinition>(Root + "ConceptWorldDefinition.asset")));
                }
                Assert.That(streamer, Is.Not.Null);
                Assert.That(streamer.Definition, Is.EqualTo(AssetDatabase.LoadAssetAtPath<WorldDefinition>(Root + "ConceptWorldDefinition.asset")));
                Assert.That(chunks, Is.Zero, "Scene must generate chunks at runtime, not bake a fake world.");
                Assert.That(streamer.Definition.MacroPlannerSettings.Bridges.enableBridgeAwareRouting, Is.True);
                Assert.That(streamer.Definition.MacroPlannerSettings.Bridges.minimumFixedBridgeCount, Is.GreaterThanOrEqualTo(1));
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [Test]
        public void ConceptMaterialAndCatalogReferenceRealApprovedAssets()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(Root + "Concept_Terrain_Masked.mat");
            foreach (var property in new[] { "_GrassTex", "_DirtTex", "_SoilTex", "_RockTex" })
                Assert.That(material.GetTexture(property), Is.Not.Null, property);
            var catalog = AssetDatabase.LoadAssetAtPath<WorldSpawnCatalog>(Root + "Concept_MainWorldSpawnCatalog.asset");
            foreach (var dependency in AssetDatabase.GetDependencies(new[] { Root + "Concept_MainWorldSpawnCatalog.asset", Root + "Concept_Terrain_Masked.mat" }, true))
            {
                Assert.That(dependency, Does.Not.Contain("/TerrainStarter_v001/"), "Committed concept must not depend on ignored local art.");
                Assert.That(dependency, Does.Not.Contain("/Models/Ready/"));
                Assert.That(dependency, Does.Not.Contain("/Textures/Ready/"));
            }
            foreach (var id in new[] { "tree_main_01", "rock_ground_01", "neutral_village_01" })
            {
                Assert.That(catalog.TryResolve(id, 1, out _, out GameObject prefab), Is.True, id);
                Assert.That(prefab.GetComponentsInChildren<MeshRenderer>(true).Length > 0 ||
                    prefab.GetComponent<ConceptVillageFootprintPresenter>() != null, Is.True, id);
                Assert.That(AssetDatabase.GetAssetPath(prefab), Does.Not.Contain("Placeholder"));
            }
        }
    }
}
