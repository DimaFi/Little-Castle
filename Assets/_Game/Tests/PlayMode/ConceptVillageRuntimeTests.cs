using System.Collections;
using LittleCastle.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace LittleCastle.Tests
{
    public sealed class ConceptVillageRuntimeTests
    {
        [UnityTest]
        public IEnumerator PlacesThreeHousesOnlyOnPlayableStreamedTerrain()
        {
            var ground = new GameObject("Synthetic streamed terrain");
            var mesh = FlatMesh();
            var source = new GameObject("Synthetic structure source");
            source.transform.position = new Vector3(1000, 1000, 1000);
            var village = new GameObject("Supported village");
            try
            {
                var view = ground.AddComponent<StreamedChunkView>();
                view.Initialize(new ChunkCoordinate(0, 0), mesh);
                view.SetTerrainColliderEnabled(true);
                village.transform.rotation = Quaternion.Euler(0, 37, 0);
                var presenter = village.AddComponent<ConceptVillageFootprintPresenter>();
                presenter.housePrefab = source;
                presenter.wellPrefab = source;
                Physics.SyncTransforms();
                yield return null;
                yield return null;
                Assert.That(presenter.PlacementFinished, Is.True);
                Assert.That(presenter.PlacedHouseCount, Is.EqualTo(3));
                Assert.That(village.transform.childCount, Is.EqualTo(4));
                foreach (Transform child in village.transform)
                    Assert.That(child.position.y, Is.EqualTo(-.05f).Within(.001f));
            }
            finally
            {
                Object.Destroy(village); Object.Destroy(source);
                Object.Destroy(ground); Object.Destroy(mesh);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator DoesNotUseDecorativeMeshColliderAsGroundSupport()
        {
            var ground = new GameObject("Decor mesh, not streamed terrain");
            var mesh = FlatMesh();
            var source = new GameObject("Synthetic source");
            source.transform.position = new Vector3(1000, 1000, 1000);
            var village = new GameObject("Unsupported village");
            try
            {
                ground.AddComponent<MeshCollider>().sharedMesh = mesh;
                var presenter = village.AddComponent<ConceptVillageFootprintPresenter>();
                presenter.housePrefab = source;
                Physics.SyncTransforms();
                yield return null;
                yield return null;
                Assert.That(presenter.PlacedHouseCount, Is.Zero);
                Assert.That(village.transform.childCount, Is.Zero);
                Assert.That(presenter.PlacementFinished, Is.False, "Still bounded-waiting for real terrain, not accepting decoration.");
            }
            finally
            {
                Object.Destroy(village); Object.Destroy(source);
                Object.Destroy(ground); Object.Destroy(mesh);
            }
            yield return null;
        }

        private static Mesh FlatMesh()
        {
            var mesh = new Mesh();
            mesh.vertices = new[] { new Vector3(-30, 0, -30), new Vector3(30, 0, -30),
                new Vector3(30, 0, 30), new Vector3(-30, 0, 30) };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
