using LittleCastle.Rendering;
using LittleCastle.Research;
using LittleCastle.World;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace LittleCastle.Tests
{
    public sealed class WorldEditorIntegrationTests
    {
        [Test]
        public void AuthoringScene_HasContinuousClockAndNoAwakeReviewDriverOrSpatialField()
        {
            const string path="Assets/_Game/Scenes/WorldEditor_20261027.unity";
            var scene=SceneManager.GetSceneByPath(path);bool opened=!scene.IsValid()||!scene.isLoaded;
            if(opened)scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
            try
            {
                WorldTimeSystem clock=null;MeadowPreview ui=null;MeadowSurfacePainter painter=null;
                foreach(var root in scene.GetRootGameObjects())
                {
                    Assert.That(root.GetComponentsInChildren<LightingMeadowReview>(true),Is.Empty,"Disabled review still runs Awake and freezes time.");
                    Assert.That(root.GetComponentsInChildren<SpatialIndirectVolume>(true),Is.Empty,"Spatial GI must remain an explicit separate experiment.");
                    clock=clock?clock:root.GetComponentInChildren<WorldTimeSystem>(true);
                    ui=ui?ui:root.GetComponentInChildren<MeadowPreview>(true);
                    painter=painter?painter:root.GetComponentInChildren<MeadowSurfacePainter>(true);
                }
                Assert.That(clock,Is.Not.Null);Assert.That(clock.RunAutomatically,Is.True);
                Assert.That(clock.SimulationSpeedMultiplier,Is.EqualTo(1));
                Assert.That(ui,Is.Not.Null);Assert.That(ui.worldEditorMode,Is.True);
                Assert.That(new UnityEditor.SerializedObject(ui).FindProperty("profile").objectReferenceValue,Is.Not.Null);Assert.That(painter,Is.Not.Null);
                Assert.That(painter.source,Is.Not.Null);Assert.That(painter.allowedTools,Is.EqualTo(MapEditAccess.All));
                Assert.That(painter.treeCatalog?painter.treeCatalog:MapTreeCatalog.Default,Is.Not.Null);
                Assert.That(QualitySettings.activeColorSpace,Is.EqualTo(ColorSpace.Linear));
                Assert.That(QualitySettings.renderPipeline??GraphicsSettings.defaultRenderPipeline,Is.Not.Null);
            }
            finally{if(opened)EditorSceneManager.CloseScene(scene,true);}
        }
    }
}
