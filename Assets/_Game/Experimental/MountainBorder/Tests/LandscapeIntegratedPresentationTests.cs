using LittleCastle.Experimental.MountainBorder.Presentation;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

namespace LittleCastle.Experimental.MountainBorder.Tests
{
    public sealed class LandscapeIntegratedPresentationTests
    {
        [Test] public void IntegratedViewSelectsLodsWithoutChangingSourceMaterialsOrAddingLightsAndCollision()
        {
            var profile=ScriptableObject.CreateInstance<MountainBorderProfile>();
            var material=new Material(Shader.Find("Little Castle/Terrain/LC Terrain"));
            var mesh=new Mesh{vertices=new[]{Vector3.zero,Vector3.forward,Vector3.right},triangles=new[]{0,1,2}};mesh.RecalculateNormals();mesh.RecalculateBounds();
            var root=new GameObject("Isolated integrated landscape lifecycle");var cameraRoot=new GameObject("Isolated landscape LOD eye");
            int lights=Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Length;
            try
            {
                profile.lodMeshes=new[]{mesh,mesh,mesh};profile.alpineMaterial=material;profile.lod0Distance=120;profile.lod1Distance=260;
                var view=root.AddComponent<LandscapeBorderPresentation>();view.showWaterfall=false;view.showTrees=false;view.profile=profile;view.Rebuild();
                var camera=cameraRoot.AddComponent<Camera>();camera.enabled=false;camera.transform.position=new Vector3(0,55,80);
                view.TickPresentation(camera,12);Assert.That(view.activeLandLod,Is.Zero);
                camera.transform.position+=Vector3.back*160;view.TickPresentation(camera,12);Assert.That(view.activeLandLod,Is.EqualTo(1));
                camera.transform.position+=Vector3.back*300;view.TickPresentation(camera,12);Assert.That(view.activeLandLod,Is.EqualTo(2));
                Assert.That(root.GetComponentsInChildren<Collider>(),Is.Empty);
                Assert.That(Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Length,Is.EqualTo(lights));
                Assert.That(material.GetFloat("_AlpineEnabled"),Is.Zero,"The optional appearance is bound on the owned alpine asset, not on unrelated source materials.");
                view.Release();Assert.That(root.GetComponentsInChildren<Renderer>(),Is.Empty);Assert.That(mesh,Is.Not.Null);Assert.That(material,Is.Not.Null);
            }
            finally{Object.DestroyImmediate(root);Object.DestroyImmediate(cameraRoot);Object.DestroyImmediate(mesh);Object.DestroyImmediate(material);Object.DestroyImmediate(profile);}
        }
    }
}
