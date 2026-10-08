using System.Collections;
using LittleCastle.Rendering;
using LittleCastle.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace LittleCastle.Tests
{
    public sealed class PresentationOwnershipPlayModeTests
    {
        [UnityTest]
        public IEnumerator RuntimeEmitterAwake_DisablesUnbudgetedLight()
        {
            var root = new GameObject("Runtime light ownership test");
            try
            {
                Light light = root.AddComponent<Light>();
                light.type = LightType.Point;
                light.enabled = true;
                var emitter = root.AddComponent<NightLightEmitter>();
                Assert.That(emitter.TargetLight, Is.SameAs(light));
                Assert.That(light.enabled, Is.False);
                yield return null;
                Assert.That(light.enabled, Is.False);
            }
            finally { Object.Destroy(root); }
        }

        [UnityTest]
        public IEnumerator ChunkReleaseAndReload_FreesWaterWithoutDuplicateOwner()
        {
            var root = new GameObject("Runtime water ownership test");
            var material = new Material(Shader.Find("Unlit/Color"));
            Mesh first = null;
            Mesh second = null;
            try
            {
                var chunk = new WorldChunkData(new ChunkCoordinate(0, 0), 8);
                var plan = new MacroWorldPlan(7);
                var river = new WorldRiverData {
                    stableId = 101, nominalWidth = 5f, nominalDepth = 1f };
                river.centerline.Add(new Vector2(-10f, 8f));
                river.centerline.Add(new Vector2(30f, 8f));
                plan.AddRiver(river);
                var owner = RiverWaterPresenter.Populate(root.transform, chunk, 16f, plan, material);
                Assert.That(owner, Is.Not.Null);
                first = owner.OwnedMesh;
                Assert.That(first, Is.Not.Null);
                var view = root.AddComponent<StreamedChunkView>();
                view.Initialize(chunk.Coordinate, null);
                view.SetOwnedRiverWater(owner);
                view.ReleaseOwnedResources();
                view.ReleaseOwnedResources();
                Assert.That(owner.OwnedMesh, Is.Null);
                yield return null;
                Assert.That(first == null, Is.True, "Runtime mesh must be destroyed after the frame.");
                var reloaded = RiverWaterPresenter.Populate(root.transform, chunk, 16f, plan, material);
                Assert.That(reloaded, Is.SameAs(owner));
                Assert.That(root.GetComponentsInChildren<RiverWaterPresenter>().Length, Is.EqualTo(1));
                second = reloaded.OwnedMesh;
                Object.Destroy(root);
                yield return null;
                yield return null;
                Assert.That(second == null, Is.True, "Destroying the chunk must also free its water mesh.");
            }
            finally
            {
                if (root != null) Object.Destroy(root);
                Object.Destroy(material);
                if (first != null) Object.Destroy(first);
                if (second != null) Object.Destroy(second);
            }
        }
    }
}
